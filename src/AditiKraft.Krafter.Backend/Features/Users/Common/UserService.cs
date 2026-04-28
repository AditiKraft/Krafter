using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Common;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Users.Common;

public static class QueryStringKeys
{
    public const string Code = "code";
    public const string UserId = "userId";
}

public static class SeedDataConstants
{
    public static class RootUser
    {
        public const string Id = "root";
        public const string LastName = "Admin";
        public const string FirstName = "Admin";
        public const string EmailAddress = "admin@example.com";
    }

    public static class RootTenant
    {
        public const string Id = "root";

        public const string Identifier = DefaultTenantConstants.Identifier;
        public const string Name = DefaultTenantConstants.Name;
    }

    public const string DefaultPassword = "123Pa$$word!";

    public static Tenant DefaultTenant { private set; get; } = new()
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
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ITenantGetterService tenantGetterService,
    TenantDbContext tenantDbContext,
    ApplicationDbContext db,
    IJobService jobService)
    : IUserService, IScopedService
{
    public async Task<Response<List<string>>> GetPermissionsAsync(string userId, CancellationToken cancellationToken)
    {
        //var user = await userManager.Asn.FindByIdAsync(userId);
        ApplicationUser? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(c => c.Id == userId, cancellationToken);
        if (user is null)
        {
            return Response<List<string>>.NotFound("User Not Found.");
        }

        IList<string> userRoles = await userManager.GetRolesAsync(user);
        var permissions = new List<string>();
        foreach (ApplicationRole role in await roleManager.Roles.AsNoTracking()
                     .Where(r => userRoles.Contains(r.Name!) && r.IsDeleted == false)
                     .ToListAsync(cancellationToken))
        {
            permissions.AddRange(await db.RoleClaims.AsNoTracking()
                .Where(rc =>
                    rc.RoleId == role.Id && rc.ClaimType == AppClaimTypes.Permission && rc.IsDeleted == false)
                .Select(rc => rc.ClaimValue!)
                .ToListAsync(cancellationToken));
        }

        return Response<List<string>>.Success(permissions.Distinct().ToList());
    }

    public async Task<Response<bool>> HasPermissionAsync(string userId, string permission,
        CancellationToken cancellationToken)
    {
        Response<List<string>>? permissions = await GetPermissionsAsync(userId, cancellationToken);
        if (permissions.IsError)
        {
            return Response<bool>.NotFound(permissions.Message ?? "User Not Found.");
        }

        return Response<bool>.Success(permissions?.Data?.Contains(permission) ?? false);
    }

    public async Task<Response> CreateOrUpdateAsync(CreateUserRequest request)
    {
        ApplicationUser? user;
        bool isNewUser = string.IsNullOrEmpty(request.Id);

        if (isNewUser)
        {
            ApplicationRole? basic = await roleManager.FindByNameAsync(RoleConstants.Basic);
            if (basic is null)
            {
                return Response.NotFound("Basic Role Not Found.");
            }

            request.Roles ??= new List<string>();
            request.Roles.Add(basic.Id);

            user = request.Adapt<ApplicationUser>();
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
                return Response.BadRequest("An error occurred while creating user.");
            }

            string loginUrl = $"{tenantGetterService.Tenant.TenantLink}/login";
            string emailSubject = "Account Created";
            string emailBody = $"Hello {user.FirstName} {user.LastName},<br/><br/>" +
                               "Your account has been created successfully.,<br/><br/> " +
                               $"Your username/email is:<br/>{user.UserName}<br/><br/>" +
                               $"Your password is:<br/>{password}<br/><br/>" +
                               $"Please <a href='{loginUrl}'>click here</a> to log in.<br/><br/>" +
                               $"Regards,<br/>{tenantGetterService.Tenant.Name} Team";

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                await jobService.EnqueueAsync(
                    new SendEmailRequestInput { Email = user.Email, Subject = emailSubject, HtmlMessage = emailBody },
                    nameof(Jobs.SendEmailJob), CancellationToken.None);
            }
        }
        else
        {
            user = await userManager.FindByIdAsync(request.Id!);
            if (user is null)
            {
                return Response.NotFound("User Not Found.");
            }

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
                return Response.BadRequest($"Update profile failed {result.Errors.ToString()}");
            }

            await signInManager.RefreshSignInAsync(user);
        }

        if (request.Roles.Any())
        {
            List<ApplicationUserRole> roles = await db.UserRoles
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantGetterService.Tenant.Id && c.UserId == request.Id)
                .ToListAsync();

            var permissionsToRemove = new List<ApplicationUserRole>();
            var permissionsToUpdate = new List<ApplicationUserRole>();
            var permissionsToAdd = new List<ApplicationUserRole>();

            foreach (ApplicationUserRole krafterRoleClaim in roles)
            {
                if (!request.Roles.Contains(krafterRoleClaim.RoleId))
                {
                    krafterRoleClaim.IsDeleted = true;

                    permissionsToRemove.Add(krafterRoleClaim);
                }
            }

            foreach (ApplicationUserRole krafterRoleClaim in roles)
            {
                if (request.Roles.Contains(krafterRoleClaim.RoleId))
                {
                    krafterRoleClaim.IsDeleted = false;

                    permissionsToUpdate.Add(krafterRoleClaim);
                }
            }

            foreach (string claim in request.Roles)
            {
                ApplicationUserRole? firstOrDefault = roles.FirstOrDefault(c => c.RoleId == claim);
                if (firstOrDefault is null)
                {
                    permissionsToAdd.Add(new ApplicationUserRole { RoleId = claim, UserId = user.Id });
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
            List<ApplicationUserRole> roles = await db.UserRoles
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantGetterService.Tenant.Id && c.UserId == request.Id)
                .ToListAsync();

            db.UserRoles.UpdateRange(roles);
        }

        await db.SaveChangesAsync(new List<string>());
        await tenantDbContext.SaveChangesAsync();
        return Response.Success();
    }
}




