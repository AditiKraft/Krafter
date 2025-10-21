# copilot-instructions.md

## Table of Contents
- [1. Overview](#1-overview)
- [2. Solution Structure](#2-solution-structure)
- [3. Target Platform](#3-target-platform)
- [4. Core Concepts](#4-core-concepts)
- [5. Coding Conventions](#5-coding-conventions)
- [6. AI Agent Feature Playbook](#6-ai-agent-feature-playbook)
- [7. VSA Layer Placement Rules](#7-vsa-layer-placement-rules)
- [8. External Integration Pattern](#8-external-integration-pattern)
- [9. Authentication & Permissions](#9-authentication--permissions)
- [10. UI Guidelines](#10-ui-guidelines)
- [11. Standards & Patterns](#11-standards--patterns)
- [12. Environment & Configuration](#12-environment--configuration)
- [13. Error Handling & Telemetry](#13-error-handling--telemetry)
- [14. Development Workflow](#14-development-workflow)
- [15. AI Agent Rules](#15-ai-agent-rules)
- [16. Security & Performance](#16-security--performance)
- [17. Prompting Guide & Future](#17-prompting-guide--future)
- [18. Quick Start](#18-quick-start)

## Glossary/Index
- **VSA**: Vertical Slice Architecture – One file per operation (Request, Handler, Validator, Route)
- **Kiota**: Microsoft Kiota – Type-safe API client for backend calls
- **Radzen**: UI component library (DataGrid, TemplateForm, etc.)
- **Aspire**: .NET Aspire – Orchestration for distributed apps
- **JWT**: JSON Web Tokens – For authentication
- **EF Core**: Entity Framework Core – ORM for database
- **FluentValidation**: Validation library for DTOs
- **SignalR**: Real-time communication library
- **TickerQ**: Background job processing library
- **OpenTelemetry**: Observability (traces, metrics, logs)
- **Multi-tenancy**: Tenant isolation at database level
- **Response<T>**: Generic response wrapper for all handlers
- **KrafterClient**: Generated Kiota client for UI API calls
- **_Shared/**: Folder for code shared within a feature
- **Common/**: Folder for cross-feature shared code
- **Infrastructure/**: Folder for cross-cutting concerns (persistence, jobs, etc.)
- **Application/**: Folder for application services
- **Api/**: Folder for API configuration (middleware, auth, etc.)
- **Entities/**: Base entity interfaces
- **Hubs/**: SignalR hubs
- **Migrations/**: EF Core database migrations
- **Features/**: Vertical slices by business capability
- **UI Features/**: Feature-specific UI components
- **Kiota/**: Kiota setup folder
- **Client/**: Generated Kiota API client
- **Common/**: Shared UI utilities (models, constants, extensions)
- **Infrastructure/**: UI infrastructure (auth, API, storage, services)
- **Routes.razor**: Router configuration
- **_Imports.razor**: Global using directives
- **GlobalUsings.cs**: C# global usings
- **RegisterUIServices.cs**: UI service registration
- **Program.cs**: Entry points for backend/UI
- **appsettings.json**: Configuration files
- **wwwroot/**: Static files for UI

## 1. Overview
Krafter: .NET 9 full-stack platform with Backend (VSA, multi-tenancy, JWT), UI (Blazor WASM + Server hybrid, Radzen, Kiota), Infrastructure (Aspire, OpenTelemetry, Redis).

## 2. Solution Structure
```
aspire/ – Orchestration
src/Backend/ (VSA) – Features/, Infrastructure/, Application/, Common/, Api/, Entities/, Hubs/, Migrations/
src/UI/Krafter.UI.Web.Client/ (Blazor WASM) – Features/, Infrastructure/, Common/, Kiota/, Client/
src/UI/Krafter.UI.Web/ (Blazor Server Host)
```

## 3. Target Platform
- Backend: .NET 9, ASP.NET Core Minimal APIs, EF Core, PostgreSQL/MySQL
- UI: Blazor WASM + Server hybrid, Radzen components, Kiota client, SignalR

## 4. Core Concepts
- **Backend**: VSA (one file per operation), multi-tenancy, permissions, background jobs, observability
- **UI**: Hybrid rendering, code-behind pattern, Kiota API client, permission-based UI, Radzen components, state management, theme support

## 5. Coding Conventions
- Nullable enabled, file-scoped namespaces, primary constructors
- Async suffix for public methods
- DI over statics
- Backend: Singular entities, plural DbSets, permissions via `KrafterPermission.NameFor(action, resource)`
- UI: PascalCase components, code-behind matching, primary constructor DI

## 6. AI Agent Feature Playbook
### Backend Feature (VSA)
1. Clarify fields, constraints, permissions, endpoints
2. Search existing patterns (Users, Roles, Tenants)
3. Entity: `Features/<Feature>/_Shared/<Entity>.cs`
4. Operations: Single files in `Features/<Feature>/` (Create, Update, Delete, Get)
5. DbContext: Add DbSet, EF config
6. Migration: `dotnet ef migrations add Add<Feature>`
7. Permissions: Update `KrafterPermissions.cs`
8. Service: `Features/<Feature>/_Shared/` if shared logic
9. Telemetry: Add spans
10. Test: Build, test endpoints

### UI Feature (Blazor)
1. Clarify pages, permissions, API endpoints
2. Search patterns (Users, Roles, Tenants)
3. Folder: `Features/<Feature>/`
4. List Page: `<Feature>s.razor` + `.razor.cs` (RadzenDataGrid, pagination)
5. Form Dialog: `CreateOrUpdate<Feature>.razor` + `.razor.cs` (RadzenTemplateForm, FluentValidation)
6. Shared Components: `Features/<Feature>/_Shared/`
7. Permissions: Mirror backend in `KrafterPermissions.cs`
8. Routes: Add to `KrafterRoute.cs`
9. Menu: Update `MenuService.cs`
10. Kiota: Regenerate if API changed
11. Test: Build, verify UI/API

## 7. VSA Layer Placement Rules
### Backend Feature Organization
```
Features/<Feature>/
  ├── <Operation>.cs (Request, Handler, Validator, Route)
  └── _Shared/ (Entity, DTO, I/Service)
```

### UI Feature Organization
```
Features/<Feature>/
  ├── <Feature>s.razor + .razor.cs (list page)
  ├── CreateOrUpdate<Feature>.razor + .razor.cs (form dialog)
  └── _Shared/ (shared components, services, validators)
```

### Backend Placement Rules
| Need | Location | Example |
|------|----------|---------|
| Feature operation | `Features/<Feature>/<Operation>.cs` | `Features/Users/CreateOrUpdateUser.cs` |
| Entity | `Features/<Feature>/_Shared/<Entity>.cs` | `Features/Users/_Shared/KrafterUser.cs` |
| Service interface | `Features/<Feature>/_Shared/I<Service>.cs` | `Features/Users/_Shared/IUserService.cs` |
| Service implementation | `Features/<Feature>/_Shared/<Service>.cs` | `Features/Users/_Shared/UserService.cs` |
| Feature-specific DTOs | Inside operation or `_Shared/` | `Features/Users/_Shared/UserInfo.cs` |
| Validators | Inside operation | `CreateOrUpdateUser.Validator` |
| Routes | Inside operation | `CreateOrUpdateUser.Route` |
| Shared utilities | `Common/` | `Common/PasswordGenerator.cs` |
| Shared DTOs | `Common/Models/` | `Common/Models/Response.cs` |
| Permissions | `Common/Auth/Permissions/` | `KrafterPermissions.cs` |
| EF configurations | `Infrastructure/Persistence/Configurations/` | Entity configs |
| Middleware | `Api/Middleware/` or `Common/Middleware/` | `ExceptionMiddleware.cs` |
| Cross-cutting services | `Infrastructure/` | `Infrastructure/Persistence/`, `Infrastructure/BackgroundJobs/` |

### UI Placement Rules
| Need | Location | Example |
|------|----------|---------|
| List page | `Features/<Feature>/<Feature>s.razor[.cs]` | `Features/Users/Users.razor` |
| Form page/dialog | `Features/<Feature>/CreateOrUpdate<Feature>.razor[.cs]` | `Features/Users/CreateOrUpdateUser.razor` |
| Other feature pages | `Features/<Feature>/<PageName>.razor[.cs]` | `Features/Users/ChangePassword.razor` |
| Feature-shared components | `Features/<Feature>/_Shared/<Component>.razor` | `Features/Users/_Shared/SingleSelectUserDropDown.razor` |
| Feature services | `Features/<Feature>/_Shared/<Service>.cs` | `Features/Auth/_Shared/AuthenticationService.cs` |
| Feature validators | `Features/<Feature>/_Shared/<Validator>.cs` | `Features/Tenants/TenantValidator.cs` |
| Layout components | `Common/Components/Layout/` | `Common/Components/Layout/MainLayout.razor` |
| Reusable UI components | `Common/Components/<Category>/` | `Common/Components/Forms/DebouncedSearchInput.razor` |
| Dialog components | `Common/Components/Dialogs/` | `Common/Components/Dialogs/DeleteDialog.razor` |
| Infrastructure services | `Infrastructure/Services/` | `Infrastructure/Services/MenuService.cs` |
| API services | `Infrastructure/Api/` | `Infrastructure/Api/ClientSideApiService.cs` |
| Auth providers | `Infrastructure/Auth/` | `Infrastructure/Auth/UIAuthenticationStateProvider.cs` |
| Shared models | `Common/Models/` | `Common/Models/Response.cs` |
| Permissions | `Common/Permissions/` | `Common/Permissions/KrafterPermissions.cs` |
| Constants | `Common/Constants/` | `Common/Constants/KrafterRoute.cs` |
| Extensions | `Common/Extensions/` | `Common/Extensions/ClaimsPrincipalExtensions.cs` |

### Critical VSA Rules (Backend)
1. One file per operation (Request, Handler, Validator, Route)
2. Co-locate by feature, not layer
3. Shared abstractions in `_Shared/` only when reused across operations
4. ALL handlers return `Response<T>` or `Response`
5. Auto-registration: `IScopedHandler`, `IScopedService`, `IRouteRegistrar`
6. Produces attribute: - Always add `.Produces<T>()` to endpoint mappings (e.g., `.Produces<Response<GenerateImageResponse>>()`) so OpenAPI/Swagger accurately documents response models for client generation.
  
### Critical UI Rules
1. Code-behind separation: `.razor` + `.razor.cs`
2. Feature-based organization
3. Shared components in `_Shared/` only when reused
4. Permission-based rendering: `MustHavePermission`, `AuthorizeView`
5. Kiota client for API calls, never `HttpClient`
6. `DialogService` for modals
7. Primary constructor DI
8. `RadzenDataGrid` for lists with `LoadData`
9. Update `MenuService.cs` for new pages
10. Mirror backend permissions

## 8. External Integration Pattern
### Backend
- Operation: `Features/<Feature>/<Operation>.cs`
- External client: `Features/<Feature>/_Shared/<External>Client.cs` (interface)
- DTOs: In operation or `_Shared/`
- Config: `Api/Configuration/<Feature>Settings.cs`
- Permissions: Add to `KrafterPermissions.cs`
- Telemetry: Add spans

### UI
- Install package, register services in `RegisterUIServices.cs`
- Add imports to `_Imports.razor`
- Wrapper: `Common/Components/<Category>/<Wrapper>.razor`

## 9. Authentication & Permissions
- Backend: JWT + Google OAuth, permission-based auth via `MustHavePermission`
- UI: `UIAuthenticationStateProvider`, permission checks on pages/components, auto token refresh

## 10. UI Guidelines
- Radzen components: DataGrid, TemplateForm, Dialog, Button, etc.
- Permission checks: Page-level `@attribute [MustHavePermission]`, component-level `<AuthorizeView>`
- API calls: Always `KrafterClient` via Kiota
- State: `LayoutService` + `IKrafterLocalStorageService`
- Validation: FluentValidation
- Navigation: `NavigationManager`, `DialogService`
- Loading: `IsLoading` on components
- Responsive: `IFormFactor`, `CascadingValue`

## 11. Standards & Patterns
### Backend Response Pattern (MANDATORY)
- ALL handlers: `Response<T>.Success(data)` or `Response<T>.Failure("msg", code)`

### UI Response Handling (MANDATORY)
- Check `response.IsError`, use `response.Data` or `response.Message`

### VSA Operation File Pattern (MANDATORY)
- Nested classes: Request DTO, Handler (`IScopedHandler`), Validator (FluentValidation), Route (`IRouteRegistrar`)

### Auto Registration
- Backend: Assembly scanning for markers
- UI: Register in `RegisterUIServices.cs`

### Naming
- Backend: `<Operation>.cs`, `Backend.Features.<Feature>`, lowercase-kebab routes
- UI: `<Feature>s.razor`, `Krafter.UI.Web.Client.Features.<Feature>`

## 12. Environment & Configuration
- Backend: User-secrets (dev), env vars (prod), format `Section__Property`
- UI: `appsettings.json`, API URL via Aspire, theme/storage via services

## 13. Error Handling & Telemetry
- Backend: Global middleware, `Response<T>` pattern, OpenTelemetry spans
- UI: Kiota handles errors, `NotificationService`, global boundary

## 14. Development Workflow
### Backend Feature
1. Folder: `Features/<Feature>/`
2. Operations: Single files
3. `_Shared/`: Entities, services
4. DbContext, EF config, migration
5. Permissions, build/test

### UI Feature
1. Folder: `Features/<Feature>/`
2. List page + form dialog
3. Permissions, routes, menu
4. Kiota update, build/test

### Migration
- `dotnet ef migrations add <Name> --project src/Backend --context KrafterContext`
- `dotnet ef database update`

### Kiota
- `cd src/UI/Krafter.UI.Web.Client; kiota update -o ./Client`

### Commit Style
- `type(scope): summary` (e.g., `feat(users): add endpoint`)

### PR Checklist
- Backend: Build, migration, permissions, Response pattern, VSA
- UI: Build, code-behind, permissions, Kiota, menu, routes

## 15. AI Agent Rules
### General
1. Restate assumptions
2. Search patterns (Users, Roles, Tenants)
3. Follow namespace conventions
4. Enforce Response pattern
5. Show minimal diffs

### Backend-Specific
1. One file per operation
2. Use `_Shared` sparingly
3. Auto-register markers

### UI-Specific
1. Code-behind separation
2. Feature organization
3. Kiota client only
4. Permission rendering
5. DialogService for modals
6. Primary constructor DI
7. RadzenDataGrid for lists
8. Update MenuService
9. Mirror permissions

### VSA Decision Tree
**Backend:**
- Feature operation? → `Features/<Feature>/<Operation>.cs`
- Shared in feature? → `Features/<Feature>/_Shared/`
- Shared across features? → `Common/` or `Infrastructure/`

**UI:**
- List page? → `Features/<Feature>/<Feature>s.razor`
- Form? → `CreateOrUpdate<Feature>.razor`
- Shared in feature? → `Features/<Feature>/_Shared/`
- Layout/reusable? → `Common/Components/`
- Infrastructure? → `Infrastructure/`

## 16. Security & Performance
- Backend: Multi-tenant isolation, JWT, permission auth, input validation
- UI: JWT storage, auto refresh, permission checks, XSS/CSRF protection
- Performance: AsNoTracking, pagination, cancellation tokens, virtualization, minimize StateHasChanged

## 17. Prompting Guide & Future
### Prompts
- Backend: "Add <Feature> to Backend: fields, CRUD, permissions, VSA like Users"
- UI: "Add <Feature> UI: list with grid, form dialog, permissions, update menu"
- Full: "Add <Feature>: Backend VSA + UI pages, permissions, migrations"

### Future
- Backend: Polly, audit, rate limiting, soft delete, tests
- UI: Offline, advanced filters, real-time, export, PWA, dark mode

## 18. Quick Start
### Run
- Aspire: `dotnet run --project aspire/Krafter.Aspire.AppHost`
- Backend: `dotnet run --project src/Backend/Backend.csproj`
- UI: `dotnet run --project src/UI/Krafter.UI.Web/Krafter.UI.Web.csproj`

### Migrations
- Add: `dotnet ef migrations add <Name> --project src/Backend --context KrafterContext`
- Update: `dotnet ef database update`

### Kiota
- `cd src/UI/Krafter.UI.Web.Client; kiota update -o ./Client`