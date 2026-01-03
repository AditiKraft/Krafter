using Backend.Api;
using Backend.Common;
using Backend.Common.Extensions;
using Backend.Common.Interfaces;
using Backend.Infrastructure.Persistence;
using LinqKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using Backend.Api.Authorization;
using Backend.Features.Tenants._Shared;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Tenants;

namespace Backend.Features.Tenants;

public sealed class Get
{
    internal sealed class Handler(TenantDbContext dbContext) : IScopedHandler
    {
        public async Task<Response<PaginationResponse<TenantDto>>> Get(
            GetRequestInput requestInput,
            CancellationToken cancellationToken)
        {
            ExpressionStarter<Tenant>? predicate = PredicateBuilder.New<Tenant>(true);
            if (!string.IsNullOrWhiteSpace(requestInput.Id))
            {
                predicate = predicate.And(c => c.Id == requestInput.Id);
            }

            IQueryable<TenantDto> queryableProducts = null;
            if (requestInput.History)
            {
                if (requestInput.Filter == "CreatedOn desc")
                {
                    requestInput.Filter = "PeriodEnd desc";
                }

                predicate = predicate.And(c => EF.Property<DateTime>(c, "PeriodEnd") < DateTime.UtcNow);
                queryableProducts = dbContext.Tenants.TemporalAll().Where(predicate)
                    .Select(x => new TenantDto
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Identifier = x.Identifier,
                        AdminEmail = x.AdminEmail,
                        ValidUpto = x.ValidUpto,
                        IsActive = x.IsActive,
                        CreatedById = x.CreatedById,
                        IsDeleted = x.IsDeleted,
                        CreatedOn = x.CreatedOn,
                        DeleteReason = x.DeleteReason,
                        PeriodEnd = EF.Property<DateTime>(x, "PeriodEnd"),
                        PeriodStart = EF.Property<DateTime>(x, "PeriodStart")
                    });
            }
            else
            {
                if (requestInput.IsDeleted)
                {
                    predicate = predicate.And(c => c.IsDeleted == true);
                    queryableProducts = dbContext.Tenants.IgnoreQueryFilters().Where(predicate)
                        .Select(x => new TenantDto
                        {
                            Id = x.Id,
                            Name = x.Name,
                            Identifier = x.Identifier,
                            AdminEmail = x.AdminEmail,
                            ValidUpto = x.ValidUpto,
                            IsActive = x.IsActive,
                            CreatedById = x.CreatedById,
                            IsDeleted = x.IsDeleted,
                            CreatedOn = x.CreatedOn,
                            DeleteReason = x.DeleteReason
                        });
                }
                else
                {
                    queryableProducts = dbContext.Tenants.Where(predicate)
                        .Select(x => new TenantDto
                        {
                            Id = x.Id,
                            Name = x.Name,
                            Identifier = x.Identifier,
                            AdminEmail = x.AdminEmail,
                            ValidUpto = x.ValidUpto,
                            IsActive = x.IsActive,
                            CreatedById = x.CreatedById,
                            IsDeleted = x.IsDeleted,
                            CreatedOn = x.CreatedOn,
                            DeleteReason = x.DeleteReason
                        });
                }
            }

            if (!string.IsNullOrEmpty(requestInput.Filter))
            {
                queryableProducts = queryableProducts.Where(requestInput.Filter);
            }

            if (!string.IsNullOrEmpty(requestInput.OrderBy))
            {
                queryableProducts = queryableProducts.OrderBy(requestInput.OrderBy);
            }

            List<TenantDto> res =
                await queryableProducts.PageBy(requestInput).ToListAsync(cancellationToken);

            return new Response<PaginationResponse<TenantDto>>
            {
                Data = new PaginationResponse<TenantDto>(res,
                    await queryableProducts.CountAsync(cancellationToken),
                    requestInput.SkipCount, requestInput.MaxResultCount)
            };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder tenant = endpointRouteBuilder.MapGroup(KrafterRoute.Tenants).AddFluentValidationFilter();

            tenant.MapGet("/", async
                (
                    [FromServices] Handler service, [AsParameters] GetRequestInput requestInput,
                    CancellationToken cancellationToken) =>
                {
                    Response<PaginationResponse<TenantDto>> res =
                        await service.Get(requestInput, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<PaginationResponse<TenantDto>>>()
                .MustHavePermission(KrafterAction.View, KrafterResource.Tenants);
        }
    }
}
