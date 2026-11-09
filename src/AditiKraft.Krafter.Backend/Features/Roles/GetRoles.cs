using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Backend.Common.Extensions;
using LinqKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Features.Roles.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Contracts.Contracts.Roles;

namespace AditiKraft.Krafter.Backend.Features.Roles;

public sealed class GetRoles
{
    internal sealed class Handler(ApplicationDbContext db) : IScopedHandler
    {
        private static readonly HashSet<string> QueryProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(RoleDto.Id), nameof(RoleDto.Name), nameof(RoleDto.Description), nameof(RoleDto.CreatedOn),
            nameof(RoleDto.CreatedById), nameof(RoleDto.UpdatedOn), nameof(RoleDto.UpdatedById),
            nameof(RoleDto.IsDeleted), nameof(RoleDto.DeleteReason)
        };

        public async Task<Response<PaginationResponse<RoleDto>>> GetListAsync(
            [AsParameters] GetRequestInput requestInput,
            CancellationToken cancellationToken)
        {
            ExpressionStarter<ApplicationRole>? predicate = PredicateBuilder.New<ApplicationRole>(true);

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
                                Email = x.CreatedBy.Email ?? string.Empty,
                                CreatedOn = x.CreatedBy.CreatedOn
                            }
                            : null,
                    UpdatedBy = x.UpdatedBy != null
                        ? new UserInfo
                        {
                            Id = x.UpdatedBy.Id,
                            FirstName = x.UpdatedBy.FirstName,
                            LastName = x.UpdatedBy.LastName,
                            Email = x.UpdatedBy.Email ?? string.Empty,
                            CreatedOn = x.UpdatedBy.CreatedOn
                        }
                        : null,
                    UpdatedById = x.UpdatedById,
                    UpdatedOn = x.UpdatedOn,
                    DeleteReason = x.DeleteReason
                });

            if (!query.TryApplyListQuery(requestInput, QueryProperties, out query, out string error))
            {
                return Response<PaginationResponse<RoleDto>>.BadRequest(error);
            }

            if (!string.IsNullOrWhiteSpace(requestInput.Query))
            {
                string search = requestInput.Query.ToLowerInvariant();
                query = query.Where(c => (c.Name ?? "").ToLower().Contains(search));
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
            RouteGroupBuilder roleGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Roles)
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
                .MustHavePermission(PermissionAction.View, PermissionResource.Roles);
        }
    }
}



