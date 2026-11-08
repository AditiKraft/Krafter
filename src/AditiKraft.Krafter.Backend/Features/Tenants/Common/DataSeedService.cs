using System.Data;
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
    TenantDbContext dbContext,
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

        SendEmailRequestInput? welcome = null;
        // Identity managers save during creation. One transaction must cover the roles, claims,
        // owner and memberships; replicas must check for existing data only after taking the lock.
        await using (var transaction = applicationDbContext.Database.IsRelational()
            ? await applicationDbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            : null) // InMemory unit tests only; concurrency guarantees require PostgreSQL.
        {
            try
            {
                if (transaction is not null)
                {
                    string key = $"tenant-bootstrap:{tenant.Id}";
                    await applicationDbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
                }
                applicationDbContext.ChangeTracker.Clear();

                // Any identity history means this is an existing or partially provisioned tenant.
                // Include soft-deleted rows without losing tenant isolation. Never repair roles,
                // restore removed permissions, or recreate an intentionally deleted owner.
                if (await applicationDbContext.Users.IgnoreQueryFilters().AnyAsync(
                        user => user.TenantId == tenant.Id, cancellationToken) ||
                    await applicationDbContext.Roles.IgnoreQueryFilters().AnyAsync(
                        role => role.TenantId == tenant.Id, cancellationToken))
                    return;

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
                var owner = new ApplicationUser
                {
                    Id = isRoot ? SeedDataConstants.RootUser.Id : Guid.NewGuid().ToString(),
                    FirstName = isRoot ? SeedDataConstants.RootUser.FirstName : "Admin",
                    LastName = isRoot ? SeedDataConstants.RootUser.LastName : "User",
                    Email = isRoot ? SeedDataConstants.RootUser.EmailAddress : tenant.AdminEmail,
                    UserName = isRoot ? SeedDataConstants.RootUser.EmailAddress : tenant.AdminEmail,
                    IsActive = true,
                    IsOwner = true
                };
                // Use Identity's configured password rules before staging any identity rows.
                foreach (IPasswordValidator<ApplicationUser> validator in userManager.PasswordValidators)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    EnsureSucceeded(await validator.ValidateAsync(userManager, owner, password));
                }
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
                    Tenant registeredTenant = await dbContext.Tenants.AsNoTracking()
                        .SingleAsync(registered => registered.Id == tenant.Id, cancellationToken);
                    string loginUrl = $"{tenant.TenantLink}/login";
                    welcome = new SendEmailRequestInput
                    {
                        Email = registeredTenant.AdminEmail,
                        Subject = "Welcome to Krafter",
                        HtmlMessage = $"Dear {owner.FirstName} {owner.LastName} ({owner.Email}),<br><br>" +
                                      "Your Krafter account has been successfully created. Here are your login details:<br><br>" +
                                      $"Username: {owner.Email}<br>" +
                                      $"Password: {password}<br><br>" +
                                      $"Please <a href='{loginUrl}'>click here</a> to log in.<br><br>" +
                                      "We recommend changing your password after your first login for security reasons.<br><br>" +
                                      "Best Regards,<br>" +
                                      "The Krafter Team"
                    };
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (transaction is not null)
                    await transaction.CommitAsync(CancellationToken.None);
            }
            catch
            {
                // A rolled-back Identity save must not leave apparently persisted tracked entities.
                applicationDbContext.ChangeTracker.Clear();
                throw;
            }
        }

        // Send credentials only after identity provisioning commits. Complete this work even if
        // the caller disconnects after commit; reseeding must not reset the owner's credentials.
        if (welcome is not null)
            await jobService.EnqueueAsync(welcome, nameof(Jobs.SendEmailJob), CancellationToken.None);
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
