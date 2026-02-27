using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Api.Authorization;
using AditiKraft.Krafter.Backend.Common.Interfaces;
using AditiKraft.Krafter.Backend.Jobs;
using AditiKraft.Krafter.Backend.Notifications;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
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

public sealed class CreateUser
{
    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        RoleManager<KrafterRole> roleManager,
        ITenantGetterService tenantGetterService,
        KrafterContext db,
        IJobService jobService) : IScopedHandler
    {
        public async Task<Response> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
        {
            KrafterRole? basic = await roleManager.FindByNameAsync(KrafterRoleConstant.Basic);
            if (basic is null)
            {
                return Response.NotFound("Basic Role Not Found.");
            }

            request.Id = null;
            request.Roles ??= [];
            if (!request.Roles.Contains(basic.Id))
            {
                request.Roles.Add(basic.Id);
            }

            var user = new KrafterUser
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

            string loginUrl = $"{tenantGetterService.Tenant.TenantLink}/login";
            string emailSubject = "Account Created";
            string emailBody = $"Hello {user.FirstName} {user.LastName},<br/><br/>" +
                               "Your account has been created successfully.<br/><br/> " +
                               $"Your username/email is:<br/>{user.UserName}<br/><br/>" +
                               $"Your password is:<br/>{password}<br/><br/>" +
                               $"Please <a href='{loginUrl}'>click here</a> to log in.<br/><br/>" +
                               $"Regards,<br/>{tenantGetterService.Tenant.Name} Team";

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                await jobService.EnqueueAsync(
                    new SendEmailRequestInput { Email = user.Email, Subject = emailSubject, HtmlMessage = emailBody },
                    "SendEmailJob",
                    cancellationToken);
            }

            await SyncRolesAsync(user.Id, request.Roles, cancellationToken);
            await db.SaveChangesAsync([], true, cancellationToken);

            return new Response();
        }

        private async Task SyncRolesAsync(string userId, IReadOnlyCollection<string> requestedRoles,
            CancellationToken cancellationToken)
        {
            List<KrafterUserRole> existingRoles = await db.UserRoles
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantGetterService.Tenant.Id && c.UserId == userId)
                .ToListAsync(cancellationToken);

            var rolesToRemove = existingRoles.Where(r => !requestedRoles.Contains(r.RoleId)).ToList();
            var rolesToUpdate = existingRoles.Where(r => requestedRoles.Contains(r.RoleId)).ToList();
            var rolesToAdd = requestedRoles
                .Where(roleId => !existingRoles.Any(er => er.RoleId == roleId))
                .Select(roleId => new KrafterUserRole { RoleId = roleId, UserId = userId })
                .ToList();

            foreach (KrafterUserRole role in rolesToRemove)
            {
                role.IsDeleted = true;
            }

            foreach (KrafterUserRole role in rolesToUpdate)
            {
                role.IsDeleted = false;
            }

            if (rolesToAdd.Count > 0)
            {
                db.UserRoles.AddRange(rolesToAdd);
            }

            if (rolesToRemove.Count > 0)
            {
                db.UserRoles.UpdateRange(rolesToRemove);
            }

            if (rolesToUpdate.Count > 0)
            {
                db.UserRoles.UpdateRange(rolesToUpdate);
            }
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
                    [FromServices] Handler handler,
                    CancellationToken cancellationToken) =>
                {
                    Response res = await handler.CreateAsync(request, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Create, KrafterResource.Users);
        }
    }
}


