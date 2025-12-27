using System.Security.Claims;
using Backend.Application.BackgroundJobs;
using Backend.Application.Notifications;
using Backend.Common;
using Backend.Common.Interfaces;
using Backend.Features.Roles._Shared;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using Krafter.Shared.Common.Auth;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static Backend.Features.Tenants.SeedBasicData;

namespace Backend.Features.Tenants._Shared;

public class DataSeedService(
    ITenantGetterService tenantGetterService,
    IJobService jobService,
    KrafterContext krafterContext,
    TenantDbContext dbContext,
    RoleManager<KrafterRole> roleManager,
    UserManager<KrafterUser> userManager) : IScopedHandler
{
    public async Task<Response> SeedBasicData(
        Krafter.Shared.Features.Tenants.SeedBasicData.SeedDataRequestInput requestInput)
    {
        CurrentTenantDetails currentTenantResponse = tenantGetterService.Tenant;

        int roleCount = await krafterContext.Roles.CountAsync();
        if (roleCount == 0)
        {
            var role = new KrafterRole(KrafterRoleConstant.Basic, KrafterRoleConstant.Basic)
            {
                Id = Guid.NewGuid().ToString()
            };
            IdentityResult result = await roleManager.CreateAsync(role);
            var role1 = new KrafterRole(KrafterRoleConstant.Admin, KrafterRoleConstant.Admin)
            {
                Id = Guid.NewGuid().ToString()
            };
            IdentityResult result1 = await roleManager.CreateAsync(role1);
        }

        KrafterRole? adminRole = await roleManager.FindByNameAsync(KrafterRoleConstant.Admin);
        if (adminRole != null)
        {
            IList<Claim> adminClaims = await roleManager.GetClaimsAsync(adminRole);
            var adMinRolePermissions = adminClaims
                .Where(c => c.Type == KrafterClaims.Permission).Select(p => p.Value).ToList();

            IReadOnlyList<KrafterPermission> allPermissions =
                currentTenantResponse.Id == KrafterInitialConstants.RootTenant.Id
                    ? KrafterPermissions.All
                    : KrafterPermissions.Admin;

            var allPermissionsString = allPermissions.Select(krafterPermission =>
                    KrafterPermission.NameFor(krafterPermission.Action, krafterPermission.Resource))
                .ToList();
            var permissionNotWithAdmin = allPermissionsString.Except(adMinRolePermissions).ToList();
            if (permissionNotWithAdmin.Count > 0)
            {
                foreach (string permission in permissionNotWithAdmin)
                {
                    krafterContext.RoleClaims.Add(new KrafterRoleClaim
                    {
                        RoleId = adminRole.Id, ClaimType = KrafterClaims.Permission, ClaimValue = permission
                    });
                }

                await krafterContext.SaveChangesAsync();
            }
        }

        KrafterRole? basicRole = await roleManager.FindByNameAsync(KrafterRoleConstant.Basic);
        if (basicRole != null)
        {
            IList<Claim> basicClaims = await roleManager.GetClaimsAsync(basicRole);
            var basicRolePermissions = basicClaims
                .Where(c => c.Type == KrafterClaims.Permission).Select(p => p.Value).ToList();

            IReadOnlyList<KrafterPermission> allBasicPermissions =
                KrafterPermissions.Basic;

            var allPermissionsString = allBasicPermissions.Select(krafterPermission =>
                    KrafterPermission.NameFor(krafterPermission.Action, krafterPermission.Resource))
                .ToList();
            var permissionNotWithBasic = allPermissionsString.Except(basicRolePermissions).ToList();
            if (permissionNotWithBasic.Count > 0)
            {
                foreach (string permission in permissionNotWithBasic)
                {
                    krafterContext.RoleClaims.Add(new KrafterRoleClaim
                    {
                        RoleId = basicRole.Id, ClaimType = KrafterClaims.Permission, ClaimValue = permission
                    });
                }

                await krafterContext.SaveChangesAsync();
            }
        }

        int userCount = await krafterContext.Users.CountAsync();
        if (userCount == 0)
        {
            string password = KrafterInitialConstants.DefaultPassword;
            KrafterUser rootUser;
            if (tenantGetterService.Tenant.Id == KrafterInitialConstants.RootTenant.Id)
            {
                rootUser = new KrafterUser
                {
                    Id = KrafterInitialConstants.RootUser.Id,
                    FirstName = KrafterInitialConstants.RootUser.FirstName,
                    LastName = KrafterInitialConstants.RootUser.LastName,
                    Email = KrafterInitialConstants.RootUser.EmailAddress,
                    UserName = KrafterInitialConstants.RootUser.EmailAddress,
                    IsActive = true,
                    IsOwner = true
                };
            }
            else
            {
                rootUser = new KrafterUser
                {
                    Id = Guid.NewGuid().ToString(),
                    FirstName = "Admin",
                    LastName = "User",
                    Email = tenantGetterService.Tenant.AdminEmail,
                    UserName = tenantGetterService.Tenant.AdminEmail,
                    IsActive = true,
                    IsOwner = true
                };
                //generate aspnet core valid password
                password = PasswordGenerator.GeneratePassword();
            }

            IdentityResult res = await userManager.CreateAsync(rootUser, password);
            Tenant? tenant = await dbContext.Tenants.FirstOrDefaultAsync(c => c.Id == tenantGetterService.Tenant.Id);
            if (adminRole is not null)
            {
                krafterContext.UserRoles.Add(new KrafterUserRole
                {
                    RoleId = adminRole.Id, UserId = rootUser.Id, CreatedById = rootUser.Id
                });
            }

            KrafterRole? basic = await roleManager.FindByNameAsync(KrafterRoleConstant.Basic);
            if (basic is not null)
            {
                krafterContext.UserRoles.Add(new KrafterUserRole
                {
                    RoleId = basic.Id, UserId = rootUser.Id, CreatedById = rootUser.Id
                });
            }

            await krafterContext.SaveChangesAsync();

            if (tenant is not null)
            {
                if (tenantGetterService.Tenant.Id != KrafterInitialConstants.RootTenant.Id)
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
                    }, "SendEmailJob", CancellationToken.None);
                }
            }
        }

        return new Response();
    }
}
