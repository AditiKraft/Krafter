# Backend AI Instructions (Vertical Slice Architecture)

> **SCOPE**: API endpoints, handlers, entities, and backend feature organization.
> **PARENT**: See also: ../../Agents.md

## Quick Start: New Backend Feature
1. If `src/Backend/Features/<Feature>/Agents.md` exists, read it first.
2. Add shared request/response DTOs in `src/Krafter.Shared/Contracts/<Feature>/` (see `src/Krafter.Shared/Agents.md`).
3. Add operations in `src/Backend/Features/<Feature>/<Operation>.cs` (one file per operation).
4. Add entity in `src/Backend/Features/<Feature>/_Shared/<Entity>.cs` if needed.
5. Add DbSet + model configuration in `src/Backend/Infrastructure/Persistence/KrafterContext.cs`.
6. Add permissions + routes in Shared (`src/Krafter.Shared/Common/Auth/Permissions/` and `src/Krafter.Shared/Common/KrafterRoute.cs`).
7. Map endpoints using `KrafterRoute` and `RouteSegment`.
8. Run migrations if schema changed.

## Core Rules
- One file per operation: Handler + Route (validator stays with request in Shared).
- All handlers implement `IScopedHandler`.
- Use `Response` / `Response<T>` (factory methods preferred).
- Use Shared contracts; do not create backend-only DTOs.
- GET list endpoints accept `[AsParameters] GetRequestInput` and return `PaginationResponse<T>`.
- DELETE endpoints use `MapDelete($"/{RouteSegment.ById}", ...)` with route parameter name matching the placeholder.

## File Placement
- Feature operation: `src/Backend/Features/<Feature>/<Operation>.cs`
- Feature entity: `src/Backend/Features/<Feature>/_Shared/<Entity>.cs`
- Feature-only service: `src/Backend/Features/<Feature>/_Shared/<Service>.cs`
- Cross-feature service: `src/Backend/Infrastructure/` or `src/Backend/Common/`

## Minimal Operation Skeleton
```csharp
namespace Backend.Features.Users;

public sealed class GetUsers
{
    internal sealed class Handler(KrafterContext db) : IScopedHandler
    {
        public async Task<Response<PaginationResponse<UserDto>>> Get(
            GetRequestInput request,
            CancellationToken cancellationToken)
        {
            // query + map to DTOs
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder group = endpointRouteBuilder
                .MapGroup(KrafterRoute.Users)
                .AddFluentValidationFilter();

            group.MapGet("/", async (
                    [FromServices] Handler handler,
                    [AsParameters] GetRequestInput request,
                    CancellationToken cancellationToken) =>
                {
                    Response<PaginationResponse<UserDto>> res = await handler.Get(request, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<PaginationResponse<UserDto>>>()
                .MustHavePermission(KrafterAction.View, KrafterResource.Users);
        }
    }
}
```

## Related Agents
- `src/Backend/Infrastructure/Persistence/Agents.md`
- `src/Backend/Infrastructure/BackgroundJobs/Agents.md`
- `src/Backend/Features/Auth/Agents.md`
- `src/Backend/Features/Users/Agents.md`
- `src/Backend/Features/Tenants/Agents.md`

## References (real code)
- `src/Backend/Features/Users/CreateOrUpdateUser.cs`
- `src/Backend/Features/Users/GetUsers.cs`
- `src/Backend/Features/Users/DeleteUser.cs`
- `src/Backend/Features/Roles/CreateOrUpdateRole.cs`
- `src/Backend/Features/Tenants/Get.cs`
- `src/Backend/Features/Tenants/Delete.cs`

## Common Mistakes
- Returning raw types instead of `Response` / `Response<T>`.
- Using `MapPost("/delete", ...)` instead of `MapDelete($"/{RouteSegment.ById}", ...)`.
- Route parameter name mismatches (e.g., `{id}` requires parameter `id`).
- Putting DTOs in Backend instead of `src/Krafter.Shared/Contracts/`.

## Evolution & Maintenance

- Update this file when backend routing patterns or response conventions change.
- Add feature-specific Agents when a feature grows beyond 5 operations.

---
Last Updated: 2026-01-26
Verified Against: Features/Auth/Login.cs, Features/Auth/RefreshToken.cs, Features/Auth/ExternalLogin.cs, Features/Users/CreateOrUpdateUser.cs, Features/Users/GetUsers.cs, Features/Users/DeleteUser.cs, Features/Roles/CreateOrUpdateRole.cs, Features/Tenants/Get.cs, Features/Tenants/Delete.cs, Features/Tenants/CreateOrUpdate.cs, Features/Tenants/SeedBasicData.cs, Infrastructure/Persistence/KrafterContext.cs, src/Krafter.Shared/Common/KrafterRoute.cs
---
