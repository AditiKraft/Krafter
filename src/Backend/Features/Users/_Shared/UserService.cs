using Backend.Application.BackgroundJobs;
using Backend.Application.Common;
using Backend.Application.Notifications;
using Backend.Common;
using Backend.Common.Auth;
using Backend.Common.Interfaces;
using Backend.Common.Interfaces.Auth;
using Backend.Common.Models;
using Backend.Features.Roles._Shared;
using Backend.Features.Tenants;
using Backend.Infrastructure.Persistence;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Users._Shared;

public static class QueryStringKeys
{
    public const string Code = "code";
    public const string UserId = "userId";
}

public static class KrafterInitialConstants
{
    public static class RootUser
    {
        public const string Id = "root";
        public const string LastName = "Admin";
        public const string FirstName = "Admin";
        public const string EmailAddress = "admin@getkrafter.dev";
    }

    public static class RootTenant
    {
        public const string Id = "root";

        public const string Identifier = "krafter";
        public const string Name = "krafter";
    }

    public const string DefaultPassword = "123Pa$$word!";

    public static Tenant KrafterTenant { private set; get; } = new()
    {
        Id = RootTenant.Id,
        Identifier = RootTenant.Identifier,
        IsActive = true,
        Name = RootTenant.Name,
        CreatedOn = DateTime.UtcNow,
        ValidUpto = DateTime.MaxValue,
        AdminEmail = RootUser.EmailAddress
    };
}

public class UserService(
    SignInManager<KrafterUser> signInManager,
    UserManager<KrafterUser> userManager,
    RoleManager<KrafterRole> roleManager,
    ITenantGetterService tenantGetterService,
    TenantDbContext tenantDbContext,
    KrafterContext db,
    IJobService jobService,
    ICurrentUser currentUser)
    : IUserService, IScopedService
{
    public async Task<Response<List<string>>> GetPermissionsAsync(string userId, CancellationToken cancellationToken)
    {
        //var user = await userManager.Asn.FindByIdAsync(userId);
        KrafterUser? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(c => c.Id == userId, cancellationToken);
        _ = user ?? throw new NotFoundException("User Not Found.");

        IList<string> userRoles = await userManager.GetRolesAsync(user);
        var permissions = new List<string>();
        foreach (KrafterRole role in await roleManager.Roles.AsNoTracking()
                     .Where(r => userRoles.Contains(r.Name!) && r.IsDeleted == false)
                     .ToListAsync(cancellationToken))
        {
            permissions.AddRange(await db.RoleClaims.AsNoTracking()
                .Where(rc =>
                    rc.RoleId == role.Id && rc.ClaimType == KrafterClaims.Permission && rc.IsDeleted == false)
                .Select(rc => rc.ClaimValue!)
                .ToListAsync(cancellationToken));
        }

        return new Response<List<string>> { Data = permissions.Distinct().ToList() };
    }

    public async Task<Response<bool>> HasPermissionAsync(string userId, string permission,
        CancellationToken cancellationToken)
    {
        Response<List<string>>? permissions = await GetPermissionsAsync(userId, cancellationToken);
        return new Response<bool> { Data = permissions?.Data?.Contains(permission) ?? false };
    }

    public async Task<Response> CreateOrUpdateAsync(CreateUserRequest request)
    {
        KrafterUser? user;
        bool isNewUser = string.IsNullOrEmpty(request.Id);

        if (isNewUser)
        {
            KrafterRole? basic = await roleManager.FindByNameAsync(KrafterRoleConstant.Basic);
            if (basic is null)
            {
                throw new NotFoundException("Basic Role Not Found.");
            }

            request.Roles ??= new List<string>();
            request.Roles.Add(basic.Id);

            user = request.Adapt<KrafterUser>();
            user.IsActive = true;

            user.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrWhiteSpace(user.UserName))
            {
                user.UserName = user.Email;
            }

            string password = PasswordGenerator.GeneratePassword();
            IdentityResult result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new KrafterException("An error occurred while creating user.");
            }

            string loginUrl = $"{tenantGetterService.Tenant.TenantLink}/login";
            string emailSubject = "Account Created";
            string emailBody = $"Hello {user.FirstName} {user.LastName},<br/><br/>" +
                               "Your account has been created successfully.,<br/><br/> " +
                               $"Your username/email is:<br/>{user.UserName}<br/><br/>" +
                               $"Your password is:<br/>{password}<br/><br/>" +
                               $"Please <a href='{loginUrl}'>click here</a> to log in.<br/><br/>" +
                               $"Regards,<br/>{tenantGetterService.Tenant.Name} Team";

            await jobService.EnqueueAsync(
                new SendEmailRequestInput { Email = user.Email, Subject = emailSubject, HtmlMessage = emailBody },
                "SendEmailJob", CancellationToken.None);
        }
        else
        {
            user = await userManager.FindByIdAsync(request.Id);
            _ = user ?? throw new NotFoundException("User Not Found.");

            if (!string.IsNullOrWhiteSpace(request.FirstName) && user.FirstName != request.FirstName)
            {
                user.FirstName = request.FirstName;
            }

            if (!string.IsNullOrWhiteSpace(request.LastName) && user.LastName != request.LastName)
            {
                user.LastName = request.LastName;
            }

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && user.PhoneNumber != request.PhoneNumber)
            {
                user.PhoneNumber = request.PhoneNumber;
            }

            if (!string.IsNullOrWhiteSpace(request.Email) && user.Email != request.Email)
            {
                if (request.UpdateTenantEmail)
                {
                    Tenant? firstOrDefaultAsync = await tenantDbContext.Tenants.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(c => c.AdminEmail == user.Email);
                    if (firstOrDefaultAsync is not null)
                    {
                        firstOrDefaultAsync.AdminEmail = request.Email;

                        tenantDbContext.Tenants.Update(firstOrDefaultAsync);
                    }
                }

                user.Email = request.Email;
                user.UserName = request.Email;
            }

            IdentityResult result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                throw new KrafterException($"Update profile failed {result.Errors.ToString()}");
            }

            await signInManager.RefreshSignInAsync(user);
        }

        if (request.Roles.Any())
        {
            List<KrafterUserRole> roles = await db.UserRoles
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantGetterService.Tenant.Id && c.UserId == request.Id)
                .ToListAsync();

            var permissionsToRemove = new List<KrafterUserRole>();
            var permissionsToUpdate = new List<KrafterUserRole>();
            var permissionsToAdd = new List<KrafterUserRole>();

            foreach (KrafterUserRole krafterRoleClaim in roles)
            {
                if (!request.Roles.Contains(krafterRoleClaim.RoleId))
                {
                    krafterRoleClaim.IsDeleted = true;

                    permissionsToRemove.Add(krafterRoleClaim);
                }
            }

            foreach (KrafterUserRole krafterRoleClaim in roles)
            {
                if (request.Roles.Contains(krafterRoleClaim.RoleId))
                {
                    krafterRoleClaim.IsDeleted = false;

                    permissionsToUpdate.Add(krafterRoleClaim);
                }
            }

            foreach (string claim in request.Roles)
            {
                KrafterUserRole? firstOrDefault = roles.FirstOrDefault(c => c.RoleId == claim);
                if (firstOrDefault is null)
                {
                    permissionsToAdd.Add(new KrafterUserRole { RoleId = claim, UserId = user.Id });
                }
            }

            if (permissionsToAdd.Count > 0)
            {
                db.UserRoles.AddRange(permissionsToAdd);
            }

            if (permissionsToRemove.Count > 0)
            {
                db.UserRoles.UpdateRange(permissionsToRemove);
            }

            if (permissionsToUpdate.Any())
            {
                db.UserRoles.UpdateRange(permissionsToUpdate);
            }
        }
        else
        {
            List<KrafterUserRole> roles = await db.UserRoles
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantGetterService.Tenant.Id && c.UserId == request.Id)
                .ToListAsync();

            db.UserRoles.UpdateRange(roles);
        }

        await db.SaveChangesAsync(new List<string>());
        await tenantDbContext.SaveChangesAsync();
        return new Response();
    }
}
