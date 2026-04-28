using System.Security.Claims;
using AditiKraft.Krafter.Backend.Infrastructure.Jobs;
using AditiKraft.Krafter.Backend.Infrastructure.Notifications;
using AditiKraft.Krafter.Backend.Common;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common.Auth;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AditiKraft.Krafter.Backend.Features.Tenants.Common;

public class DataSeedService(
    ITenantGetterService tenantGetterService,
    IJobService jobService,
    ApplicationDbContext applicationDbContext,
    TenantDbContext dbContext,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager) : IScopedHandler
{
    public async Task<Response> SeedBasicData(SeedDataRequest request)
    {
        CurrentTenantDetails currentTenantResponse = tenantGetterService.Tenant;

        int roleCount = await applicationDbContext.Roles.CountAsync();
        if (roleCount == 0)
        {
            var role = new ApplicationRole(RoleConstants.Basic, RoleConstants.Basic)
            {
                Id = Guid.NewGuid().ToString()
            };
            IdentityResult result = await roleManager.CreateAsync(role);
            var role1 = new ApplicationRole(RoleConstants.Admin, RoleConstants.Admin)
            {
                Id = Guid.NewGuid().ToString()
            };
            IdentityResult result1 = await roleManager.CreateAsync(role1);
        }

        ApplicationRole? adminRole = await roleManager.FindByNameAsync(RoleConstants.Admin);
        if (adminRole != null)
        {
            IList<Claim> adminClaims = await roleManager.GetClaimsAsync(adminRole);
            var adMinRolePermissions = adminClaims
                .Where(c => c.Type == AppClaimTypes.Permission).Select(p => p.Value).ToList();

            IReadOnlyList<PermissionDefinition> allPermissions =
                currentTenantResponse.Id == SeedDataConstants.RootTenant.Id
                    ? PermissionCatalog.All
                    : PermissionCatalog.Admin;

            var allPermissionsString = allPermissions.Select(krafterPermission =>
                    PermissionDefinition.NameFor(krafterPermission.Action, krafterPermission.Resource))
                .ToList();
            var permissionNotWithAdmin = allPermissionsString.Except(adMinRolePermissions).ToList();
            if (permissionNotWithAdmin.Count > 0)
            {
                foreach (string permission in permissionNotWithAdmin)
                {
                    applicationDbContext.RoleClaims.Add(new ApplicationRoleClaim
                    {
                        RoleId = adminRole.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = permission
                    });
                }

                await applicationDbContext.SaveChangesAsync();
            }
        }

        ApplicationRole? basicRole = await roleManager.FindByNameAsync(RoleConstants.Basic);
        if (basicRole != null)
        {
            IList<Claim> basicClaims = await roleManager.GetClaimsAsync(basicRole);
            var basicRolePermissions = basicClaims
                .Where(c => c.Type == AppClaimTypes.Permission).Select(p => p.Value).ToList();

            IReadOnlyList<PermissionDefinition> allBasicPermissions =
                PermissionCatalog.Basic;

            var allPermissionsString = allBasicPermissions.Select(krafterPermission =>
                    PermissionDefinition.NameFor(krafterPermission.Action, krafterPermission.Resource))
                .ToList();
            var permissionNotWithBasic = allPermissionsString.Except(basicRolePermissions).ToList();
            if (permissionNotWithBasic.Count > 0)
            {
                foreach (string permission in permissionNotWithBasic)
                {
                    applicationDbContext.RoleClaims.Add(new ApplicationRoleClaim
                    {
                        RoleId = basicRole.Id, ClaimType = AppClaimTypes.Permission, ClaimValue = permission
                    });
                }

                await applicationDbContext.SaveChangesAsync();
            }
        }

        int userCount = await applicationDbContext.Users.CountAsync();
        if (userCount == 0)
        {
            string password = SeedDataConstants.DefaultPassword;
            ApplicationUser rootUser;
            if (tenantGetterService.Tenant.Id == SeedDataConstants.RootTenant.Id)
            {
                rootUser = new ApplicationUser
                {
                    Id = SeedDataConstants.RootUser.Id,
                    FirstName = SeedDataConstants.RootUser.FirstName,
                    LastName = SeedDataConstants.RootUser.LastName,
                    Email = SeedDataConstants.RootUser.EmailAddress,
                    UserName = SeedDataConstants.RootUser.EmailAddress,
                    IsActive = true,
                    IsOwner = true
                };
            }
            else
            {
                rootUser = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    FirstName = "Admin",
                    LastName = "User",
                    Email = tenantGetterService.Tenant.AdminEmail,
                    UserName = tenantGetterService.Tenant.AdminEmail,
                    IsActive = true,
                    IsOwner = true
                };
                password = PasswordGenerator.GeneratePassword();
            }

            IdentityResult res = await userManager.CreateAsync(rootUser, password);
            Tenant? tenant = await dbContext.Tenants.FirstOrDefaultAsync(c => c.Id == tenantGetterService.Tenant.Id);
            if (adminRole is not null)
            {
                applicationDbContext.UserRoles.Add(new ApplicationUserRole
                {
                    RoleId = adminRole.Id, UserId = rootUser.Id, CreatedById = rootUser.Id
                });
            }

            ApplicationRole? basic = await roleManager.FindByNameAsync(RoleConstants.Basic);
            if (basic is not null)
            {
                applicationDbContext.UserRoles.Add(new ApplicationUserRole
                {
                    RoleId = basic.Id, UserId = rootUser.Id, CreatedById = rootUser.Id
                });
            }

            await applicationDbContext.SaveChangesAsync();

            if (tenant is not null)
            {
                if (tenantGetterService.Tenant.Id != SeedDataConstants.RootTenant.Id)
                {
                    string loginUrl = $"{tenantGetterService.Tenant.TenantLink}/login";

                    await jobService.EnqueueAsync(new SendEmailRequestInput
                    {
                        Email = tenant.AdminEmail,
                        Subject = "Welcome to Krafter",
                        HtmlMessage = $"Dear {rootUser.FirstName} {rootUser.LastName} ({rootUser.Email}),<br><br>" +
                                      "Your Krafter account has been successfully created. Here are your login details:<br><br>" +
                                      $"Username: {rootUser.Email}<br>" +
                                      $"Password: {password}<br><br>" +
                                      $"Please <a href='{loginUrl}'>click here</a> to log in.<br><br>" +
                                      "We recommend changing your password after your first login for security reasons.<br><br>" +
                                      "Best Regards,<br>" +
                                      "The Krafter Team"
                    }, nameof(Jobs.SendEmailJob), CancellationToken.None);
                }
            }
        }

        return new Response();
    }
}




