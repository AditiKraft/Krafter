# UI Refit AI Instructions

> **SCOPE**: Refit interfaces, routes, and registration.
> **PARENT**: See also: ../../../Agents.md

## 1. Core Principles
- Use literal route strings in Refit, especially when parameters are present.
- Route parameter names must match method parameter names.
- Use `[Query] GetRequestInput` for list endpoints.
- Register authenticated backend APIs with both `RefitTenantHandler` and `RefitAuthHandler`.
- Register BFF/auth endpoints and tenant-resolved internal endpoints that do not need auth forwarding with `RefitTenantHandler` only.
- External APIs should not use tenant/auth handlers.

## 2. Decision Tree
- Authenticated backend API? Register with both handlers.
- BFF/auth endpoint or internal tenant-resolved endpoint without auth forwarding? Register with `RefitTenantHandler` only.
- External API? Register without tenant/auth handlers.
- Endpoint with `{id}` or `{roleId}`? Use a literal route string.

## 3. Code Templates

### Interface Example (Users)
```csharp
public interface IUsersApi
{
    [Get("/users")]
    Task<Response<PaginationResponse<UserDto>>> GetUsersAsync(
        [Query] GetRequestInput request,
        CancellationToken cancellationToken = default);

    [Post("/users")]
    Task<Response> CreateUserAsync([Body] CreateUserRequest request,
        CancellationToken cancellationToken = default);

    [Put("/users/{id}")]
    Task<Response> UpdateUserAsync(string id, [Body] CreateUserRequest request,
        CancellationToken cancellationToken = default);

    [Delete("/users/{id}")]
    Task<Response> DeleteUserAsync(string id,
        CancellationToken cancellationToken = default);
}
```

### Registration Example
```csharp
services.AddRefitClient<IUsersApi>(refitSettings)
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(placeholderUrl))
    .AddHttpMessageHandler(sp => new RefitTenantHandler(sp.GetRequiredService<TenantIdentifier>(), isBffClient: false))
    .AddHttpMessageHandler<RefitAuthHandler>();
```

## 4. Checklist
1. Create the interface in `Infrastructure/Refit/`.
2. Use literal routes in attributes (`[Get("/users/{id}")]`).
3. Add `[Query] GetRequestInput` for list endpoints.
4. Register the Refit client in `RefitServiceExtensions.cs` with the correct handler chain for that API.
5. Call through `ApiCallService` from UI components, except auth flows that go through `IAuthenticationService` / `IAuthApiService`.

## 5. Common Mistakes
- Using `KrafterRoute` or `RouteSegment` constants in Refit routes with parameters.
- Parameter name mismatch (e.g., `{id}` requires `id`).
- Adding `RefitAuthHandler` to tenant-resolved APIs that currently do not use auth forwarding (`IAuthApi`, `IAppInfoApi`).
- Using raw `HttpClient` instead of Refit + `ApiCallService`.

## 6. Evolution Triggers
- Refit registration or handler chain changes.
- New API client conventions added (e.g., BFF vs direct).

---
Last Updated: 2026-03-07
Verified Against: src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/IUsersApi.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/IRolesApi.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/ITenantsApi.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/IAuthApi.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/IAppInfoApi.cs, src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/RefitServiceExtensions.cs
---
