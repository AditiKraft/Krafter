# Backend AI Instructions (Vertical Slice Architecture)

> **SCOPE**: API endpoints, Handlers, Entities, Database, Permissions, Background Jobs.

## 1. Core Principle: One File Per Operation
Every feature operation MUST be self-contained in a **single file**:
```
Features/<Feature>/<Operation>.cs
├── Request      (DTO)
├── Handler      (Business Logic)
├── Validator    (FluentValidation)
└── Route        (Endpoint Mapping)
```

## 2. Decision Tree: Where Does This Code Go?

```
┌─────────────────────────────────────────────────────────────┐
│ What are you adding?                                        │
├─────────────────────────────────────────────────────────────┤
│ Feature operation (CRUD, business logic)?                   │
│   → Features/<Feature>/<Operation>.cs                       │
│                                                             │
│ Entity/Domain model?                                        │
│   → Features/<Feature>/_Shared/<Entity>.cs                  │
│                                                             │
│ Service shared across operations in same feature?           │
│   → Features/<Feature>/_Shared/<Service>.cs                 │
│                                                             │
│ Service shared across MULTIPLE features?                    │
│   → Infrastructure/ or Common/                              │
│                                                             │
│ Permission definition?                                      │
│   → Common/Auth/Permissions/KrafterPermissions.cs           │
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
src/Backend/
├── Features/
│   ├── Users/
│   │   ├── CreateOrUpdateUser.cs    ← Operation slice
│   │   ├── DeleteUser.cs
│   │   ├── GetUsers.cs
│   │   └── _Shared/
│   │       ├── KrafterUser.cs       ← Entity
│   │       ├── IUserService.cs      ← Interface
│   │       └── UserService.cs       ← Implementation
│   └── <YourFeature>/
│       ├── <Operation>.cs
│       └── _Shared/
├── Infrastructure/
│   ├── Persistence/
│   │   ├── KrafterContext.cs        ← Main DbContext
│   │   └── Configurations/          ← EF configurations
│   └── BackgroundJobs/
├── Common/
│   ├── Auth/Permissions/
│   │   └── KrafterPermissions.cs    ← All permissions
│   └── Models/
│       └── Response.cs              ← Response wrapper
└── Api/
    ├── Middleware/
    └── Configuration/
```

## 4. Code Templates

### 4.1 Complete Operation File (Copy This)
```csharp
namespace Backend.Features.Products;

public sealed class CreateOrUpdateProduct
{
    // ════════════════════════════════════════════════════════
    // REQUEST DTO
    // ════════════════════════════════════════════════════════
    public sealed class Request
    {
        public string? Id { get; set; }
        public string Name { get; set; } = default!;
        public decimal Price { get; set; }
        public bool IsActive { get; set; } = true;
    }

    // ════════════════════════════════════════════════════════
    // HANDLER (Business Logic)
    // ════════════════════════════════════════════════════════
    internal sealed class Handler(
        KrafterContext context,
        ICurrentUser currentUser) : IScopedHandler
    {
        public async Task<Response> ExecuteAsync(Request request, CancellationToken ct)
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
                    IsActive = request.IsActive,
                    CreatedBy = currentUser.GetUserId()
                };
                context.Products.Add(entity);
            }
            else
            {
                // UPDATE
                entity = await context.Products.FindAsync([request.Id], ct);
                if (entity is null)
                    return Response.Failure("Product not found", 404);

                entity.Name = request.Name ?? entity.Name;
                entity.Price = request.Price;
                entity.IsActive = request.IsActive;
                entity.ModifiedBy = currentUser.GetUserId();
            }

            await context.SaveChangesAsync(ct);
            return Response.Success();
        }
    }

    // ════════════════════════════════════════════════════════
    // VALIDATOR (FluentValidation)
    // ════════════════════════════════════════════════════════
    internal sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Price)
                .GreaterThan(0);
        }
    }

    // ════════════════════════════════════════════════════════
    // ROUTE (Endpoint Registration)
    // ════════════════════════════════════════════════════════
    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder app)
        {
            app.MapGroup(KrafterRoute.Products)
                .AddFluentValidationFilter()
                .MapPost("/create-or-update", async (
                    [FromBody] Request request,
                    [FromServices] Handler handler,
                    CancellationToken ct) =>
                {
                    var result = await handler.ExecuteAsync(request, ct);
                    return result.IsError
                        ? Results.Json(result, statusCode: result.StatusCode)
                        : TypedResults.Ok(result);
                })
                .Produces<Response>()
                .MustHavePermission(KrafterAction.Create, KrafterResource.Products);
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
1. [ ] Create `Features/<Feature>/` folder
2. [ ] Create operation files (`Create.cs`, `Get.cs`, `Delete.cs`)
3. [ ] Create `_Shared/<Entity>.cs`
4. [ ] Add DbSet to `KrafterContext.cs`
5. [ ] Create EF configuration in `Infrastructure/Persistence/Configurations/`
6. [ ] Add permissions to `KrafterPermissions.cs`
7. [ ] Run migration:
   ```bash
   dotnet ef migrations add Add<Feature> --project src/Backend --context KrafterContext
   dotnet ef database update --project src/Backend --context KrafterContext
   ```
8. [ ] Test with `dotnet build` and Swagger UI
