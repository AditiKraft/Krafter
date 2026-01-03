using Backend.Api;
using Backend.Api.Authorization;
using Backend.Common.Interfaces;
using Backend.Common.Interfaces.Auth;
using Backend.Features.Tenants._Shared;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Tenants;
using Krafter.Shared.Contracts.Users;
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
        public async Task<Response> CreateOrUpdateAsync(CreateOrUpdateTenantRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.Identifier))
            {
                request.Identifier = request.Identifier.Trim();
            }

            if (string.IsNullOrWhiteSpace(request.Id))
            {
                if (!string.IsNullOrWhiteSpace(request.Identifier))
                {
                    Tenant? existingTenant = await dbContext.Tenants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(c => c.Identifier.ToLower() == request.Identifier.ToLower());
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

                request.Id = Guid.NewGuid().ToString();
                Tenant entity = request.Adapt<Tenant>();
                entity.ValidUpto = new DateTime(request.ValidUpto!.Value.Year,
                    request.ValidUpto.Value.Month, request.ValidUpto.Value.Day, 0, 0, 0, 0, 0,
                    DateTimeKind.Utc);
                entity.CreatedById = currentUser.GetUserId();

                dbContext.Tenants.Add(entity);
                await dbContext.SaveChangesAsync();
                await krafterContext.SaveChangesAsync([nameof(Tenant)]);

                string rootTenantLink = tenantGetterService.Tenant.TenantLink;
                using (IServiceScope scope = serviceProvider.CreateScope())
                {
                    ITenantSetterService requiredService =
                        scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
                    CurrentTenantDetails currentTenantDetails = entity.Adapt<CurrentTenantDetails>();
                    currentTenantDetails.TenantLink =
                        GetSubTenantLinkBasedOnRootTenant(rootTenantLink, request.Identifier);
                    requiredService.SetTenant(currentTenantDetails);
                    DataSeedService seedService = scope.ServiceProvider.GetRequiredService<DataSeedService>();
                    await seedService.SeedBasicData(new SeedDataRequest());
                }
            }
            else
            {
                Tenant? tenant = await dbContext.Tenants.FirstOrDefaultAsync(c => c.Id == request.Id);
                if (tenant is null)
                {
                    return Response.BadRequest(
                        "Unable to find tenant, please try again later or contact support.");
                }

                if (request.Name != tenant.Name)
                {
                    tenant.Name = request.Name;
                }

                if (request.Identifier != tenant.Identifier)
                {
                    tenant.Identifier = request.Identifier;
                }

                if (request.AdminEmail != tenant.AdminEmail)
                {
                    string rootTenantLink = tenantGetterService.Tenant.TenantLink;
                    using (IServiceScope scope = serviceProvider.CreateScope())
                    {
                        ITenantSetterService requiredService =
                            scope.ServiceProvider.GetRequiredService<ITenantSetterService>();
                        CurrentTenantDetails currentTenantDetails = tenant.Adapt<CurrentTenantDetails>();
                        currentTenantDetails.TenantLink =
                            GetSubTenantLinkBasedOnRootTenant(rootTenantLink, request.Identifier);
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
                                Id = user.Id, Email = request.AdminEmail, UpdateTenantEmail = false
                            });
                        }
                    }

                    tenant.AdminEmail = request.AdminEmail;
                }

                if (request.IsActive != tenant.IsActive)
                {
                    tenant.IsActive = request.IsActive ?? tenant.IsActive;
                }

                if (request.ValidUpto != tenant.ValidUpto)
                {
                    tenant.ValidUpto = request.ValidUpto ?? tenant.ValidUpto;
                }

                await dbContext.SaveChangesAsync();
                await krafterContext.SaveChangesAsync([nameof(Tenant)]);
            }

            return new Response();
        }

        internal string GetSubTenantLinkBasedOnRootTenant(string tenantDomain, string? identifier)
        {
            if (tenantDomain.EndsWith("/"))
            {
                tenantDomain = tenantDomain.Substring(0, tenantDomain.Length - 1);
            }

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return tenantDomain;
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

            tenant.MapPost("/", async
            ([FromBody] CreateOrUpdateTenantRequest request,
                [FromServices] Handler handler) =>
            {
                Response res = await handler.CreateOrUpdateAsync(request);
                return TypedResults.Ok(res);
            }).MustHavePermission(KrafterAction.Create, KrafterResource.Tenants);
        }
    }
}
