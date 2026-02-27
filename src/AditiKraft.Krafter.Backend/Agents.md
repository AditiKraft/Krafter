# Backend AI Instructions (Vertical Slice Architecture)

> **SCOPE**: API endpoints, handlers, entities, and backend feature organization.
> **PARENT**: See also: ../../Agents.md

## Quick Start: New Backend Feature
1. If `src/AditiKraft.Krafter.Backend/Features/<Feature>/Agents.md` exists, read it first.
2. Add shared request/response DTOs in `src/AditiKraft.Krafter.Contracts/Contracts/<Feature>/` (see `src/AditiKraft.Krafter.Contracts/Agents.md`).
3. Add operations in `src/AditiKraft.Krafter.Backend/Features/<Feature>/<Operation>.cs` (one file per operation).
4. Add entity in `src/AditiKraft.Krafter.Backend/Features/<Feature>/Common/<Entity>.cs` if needed.
5. Add DbSet + model configuration in `src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/KrafterContext.cs`.
6. Add permissions + routes in Shared (`src/AditiKraft.Krafter.Contracts/Common/Auth/Permissions/` and `src/AditiKraft.Krafter.Contracts/Common/KrafterRoute.cs`).
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
- Feature operation: `src/AditiKraft.Krafter.Backend/Features/<Feature>/<Operation>.cs`
- Feature entity: `src/AditiKraft.Krafter.Backend/Features/<Feature>/Common/<Entity>.cs`
- Feature-only service: `src/AditiKraft.Krafter.Backend/Features/<Feature>/Common/<Service>.cs`
- Cross-feature service: `src/AditiKraft.Krafter.Backend/Infrastructure/` or `src/AditiKraft.Krafter.Backend/Common/`

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
- `src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/Agents.md`
- `src/AditiKraft.Krafter.Backend/Infrastructure/BackgroundJobs/Agents.md`
- `src/AditiKraft.Krafter.Backend/Features/Auth/Agents.md`
- `src/AditiKraft.Krafter.Backend/Features/Users/Agents.md`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/Agents.md`

## References (real code)
- `src/AditiKraft.Krafter.Backend/Features/Users/CreateUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/UpdateUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/GetUsers.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/DeleteUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/CreateRole.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRole.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/GetTenants.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/Delete.cs`

## Common Mistakes
- Returning raw types instead of `Response` / `Response<T>`.
- Using `MapPost("/delete", ...)` instead of `MapDelete($"/{RouteSegment.ById}", ...)`.
- Route parameter name mismatches (e.g., `{id}` requires parameter `id`).
- Putting DTOs in Backend instead of `src/AditiKraft.Krafter.Contracts/Contracts/`.

## Evolution & Maintenance

- Update this file when backend routing patterns or response conventions change.
- Add feature-specific Agents when a feature grows beyond 5 operations.

---
Last Updated: 2026-01-26
Verified Against: Features/Auth/Login.cs, Features/Auth/RefreshToken.cs, Features/Auth/ExternalLogin.cs, Features/Users/CreateUser.cs, Features/Users/UpdateUser.cs, Features/Users/GetUsers.cs, Features/Users/DeleteUser.cs, Features/Roles/CreateRole.cs, Features/Roles/UpdateRole.cs, Features/Tenants/GetTenants.cs, Features/Tenants/Delete.cs, Features/Tenants/CreateTenant.cs, Features/Tenants/UpdateTenant.cs, Features/Tenants/SeedBasicData.cs, Infrastructure/Persistence/KrafterContext.cs, src/AditiKraft.Krafter.Contracts/Common/KrafterRoute.cs
---



