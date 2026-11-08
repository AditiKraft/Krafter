using System.Text.Encodings.Web;
using AditiKraft.Krafter.Backend.Common;
using AditiKraft.Krafter.Backend.Common.Auth;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
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
    IJobService jobService,
    ICurrentUser currentUser,
    IUserService userService) : IUserMutationService, IScopedService
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

        if (roles.Any(roleId => roleId != basic.Id) && !await CanChangeRolesAsync(cancellationToken))
        {
            return Response.Forbidden("You are not authorized to change user roles.");
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

        Response validation = UserIdentityValidation.Validate(userManager, user.Email, user.UserName);
        if (validation.IsError)
            return validation;
        if (await IdentifierExistsAsync(user.Email!, user.UserName!, user.Id, cancellationToken))
            return Response.Conflict(IdentityConflict.UserMessage);

        string password = PasswordGenerator.GeneratePassword();
        await using (var transaction = await DomainTransaction.BeginAsync(db, cancellationToken))
        {
            try
            {
                IdentityResult result = await userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                    return IdentityConflict.IsDuplicate(result)
                        ? Response.Conflict(IdentityConflict.UserMessage)
                        : Response.BadRequest("An error occurred while creating user.");

                cancellationToken.ThrowIfCancellationRequested();
                await SyncRolesAsync(user.Id, roles, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IdentityConflict.IsUser(exception))
            {
                return Response.Conflict(IdentityConflict.UserMessage);
            }
        }
        await SendAccountEmailAsync(user, password, CancellationToken.None);
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

        if (roles is not null)
        {
            List<string> currentRoles = await db.UserRoles.AsNoTracking()
                .Where(role => role.UserId == user.Id && !role.Role.IsDeleted)
                .Select(role => role.RoleId).ToListAsync(cancellationToken);
            if (roles.SetEquals(currentRoles))
            {
                // Do not synchronize an unchanged submission: a concurrent removal must stay removed.
                roles = null;
            }
            else if (!await CanChangeRolesAsync(cancellationToken))
            {
                return Response.Forbidden("You are not authorized to change user roles.");
            }
        }

        bool changesLoginEmail = !string.Equals(userManager.NormalizeEmail(user.Email),
            userManager.NormalizeEmail(request.Email), StringComparison.Ordinal);
        if (changesLoginEmail && user.Id != currentUser.GetUserId() &&
            !await CanChangeRolesAsync(cancellationToken))
        {
            // Changing another account's login email can transfer all of its existing permissions.
            return Response.Forbidden("You are not authorized to change another user's login email.");
        }

        Response validation = await ValidateIdentifiersAsync(user, request.Email, cancellationToken);
        if (validation.IsError)
            return validation;

        await using var transaction = await DomainTransaction.BeginAsync(db, cancellationToken,
            request.UpdateTenantEmail ? tenantDbContext : null);
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

        await transaction.CommitAsync(cancellationToken);
        return Response.Success();
    }

    public async Task<Response> UpdateEmailAsync(string id, string email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ApplicationUser? user = await userManager.FindByIdAsync(id);
        if (user is null)
            return Response.NotFound("User Not Found");
        Response validation = await ValidateIdentifiersAsync(user, email, cancellationToken);
        if (validation.IsError)
            return validation;

        // Tenant updates already own the shared registry/identity transaction. Standalone callers
        // get the same cancellation and tracked-state cleanup guarantees for their identity write.
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is not null
            ? null : await DomainTransaction.BeginAsync(db, cancellationToken);
        Response result = await SaveUserAsync(user, email, updateTenantEmail: false, cancellationToken);
        if (!result.IsError && transaction is not null)
            await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task<Response> SaveUserAsync(
        ApplicationUser user, string email, bool updateTenantEmail, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? previousEmail = user.Email;
        bool emailChanged = email != previousEmail;
        if (emailChanged)
        {
            user.Email = email;
            user.UserName = email;
        }

        IdentityResult result;
        try
        {
            result = await userManager.UpdateAsync(user);
        }
        catch (DbUpdateException exception) when (IdentityConflict.IsUser(exception))
        {
            db.Entry(user).State = EntityState.Detached;
            return Response.Conflict(IdentityConflict.UserMessage);
        }
        if (!result.Succeeded)
        {
            db.Entry(user).State = EntityState.Detached;
            return IdentityConflict.IsDuplicate(result) ? Response.Conflict(IdentityConflict.UserMessage) : Response.BadRequest(
                $"Update profile failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        if (emailChanged && updateTenantEmail)
        {
            Tenant? tenant = await tenantDbContext.Tenants
                .FirstOrDefaultAsync(t => t.Id == tenantGetterService.Tenant.Id && (user.IsOwner || t.AdminEmail == previousEmail),
                    cancellationToken);
            if (tenant is null && user.IsOwner)
                return Response.Conflict("The tenant registry is unavailable; the owner's email could not be synchronized.");
            if (tenant is not null)
            {
                tenant.AdminEmail = email;
                await tenantDbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return Response.Success();
    }

    private async Task<Response> ValidateIdentifiersAsync(ApplicationUser user, string email,
        CancellationToken cancellationToken)
    {
        string userName = email != user.Email ? email : user.UserName!;
        Response validation = UserIdentityValidation.Validate(userManager, email, userName);
        if (validation.IsError)
            return validation;
        return await IdentifierExistsAsync(email, userName, user.Id, cancellationToken)
            ? Response.Conflict(IdentityConflict.UserMessage) : Response.Success();
    }

    private Task<bool> IdentifierExistsAsync(string email, string userName, string excludedId,
        CancellationToken cancellationToken)
    {
        string? normalizedEmail = userManager.NormalizeEmail(email);
        string? normalizedName = userManager.NormalizeName(userName);
        return db.Users.IgnoreQueryFilters().AnyAsync(candidate =>
            candidate.TenantId == tenantGetterService.Tenant.Id && candidate.Id != excludedId &&
            (candidate.NormalizedEmail == normalizedEmail || candidate.NormalizedUserName == normalizedName), cancellationToken);
    }

    private async Task<bool> RolesExistAsync(HashSet<string> roles, CancellationToken cancellationToken)
        => await db.Roles.CountAsync(role => roles.Contains(role.Id), cancellationToken) == roles.Count;

    private async Task<bool> CanChangeRolesAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated() || string.IsNullOrWhiteSpace(currentUser.GetUserId()))
        {
            return false;
        }

        Response<bool> permission = await userService.HasPermissionAsync(currentUser.GetUserId(),
            PermissionDefinition.NameFor(PermissionAction.Update, PermissionResource.UserRoles), cancellationToken);
        return !permission.IsError && permission.Data;
    }

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
        HtmlEncoder html = HtmlEncoder.Default;
        string emailBody = $"Hello {html.Encode(user.FirstName ?? string.Empty)} {html.Encode(user.LastName ?? string.Empty)},<br/><br/>" +
                           "Your account has been created successfully.<br/><br/> " +
                           $"Your username/email is:<br/>{html.Encode(user.UserName ?? string.Empty)}<br/><br/>" +
                           $"Your password is:<br/>{html.Encode(password)}<br/><br/>" +
                           $"Please <a href='{html.Encode(loginUrl)}'>click here</a> to log in.<br/><br/>" +
                           $"Regards,<br/>{html.Encode(tenantGetterService.Tenant.Name ?? string.Empty)} Team";
        await jobService.EnqueueAsync(
            new SendEmailRequestInput { Email = user.Email, Subject = "Account Created", HtmlMessage = emailBody },
            nameof(Jobs.SendEmailJob), cancellationToken);
    }
}
