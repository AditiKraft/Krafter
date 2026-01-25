# UI Refit AI Instructions

> **SCOPE**: Refit interfaces, routes, and registration.
> **PARENT**: See also: ../../../Agents.md

## 1. Core Principles
- Use literal route strings in Refit, especially when parameters are present.
- Route parameter names must match method parameter names.
- Use `[Query] GetRequestInput` for list endpoints.
- Register internal APIs with `RefitTenantHandler` and `RefitAuthHandler`.
- External APIs should not use tenant/auth handlers.

## 2. Decision Tree
- Internal API with auth/tenant context? Register with both handlers.
- External API? Register without auth/tenant handlers.
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
    Task<Response> CreateOrUpdateUserAsync([Body] CreateUserRequest request,
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
4. Register the Refit client in `RefitServiceExtensions.cs` with handlers.
5. Call through `ApiCallService` from UI components.

## 5. Common Mistakes
- Using `KrafterRoute` or `RouteSegment` constants in Refit routes with parameters.
- Parameter name mismatch (e.g., `{id}` requires `id`).
- Using raw `HttpClient` instead of Refit + `ApiCallService`.

## 6. Evolution Triggers
- Refit registration or handler chain changes.
- New API client conventions added (e.g., BFF vs direct).

---
Last Updated: 2026-01-25
Verified Against: Infrastructure/Refit/IUsersApi.cs, Infrastructure/Refit/IRolesApi.cs, Infrastructure/Refit/ITenantsApi.cs, Infrastructure/Refit/IAuthApi.cs, Infrastructure/Refit/RefitServiceExtensions.cs
---
