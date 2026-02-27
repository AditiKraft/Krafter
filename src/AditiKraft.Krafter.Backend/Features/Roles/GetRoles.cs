using AditiKraft.Krafter.Backend.Api.Authorization;
using AditiKraft.Krafter.Backend.Common.Extensions;
using LinqKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using AditiKraft.Krafter.Backend.Api;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;

namespace AditiKraft.Krafter.Backend.Features.Roles;

public sealed class GetRoles
{
    internal sealed class Handler(KrafterContext db) : IScopedHandler
    {
        public async Task<Response<PaginationResponse<RoleDto>>> GetListAsync(
            [AsParameters] GetRequestInput requestInput,
            CancellationToken cancellationToken)
        {
            ExpressionStarter<KrafterRole>? predicate = PredicateBuilder.New<KrafterRole>(true);

            if (!string.IsNullOrWhiteSpace(requestInput.Id))
            {
                predicate = predicate.And(c => c.Id == requestInput.Id);
            }

            IQueryable<RoleDto> query = db.Roles
                .Where(predicate)
                .Select(x => new RoleDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    CreatedById = x.CreatedById,
                    IsDeleted = x.IsDeleted,
                    CreatedOn = x.CreatedOn,
                    CreatedBy =
                        x.CreatedBy != null
                            ? new UserInfo
                            {
                                Id = x.CreatedBy.Id,
                                FirstName = x.CreatedBy.FirstName,
                                LastName = x.CreatedBy.LastName,
                                CreatedOn = x.CreatedBy.CreatedOn
                            }
                            : null,
                    UpdatedBy = x.UpdatedBy != null
                        ? new UserInfo
                        {
                            Id = x.UpdatedBy.Id,
                            FirstName = x.UpdatedBy.FirstName,
                            LastName = x.UpdatedBy.LastName,
                            CreatedOn = x.UpdatedBy.CreatedOn
                        }
                        : null,
                    UpdatedById = x.UpdatedById,
                    UpdatedOn = x.UpdatedOn,
                    DeleteReason = x.DeleteReason
                });

            // Apply filters
            if (!string.IsNullOrEmpty(requestInput.Filter))
            {
                if (requestInput.Filter.Contains("!=") ||
                    requestInput.Filter.Contains("==") ||
                    requestInput.Filter.Contains(".Contains(") ||
                    requestInput.Filter.Contains(".StartsWith(") ||
                    requestInput.Filter.Contains(".EndsWith(") ||
                    requestInput.Filter.Contains("np("))
                {
                    query = query.Where(requestInput.Filter);
                }
                else
                {
                    string filter = requestInput.Filter.ToLower();
                    query = query.Where(c =>
                        (c.Name ?? "").ToLower().Contains(filter));
                }
            }

            // Apply sorting
            if (!string.IsNullOrEmpty(requestInput.OrderBy))
            {
                query = query.OrderBy(requestInput.OrderBy);
            }

            List<RoleDto> items = await query
                .PageBy(requestInput)
                .ToListAsync(cancellationToken);

            int totalCount = await query.CountAsync(cancellationToken);

            var result = new PaginationResponse<RoleDto>(
                items,
                totalCount,
                requestInput.SkipCount,
                requestInput.MaxResultCount);

            return new Response<PaginationResponse<RoleDto>> { Data = result };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder roleGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Roles)
                .AddFluentValidationFilter();

            roleGroup.MapGet("/", async (
                    [FromServices] Handler handler,
                    [AsParameters] GetRequestInput requestInput,
                    CancellationToken cancellationToken) =>
                {
                    Response<PaginationResponse<RoleDto>> res =
                        await handler.GetListAsync(requestInput, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<PaginationResponse<RoleDto>>>()
                .MustHavePermission(KrafterAction.View, KrafterResource.Roles);
        }
    }
}


