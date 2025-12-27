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

**Note**: Request/Response DTOs are in `src/Shared/Krafter.Shared/Contracts/<Feature>/`

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

            group.MapPost("/create-or-update", async (
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

            group.MapGet("/get", async (
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

```csharp
// ✅ CORRECT
return Response<ProductDto>.Success(new ProductDto());
return Response.Failure("Not found", 404);

// ❌ WRONG - Never do this
return product;           // Raw type
throw new Exception();    // Unhandled exception
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
