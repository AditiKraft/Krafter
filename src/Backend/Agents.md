# Backend AI Instructions (Vertical Slice Architecture)

> **SCOPE**: API endpoints, Handlers, Entities, Database, Permissions, Background Jobs.

## 1. Core Principle: One File Per Operation
Every feature operation MUST be self-contained in a **single file**:
```
Features/<Feature>/<Operation>.cs
├── Handler      (Business Logic)
├── Validator    (FluentValidation - if not in Shared)
└── Route        (Endpoint Mapping)
```

**Note**: Request/Response DTOs are in `src/Krafter.Shared/Contracts/<Feature>/`

## 2. Decision Tree: Where Does This Code Go?

```
┌─────────────────────────────────────────────────────────────┐
│ What are you adding?                                        │
├─────────────────────────────────────────────────────────────┤
│ Request/Response DTO (shared with UI)?                      │
│   → src/Krafter.Shared/Contracts/<Feature>/                 │
│   → Namespace: Krafter.Shared.Contracts.<Feature>           │
│   → Include FluentValidation validator in same file         │
│                                                             │
│ Feature operation (CRUD, business logic)?                   │
│   → Features/<Feature>/<Operation>.cs                       │
│   → Import DTOs from Krafter.Shared.Contracts.<Feature>     │
│                                                             │
│ Entity/Domain model (EF Core)?                              │
│   → Features/<Feature>/_Shared/<Entity>.cs                  │
│   → Backend only - never in Shared                          │
│                                                             │
│ Service shared across operations in same feature?           │
│   → Features/<Feature>/_Shared/<Service>.cs                 │
│                                                             │
│ Service shared across MULTIPLE features?                    │
│   → Infrastructure/ or Common/                              │
│                                                             │
│ Permission definition?                                      │
│   → src/Krafter.Shared/Common/Auth/Permissions/             │
│                                                             │
│ EF Core configuration?                                      │
│   → Infrastructure/Persistence/Configurations/              │
│                                                             │
│ Middleware or API config?                                   │
│   → Api/Middleware/ or Api/Configuration/                   │
└─────────────────────────────────────────────────────────────┘
```

## 3. Directory Structure
```
src/Krafter.Shared/              # Shared contracts library
├── Contracts/                   # API DTOs (shared with UI)
│   ├── Auth/
│   │   ├── TokenRequest.cs      ← Request + Validator
│   │   ├── TokenResponse.cs
│   │   └── GoogleAuthRequest.cs
│   ├── Users/
│   │   ├── UserDto.cs
│   │   ├── CreateUserRequest.cs ← Request + Validator
│   │   └── ChangePasswordRequest.cs
│   ├── Roles/
│   │   ├── RoleDto.cs
│   │   ├── CreateOrUpdateRoleRequest.cs
│   │   └── KrafterRoleConstant.cs
│   └── Tenants/
│       ├── TenantDto.cs
│       └── CreateOrUpdateTenantRequest.cs
└── Common/                      # Shared utilities
    ├── Auth/Permissions/        # Permission definitions
    ├── Models/                  # Response, PaginationResponse, etc.
    ├── Enums/                   # EntityKind, RecordState
    └── KrafterRoute.cs          # API route constants

src/Backend/
├── Features/
│   ├── Users/
│   │   ├── CreateOrUpdateUser.cs    ← Operation slice (uses Shared DTOs)
│   │   ├── DeleteUser.cs
│   │   ├── GetUsers.cs
│   │   └── _Shared/
│   │       ├── KrafterUser.cs       ← Entity (Backend only)
│   │       ├── IUserService.cs      ← Interface
│   │       └── UserService.cs       ← Implementation
│   └── <YourFeature>/
│       ├── <Operation>.cs
│       └── _Shared/
│           └── <Entity>.cs
├── Infrastructure/
│   ├── Persistence/
│   │   ├── KrafterContext.cs        ← Main DbContext
│   │   └── Configurations/          ← EF configurations
│   └── BackgroundJobs/
├── Common/                          ← Backend-specific utilities
│   └── Extensions/
├── Api/
│   ├── IRouteRegistrar.cs           ← Route registration interface
│   ├── Authorization/               ← Permission attributes
│   └── Middleware/
└── Features/
    └── IScopedHandler.cs            ← Handler marker interface
```

## 4. Code Templates

### 4.1 Complete Operation File (Copy This)
```csharp
using Backend.Api;
using Backend.Api.Authorization;
using Backend.Features;
using Backend.Features.Products._Shared;
using Backend.Infrastructure.Persistence;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Products;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Features.Products;

public sealed class CreateOrUpdateProduct
{
    // ════════════════════════════════════════════════════════
    // HANDLER (Business Logic)
    // ════════════════════════════════════════════════════════
    internal sealed class Handler(KrafterContext db) : IScopedHandler
    {
        public async Task<Response> CreateOrUpdateAsync(CreateProductRequest request)
        {
            Product? entity;

            if (string.IsNullOrWhiteSpace(request.Id))
            {
                // CREATE
                entity = new Product
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = request.Name,
                    Price = request.Price,
                    IsActive = request.IsActive
                };
                db.Products.Add(entity);
            }
            else
            {
                // UPDATE
                entity = await db.Products.FindAsync(request.Id);
                if (entity is null)
                    return new Response { IsError = true, Message = "Product not found", StatusCode = 404 };

                entity.Name = request.Name ?? entity.Name;
                entity.Price = request.Price;
                entity.IsActive = request.IsActive;
            }

            await db.SaveChangesAsync();
            return new Response();
        }
    }

    // ════════════════════════════════════════════════════════
    // ROUTE (Endpoint Registration)
    // ════════════════════════════════════════════════════════
    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder group = endpointRouteBuilder
                .MapGroup(KrafterRoute.Products)
                .AddFluentValidationFilter();

            group.MapPost("/", async (
                    [FromBody] CreateProductRequest request,
                    [FromServices] Handler handler) =>
                {
                    Response res = await handler.CreateOrUpdateAsync(request);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Create, KrafterResource.Products);
        }
    }
}
```

> **NOTE**: Request DTOs (`CreateProductRequest`) and validators are defined in `src/Krafter.Shared/Contracts/Products/`. Backend operations import and use them directly.

### 4.2 Get Operation with Pagination (Copy This)
```csharp
using Backend.Api;
using Backend.Api.Authorization;
using Backend.Common.Extensions;
using Backend.Features;
using Backend.Features.Products._Shared;
using Backend.Infrastructure.Persistence;
using Krafter.Shared.Common;
using Krafter.Shared.Common.Auth.Permissions;
using Krafter.Shared.Common.Models;
using Krafter.Shared.Contracts.Products;
using LinqKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;

namespace Backend.Features.Products;

public sealed class Get
{
    internal sealed class Handler(KrafterContext db) : IScopedHandler
    {
        public async Task<Response<PaginationResponse<ProductDto>>> GetAsync(
            GetRequestInput requestInput,
            CancellationToken cancellationToken)
        {
            var predicate = PredicateBuilder.New<Product>(true);
            
            if (!string.IsNullOrWhiteSpace(requestInput.Id))
                predicate = predicate.And(c => c.Id == requestInput.Id);

            var query = db.Products.Where(predicate)
                .Select(x => new ProductDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Price = x.Price,
                    IsActive = x.IsActive,
                    CreatedOn = x.CreatedOn
                });

            if (!string.IsNullOrEmpty(requestInput.Filter))
                query = query.Where(requestInput.Filter);

            if (!string.IsNullOrEmpty(requestInput.OrderBy))
                query = query.OrderBy(requestInput.OrderBy);

            var items = await query.PageBy(requestInput).ToListAsync(cancellationToken);
            var totalCount = await query.CountAsync(cancellationToken);

            return new Response<PaginationResponse<ProductDto>>
            {
                Data = new PaginationResponse<ProductDto>(items, totalCount,
                    requestInput.SkipCount, requestInput.MaxResultCount)
            };
        }
    }

    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
        {
            RouteGroupBuilder group = endpointRouteBuilder
                .MapGroup(KrafterRoute.Products)
                .AddFluentValidationFilter();

            group.MapGet("/", async (
                    [FromServices] Handler handler,
                    [AsParameters] GetRequestInput requestInput,
                    CancellationToken cancellationToken) =>
                {
                    var res = await handler.GetAsync(requestInput, cancellationToken);
                    return Results.Json(res, statusCode: res.StatusCode);
                })
                .Produces<Response<PaginationResponse<ProductDto>>>()
                .MustHavePermission(KrafterAction.View, KrafterResource.Products);
        }
    }
}
```

### 4.2 Entity Template
```csharp
namespace Backend.Features.Products._Shared;

public class Product : CommonEntityProperty
{
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}
```

### 4.3 Adding to DbContext
```csharp
// In KrafterContext.cs
public DbSet<Product> Products => Set<Product>();
```

## 5. Response Pattern (MANDATORY)
**ALL handlers MUST return `Response<T>` or `Response`.**

### 5.1 Factory Methods (PREFERRED)
Use static factory methods for cleaner, more readable code:

```csharp
// ✅ PREFERRED - Use factory methods for error responses
return Response.NotFound("Product not found");           // 404
return Response.BadRequest("Invalid input");             // 400
return Response.Unauthorized("Invalid credentials");     // 401
return Response.Forbidden("Access denied");              // 403
return Response.Conflict("Resource already exists");     // 409
return Response.CustomError("Custom error", 422);        // Custom status code

// ✅ PREFERRED - Use factory methods for success responses
return Response.Success();                               // 200, no message
return Response.Success("Operation completed");          // 200, with message

// ✅ For generic Response<T>
return Response<ProductDto>.NotFound("Product not found");
return Response<ProductDto>.BadRequest("Invalid input");
return Response<ProductDto>.Success(productDto);         // 200, with data
return Response<ProductDto>.Success(productDto, "Product created"); // 200, with data and message
```

### 5.2 Constructor Initialization (ALTERNATIVE)
Constructor initialization is still valid but less preferred:

```csharp
// ✅ CORRECT - Constructor initialization (still works)
return new Response { IsError = true, Message = "Product not found", StatusCode = 404 };
return new Response<ProductDto> { Data = productDto };
return new Response();  // Success with default values (IsError = false, StatusCode = 200)

// For generic responses with data:
return new Response<PaginationResponse<ProductDto>> 
{ 
    Data = new PaginationResponse<ProductDto>(items, totalCount, skipCount, maxResultCount) 
};
```

### 5.3 What NOT to Do

```csharp
// ❌ WRONG - Never do this
return product;           // Raw type
throw new Exception();    // Unhandled exception (use custom exceptions instead)
return null;              // Null response
```

## 6. Naming Conventions
| Item | Convention | Example |
|------|------------|---------|
| Entity | Singular | `Product` |
| DbSet | Plural | `Products` |
| Namespace | `Backend.Features.<Feature>` | `Backend.Features.Products` |
| Operation File | `<Verb><Entity>.cs` | `CreateOrUpdateProduct.cs` |
| Route | lowercase, plural | `/products` |

## 7. New Feature Checklist
1. [ ] Create DTOs in `src/Krafter.Shared/Contracts/<Feature>/`:
   - `<Feature>Dto.cs` (response DTO)
   - `Create<Feature>Request.cs` (request + validator)
2. [ ] Create `Features/<Feature>/` folder in Backend
3. [ ] Create operation files (`CreateOrUpdate.cs`, `Get.cs`, `Delete.cs`)
4. [ ] Create `_Shared/<Entity>.cs` (EF entity)
5. [ ] Add DbSet to `KrafterContext.cs`
6. [ ] Create EF configuration in `Infrastructure/Persistence/Configurations/`
7. [ ] Add permissions to `src/Krafter.Shared/Common/Auth/Permissions/KrafterPermissions.cs`
8. [ ] Run migration:
   ```bash
   dotnet ef migrations add Add<Feature> --project src/Backend --context KrafterContext
   dotnet ef database update --project src/Backend --context KrafterContext
   ```
9. [ ] Test with `dotnet build` and Swagger UI


## 8. Cross-Cutting Feature Workflow

When adding a new feature that spans Backend + UI:

```
┌─────────────────────────────────────────────────────────────┐
│ STEP 1: Shared Contracts (src/Krafter.Shared/Contracts/)    │
├─────────────────────────────────────────────────────────────┤
│ Create DTOs + Validators:                                   │
│   - ProductDto.cs                                           │
│   - CreateProductRequest.cs (with validator)                │
│   - UpdateProductRequest.cs (if different from create)      │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ STEP 2: Backend (src/Backend/Features/)                     │
├─────────────────────────────────────────────────────────────┤
│ Create operations + entity:                                 │
│   - Features/Products/_Shared/Product.cs (entity)           │
│   - Features/Products/Get.cs                                │
│   - Features/Products/CreateOrUpdate.cs                     │
│   - Features/Products/Delete.cs                             │
│   - Add DbSet + EF config + migration                       │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ STEP 3: UI (src/UI/Krafter.UI.Web.Client/)                  │
├─────────────────────────────────────────────────────────────┤
│ Create Refit interface + pages:                             │
│   - Infrastructure/Refit/IProductsApi.cs                    │
│   - Register in RefitServiceExtensions.cs                   │
│   - Features/Products/Products.razor + .razor.cs            │
│   - Features/Products/CreateOrUpdateProduct.razor + .cs     │
│   - Add route + menu item                                   │
└─────────────────────────────────────────────────────────────┘
```

## 9. Import Patterns

```csharp
// Backend operation file - import shared contracts
using Krafter.Shared.Common.Models;           // Response<T>, PaginationResponse<T>
using Krafter.Shared.Contracts.Products;      // ProductDto, CreateProductRequest

namespace Backend.Features.Products;

public sealed class Get
{
    internal sealed class Handler(KrafterContext context) : IScopedHandler
    {
        public async Task<Response<PaginationResponse<ProductDto>>> ExecuteAsync(...)
        {
            // Use shared DTOs for response
        }
    }
}
```

## 10. Edge Cases: EF Core Configuration

### 10.1 Entity Configuration in KrafterContext
Add entity configuration in `Infrastructure/Persistence/KrafterContext.cs` `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // ... existing configurations ...
    
    // Add new entity configuration
    modelBuilder.Entity<Product>(entity =>
    {
        entity.Property(c => c.Id).HasMaxLength(36);
        entity.Property(c => c.CreatedById).HasMaxLength(36);
        
        // Multi-tenant query filter (REQUIRED for tenant isolation)
        entity.HasQueryFilter(c => c.IsDeleted == false && c.TenantId == tenantGetterService.Tenant.Id);
        
        // Relationships (if any)
        entity.HasMany(e => e.Categories)
            .WithOne(e => e.Product)
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    });
    
    // Apply common configuration (temporal tables, etc.)
    modelBuilder.ApplyCommonConfigureAcrossEntity();
}
```

### 10.2 Entity Base Class Pattern
Entities should inherit from `CommonEntityProperty` for audit fields:

```csharp
// File: Features/Products/_Shared/Product.cs
using Backend.Entities;

namespace Backend.Features.Products._Shared;

public class Product : CommonEntityProperty
{
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual ICollection<ProductCategory> Categories { get; set; } = [];
}
```

**CommonEntityProperty provides:**
- `Id` (string, GUID)
- `TenantId` (multi-tenancy)
- `CreatedOn`, `CreatedById` (audit)
- `IsDeleted`, `DeleteReason` (soft delete)

### 10.3 Adding DbSet
Add to `KrafterContext.cs`:

```csharp
public class KrafterContext : IdentityDbContext<...>
{
    // ... existing DbSets ...
    
    public virtual DbSet<Product> Products { get; set; }
}
```

### 10.4 Migration Commands
```bash
# Create migration
dotnet ef migrations add AddProducts --project src/Backend --context KrafterContext

# Apply migration
dotnet ef database update --project src/Backend --context KrafterContext

# Remove last migration (if needed)
dotnet ef migrations remove --project src/Backend --context KrafterContext
```


## 11. Exception Handling (IMPORTANT)

Krafter uses custom exception types that are caught by `ExceptionMiddleware` and converted to appropriate HTTP responses.

### 11.1 Custom Exception Types

```csharp
// Located in Backend/Application/Common/

// General business logic errors - returns 400 Bad Request
throw new KrafterException("Operation failed: reason here");

// Resource not found - returns 404 Not Found
throw new NotFoundException("User Not Found");

// Access denied - returns 403 Forbidden
throw new ForbiddenException("Not allowed to modify Admin Role.");

// Authentication failed - returns 401 Unauthorized
throw new UnauthorizedException("Invalid credentials");
```

### 11.2 When to Use Factory Methods vs Exceptions

**PREFERRED: Use Response Factory Methods** for expected business outcomes:

```csharp
// ✅ PREFERRED - Use factory methods for expected error conditions
if (entity is null)
    return Response.NotFound("Product not found");

if (!isValid)
    return Response.BadRequest("Invalid input data");

if (!hasPermission)
    return Response.Forbidden("Access denied");

if (!isAuthenticated)
    return Response.Unauthorized("Invalid credentials");
```

**ALTERNATIVE: Use Exceptions** for security violations or when you want to abort the entire operation:

```csharp
// ✅ Use Exceptions for security-critical violations
if (KrafterRoleConstant.IsDefault(role.Name!))
    throw new ForbiddenException($"Not allowed to modify {role.Name} Role.");

// ✅ Use Exceptions when you want to abort and rollback
if (!result.Succeeded)
    throw new KrafterException($"Register role failed {result.Errors}");
```

### 11.3 SignalR Hub Error Handling

SignalR Hubs are different from HTTP handlers - they don't return `Response` objects. Use `HubException` for SignalR error handling:

```csharp
using Microsoft.AspNetCore.SignalR;

public class RealtimeHub : Hub
{
    private const string AuthenticationFailedMessage = "Authentication Failed.";

    public override async Task OnConnectedAsync()
    {
        // ✅ Use HubException for SignalR errors (NOT Response factory methods)
        if (authFailed)
        {
            throw new HubException(AuthenticationFailedMessage);
        }
        
        await base.OnConnectedAsync();
    }
}
```

**Why HubException?**
- SignalR methods return `Task` (void), not `Response` objects
- `HubException` is specifically designed for SignalR error handling
- It properly sends error messages to connected clients
- It's the recommended pattern from Microsoft for SignalR error scenarios

### 11.4 Exception Handling in Actual Code
```csharp
// From Features/Roles/CreateOrUpdateRole.cs - Using factory methods (PREFERRED)
public async Task<Response> CreateOrUpdateAsync(CreateOrUpdateRoleRequest request)
{
    if (role == null)
    {
        return Response.NotFound("Role Not Found");
    }

    if (KrafterRoleConstant.IsDefault(role.Name!))
    {
        return Response.Forbidden($"Not allowed to modify {role.Name} Role.");
    }

    IdentityResult result = await roleManager.UpdateAsync(role);
    if (!result.Succeeded)
    {
        return Response.BadRequest($"Update role failed {result.Errors}");
    }

    return Response.Success();
}
```

## 12. Dual DbContext Architecture

Krafter uses TWO database contexts for different purposes:

### 12.1 KrafterContext (Main Context)
- **Purpose**: All feature entities, Identity tables
- **Tenant Isolation**: Has query filters for `TenantId`
- **Usage**: Most operations use this context

```csharp
internal sealed class Handler(KrafterContext db) : IScopedHandler
{
    public async Task<Response> DoSomethingAsync()
    {
        // All queries automatically filtered by TenantId
        var users = await db.Users.ToListAsync();
        await db.SaveChangesAsync();
    }
}
```

### 12.2 TenantDbContext (Cross-Tenant Context)
- **Purpose**: Tenant management, cross-tenant operations
- **Tenant Isolation**: NO automatic filtering
- **Usage**: Only for tenant CRUD and admin operations

```csharp
internal sealed class Handler(
    KrafterContext db,
    TenantDbContext tenantDbContext) : IScopedHandler
{
    public async Task<Response> UpdateTenantAsync()
    {
        // Access tenants without tenant filtering
        var tenant = await tenantDbContext.Tenants
            .FirstOrDefaultAsync(c => c.Id == tenantId);
        
        // Save to both contexts if needed
        await tenantDbContext.SaveChangesAsync();
        await db.SaveChangesAsync();
    }
}
```

### 12.3 When to Use Each Context

| Scenario | Context to Use |
|----------|---------------|
| CRUD on feature entities (Users, Roles, Products) | `KrafterContext` |
| Tenant management (create, update, delete tenants) | `TenantDbContext` |
| Cross-tenant queries (admin dashboards) | `TenantDbContext` |
| Updating tenant email when user email changes | Both contexts |

## 13. Background Jobs with TickerQ

### 13.1 Enqueueing a Job

```csharp
internal sealed class Handler(
    KrafterContext db,
    IJobService jobService) : IScopedHandler
{
    public async Task<Response> CreateUserAsync(CreateUserRequest request)
    {
        // ... create user logic ...

        // Send welcome email via background job
        await jobService.EnqueueAsync(
            new SendEmailRequestInput 
            { 
                Email = user.Email, 
                Subject = "Welcome!", 
                HtmlMessage = $"Hello {user.FirstName}..." 
            },
            "SendEmailJob",
            CancellationToken.None);

        return new Response();
    }
}
```

### 13.2 Available Job Types
- `SendEmailJob` - Sends email via configured SMTP

### 13.3 Creating New Job Types
Add to `Infrastructure/BackgroundJobs/JobService.cs`:

```csharp
public class Jobs(IEmailService emailService)
{
    [TickerFunction(nameof(SendEmailJob))]
    public async Task SendEmailJob(TickerFunctionContext<SendEmailRequestInput> tickerContext,
        CancellationToken cancellationToken)
    {
        await emailService.SendEmailAsync(
            tickerContext.Request.Email,
            tickerContext.Request.Subject,
            tickerContext.Request.HtmlMessage);
    }
    
    // Add new job types here
    [TickerFunction(nameof(MyNewJob))]
    public async Task MyNewJob(TickerFunctionContext<MyJobInput> tickerContext,
        CancellationToken cancellationToken)
    {
        // Job implementation
    }
}
```

## 14. Delete Operations (REST-Compliant Pattern)

Krafter uses soft delete with REST-compliant HTTP DELETE method.

### 14.1 Delete Handler Pattern

```csharp
// From Features/Users/DeleteUser.cs
internal sealed class Handler(
    UserManager<KrafterUser> userManager,
    KrafterContext db) : IScopedHandler
{
    public async Task<Response> DeleteAsync(string id)
    {
        KrafterUser? user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return new Response { IsError = true, Message = "User Not Found", StatusCode = 404 };
        }

        // Business rule validation
        if (user.IsOwner)
        {
            return new Response { IsError = true, Message = "Owner cannot be deleted", StatusCode = 403 };
        }

        // Soft delete - set flags, don't remove
        user.IsDeleted = true;
        db.Users.Update(user);

        // Also soft-delete related entities if needed
        List<KrafterUserRole> userRoles = await db.UserRoles
            .Where(c => c.UserId == id)
            .ToListAsync();

        foreach (KrafterUserRole userRole in userRoles)
        {
            userRole.IsDeleted = true;
        }

        await db.SaveChangesAsync();
        return new Response();
    }
}
```

### 14.2 Delete Route Pattern (REST-Compliant)

```csharp
// DELETE uses HTTP DELETE method with {id} route parameter
userGroup.MapDelete($"/{RouteSegment.ById}", async (
        [FromRoute] string id,
        [FromServices] Handler handler) =>
    {
        Response res = await handler.DeleteAsync(id);
        return Results.Json(res, statusCode: res.StatusCode);
    })
    .Produces<Response>()
    .MustHavePermission(KrafterAction.Delete, KrafterResource.Users);
```

**Important**: 
- Delete endpoints use `MapDelete("/{id}", ...)` with HTTP DELETE method
- ID comes from route parameter, not request body
- Use `RouteSegment.ById` constant for consistency

## 15. Object Mapping with Mapster

Krafter uses Mapster for object-to-object mapping.

```csharp
using Mapster;

// Map DTO to Entity
Tenant entity = request.Adapt<Tenant>();

// Map Entity to DTO
TenantDto dto = tenant.Adapt<TenantDto>();

// Map to existing object
request.Adapt(existingEntity);
```


---

## 16. Evolution & Maintenance

> **PARENT**: See also: [Root Agents.md](../../Agents.md) for global evolution rules.

### 16.1 When to UPDATE This File

| Trigger | Action |
|---------|--------|
| New handler pattern discovered | Add to Section 4 Code Templates |
| New exception type added | Add to Section 11 Exception Handling |
| New DbContext or data access pattern | Add to Section 12 |
| New background job type | Add to Section 13 |
| EF Core configuration pattern changes | Add to Section 10 |
| AI agent makes repeated Backend mistakes | Add to Common Mistakes section |

### 16.2 When to CREATE Child Agents.md

| Trigger | Location |
|---------|----------|
| Auth feature has 5+ unique patterns | `Features/Auth/Agents.md` |
| Tenant operations become complex | `Features/Tenants/Agents.md` |
| EF Core has 3+ advanced patterns | `Infrastructure/Persistence/Agents.md` |
| Background jobs grow beyond email | `Infrastructure/BackgroundJobs/Agents.md` |

### 16.3 Common Mistakes (AI Agents)

| Mistake | Correct Approach |
|---------|-----------------|
| Using constructor for error responses | Use factory methods: `Response.NotFound("...")`, `Response.BadRequest("...")` |
| Forgetting tenant query filter in EF config | Always add `HasQueryFilter(c => c.TenantId == ...)` |
| Using `MapPost("/delete", ...)` for delete endpoints | Use `MapDelete($"/{RouteSegment.ById}", ...)` with HTTP DELETE |
| Throwing raw `Exception` | Use `KrafterException`, `NotFoundException`, `ForbiddenException` |
| Using `UnauthorizedException` in SignalR Hubs | Use `HubException` for SignalR error handling |
| Missing `IScopedHandler` interface | All handlers must implement `IScopedHandler` |
| Forgetting to add DbSet to KrafterContext | Add `public virtual DbSet<Entity> Entities { get; set; }` |

## 17. Route Constants (KrafterRoute & RouteSegment)

### 17.1 Overview
Krafter uses centralized route constants defined in `src/Krafter.Shared/Common/KrafterRoute.cs`:

```csharp
// Base route prefixes
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
    public const string ById = "{id}";
    public const string UserRoles = "{userId}/roles";
    public const string RolePermissions = "{roleId}/permissions";
    public const string ByRole = "by-role/{roleId}";
    public const string Permissions = "permissions";
    public const string Refresh = "refresh";
    public const string Logout = "logout";
    // ... etc
}
```

### 17.2 Usage in Backend (Minimal APIs)
Backend endpoints SHOULD use `KrafterRoute` and `RouteSegment` constants:

```csharp
// ✅ CORRECT - Use constants in Backend
public sealed class Route : IRouteRegistrar
{
    public void MapRoute(IEndpointRouteBuilder endpointRouteBuilder)
    {
        RouteGroupBuilder group = endpointRouteBuilder
            .MapGroup(KrafterRoute.Users)
            .AddFluentValidationFilter();

        // Standard REST endpoints
        group.MapGet("/", async (...) => { ... });                    // GET /users
        group.MapPost("/", async (...) => { ... });                   // POST /users
        group.MapDelete($"/{RouteSegment.ById}", async (...) => { }); // DELETE /users/{id}
        
        // Nested resources
        group.MapGet($"/{RouteSegment.UserRoles}", async (           // GET /users/{userId}/roles
            [FromRoute] string userId, ...) => { ... });
    }
}
```

### 17.3 Route Parameter Matching (CRITICAL)
When using `RouteSegment` constants with route parameters, the method parameter name MUST match:

```csharp
// RouteSegment.ById = "{id}"
// ✅ CORRECT - Parameter name matches route placeholder
group.MapDelete($"/{RouteSegment.ById}", async ([FromRoute] string id, ...) => { });

// ❌ WRONG - Parameter name doesn't match
group.MapDelete($"/{RouteSegment.ById}", async ([FromRoute] string roleId, ...) => { });
// Error: 'roleId' is not a route parameter

// RouteSegment.UserRoles = "{userId}/roles"
// ✅ CORRECT
group.MapGet($"/{RouteSegment.UserRoles}", async ([FromRoute] string userId, ...) => { });
```

### 17.4 Where to Use Constants

| Layer | Use Constants? | Reason |
|-------|---------------|--------|
| Backend (Minimal APIs) | ✅ Yes | ASP.NET Core handles interpolated strings correctly |
| BFF (Program.cs) | ✅ Yes | Same as backend - server-side ASP.NET |
| Refit Interfaces | ❌ No | Refit has runtime issues with route parameters in interpolated strings |

---
Last Updated: 2026-01-25
Verified Against: Features/Users/CreateOrUpdateUser.cs, Features/Users/GetUsers.cs, Features/Users/DeleteUser.cs, Features/Roles/CreateOrUpdateRole.cs, Features/Tenants/Get.cs, Features/Tenants/Delete.cs, Infrastructure/Persistence/KrafterContext.cs, Hubs/RealtimeHub.cs, Common/Models/Response.cs, src/Krafter.Shared/Common/KrafterRoute.cs
---
