using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Application.BackgroundJobs;
using AditiKraft.Krafter.Backend.Application.Notifications;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Features.Roles._Shared;
using AditiKraft.Krafter.Backend.Features.Tenants._Shared;
using AditiKraft.Krafter.Backend.Features.Users._Shared;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Api.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PasswordGenerator = AditiKraft.Krafter.Backend.Common.PasswordGenerator;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class CreateOrUpdateUser
{
    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        RoleManager<KrafterRole> roleManager,
        ITenantGetterService tenantGetterService,
        TenantDbContext tenantDbContext,
        KrafterContext db,
        IJobService jobService) : IScopedHandler
    {
        public async Task<Response> CreateOrUpdateAsync(CreateUserRequest request)
        {
            KrafterUser? user;
            bool isNewUser = string.IsNullOrEmpty(request.Id);

            if (isNewUser)
            {
                KrafterRole? basic = await roleManager.FindByNameAsync(KrafterRoleConstant.Basic);
                if (basic is null)
                {
                    return new Response { IsError = true, Message = "Basic Role Not Found.", StatusCode = 404 };
                }

                request.Roles ??= new List<string>();
                request.Roles.Add(basic.Id);

                user = new KrafterUser
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
                    return new Response
                    {
                        IsError = true, Message = "An error occurred while creating user.", StatusCode = 400
                    };
                }

                string loginUrl = $"{tenantGetterService.Tenant.TenantLink}/login";
                string emailSubject = "Account Created";
                string emailBody = $"Hello {user.FirstName} {user.LastName},<br/><br/>" +
                                   "Your account has been created successfully.<br/><br/> " +
                                   $"Your username/email is:<br/>{user.UserName}<br/><br/>" +
                                   $"Your password is:<br/>{password}<br/><br/>" +
                                   $"Please <a href='{loginUrl}'>click here</a> to log in.<br/><br/>" +
                                   $"Regards,<br/>{tenantGetterService.Tenant.Name} Team";

                await jobService.EnqueueAsync(
                    new SendEmailRequestInput { Email = user.Email, Subject = emailSubject, HtmlMessage = emailBody },
                    "SendEmailJob",
                    CancellationToken.None);
            }
            else
            {
                user = await userManager.FindByIdAsync(request.Id!);
                if (user is null)
                {
                    return new Response { IsError = true, Message = "User Not Found", StatusCode = 404 };
                }

                if (request.FirstName != user.FirstName)
                {
                    user.FirstName = request.FirstName;
                }

                if (request.LastName != user.LastName)
                {
                    user.LastName = request.LastName;
                }

                if (request.PhoneNumber != user.PhoneNumber)
                {
                    user.PhoneNumber = request.PhoneNumber;
                }

                if (request.Email != user.Email)
                {
                    if (request.UpdateTenantEmail)
                    {
                        Tenant? tenant = await tenantDbContext.Tenants
                            .IgnoreQueryFilters()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.AdminEmail == user.Email);

                        if (tenant is not null)
                        {
                            tenant.AdminEmail = request.Email;
                            tenantDbContext.Tenants.Update(tenant);
                        }
                    }

                    user.Email = request.Email;
                    user.UserName = request.Email;
                }

                IdentityResult result = await userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    return new Response
                    {
                        IsError = true,
                        Message =
                            $"Update profile failed: {string.Join(", ", result.Errors.Select(e => e.Description))}",
                        StatusCode = 400
                    };
                }
            }

            // Handle roles
            if (request.Roles?.Any() == true)
            {
                List<KrafterUserRole> existingRoles = await db.UserRoles
                    .IgnoreQueryFilters()
                    .Where(c => c.TenantId == tenantGetterService.Tenant.Id && c.UserId == user.Id)
                    .ToListAsync();

                var rolesToRemove = existingRoles.Where(r => !request.Roles.Contains(r.RoleId)).ToList();
                var rolesToUpdate = existingRoles.Where(r => request.Roles.Contains(r.RoleId)).ToList();
                var rolesToAdd = request.Roles
                    .Where(roleId => !existingRoles.Any(er => er.RoleId == roleId))
                    .Select(roleId => new KrafterUserRole { RoleId = roleId, UserId = user.Id })
                    .ToList();

                foreach (KrafterUserRole role in rolesToRemove)
                {
                    role.IsDeleted = true;
                }

                foreach (KrafterUserRole role in rolesToUpdate)
                {
                    role.IsDeleted = false;
                }

                if (rolesToAdd.Any())
                {
                    db.UserRoles.AddRange(rolesToAdd);
                }

                if (rolesToRemove.Any())
                {
                    db.UserRoles.UpdateRange(rolesToRemove);
                }

                if (rolesToUpdate.Any())
                {
                    db.UserRoles.UpdateRange(rolesToUpdate);
                }
            }

            await db.SaveChangesAsync([]);
            await tenantDbContext.SaveChangesAsync();

            return new Response();
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Users)
                .AddFluentValidationFilter();

            userGroup.MapPost("/", async (
                    [FromBody] CreateUserRequest request,
                    [FromServices] Handler handler) =>
                {
                    Response res = await handler.CreateOrUpdateAsync(request);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Create, KrafterResource.Users);
        }
    }
}
