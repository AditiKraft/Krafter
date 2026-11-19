using System.Text.Encodings.Web;
using AditiKraft.Krafter.Backend.Common;
using AditiKraft.Krafter.Backend.Common.Tenants;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Tenants.Common;

public sealed class DataSeedService(
    ITenantGetterService tenantGetterService,
    IJobService jobService,
    ApplicationDbContext applicationDbContext,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    IHostEnvironment environment) : IScopedHandler
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        CurrentTenantDetails tenant = tenantGetterService.Tenant;
        if (string.IsNullOrWhiteSpace(tenant.Id) || string.IsNullOrWhiteSpace(tenant.TenantLink))
            throw new InvalidOperationException("Set the tenant before provisioning its identity data.");
        cancellationToken.ThrowIfCancellationRequested();
        if (applicationDbContext.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Save pending changes before provisioning tenant identity data.");

        SendEmailRequestInput? welcome;
        await using (var transaction = await DomainTransaction.BeginAsync(applicationDbContext, cancellationToken))
        {
            welcome = await ProvisionAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        await QueueWelcomeAsync(welcome);
    }

    // The caller owns the registry/identity transaction and must queue the returned email after commit.
    internal async Task<SendEmailRequestInput?> ProvisionAsync(CancellationToken cancellationToken)
    {
        CurrentTenantDetails tenant = tenantGetterService.Tenant;
        cancellationToken.ThrowIfCancellationRequested();
        if (applicationDbContext.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Save pending changes before provisioning tenant identity data.");
        if (applicationDbContext.Database.IsRelational())
        {
            if (applicationDbContext.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Provisioning requires an identity transaction.");
            string key = $"tenant-bootstrap:{tenant.Id}";
            await applicationDbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
        }
        applicationDbContext.ChangeTracker.Clear();
        SendEmailRequestInput? welcome = null;
        // Any identity history means this is an existing or partially provisioned tenant.
        // Include soft-deleted rows without losing tenant isolation. Never repair roles,
        // restore removed permissions, or recreate an intentionally deleted owner.
        if (await applicationDbContext.Users.IgnoreQueryFilters().AnyAsync(
                user => user.TenantId == tenant.Id, cancellationToken) ||
            await applicationDbContext.Roles.IgnoreQueryFilters().AnyAsync(
                role => role.TenantId == tenant.Id, cancellationToken))
            return null;

        bool isRoot = tenant.Id == SeedDataConstants.RootTenant.Id;
        string password;
        if (isRoot)
        {
            string? configuredPassword = configuration["Bootstrap:RootPassword"];
            if (!environment.IsDevelopment() &&
                (string.IsNullOrWhiteSpace(configuredPassword) || configuredPassword == SeedDataConstants.DefaultPassword))
                throw new InvalidOperationException("Configure Bootstrap:RootPassword with a non-default password before provisioning a new production root account.");
            password = string.IsNullOrWhiteSpace(configuredPassword) ? SeedDataConstants.DefaultPassword : configuredPassword;
            if (password.Length > PasswordLimits.MaximumLength)
                throw new InvalidOperationException("Bootstrap:RootPassword exceeds the supported login password length.");
        }
        else
        {
            password = PasswordGenerator.GeneratePassword();
        }
        ApplicationUser owner = CreateOwner();
        // Use Identity's configured password rules before staging any identity rows.
        foreach (IPasswordValidator<ApplicationUser> validator in userManager.PasswordValidators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureSucceeded(await validator.ValidateAsync(userManager, owner, password));
        }
        Response identityValidation = await UserIdentityValidation.ValidateAsync(userManager, owner, cancellationToken);
        if (identityValidation.IsError)
            throw new InvalidOperationException(identityValidation.Message);
        ApplicationRole basic = await CreateRoleAsync(RoleConstants.Basic, PermissionCatalog.Basic, cancellationToken);
        ApplicationRole admin = await CreateRoleAsync(RoleConstants.Admin,
            isRoot ? PermissionCatalog.All : PermissionCatalog.Admin, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        EnsureSucceeded(await userManager.CreateAsync(owner, password));
        cancellationToken.ThrowIfCancellationRequested();

        foreach (ApplicationRole role in new[] { basic, admin })
            applicationDbContext.UserRoles.Add(new ApplicationUserRole
            {
                RoleId = role.Id, UserId = owner.Id, CreatedById = owner.Id
            });
        await applicationDbContext.SaveChangesAsync(cancellationToken);

        if (!isRoot)
        {
            string loginUrl = $"{tenant.TenantLink}/login";
            HtmlEncoder html = HtmlEncoder.Default;
            welcome = new SendEmailRequestInput
            {
                Email = owner.Email!,
                Subject = "Welcome to Krafter",
                HtmlMessage = $"Dear {html.Encode(owner.FirstName ?? string.Empty)} {html.Encode(owner.LastName ?? string.Empty)} ({html.Encode(owner.Email ?? string.Empty)}),<br><br>" +
                              "Your Krafter account has been successfully created. Here are your login details:<br><br>" +
                              $"Username: {html.Encode(owner.Email ?? string.Empty)}<br>" +
                              $"Password: {html.Encode(password)}<br><br>" +
                              $"Please <a href='{html.Encode(loginUrl)}'>click here</a> to log in.<br><br>" +
                              "We recommend changing your password after your first login for security reasons.<br><br>" +
                              "Best Regards,<br>" +
                              "The Krafter Team"
            };
        }

        return welcome;
    }

    internal async Task QueueWelcomeAsync(SendEmailRequestInput? welcome)
    {
        // This is a separate durable job boundary after identity commit. A failed enqueue is
        // surfaced by IJobService; credentials are never queued for rolled-back identities.
        if (welcome is not null)
            await jobService.EnqueueAsync(welcome, nameof(Jobs.SendEmailJob), CancellationToken.None);
    }

    internal Task<Response> ValidateOwnerAsync(CancellationToken cancellationToken)
        => UserIdentityValidation.ValidateAsync(userManager, CreateOwner(), cancellationToken);

    private ApplicationUser CreateOwner()
    {
        bool isRoot = tenantGetterService.Tenant.Id == SeedDataConstants.RootTenant.Id;
        string email = isRoot ? SeedDataConstants.RootUser.EmailAddress : tenantGetterService.Tenant.AdminEmail;
        return new ApplicationUser
        {
            Id = isRoot ? SeedDataConstants.RootUser.Id : Guid.NewGuid().ToString(),
            FirstName = isRoot ? SeedDataConstants.RootUser.FirstName : "Admin",
            LastName = isRoot ? SeedDataConstants.RootUser.LastName : "User",
            Email = email,
            UserName = email,
            IsActive = true,
            IsOwner = true
        };
    }

    private async Task<ApplicationRole> CreateRoleAsync(string name,
        IReadOnlyList<PermissionDefinition> permissions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var role = new ApplicationRole(name, name) { Id = Guid.NewGuid().ToString() };
        EnsureSucceeded(await roleManager.CreateAsync(role));
        cancellationToken.ThrowIfCancellationRequested();
        foreach (PermissionDefinition permission in permissions)
            applicationDbContext.RoleClaims.Add(new ApplicationRoleClaim
            {
                RoleId = role.Id,
                ClaimType = AppClaimTypes.Permission,
                ClaimValue = PermissionDefinition.NameFor(permission.Action, permission.Resource)
            });
        return role;
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Tenant identity provisioning failed: " +
                string.Join(" ", result.Errors.Select(error => error.Description)));
    }
}
