using Backend.Api;
using Backend.Api.Authorization;
using Backend.Application.BackgroundJobs;
using Backend.Application.Notifications;
using Backend.Common;
using Backend.Common.Auth.Permissions;
using Backend.Common.Interfaces;
using Backend.Common.Models;
using Backend.Features.Auth;
using Backend.Features.Roles._Shared;
using Backend.Features.Tenants;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Users;

public sealed class CreateOrUpdateUser
{
    public sealed class CreateUserRequest
    {
        public string? Id { get; set; }
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? UserName { get; set; }
        public string? PhoneNumber { get; set; }
        public List<string>? Roles { get; set; }
        public bool UpdateTenantEmail { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public bool IsExternalLogin { get; set; }
    }

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
                user = await userManager.FindByIdAsync(request.Id);
                if (user is null)
                {
                    return new Response { IsError = true, Message = "User Not Found", StatusCode = 404 };
                }

                if (!string.IsNullOrWhiteSpace(request.FirstName))
                {
                    user.FirstName = request.FirstName;
                }

                if (!string.IsNullOrWhiteSpace(request.LastName))
                {
                    user.LastName = request.LastName;
                }

                if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                {
                    user.PhoneNumber = request.PhoneNumber;
                }

                if (!string.IsNullOrWhiteSpace(request.Email) && user.Email != request.Email)
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

    internal sealed class Validator : AbstractValidator<CreateUserRequest>
    {
        public Validator()
        {
            RuleFor(p => p.FirstName)
                .NotEmpty().WithMessage("First name is required")
                .MaximumLength(100).WithMessage("First name cannot exceed 100 characters");

            RuleFor(p => p.LastName)
                .NotEmpty().WithMessage("Last name is required")
                .MaximumLength(100).WithMessage("Last name cannot exceed 100 characters");

            RuleFor(p => p.Email)
                .NotEmpty().WithMessage("Email is required")
                .EmailAddress().WithMessage("Invalid email format")
                .MaximumLength(256).WithMessage("Email cannot exceed 256 characters");

            RuleFor(p => p.PhoneNumber)
                .MaximumLength(20).When(p => !string.IsNullOrWhiteSpace(p.PhoneNumber))
                .WithMessage("Phone number cannot exceed 20 characters");
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Users)
                .AddFluentValidationFilter();

            userGroup.MapPost("/create-or-update", async (
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
