using Backend.Api;
using Backend.Api.Authorization;
using Backend.Common;
using Backend.Common.Extensions;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using LinqKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using System.Net;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;

namespace Backend.Features.Users;

public sealed class GetUsers
{
    internal sealed class Handler(KrafterContext db) : IScopedHandler
    {
        public async Task<Response<PaginationResponse<Krafter.Shared.Features.Users.GetUsers.UserDto>>> Get(
            [AsParameters] GetRequestInput requestInput,
            CancellationToken cancellationToken)
        {
            ExpressionStarter<KrafterUser>? predicate = PredicateBuilder.New<KrafterUser>(true);

            if (!string.IsNullOrWhiteSpace(requestInput.Id))
            {
                predicate = predicate.And(c => c.Id == requestInput.Id);
            }

            IQueryable<Krafter.Shared.Features.Users.GetUsers.UserDto> query = db.Users
                .Where(predicate)
                .Select(x => new Krafter.Shared.Features.Users.GetUsers.UserDto
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
                            CreatedOn = x.UpdatedBy.CreatedOn
                        }
                        : null,
                    UpdatedById = x.UpdatedById
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
                        (c.FirstName ?? "").ToLower().Contains(filter) ||
                        (c.LastName ?? "").ToLower().Contains(filter));
                }
            }

            // Apply sorting
            if (!string.IsNullOrEmpty(requestInput.OrderBy))
            {
                query = query.OrderBy(requestInput.OrderBy);
            }

            List<Krafter.Shared.Features.Users.GetUsers.UserDto> items = await query
                .PageBy(requestInput)
                .ToListAsync(cancellationToken);

            int totalCount = await query.CountAsync(cancellationToken);

            var result = new PaginationResponse<Krafter.Shared.Features.Users.GetUsers.UserDto>(
                items,
                totalCount,
                requestInput.SkipCount,
                requestInput.MaxResultCount);

            return new Response<PaginationResponse<Krafter.Shared.Features.Users.GetUsers.UserDto>>
            {
                Data = result, IsError = false
            };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder userGroup = endpointRouteBuilder.MapGroup(KrafterRoute.Users)
                .AddFluentValidationFilter();

            userGroup.MapGet("/get", async (
                    [FromServices] Handler handler,
                    [AsParameters] GetRequestInput requestInput,
                    CancellationToken cancellationToken) =>
                {
                    Response<PaginationResponse<Krafter.Shared.Features.Users.GetUsers.UserDto>> res =
                        await handler.Get(requestInput, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<PaginationResponse<Krafter.Shared.Features.Users.GetUsers.UserDto>>>()
                .MustHavePermission(KrafterAction.View, KrafterResource.Users);
        }
    }
}
