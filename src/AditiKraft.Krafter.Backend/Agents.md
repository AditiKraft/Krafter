# Backend AI Instructions (Vertical Slice Architecture)

> **SCOPE**: API endpoints, handlers, entities, and backend feature organization.
> **PARENT**: See also: ../../Agents.md

## Quick Start: New Backend Feature
1. If `src/AditiKraft.Krafter.Backend/Features/<Feature>/Agents.md` exists, read it first.
2. Add shared request/response DTOs in `src/AditiKraft.Krafter.Contracts/Contracts/<Feature>/` (see `src/AditiKraft.Krafter.Contracts/Agents.md`).
3. Add operations in `src/AditiKraft.Krafter.Backend/Features/<Feature>/<Operation>.cs` (one file per operation).
4. Add entity in `src/AditiKraft.Krafter.Backend/Features/<Feature>/Common/<Entity>.cs` if needed.
5. Add DbSet + model configuration in `src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/ApplicationDbContext.cs`.
6. Add permissions + routes in Shared (`src/AditiKraft.Krafter.Contracts/Common/Auth/Permissions/` and `src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs`).
7. Map endpoints using `ApiRoutes`, `RouteSegment`, and Shared permission constants.
8. Add a migration if the schema changed; AppHost will apply it through `src/AditiKraft.Krafter.Backend.Migrator/` on the next startup.

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
- Current user interfaces and implementation: `Common/Auth/`
- Current tenant interfaces and implementation: `Common/Tenants/`
- Shared entity types and query helpers: `Common/Entities/` and `Common/Extensions/`
- Database, email, jobs, and realtime implementations: `Infrastructure/`
- HTTP pipeline, endpoint discovery, and service registration: `Web/`
- Authentication registration and JWT options: `Web/Authentication/`

Keep business rules in their feature. Keep an interface beside its implementation or related types. Name a standalone type's file after the type. Keep Handler + Route together in each operation file; keep request validators with requests in Contracts.

`Web/ServiceRegistration.cs` discovers handlers and services through `AddApplicationServices()`. `Common/IScopedService.cs` and `Features/IScopedHandler.cs` are its registration markers. Database registration remains in `Web/Configuration/DatabaseConfiguration.cs`.

## Minimal Operation Skeleton
```csharp
namespace AditiKraft.Krafter.Backend.Features.Users;

public sealed class GetUsers
{
    internal sealed class Handler(ApplicationDbContext db) : IScopedHandler
    {
        public async Task<Response<PaginationResponse<UserDto>>> GetAsync(
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
                .MapGroup(ApiRoutes.Users)
                .AddFluentValidationFilter();

            group.MapGet("/", async (
                    [FromServices] Handler handler,
                    [AsParameters] GetRequestInput request,
                    CancellationToken cancellationToken) =>
                {
                    Response<PaginationResponse<UserDto>> res = await handler.GetAsync(request, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<PaginationResponse<UserDto>>>()
                .MustHavePermission(PermissionAction.View, PermissionResource.Users);
        }
    }
}
```

## Related Agents
- `src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/Agents.md`
- `src/AditiKraft.Krafter.Backend/Infrastructure/Jobs/Agents.md`
- `src/AditiKraft.Krafter.Backend/Features/Auth/Agents.md`
- `src/AditiKraft.Krafter.Backend/Features/Users/Agents.md`
- `src/AditiKraft.Krafter.Backend/Features/Roles/Agents.md`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/Agents.md`

## References (real code)
- `src/AditiKraft.Krafter.Backend/Features/Users/CreateUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/UpdateUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/GetUsers.cs`
- `src/AditiKraft.Krafter.Backend/Features/Users/DeleteUser.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/CreateRole.cs`
- `src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRole.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/GetTenants.cs`
- `src/AditiKraft.Krafter.Backend/Features/Tenants/DeleteTenant.cs`

## Common Mistakes
- Returning raw types instead of `Response` / `Response<T>`.
- Using `MapPost("/delete", ...)` instead of `MapDelete($"/{RouteSegment.ById}", ...)`.
- Route parameter name mismatches (e.g., `{id}` requires parameter `id`).
- Putting DTOs in Backend instead of `src/AditiKraft.Krafter.Contracts/Contracts/`.
- Tenant subdomain detection must exclude IP addresses and use `Request.Host.Host` without the port. IP hosts use `x-tenant-identifier`, then root when the header is absent.
- Permission checks use a separate service scope for concurrent Blazor rendering. Copy the current tenant with `ITenantSetterService.SetTenant()` before resolving `IUserService` or `ApplicationDbContext`, as in `Web/Authorization/PermissionAuthorization.cs`.

## Evolution & Maintenance

- Update this file when backend routing patterns or response conventions change.
- Add feature-specific Agents when a feature grows beyond 5 operations.

---
Last Updated: 2026-09-12
Verified Against: src/AditiKraft.Krafter.Backend/Web/Middleware/MultiTenantServiceMiddleware.cs, tests/AditiKraft.Krafter.Tests/Tenants/TenantSelectionTests.cs, src/AditiKraft.Krafter.Backend/Web/Authorization/PermissionAuthorization.cs, src/AditiKraft.Krafter.Backend/Features/Auth/Login.cs, src/AditiKraft.Krafter.Backend/Features/Auth/RefreshToken.cs, src/AditiKraft.Krafter.Backend/Features/Auth/ExternalLogin.cs, src/AditiKraft.Krafter.Backend/Features/Users/CreateUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/UpdateUser.cs, src/AditiKraft.Krafter.Backend/Features/Users/GetUsers.cs, src/AditiKraft.Krafter.Backend/Features/Users/DeleteUser.cs, src/AditiKraft.Krafter.Backend/Features/Roles/CreateRole.cs, src/AditiKraft.Krafter.Backend/Features/Roles/UpdateRole.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/GetTenants.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/DeleteTenant.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/CreateTenant.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/UpdateTenant.cs, src/AditiKraft.Krafter.Backend/Features/Tenants/SeedBasicData.cs, src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/ApplicationDbContext.cs, src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/Agents.md, src/AditiKraft.Krafter.Backend.Migrator/Program.cs, src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs, src/AditiKraft.Krafter.Contracts/Common/Auth/Permissions/PermissionCatalog.cs
---




