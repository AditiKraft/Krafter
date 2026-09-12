using AditiKraft.Krafter.Backend.Common;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Users.Common;

public sealed class UserMutationService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ITenantGetterService tenantGetterService,
    TenantDbContext tenantDbContext,
    ApplicationDbContext db,
    IJobService jobService) : IUserMutationService, IScopedService
{
    public async Task<Response> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        ApplicationRole? basic = await roleManager.FindByNameAsync(RoleConstants.Basic);
        if (basic is null)
        {
            return Response.NotFound("Basic Role Not Found.");
        }

        HashSet<string> roles = (request.Roles ?? []).ToHashSet(StringComparer.Ordinal);
        roles.Add(basic.Id);
        if (!await RolesExistAsync(roles, cancellationToken))
        {
            return Response.BadRequest("One or more roles were not found in the current tenant.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            UserName = string.IsNullOrWhiteSpace(request.UserName) ? request.Email : request.UserName,
            PhoneNumber = request.PhoneNumber,
            IsActive = true
        };

        string password = PasswordGenerator.GeneratePassword();
        IdentityResult result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return Response.BadRequest("An error occurred while creating user.");
        }

        await SyncRolesAsync(user.Id, roles, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await SendAccountEmailAsync(user, password, cancellationToken);
        return Response.Success();
    }

    public async Task<Response> UpdateAsync(string id, CreateUserRequest request, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Response.NotFound("User Not Found");
        }

        // Existing callers omit roles when they only update a profile.
        HashSet<string>? roles = request.Roles is { Count: > 0 }
            ? request.Roles.ToHashSet(StringComparer.Ordinal)
            : null;
        if (roles is not null && !await RolesExistAsync(roles, cancellationToken))
        {
            return Response.BadRequest("One or more roles were not found in the current tenant.");
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.PhoneNumber = request.PhoneNumber;
        Response result = await SaveUserAsync(user, request.Email, request.UpdateTenantEmail, cancellationToken);
        if (result.IsError)
        {
            return result;
        }

        if (roles is not null)
        {
            await SyncRolesAsync(user.Id, roles, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Response.Success();
    }

    public async Task<Response> UpdateEmailAsync(string id, string email, CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.FindByIdAsync(id);
        return user is null
            ? Response.NotFound("User Not Found")
            : await SaveUserAsync(user, email, updateTenantEmail: false, cancellationToken);
    }

    private async Task<Response> SaveUserAsync(
        ApplicationUser user, string email, bool updateTenantEmail, CancellationToken cancellationToken)
    {
        string? previousEmail = user.Email;
        bool emailChanged = email != previousEmail;
        if (emailChanged)
        {
            user.Email = email;
            user.UserName = email;
        }

        IdentityResult result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return Response.BadRequest(
                $"Update profile failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        if (emailChanged && updateTenantEmail)
        {
            Tenant? tenant = await tenantDbContext.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantGetterService.Tenant.Id && t.AdminEmail == previousEmail,
                    cancellationToken);
            if (tenant is not null)
            {
                tenant.AdminEmail = email;
                await tenantDbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return Response.Success();
    }

    private async Task<bool> RolesExistAsync(HashSet<string> roles, CancellationToken cancellationToken)
        => await db.Roles.CountAsync(role => roles.Contains(role.Id), cancellationToken) == roles.Count;

    private async Task SyncRolesAsync(string userId, HashSet<string> requestedRoles, CancellationToken cancellationToken)
    {
        List<ApplicationUserRole> existingRoles = await db.UserRoles.IgnoreQueryFilters()
            .Where(role => role.TenantId == tenantGetterService.Tenant.Id && role.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (ApplicationUserRole role in existingRoles)
        {
            role.IsDeleted = !requestedRoles.Contains(role.RoleId);
        }

        HashSet<string> existingIds = existingRoles.Select(role => role.RoleId).ToHashSet(StringComparer.Ordinal);
        db.UserRoles.AddRange(requestedRoles.Where(roleId => !existingIds.Contains(roleId))
            .Select(roleId => new ApplicationUserRole { RoleId = roleId, UserId = userId }));
    }

    private async Task SendAccountEmailAsync(ApplicationUser user, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        string loginUrl = $"{tenantGetterService.Tenant.TenantLink}/login";
        string emailBody = $"Hello {user.FirstName} {user.LastName},<br/><br/>" +
                           "Your account has been created successfully.<br/><br/> " +
                           $"Your username/email is:<br/>{user.UserName}<br/><br/>" +
                           $"Your password is:<br/>{password}<br/><br/>" +
                           $"Please <a href='{loginUrl}'>click here</a> to log in.<br/><br/>" +
                           $"Regards,<br/>{tenantGetterService.Tenant.Name} Team";
        await jobService.EnqueueAsync(
            new SendEmailRequestInput { Email = user.Email, Subject = "Account Created", HtmlMessage = emailBody },
            nameof(Jobs.SendEmailJob), cancellationToken);
    }
}
