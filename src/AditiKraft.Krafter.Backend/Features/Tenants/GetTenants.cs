using AditiKraft.Krafter.Backend.Common.Extensions;
using LinqKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Features.Tenants.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;

namespace AditiKraft.Krafter.Backend.Features.Tenants;

public sealed class GetTenants
{
    internal sealed class Handler(TenantDbContext dbContext) : IScopedHandler
    {
        public async Task<Response<PaginationResponse<TenantDto>>> GetAsync(
            GetRequestInput requestInput,
            CancellationToken cancellationToken)
        {
            if (requestInput.History)
            {
                return Response<PaginationResponse<TenantDto>>.BadRequest(
                    "Tenant history is not supported. Request current or deleted tenants instead.");
            }

            ExpressionStarter<Tenant>? predicate = PredicateBuilder.New<Tenant>(true);
            if (!string.IsNullOrWhiteSpace(requestInput.Id))
            {
                predicate = predicate.And(c => c.Id == requestInput.Id);
            }

            IQueryable<Tenant> tenants = requestInput.IsDeleted
                ? dbContext.Tenants.IgnoreQueryFilters().Where(tenant => tenant.IsDeleted)
                : dbContext.Tenants;
            IQueryable<TenantDto> query = tenants.Where(predicate).Select(tenant => new TenantDto
            {
                Id = tenant.Id,
                Name = tenant.Name,
                Identifier = tenant.Identifier,
                AdminEmail = tenant.AdminEmail,
                ValidUpto = tenant.ValidUpto,
                IsActive = tenant.IsActive,
                CreatedById = tenant.CreatedById,
                IsDeleted = tenant.IsDeleted,
                CreatedOn = tenant.CreatedOn,
                DeleteReason = tenant.DeleteReason
            });

            if (!string.IsNullOrEmpty(requestInput.Filter))
            {
                query = query.Where(requestInput.Filter);
            }

            if (!string.IsNullOrEmpty(requestInput.OrderBy))
            {
                query = query.OrderBy(requestInput.OrderBy);
            }

            List<TenantDto> res =
                await query.PageBy(requestInput).ToListAsync(cancellationToken);

            return new Response<PaginationResponse<TenantDto>>
            {
                Data = new PaginationResponse<TenantDto>(res,
                    await query.CountAsync(cancellationToken),
                    requestInput.SkipCount, requestInput.MaxResultCount)
            };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder tenant = endpointRouteBuilder.MapGroup(ApiRoutes.Tenants).AddFluentValidationFilter();

            tenant.MapGet("/", async
                (
                    [FromServices] Handler service, [AsParameters] GetRequestInput requestInput,
                    CancellationToken cancellationToken) =>
                {
                    Response<PaginationResponse<TenantDto>> res =
                        await service.GetAsync(requestInput, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<PaginationResponse<TenantDto>>>()
                .MustHavePermission(PermissionAction.View, PermissionResource.Tenants);
        }
    }
}



