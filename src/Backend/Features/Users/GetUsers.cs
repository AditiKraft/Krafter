using Azure;
using Backend.Api;
using Backend.Api.Authorization;
using Backend.Common;
using Backend.Common.Auth.Permissions;
using Backend.Common.Extensions;
using Backend.Common.Models;
using Backend.Features.Users._Shared;
using Backend.Infrastructure.Persistence;
using LinqKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using System.Net;

namespace Backend.Features.Users;

public sealed class GetUsers
{
    public sealed class UserDto : CommonDtoProperty
    {
        public string? Id { get; set; }
        public string? UserName { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? PhoneNumber { get; set; }
    }


    internal sealed class Handler(KrafterContext db) : IScopedHandler
    {
        public async Task<Common.Models.Response<PaginationResponse<UserDto>>> Get(
            [AsParameters] GetRequestInput requestInput,
            CancellationToken cancellationToken)
        {
            ExpressionStarter<KrafterUser>? predicate = PredicateBuilder.New<KrafterUser>(true);

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

            List<UserDto> items = await query
                .PageBy(requestInput)
                .ToListAsync(cancellationToken);

            int totalCount = await query.CountAsync(cancellationToken);

            var result = new PaginationResponse<UserDto>(
                items,
                totalCount,
                requestInput.SkipCount,
                requestInput.MaxResultCount);

            return new Common.Models.Response<PaginationResponse<UserDto>> { Data = result, IsError = false };
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
                    Common.Models.Response<PaginationResponse<UserDto>> res =
                        await handler.Get(requestInput, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Common.Models.Response<PaginationResponse<UserDto>>>()
                .MustHavePermission(KrafterAction.View, KrafterResource.Users);
        }
    }
}
