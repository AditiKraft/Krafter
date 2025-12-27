using Backend.Api;
using Backend.Api.Authorization;
using Backend.Application.Common;
using Backend.Common;
using Backend.Common.Interfaces;
using Backend.Common.Interfaces.Auth;
using Backend.Features.Tenants._Shared;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using FluentValidation;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Features.Users._Shared;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Features.Tenants;

public sealed class CreateOrUpdate
{
    internal sealed class Handler(
        TenantDbContext dbContext,
        KrafterContext krafterContext,
        ITenantGetterService tenantGetterService,
        IServiceProvider serviceProvider,
        ICurrentUser currentUser) : IScopedHandler
    {
        public async Task<Response> CreateOrUpdateAsync(
            Krafter.Shared.Features.Tenants.CreateOrUpdate.CreateOrUpdateTenantRequestInput requestInput)
        {
            if (!string.IsNullOrWhiteSpace(requestInput.Identifier))
            {
                requestInput.Identifier = requestInput.Identifier.Trim();
            }

            if (string.IsNullOrWhiteSpace(requestInput.Id))
            {
                if (!string.IsNullOrWhiteSpace(requestInput.Identifier))
                {
                    Tenant? existingTenant = await dbContext.Tenants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Identifier.ToLower() == requestInput.Identifier.ToLower());
                    if (existingTenant is not null)
                    {
                        return new Response
                        {
                            IsError = true,
                            StatusCode = (int)System.Net.HttpStatusCode.Conflict,
                            Message = "Identifier already exists, please try a different identifier."
                        };
                    }
                }

                requestInput.Id = Guid.NewGuid().ToString();
                Tenant entity = requestInput.Adapt<Tenant>();
                entity.ValidUpto = new DateTime(requestInput.ValidUpto.Value.Year,
                    requestInput.ValidUpto.Value.Month, requestInput.ValidUpto.Value.Day, 0, 0, 0, 0, 0,
                    DateTimeKind.Utc);
                entity.CreatedById = currentUser.GetUserId();

                dbContext.Tenants.Add(entity);
                await dbContext.SaveChangesAsync();
                await krafterContext.SaveChangesAsync([nameof(Tenant)]);
                //TODO need to user hangfire here
                string rootTenantLink = tenantGetterService.Tenant.TenantLink;
                using (IServiceScope scope = serviceProvider.CreateScope())
                {
                    ITenantSetterService requiredService =
                        scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
                    CurrentTenantDetails currentTenantDetails = entity.Adapt<CurrentTenantDetails>();
                    currentTenantDetails.TenantLink =
                        GetSubTenantLinkBasedOnRootTenant(rootTenantLink, requestInput.Identifier);
                    requiredService.SetTenant(currentTenantDetails);
                    DataSeedService seedService = scope.ServiceProvider.GetRequiredService<DataSeedService>();
                    await seedService.SeedBasicData(
                        new Krafter.Shared.Features.Tenants.SeedBasicData.SeedDataRequestInput { });
                }
            }
            else
            {
                Tenant? tenant = await dbContext.Tenants.FirstOrDefaultAsync(c => c.Id == requestInput.Id);
                if (tenant is null)
                {
                    throw new KrafterException(
                        "Unable to find tenant, please try again later or contact support.");
                }

                if (!string.IsNullOrWhiteSpace(requestInput.Name))
                {
                    tenant.Name = requestInput.Name;
                }

                if (!string.IsNullOrWhiteSpace(requestInput.Identifier))
                {
                    tenant.Identifier = requestInput.Identifier;
                }

                if (!string.IsNullOrWhiteSpace(requestInput.AdminEmail) &&
                    requestInput.AdminEmail != tenant.AdminEmail)
                {
                    string rootTenantLink = tenantGetterService.Tenant.TenantLink;
                    using (IServiceScope scope = serviceProvider.CreateScope())
                    {
                        ITenantSetterService requiredService =
                            scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
                        CurrentTenantDetails currentTenantDetails = tenant.Adapt<CurrentTenantDetails>();
                        currentTenantDetails.TenantLink =
                            GetSubTenantLinkBasedOnRootTenant(rootTenantLink, requestInput.Identifier);
                        requiredService.SetTenant(currentTenantDetails);

                        UserManager<KrafterUser> userManager1 =
                            scope.ServiceProvider.GetRequiredService<UserManager<KrafterUser>>();
                        IUserService userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                        KrafterUser? user = await userManager1.Users.AsNoTracking()
                            .FirstOrDefaultAsync(c => c.NormalizedEmail == tenant.AdminEmail.ToUpper());
                        if (user is not null)
                        {
                            await userService.CreateOrUpdateAsync(new CreateUserRequest
                            {
                                Id = user.Id, Email = requestInput.AdminEmail, UpdateTenantEmail = false
                            });
                        }
                    }

                    tenant.AdminEmail = requestInput.AdminEmail;
                }

                if (requestInput.IsActive.HasValue)
                {
                    tenant.IsActive = requestInput.IsActive ?? false;
                }

                if (requestInput.ValidUpto.HasValue)
                {
                    tenant.ValidUpto = requestInput.ValidUpto.Value;
                }

                await dbContext.SaveChangesAsync();
                await krafterContext.SaveChangesAsync([nameof(Tenant)]);
            }

            return new Response();
        }

        internal string GetSubTenantLinkBasedOnRootTenant(string tenantDomain, string identifier)
        {
            if (tenantDomain.EndsWith("/"))
            {
                tenantDomain = tenantDomain.Substring(0, tenantDomain.Length - 1);
            }

            // Check if running on localhost and return tenantDomain as it is
            if (tenantDomain.Contains("localhost"))
            {
                return tenantDomain;
            }

            string scheme = "";
            string domain = tenantDomain;

            // Extract scheme if present
            if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                scheme = "https://";
                domain = domain.Substring(8);
            }
            else if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                scheme = "http://";
                domain = domain.Substring(7);
            }

            string[] parts = domain.Split('.');
            if (parts.Length > 2)
            {
                // Replace only the root subdomain
                parts[0] = identifier;
                domain = string.Join(".", parts);
                return scheme + domain;
            }
            else
            {
                // No subdomain, just prepend identifier
                domain = identifier + "." + domain;
                return scheme + domain;
            }
        }
    }


    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder tenant = endpointRouteBuilder.MapGroup(KrafterRoute.Tenants).AddFluentValidationFilter();

            tenant.MapPost("/create-or-update", async
            ([FromBody] Krafter.Shared.Features.Tenants.CreateOrUpdate.CreateOrUpdateTenantRequestInput requestInput,
                [FromServices] Handler handler) =>
            {
                Response res = await handler.CreateOrUpdateAsync(requestInput);
                return TypedResults.Ok(res);
            }).MustHavePermission(KrafterAction.Create, KrafterResource.Tenants);

            //tenant creation from landing page , allow Anonymous access
            tenant.MapPost("/create", async
                ([FromBody] Krafter.Shared.Features.Tenants.CreateOrUpdate.CreateOrUpdateTenantRequestInput requestInput,
                    [FromServices] Handler handler) =>
                {
                    requestInput.ValidUpto = DateTime.UtcNow.AddDays(7);
                    requestInput.IsActive = true;
                    Response res = await handler.CreateOrUpdateAsync(requestInput);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .AllowAnonymous();
        }
    }
}
