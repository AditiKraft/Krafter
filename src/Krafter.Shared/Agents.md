# Shared Library AI Instructions

> **SCOPE**: API contracts (DTOs, Requests, Responses), shared constants, common utilities used by both Backend and UI.

## 1. Core Principles
- **Contracts Only**: This library contains NO business logic - only data contracts
- **Shared by All**: Referenced by Backend, UI, and potentially external clients
- **Validators Included**: FluentValidation validators live with their request classes
- **No Dependencies on Backend/UI**: This project must not reference Backend or UI projects

## 2. Decision Tree: Where Does This Code Go?

```
┌─────────────────────────────────────────────────────────────┐
│ What are you adding?                                        │
├─────────────────────────────────────────────────────────────┤
│ Request DTO (API input)?                                    │
│   → Contracts/<Feature>/<Name>Request.cs                    │
│   → Include FluentValidation validator in same file         │
│                                                             │
│ Response DTO (API output)?                                  │
│   → Contracts/<Feature>/<Name>Response.cs or <Name>Dto.cs   │
│                                                             │
│ Shared constant (role names, routes)?                       │
│   → Contracts/<Feature>/<Name>Constant.cs                   │
│                                                             │
│ API route constant?                                         │
│   → Common/KrafterRoute.cs                                  │
│                                                             │
│ Permission definitions?                                     │
│   → Common/Auth/Permissions/KrafterPermissions.cs           │
│                                                             │
│ Common models (Response wrapper, pagination)?               │
│   → Common/Models/                                          │
│                                                             │
│ Auth claims/utilities?                                      │
│   → Common/Auth/                                            │
│                                                             │
│ SignalR hub contracts?                                      │
│   → Hubs/                                                   │
└─────────────────────────────────────────────────────────────┘
```

## 3. Directory Structure
```
src/Krafter.Shared/
├── Contracts/                   # API contracts (DTOs)
│   ├── AppInfo/
│   │   └── BuildInfo.cs
│   ├── Auth/
│   │   ├── TokenRequest.cs      ← Request + Validator
│   │   ├── TokenResponse.cs     ← Response record
│   │   ├── RefreshTokenRequest.cs
│   │   └── GoogleAuthRequest.cs
│   ├── Users/
│   │   ├── UserDto.cs           ← Response DTO
│   │   ├── UserRoleDto.cs
│   │   ├── CreateUserRequest.cs ← Request + Validator
│   │   ├── ChangePasswordRequest.cs
│   │   ├── ForgotPasswordRequest.cs
│   │   └── ResetPasswordRequest.cs
│   ├── Roles/
│   │   ├── RoleDto.cs
│   │   ├── CreateOrUpdateRoleRequest.cs
│   │   ├── UpdateRolePermissionsRequest.cs
│   │   └── KrafterRoleConstant.cs
│   └── Tenants/
│       ├── TenantDto.cs
│       ├── CreateOrUpdateTenantRequest.cs
│       └── SeedDataRequest.cs
├── Common/
│   ├── KrafterRoute.cs          ← API route constants
│   ├── Auth/
│   │   ├── KrafterClaims.cs
│   │   └── Permissions/
│   │       ├── KrafterPermissions.cs
│   │       ├── KrafterAction.cs
│   │       └── KrafterResource.cs
│   ├── Models/
│   │   ├── Response.cs          ← Standard API response wrapper
│   │   ├── PaginationResponse.cs
│   │   ├── CommonDtoProperty.cs ← Base DTO with audit fields
│   │   ├── DeleteRequestInput.cs ← Delete request model
│   │   ├── DeleteRequestInputValidator.cs
│   │   ├── GetRequestInput.cs   ← Standard query parameters
│   │   ├── RestoreRequestInput.cs
│   │   ├── DropDownDto.cs
│   │   ├── CurrentTenantDetails.cs
│   │   └── UserInfo.cs
│   ├── Enums/
│   │   ├── EntityKind.cs
│   │   └── RecordState.cs
│   └── Extensions/
│       └── AuthExtensions.cs
└── Hubs/
    └── SignalRMethods.cs
```

## 4. Code Templates

### 4.1 Request with Validator
```csharp
using FluentValidation;

namespace Krafter.Shared.Contracts.Products;

/// <summary>
/// Request model for creating or updating a product.
/// </summary>
public class CreateProductRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(p => p.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters");

        RuleFor(p => p.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0");
    }
}
```

### 4.2 Response DTO
```csharp
using Krafter.Shared.Common.Models;

namespace Krafter.Shared.Contracts.Products;

/// <summary>
/// Data transfer object for product information.
/// </summary>
public class ProductDto : CommonDtoProperty
{
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}
```

### 4.3 Response Record (for simple responses)
```csharp
namespace Krafter.Shared.Contracts.Products;

/// <summary>
/// Response containing product summary.
/// </summary>
public record ProductSummaryResponse(
    int TotalCount,
    int ActiveCount,
    decimal TotalValue);
```

### 4.4 Constants
```csharp
using System.Collections.ObjectModel;

namespace Krafter.Shared.Contracts.Products;

public static class ProductCategoryConstant
{
    public const string Electronics = nameof(Electronics);
    public const string Clothing = nameof(Clothing);
    public const string Food = nameof(Food);

    public static IReadOnlyList<string> All { get; } = 
        new ReadOnlyCollection<string>([Electronics, Clothing, Food]);

    public static bool IsValid(string category) => All.Contains(category);
}
```

## 5. Naming Conventions

| Item | Convention | Example |
|------|------------|---------|
| Request DTO | `<Action><Entity>Request` | `CreateUserRequest` |
| Response DTO | `<Entity>Dto` or `<Entity>Response` | `UserDto`, `TokenResponse` |
| Validator | `<RequestName>Validator` | `CreateUserRequestValidator` |
| Constant class | `<Entity>Constant` | `KrafterRoleConstant` |
| Namespace | `Krafter.Shared.Contracts.<Feature>` | `Krafter.Shared.Contracts.Users` |

## 6. Response Class Factory Methods

The `Response` and `Response<T>` classes in `Common/Models/Response.cs` provide static factory methods for creating responses.

### 6.1 Error Factory Methods

```csharp
// Non-generic Response
Response.NotFound("Resource not found");      // 404
Response.BadRequest("Invalid input");         // 400
Response.Unauthorized("Invalid credentials"); // 401
Response.Forbidden("Access denied");          // 403
Response.Conflict("Resource already exists"); // 409
Response.CustomError("Custom error", 422);    // Custom status code

// Generic Response<T>
Response<UserDto>.NotFound("User not found");
Response<UserDto>.BadRequest("Invalid input");
Response<UserDto>.Unauthorized("Invalid credentials");
Response<UserDto>.Forbidden("Access denied");
Response<UserDto>.Conflict("User already exists");
Response<UserDto>.CustomError("Custom error", 422);
```

### 6.2 Success Factory Methods

```csharp
// Non-generic Response
Response.Success();                           // 200, no message
Response.Success("Operation completed");      // 200, with message

// Generic Response<T>
Response<UserDto>.Success(userDto);           // 200, with data
Response<UserDto>.Success(userDto, "User created"); // 200, with data and message
```

### 6.3 Factory Method Behavior

All error factory methods:
- Set `IsError = true`
- Set `StatusCode` to the appropriate HTTP status code
- Set `Message` to the provided message
- Set `Error.Message` to the provided message
- For `Response<T>`, set `Data = default`

All success factory methods:
- Set `IsError = false`
- Set `StatusCode = 200`
- Set `Message` to the provided message (optional)
- For `Response<T>`, set `Data` to the provided data

## 7. Rules

### DO:
- ✅ Prefer XML documentation on public classes when the file already uses it; keep style consistent
- ✅ Use `default!` for non-nullable properties that will be set by deserializer
- ✅ Keep validators in the same file as their request class
- ✅ Inherit from `CommonDtoProperty` for DTOs with audit fields
- ✅ Use records for immutable response types

### DON'T:
- ❌ Add business logic - this is contracts only
- ❌ Reference Backend or UI projects
- ❌ Add Entity Framework attributes (those belong in Backend)
- ❌ Create nested classes for DTOs - use flat structure

## 8. New Contract Checklist

1. [ ] Create file in `Contracts/<Feature>/`
2. [ ] Add namespace `Krafter.Shared.Contracts.<Feature>`
3. [ ] Add XML documentation comment if the file already uses XML docs
4. [ ] For requests: include FluentValidation validator
5. [ ] For DTOs: inherit from `CommonDtoProperty` if audit fields needed
6. [ ] Build to verify: `dotnet build src/Krafter.Shared/Krafter.Shared.csproj`
7. [ ] Update Backend imports from `Krafter.Shared.Contracts.<Feature>`

## 9. Edge Cases: Adding New Feature Support

### 9.1 Add EntityKind for Delete Operations
When adding a new feature that supports soft-delete, add to `Common/Enums/EntityKind.cs`:

```csharp
// File: src/Krafter.Shared/Common/Enums/EntityKind.cs
namespace Krafter.Shared.Common.Enums;

public enum EntityKind
{
    None = 000,
    Tenant = 001,
    
    // ... existing entries ...
    
    // Add your new entity (use next available number in appropriate group)
    Product = 400,        // New feature
    ProductCategory = 410 // Related entity
}
```

**Numbering Convention:**
- 000-099: Core entities
- 100-199: Auth entities (User, Role, Claims)
- 200-299: Course/Content entities
- 300-399: Commerce entities (Cart, Order, Payment)
- 400+: New feature entities

### 9.2 Add Permissions for New Feature
Add to `Common/Auth/Permissions/`:

**Step 1: Add Resource** (`KrafterResource.cs`):
```csharp
namespace Krafter.Shared.Common.Auth.Permissions;

public static class KrafterResource
{
    // ... existing resources ...
    public const string Products = nameof(Products);
}
```

**Step 2: Add Permissions** (`KrafterPermissions.cs`):
```csharp
private static readonly KrafterPermission[] AllPermissions =
[
    // ... existing permissions ...
    
    // Products - standard CRUD permissions
    new("View Products", KrafterAction.View, KrafterResource.Products),
    new("Create Products", KrafterAction.Create, KrafterResource.Products),
    new("Update Products", KrafterAction.Update, KrafterResource.Products),
    new("Delete Products", KrafterAction.Delete, KrafterResource.Products),
    
    // For admin-only features, add IsRoot: true
    // new("Manage Products", KrafterAction.Update, KrafterResource.Products, IsRoot: true),
];
```

**Permission Flags:**
- `IsBasic = true`: Available to all authenticated users
- `IsRoot = true`: Only available to root/super admin (like Tenant management)
- Default (no flags): Available to Admin role

### 9.3 Add API Route Constant
Add to `Common/KrafterRoute.cs`:

```csharp
namespace Krafter.Shared.Common;

public static class KrafterRoute
{
    // ... existing routes ...
    public const string Products = "products";
}
```

## 10. Route Constants (KrafterRoute & RouteSegment)

### 10.1 Overview
Route constants are defined in `Common/KrafterRoute.cs` and used across Backend and BFF:

```csharp
// Base route prefixes for API endpoints
public static class KrafterRoute
{
    public const string Roles = "roles";
    public const string Tenants = "tenants";
    public const string Tokens = "tokens";
    public const string Users = "users";
    public const string AppInfo = "app-info";
    public const string ExternalAuth = "external-auth";
}

// REST-compliant route segments
public static class RouteSegment
{
    // Standard REST
    public const string ById = "{id}";
    
    // Nested resources
    public const string UserRoles = "{userId}/roles";
    public const string RolePermissions = "{roleId}/permissions";
    public const string ByRole = "by-role/{roleId}";
    public const string Permissions = "permissions";
    
    // Auth actions
    public const string Refresh = "refresh";
    public const string Logout = "logout";
    public const string Google = "google";
    
    // User actions
    public const string ChangePassword = "change-password";
    public const string ForgotPassword = "forgot-password";
    public const string ResetPassword = "reset-password";
    
    // Tenant actions
    public const string SeedData = "seed-data";
}
```

### 10.2 Usage Guidelines

| Layer | Use Constants? | Example |
|-------|---------------|---------|
| Backend (Minimal APIs) | ✅ Yes | `MapGroup(KrafterRoute.Users)` |
| BFF (Program.cs) | ✅ Yes | `$"/{KrafterRoute.Tokens}/{RouteSegment.Refresh}"` |
| Refit Interfaces | ❌ No | Use literal strings: `[Get("/users/{id}")]` |

### 10.3 Why Refit Can't Use Constants
Refit parses route strings at runtime to match `{paramName}` placeholders to method parameters. When using interpolated strings with constants, Refit's parameter matching fails with errors like:
```
"URL /roles/{roleId}/permissions has parameter roleid, but no method parameter matches"
```

### 10.4 Adding New Route Constants

**Step 1: Add base route** (if new feature):
```csharp
public static class KrafterRoute
{
    public const string Products = "products";
}
```

**Step 2: Add route segments** (if needed):
```csharp
public static class RouteSegment
{
    public const string ProductCategories = "{productId}/categories";
}
```

**Step 3: Use in Backend**:
```csharp
group.MapGet($"/{RouteSegment.ProductCategories}", async (
    [FromRoute] string productId, ...) => { });
```

**Step 4: Use literal string in Refit**:
```csharp
[Get("/products/{productId}/categories")]
Task<Response<List<CategoryDto>>> GetCategoriesAsync(string productId, ...);
```

---
Last Updated: 2026-01-25
Verified Against: Contracts/Users/CreateUserRequest.cs, Common/Models/Response.cs, Common/Auth/Permissions/KrafterPermissions.cs, Common/KrafterRoute.cs
---
