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
using AditiKraft.Krafter.Contracts.Contracts.Users;

namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class GetUsers
{
    internal sealed class Handler(ApplicationDbContext db) : IScopedHandler
    {
        private static readonly HashSet<string> QueryProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(UserDto.Id), nameof(UserDto.UserName), nameof(UserDto.FirstName), nameof(UserDto.LastName),
            nameof(UserDto.Email), nameof(UserDto.IsActive), nameof(UserDto.EmailConfirmed), nameof(UserDto.PhoneNumber),
            nameof(UserDto.CreatedOn), nameof(UserDto.CreatedById), nameof(UserDto.UpdatedOn), nameof(UserDto.UpdatedById),
            nameof(UserDto.IsDeleted), nameof(UserDto.DeleteReason)
        };

        public async Task<Response<PaginationResponse<UserDto>>> GetAsync(
            [AsParameters] GetRequestInput requestInput,
            CancellationToken cancellationToken)
        {
            ExpressionStarter<ApplicationUser>? predicate = PredicateBuilder.New<ApplicationUser>(true);

            if (!string.IsNullOrWhiteSpace(requestInput.Id))
            {
                predicate = predicate.And(c => c.Id == requestInput.Id);
            }

            IQueryable<UserDto> query = db.Users
                .Where(predicate)
                .Select(x => new UserDto
                {
                    Id = x.Id,
                    UserName = x.UserName,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    Email = x.Email,
                    IsActive = x.IsActive,
                    EmailConfirmed = x.EmailConfirmed,
                    CreatedOn = x.CreatedOn,
                    PhoneNumber = x.PhoneNumber,
                    IsDeleted = x.IsDeleted,
                    DeleteReason = x.DeleteReason,
                    CreatedById = x.CreatedById,
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
                    UpdatedOn = x.UpdatedOn,
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
                    UpdatedById = x.UpdatedById
                });

            if (!query.TryApplyListQuery(requestInput, QueryProperties, out query, out string error))
            {
                return Response<PaginationResponse<UserDto>>.BadRequest(error);
            }

            if (!string.IsNullOrWhiteSpace(requestInput.Query))
            {
                string search = requestInput.Query.ToLowerInvariant();
                query = query.Where(c =>
                    (c.FirstName ?? "").ToLower().Contains(search) ||
                    (c.LastName ?? "").ToLower().Contains(search));
            }

            List<UserDto> items = await query
                .PageBy(requestInput)
                .ToListAsync(cancellationToken);

            int totalCount = await query.CountAsync(cancellationToken);

            var result = new PaginationResponse<UserDto>(
                items,
                totalCount,
                requestInput.SkipCount,
                requestInput.MaxResultCount);

            return new Response<PaginationResponse<UserDto>> { Data = result, IsError = false };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(ApiRoutes.Users)
                .AddFluentValidationFilter();

            userGroup.MapGet("/", async (
                    [FromServices] Handler handler,
                    [AsParameters] GetRequestInput requestInput,
                    CancellationToken cancellationToken) =>
                {
                    Response<PaginationResponse<UserDto>> res =
                        await handler.GetAsync(requestInput, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<PaginationResponse<UserDto>>>()
                .MustHavePermission(PermissionAction.View, PermissionResource.Users);
        }
    }
}



