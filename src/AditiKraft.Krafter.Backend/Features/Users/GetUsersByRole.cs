using AditiKraft.Krafter.Backend.Web.Authorization;
using AditiKraft.Krafter.Backend.Common.Extensions;
using LinqKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AditiKraft.Krafter.Backend.Web;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using AditiKraft.Krafter.Contracts.Common;
using AditiKraft.Krafter.Contracts.Common.Auth.Permissions;
using AditiKraft.Krafter.Contracts.Common.Models;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class GetUsersByRole
{
    internal sealed class Handler(ApplicationDbContext db) : IScopedHandler
    {
        private static readonly HashSet<string> QueryProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(UserInfo.Id), nameof(UserInfo.FirstName), nameof(UserInfo.LastName),
            nameof(UserInfo.Email), nameof(UserInfo.CreatedOn)
        };

        public async Task<Response<PaginationResponse<UserInfo>>> GetByRoleAsync(
            string roleId,
            [AsParameters] GetRequestInput requestInput,
            CancellationToken cancellationToken)
        {
            if (!GetRequestInputValidator.IsValidText(roleId))
            {
                return Response<PaginationResponse<UserInfo>>.BadRequest("RoleId must contain valid Unicode text without null characters.");
            }

            ExpressionStarter<ApplicationUserRole>? predicate = PredicateBuilder.New<ApplicationUserRole>(true);
            predicate = predicate.And(c => c.RoleId == roleId);

            IQueryable<UserInfo> query = db.UserRoles
                .Where(predicate)
                .Include(c => c.User)
                .Select(x => new UserInfo
                {
                    Id = x.User.Id,
                    FirstName = x.User.FirstName,
                    LastName = x.User.LastName,
                    Email = x.User.Email ?? string.Empty,
                    CreatedOn = x.User.CreatedOn
                });

            if (!query.TryApplyListQuery(requestInput, QueryProperties, out query, out string error))
            {
                return Response<PaginationResponse<UserInfo>>.BadRequest(error);
            }

            if (!string.IsNullOrWhiteSpace(requestInput.Query))
            {
                string search = requestInput.Query.ToLowerInvariant();
                query = query.Where(c =>
                    (c.FirstName ?? "").ToLower().Contains(search) ||
                    (c.LastName ?? "").ToLower().Contains(search));
            }

            List<UserInfo> items = await query
                .PageBy(requestInput)
                .ToListAsync(cancellationToken);

            int totalCount = await query.CountAsync(cancellationToken);

            var result = new PaginationResponse<UserInfo>(
                items,
                totalCount,
                requestInput.SkipCount,
                requestInput.MaxResultCount);

            return new Response<PaginationResponse<UserInfo>> { Data = result, IsError = false, StatusCode = 200 }
                ;
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Users)
                .AddFluentValidationFilter();

            userGroup.MapGet($"/{RouteSegment.ByRole}", async (
                    [FromRoute] string roleId,
                    [FromServices] Handler handler,
                    [AsParameters] GetRequestInput requestInput,
                    CancellationToken cancellationToken) =>
                {
                    Response<PaginationResponse<UserInfo>> res =
                        await handler.GetByRoleAsync(roleId, requestInput, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<PaginationResponse<UserInfo>>>()
                .MustHavePermission(PermissionAction.View, PermissionResource.Users);
        }
    }
}



