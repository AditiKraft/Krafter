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

## 1. Overview
Krafter is a .NET 9 full-stack platform combining:
- **Backend**: ASP.NET Core with **Vertical Slice Architecture (VSA)**, multi-tenancy, JWT auth, background jobs (TickerQ)
- **UI**: Blazor WebAssembly + Blazor Server hybrid with Radzen components, Microsoft Kiota API client
- **Infrastructure**: Aspire orchestration, OpenTelemetry, Redis caching

## 2. Solution Structure
```
aspire/
  ├── Krafter.Aspire.AppHost – Orchestration host
  └── Krafter.Aspire.ServiceDefaults – Shared config

src/Backend/ (Single project - Vertical Slice Architecture)
  ├── Features/                    – Vertical slices (one file per operation)
  │   ├── Auth/
  │   │   ├── Login.cs             – Login operation (Request, Handler, Validator, Route)
  │   │   ├── RefreshToken.cs      – Token refresh operation
  │   │   ├── ExternalLogin.cs     – External OAuth login operation
  │   │   └── _Shared/             – Shared auth abstractions
  │   │       ├── ITokenService.cs – Token service interface
  │   │       ├── TokenService.cs  – Token service implementation
  │   │       ├── TokenResponse.cs – Token response DTO
  │   │       └── UserRefreshToken.cs – Refresh token entity
  │   ├── Users/
  │   │   ├── CreateOrUpdateUser.cs – User create/update operation
  │   │   ├── DeleteUser.cs        – User delete operation
  │   │   ├── GetUsers.cs          – User list operation (paginated)
  │   │   ├── GetUsersByRole.cs    – Get users filtered by role
  │   │   ├── GetUserRoles.cs      – Get user's assigned roles
  │   │   ├── GetUserPermissions.cs – Get user's permissions
  │   │   ├── ChangePassword.cs    – Change password operation
  │   │   ├── ForgotPassword.cs    – Forgot password operation
  │   │   ├── ResetPassword.cs     – Reset password operation
  │   │   └── _Shared/             – Shared user code
  │   │       ├── KrafterUser.cs   – User entity (Identity)
  │   │       ├── KrafterUserRole.cs – User-role join entity
  │   │       ├── KrafterUserClaim.cs – User claims entity
  │   │       ├── IUserService.cs  – User service interface
  │   │       ├── UserService.cs   – User service implementation
  │   │       └── CreateUserRequest.cs – User creation DTO
  │   ├── Roles/
  │   │   ├── CreateOrUpdateRole.cs – Role create/update operation
  │   │   ├── DeleteRole.cs        – Role delete operation
  │   │   ├── GetRoles.cs          – Role list operation (paginated)
  │   │   ├── GetRoleById.cs       – Get single role by ID
  │   │   ├── GetRoleByIdWithPermissions.cs – Get role with permissions
  │   │   ├── UpdateRolePermissions.cs – Update role permissions
  │   │   └── _Shared/             – Shared role code
  │   │       ├── KrafterRole.cs   – Role entity (Identity)
  │   │       ├── KrafterRoleClaim.cs – Role claims entity
  │   │       ├── KrafterRoleConstant.cs – Role name constants
  │   │       ├── IRoleService.cs  – Role service interface
  │   │       ├── RoleService.cs   – Role service implementation
  │   │       └── RoleDto.cs       – Role data transfer object
  │   ├── Tenants/
  │   │   ├── CreateOrUpdate.cs    – Tenant create/update operation
  │   │   ├── Delete.cs            – Tenant delete operation
  │   │   ├── Get.cs               – Tenant list operation (paginated)
  │   │   ├── SeedBasicData.cs     – Seed initial tenant data operation
  │   │   └── _Shared/
  │   │       ├── Tenant.cs        – Tenant entity
  │   │       └── DataSeedService.cs – Data seeding service
  │   ├── AppInfo/
  │   │   └── Get.cs               – Application info endpoint
  │   └── IScopedHandler.cs        – Marker interface for auto-registration
  │
  ├── Infrastructure/              – Cross-cutting infrastructure
  │   ├── Persistence/
  │   │   ├── KrafterContext.cs    – Main EF Core DbContext
  │   │   ├── TenantDbContext.cs   – Tenant management DbContext
  │   │   ├── DesignTimeDbContextFactory.cs – EF migrations support
  │   │   ├── ModelBuilderExtensions.cs – EF model configuration helpers
  │   │   ├── PersistenceConfiguration.cs – Persistence DI registration
  │   │   ├── IPersistenceScoppedService.cs – Persistence service marker
  │   │   ├── Configurations/
  │   │   │   └── SMTPEmailSettings.cs – Email configuration
  │   │   ├── Tenants/
  │   │   │   └── TenantFinderService.cs – Tenant resolution service
  │   │   └── Notifications/
  │   │       └── EmailSender.cs   – Email sending service
  │   └── BackgroundJobs/
  │       ├── BackgroundJobsContext.cs – TickerQ DbContext
  │       └── JobService.cs        – Background job service
  │
  ├── Application/                 – Application services
  │   ├── Auth/
  │   │   └── CurrentUser.cs       – Current user accessor service
  │   ├── Multitenant/
  │   │   ├── CurrentTenantService.cs – Current tenant accessor
  │   │   └── TenantServiceRegistration.cs – Tenant DI registration
  │   ├── Notifications/
  │   │   ├── IEmailService.cs     – Email service interface
  │   │   └── NotificationConfiguration.cs – Email DI registration
  │   ├── BackgroundJobs/
  │   │   ├── IJobService.cs       – Job service interface
  │   │   └── BackgroundJobsConfiguration.cs – Jobs DI registration
  │   └── Common/
  │       └── KrafterException.cs  – Custom application exception
  │
  ├── Common/                      – Shared utilities
  │   ├── Auth/
  │   │   ├── Permissions/
  │   │   │   ├── KrafterPermissions.cs – All permission definitions
  │   │   │   ├── KrafterPermission.cs  – Permission helper methods
  │   │   │   ├── KrafterAction.cs      – Action types (View, Create, etc.)
  │   │   │   └── KrafterResource.cs    – Resource types (Users, Roles, etc.)
  │   │   └── KrafterClaims.cs     – JWT claim type constants
  │   ├── Extensions/
  │   │   ├── AuthExtensions.cs    – Auth extension methods
  │   │   └── CommonExtensions.cs  – General extension methods
  │   ├── Models/
  │   │   ├── Response.cs          – Generic response wrapper
  │   │   ├── PaginationResponse.cs – Paginated response wrapper
  │   │   ├── GetRequestInput.cs   – Pagination/filter input DTO
  │   │   ├── DeleteRequestInput.cs – Delete request DTO
  │   │   ├── DeleteRequestInputValidator.cs – Delete validation
  │   │   ├── RestoreRequestInput.cs – Restore soft-deleted record DTO
  │   │   ├── UserInfo.cs          – Current user info DTO
  │   │   ├── CurrentTenantDetails.cs – Current tenant info DTO
  │   │   ├── DropDownDto.cs       – Generic dropdown DTO
  │   │   └── CommonDtoProperty.cs – Common DTO properties
  │   ├── Interfaces/
  │   │   ├── Auth/
  │   │   │   └── ICurrentUser.cs  – Current user interface
  │   │   └── Multitenancy.cs      – Multi-tenancy interfaces
  │   ├── KrafterRoute.cs          – API route constants
  │   ├── PasswordGenerator.cs     – Password generation utility
  │   ├── DatabaseSelected.cs      – Database provider selection
  │   ├── FluentValidationConfig.cs – FluentValidation configuration
  │   └── CommonServiceRegistrationExtensions.cs – Common DI registration
  │
  ├── Api/                         – API configuration
  │   ├── Authorization/
  │   │   └── PermissionAuthorization.cs – Permission-based authorization
  │   ├── Configuration/
  │   │   ├── DatabaseConfiguration.cs – Database setup
  │   │   ├── SwaggerConfiguration.cs  – Swagger/OpenAPI setup
  │   │   ├── RouteConfiguration.cs    – Route registration
  │   │   ├── CorsConfiguration.cs     – CORS policy setup
  │   │   ├── JwtSettings.cs           – JWT configuration model
  │   │   └── SecuritySettings.cs      – Security configuration model
  │   ├── Middleware/
  │   │   ├── ExceptionMiddleware.cs   – Global exception handler
  │   │   ├── AuthMiddleware.cs        – Authentication middleware
  │   │   └── MultiTenantServiceMiddleware.cs – Tenant resolution
  │   ├── IRouteRegistrar.cs       – Route registration interface
  │   └── DependencyInjection.cs   – API DI registration
  │
  ├── Entities/                    – Base entity interfaces
  │   ├── CommonEntityProperty.cs  – Common entity properties
  │   ├── IRecordState.cs          – Record state interface
  │   └── Enums/
  │       ├── RecordState.cs       – Record state enum (Active, Deleted, etc.)
  │       └── EntityKind.cs        – Entity type enumeration
  │
  ├── Hubs/                        – SignalR hubs
  │   ├── RealtimeHub.cs           – SignalR hub for real-time updates
  │   └── SignalRMethods.cs        – SignalR method name constants
  │
  ├── Migrations/                  – EF Core migrations
  │   ├── Krafter/
  │   │   ├── 20251004200652_FirstK.cs – Initial migration
  │   │   ├── 20251004200652_FirstK.Designer.cs
  │   │   └── KrafterContextModelSnapshot.cs – Current model snapshot
  │   ├── BackgroundJobs/
  │   │   ├── 20251004200753_FirstK.cs – Initial jobs migration
  │   │   ├── 20251004200753_FirstK.Designer.cs
  │   │   └── BackgroundJobsContextModelSnapshot.cs
  │   ├── 20251004201851_FirstK.cs – Tenant migration
  │   ├── 20251004201851_FirstK.Designer.cs
  │   └── TenantDbContextModelSnapshot.cs
  │
  └── Program.cs                   – Application entry point

src/UI/Krafter.UI.Web.Client/ (Blazor WebAssembly)
  ├── Features/                    – Feature-specific UI components
  │   ├── Auth/
  │   │   ├── Login.razor          – Login page (markup)
  │   │   ├── Login.razor.cs       – Login page (code-behind)
  │   │   ├── GoogleCallback.razor – OAuth callback page
  │   │   ├── GoogleCallback.razor.cs
  │   │   └── _Shared/             – Shared auth services
  │   │       ├── IAuthenticationService.cs – Auth service interface
  │   │       ├── AuthenticationService.cs – Auth service implementation
  │   │       ├── IExternalAuthService.cs – External auth interface
  │   │       ├── RegisterPermissionClaims.cs – Permission claim registration
  │   │       └── GoogleLoginState.cs – Google OAuth state
  │   ├── Users/
  │   │   ├── Users.razor          – User list page (markup)
  │   │   ├── Users.razor.cs       – User list page (code-behind)
  │   │   ├── CreateOrUpdateUser.razor – User form dialog (markup)
  │   │   ├── CreateOrUpdateUser.razor.cs – User form (code-behind)
  │   │   ├── ChangePassword.razor – Change password page
  │   │   ├── ChangePassword.razor.cs
  │   │   ├── ForgotPassword.razor – Forgot password page
  │   │   ├── ForgotPassword.razor.cs
  │   │   ├── ResetPassword.razor  – Reset password page
  │   │   ├── ResetPassword.razor.cs
  │   │   └── _Shared/             – Shared user components
  │   │       ├── SingleSelectUserDropDownDataGrid.razor – User selector
  │   │       ├── SingleSelectUserDropDownDataGrid.razor.cs
  │   │       ├── MultiSelectUserDropDownDataGrid.razor – Multi-user selector
  │   │       └── MultiSelectUserDropDownDataGrid.razor.cs
  │   ├── Roles/
  │   │   ├── Roles.razor          – Role list page (markup)
  │   │   ├── Roles.razor.cs       – Role list page (code-behind)
  │   │   ├── CreateOrUpdateRole.razor – Role form dialog (markup)
  │   │   ├── CreateOrUpdateRole.razor.cs – Role form (code-behind)
  │   │   ├── RoleValidator.cs     – Role validation rules
  │   │   └── _Shared/
  │   │       ├── SingleSelectRoleDropDownDataGrid.razor – Role selector
  │   │       ├── SingleSelectRoleDropDownDataGrid.razor.cs
  │   │       ├── MultiSelectRoleDropDownDataGrid.razor – Multi-role selector
  │   │       └── MultiSelectRoleDropDownDataGrid.razor.cs
  │   ├── Tenants/
  │   │   ├── Tenants.razor        – Tenant list page (markup)
  │   │   ├── Tenants.razor.cs     – Tenant list page (code-behind)
  │   │   ├── CreateOrUpdateTenant.razor – Tenant form dialog (markup)
  │   │   ├── CreateOrUpdateTenant.razor.cs – Tenant form (code-behind)
  │   │   ├── TenantValidator.cs   – Tenant validation rules
  │   │   └── TablesToCopy.cs      – Tenant data seeding helper
  │   ├── Home/
  │   │   ├── HomePage.razor       – Home page (markup)
  │   │   └── HomePage.razor.cs    – Home page (code-behind)
  │   └── Appearance/
  │       ├── AppearancePage.razor – Appearance settings page (markup)
  │       └── AppearancePage.razor.cs – Appearance settings (code-behind)
  │
  ├── Infrastructure/              – UI infrastructure services
  │   ├── Auth/
  │   │   └── UIAuthenticationStateProvider.cs – Blazor auth state provider
  │   ├── Api/
  │   │   ├── IApiService.cs       – API service abstraction
  │   │   ├── ClientSideApiService.cs – WebAssembly API service
  │   │   └── ServerSideApiService.cs – Server-side API service
  │   ├── Storage/
  │   │   ├── IKrafterLocalStorageService.cs – Local storage interface
  │   │   ├── KrafterLocalStorageService.cs – WASM local storage
  │   │   └── KrafterLocalStorageServiceServer.cs – Server local storage
  │   ├── Services/
  │   │   ├── MenuService.cs       – Navigation menu service
  │   │   ├── LayoutService.cs     – Layout state service
  │   │   ├── ThemeManager.cs      – Theme switching service
  │   │   ├── CommonService.cs     – Common UI operations (delete dialogs)
  │   │   ├── FormFactor.cs        – Device detection (mobile/desktop)
  │   │   ├── HttpService.cs       – HTTP utilities
  │   │   └── NullHttpContextAccessor.cs – WASM HttpContext stub
  │   ├── Http/
  │   │   ├── WebAssemblyAuthenticationHandler.cs – JWT auth handler
  │   │   ├── ServerAuthenticationHandler.cs – Server JWT handler
  │   │   └── HttpClientTenantConfigurator.cs – Tenant header setup
  │   └── SignalR/
  │       ├── SignalRService.cs    – SignalR connection service
  │       └── SignalRMethods.cs    – SignalR method constants
  │
  ├── Common/                      – Shared UI utilities
  │   ├── Components/
  │   │   ├── Layout/
  │   │   │   ├── MainLayout.razor – Main application layout
  │   │   │   ├── MainLayout.razor.cs
  │   │   │   ├── NavigationItem.razor – Sidebar menu item
  │   │   │   ├── NavigationItem.razor.cs
  │   │   │   ├── TopRight.razor   – User menu (logout, profile)
  │   │   │   ├── TopRight.razor.cs
  │   │   │   ├── Notifications.razor – Notification center
  │   │   │   ├── Notifications.razor.cs
  │   │   │   ├── InitializeSignalr.razor – SignalR initialization
  │   │   │   └── InitializeSignalr.razor.cs
  │   │   ├── Brand/
  │   │   │   ├── Logo.razor       – Application logo component
  │   │   │   ├── Logo.razor.cs
  │   │   │   ├── LoadingIndicator.razor – Loading spinner
  │   │   │   └── LoadingIndicator.razor.cs
  │   │   ├── Forms/
  │   │   │   ├── DebouncedSearchInput.razor – Debounced search input
  │   │   │   ├── DebouncedSearchInput.razor.cs
  │   │   │   └── DebouncedSearchInput.razor.input.cs – Input handling
  │   │   └── Dialogs/
  │   │       ├── DeleteDialog.razor – Delete confirmation dialog
  │   │       ├── DeleteDialog.razor.cs
  │   │       └── DeleteRequestInputValidator.cs – Delete validation
  │   ├── Models/
  │   │   ├── Response.cs          – API response wrapper
  │   │   ├── GetRequestInput.cs   – Pagination/filtering input
  │   │   ├── LocalAppState.cs     – Application state model
  │   │   └── Menu.cs              – Menu item model
  │   ├── Permissions/
  │   │   ├── KrafterPermissions.cs – All permission definitions
  │   │   ├── KrafterPermission.cs – Permission helper methods
  │   │   ├── KrafterAction.cs     – Action types (View, Create, Update, Delete)
  │   │   ├── KrafterResource.cs   – Resource types (Users, Roles, Tenants)
  │   │   └── MustHavePermissionAttribute.cs – Permission authorization attribute
  │   ├── Constants/
  │   │   ├── KrafterRoute.cs      – Route path constants
  │   │   ├── KrafterClaims.cs     – JWT claim constants
  │   │   ├── StorageConstants.cs  – Local storage key constants
  │   │   └── KrafterRoleConstant.cs – Role name constants
  │   ├── Extensions/
  │   │   ├── ClaimsPrincipalExtensions.cs – User claim helpers
  │   │   ├── RequestInputExtensions.cs – Request input helpers
  │   │   └── MathExtensions.cs    – Math utility extensions
  │   ├── Validators/
  │   │   └── FluentValidationConfig.cs – FluentValidation UI configuration
  │   └── Enums/
  │       ├── EntityKind.cs        – Entity type enumeration
  │       ├── EnumDropDown.cs      – Enum dropdown helper
  │       ├── FetchCourseTypeEnum.cs – Course fetch type
  │       └── HistoryPageViewPlacement.cs – History view placement
  │
  ├── Kiota/                       – Microsoft Kiota API client setup
  │   ├── ServiceCollectionExtensions.Kiota.cs – Kiota DI registration
  │   ├── TenantHeaderHandler.cs   – Tenant header middleware
  │   └── RefreshingTokenProvider.cs – Token refresh provider
  │
  ├── Client/                      – Auto-generated Kiota API client
  │   ├── KrafterClient.cs         – Main Kiota client
  │   ├── Models/                  – Generated DTOs
  │   │   ├── UserDto.cs           – User data transfer object
  │   │   ├── RoleDto.cs           – Role data transfer object
  │   │   ├── TenantDto.cs         – Tenant data transfer object
  │   │   ├── TokenResponse.cs     – Token response
  │   │   ├── CreateUserRequest.cs – User creation request
  │   │   ├── Response.cs          – Generic response wrapper
  │   │   └── ... (other generated DTOs)
  │   ├── Users/                   – User API endpoints
  │   │   ├── UsersRequestBuilder.cs
  │   │   ├── Get/GetRequestBuilder.cs
  │   │   ├── CreateOrUpdate/CreateOrUpdateRequestBuilder.cs
  │   │   ├── Delete/DeleteRequestBuilder.cs
  │   │   ├── ByRole/ByRoleRequestBuilder.cs
  │   │   ├── GetRoles/GetRolesRequestBuilder.cs
  │   │   ├── Permissions/PermissionsRequestBuilder.cs
  │   │   ├── ChangePassword/ChangePasswordRequestBuilder.cs
  │   │   ├── ForgotPassword/ForgotPasswordRequestBuilder.cs
  │   │   └── ResetPassword/ResetPasswordRequestBuilder.cs
  │   ├── Roles/                   – Role API endpoints
  │   │   ├── RolesRequestBuilder.cs
  │   │   ├── Get/GetRequestBuilder.cs
  │   │   ├── GetById/GetByIdRequestBuilder.cs
  │   │   ├── GetByIdWithPermissions/GetByIdWithPermissionsRequestBuilder.cs
  │   │   ├── CreateOrUpdate/CreateOrUpdateRequestBuilder.cs
  │   │   ├── UpdatePermissions/UpdatePermissionsRequestBuilder.cs
  │   │   └── Delete/DeleteRequestBuilder.cs
  │   ├── Tenants/                 – Tenant API endpoints
  │   │   ├── TenantsRequestBuilder.cs
  │   │   ├── Get/GetRequestBuilder.cs
  │   │   ├── CreateOrUpdate/CreateOrUpdateRequestBuilder.cs
  │   │   ├── Delete/DeleteRequestBuilder.cs
  │   │   └── SeedData/SeedDataRequestBuilder.cs
  │   ├── Tokens/                  – Authentication endpoints
  │   │   ├── TokensRequestBuilder.cs
  │   │   ├── Create/CreateRequestBuilder.cs
  │   │   └── Refresh/RefreshRequestBuilder.cs
  │   ├── ExternalAuth/            – External auth endpoints
  │   │   ├── ExternalAuthRequestBuilder.cs
  │   │   └── Google/GoogleRequestBuilder.cs
  │   ├── AppInfo/                 – App info endpoint
  │   │   └── AppInfoRequestBuilder.cs
  │   └── Api/                     – Additional API endpoints
  │       ├── ApiRequestBuilder.cs
  │       ├── Ticker/              – Background job ticker endpoints
  │       ├── CronTicker/          – Cron job endpoints
  │       └── ... (other API endpoints)
  │
  ├── Routes.razor                 – Router configuration & theme setup
  ├── _Imports.razor               – Global using directives
  ├── GlobalUsings.cs              – C# global usings
  ├── Program.cs                   – WebAssembly entry point
  └── RegisterUIServices.cs        – UI service registration

src/UI/Krafter.UI.Web/ (Blazor Server Host)
  ├── Components/
  │   └── App.razor                – Root component
  ├── Services/
  │   ├── ServerSideApiService.cs  – Server API service implementation
  │   ├── PersistingServerAuthenticationStateProvider.cs – Auth persistence
  │   └── FormFactorServer.cs      – Server form factor detection
  ├── wwwroot/                     – Static files
  ├── Program.cs                   – Blazor Server entry point
  └── appsettings.json             – Server configuration
````````

## 3. Target Platform
- **Backend**: .NET 9 / C# 13, ASP.NET Core Minimal APIs, EF Core, PostgreSQL/MySQL
- **UI**: Blazor WebAssembly + Server (hybrid), Radzen UI components, Microsoft Kiota, SignalR

## 4. Core Concepts

### Backend
1. **Vertical Slices**: Each feature operation in a single file (Request, Handler, Validator, Route)
2. **Multi-Tenancy**: Tenant isolation at database level with TenantDbContext
3. **Permissions**: Central `KrafterPermissions.cs` registry; permission-based authorization
4. **Background Jobs**: TickerQ for async job processing
5. **Observability**: OpenTelemetry (traces, metrics, logs) via Aspire

### UI
1. **Hybrid Rendering**: WebAssembly + Server components for optimal performance
2. **Code-Behind Pattern**: Separate `.razor` and `.razor.cs` files
3. **Kiota API Client**: Type-safe, auto-generated API client
4. **Permission-Based UI**: `MustHavePermission` attribute and `AuthorizeView` components
5. **Radzen Components**: RadzenDataGrid, RadzenDialog, RadzenButton, etc.
6. **State Management**: LocalStorageService + LayoutService for app state
7. **Theme Support**: Light/Dark/Auto theme switching via ThemeManager

## 5. Coding Conventions

### General
- Nullable enabled, file-scoped namespaces, primary constructors
- Async suffix for public methods (except framework overrides)
- No `async void` except event handlers
- DI over statics

### Backend
- **Entities**: singular (`Tenant`), **DbSets**: plural (`Tenants`)
- **Permissions**: All in `KrafterPermissions.cs`, reference via `KrafterPermission.NameFor(action, resource)`
- **Namespaces**: `Backend.Features.<Feature>` for operations, `Backend.Features.<Feature>._Shared` for shared code

### UI
- **Components**: PascalCase (e.g., `Users.razor`, `CreateOrUpdateUser.razor`)
- **Code-Behind**: Match component name exactly (e.g., `Users.razor.cs`)
- **Routes**: Defined in component using `@attribute [Route(RoutePath)]`
- **Parameters**: Use `[Parameter]` attribute for component parameters
- **Services**: Inject via primary constructors in code-behind
- **Namespaces**: `Krafter.UI.Web.Client.Features.<Feature>` for pages, `Krafter.UI.Web.Client.Features.<Feature>._Shared` for shared components

## 6. AI Agent Feature Playbook

### Backend Feature (VSA)
When adding features (e.g., Products CRUD) in VSA:
1. **Clarify**: fields, constraints, permissions, endpoints
2. **Search**: existing patterns (look at Users, Roles, Tenants features)
3. **Entity** (if needed): Add to `Features/<Feature>/_Shared/<Entity>.cs`
4. **Operations**: Create operation files in `Features/<Feature>/`:
   - `Create<Feature>.cs` - Create operation (Request, Handler, Validator, Route)
   - `Update<Feature>.cs` - Update operation
   - `Delete<Feature>.cs` - Delete operation
   - `Get<Feature>s.cs` - List operation
   - `Get<Feature>ById.cs` - Detail operation
5. **DbContext**: Add DbSet to `KrafterContext.cs`, create EF configuration
6. **Migration**: `dotnet ef migrations add Add<Feature>`
7. **Permissions**: Update `Common/Auth/Permissions/KrafterPermissions.cs`
8. **Service** (if shared logic): Add to `Features/<Feature>/_Shared/`
9. **Telemetry**: Add spans for complex operations
10. **Test**: Run build, test endpoints

### UI Feature (Blazor)
When adding UI features (e.g., Products management):
1. **Clarify**: pages needed (list, create, edit), permissions, API endpoints
2. **Search**: existing patterns (look at Users, Roles, Tenants features)
3. **Create Feature Folder**: `Features/<Feature>/`
4. **List Page**: Create `<Feature>s.razor` and `<Feature>s.razor.cs`
   - Use `RadzenDataGrid` for data display
   - Implement pagination with `GetRequestInput`
   - Add permission check with `@attribute [MustHavePermission]`
5. **Form Dialog**: Create `CreateOrUpdate<Feature>.razor` and `.razor.cs`
   - Use `RadzenTemplateForm` with FluentValidation
   - Implement primary constructor DI
   - Use `DialogService` for modal display
6. **Shared Components** (if needed): Add to `Features/<Feature>/_Shared/`
7. **Permissions**: Update `Common/Permissions/KrafterPermissions.cs` (mirror backend)
8. **Routes**: Add constant to `Common/Constants/KrafterRoute.cs`
9. **Menu**: Update `Infrastructure/Services/MenuService.cs`
10. **Kiota Client**: Regenerate if new endpoints added
11. **Test**: Build, verify UI rendering and API calls

## 7. VSA Layer Placement Rules

### Backend Feature Organization
```
Features/<Feature>/
  ├── <Operation>.cs           – Single file per operation
  │   ├── Request class
  │   ├── Handler class (implements IScopedHandler)
  │   ├── Validator class (FluentValidation)
  │   └── Route class (implements IRouteRegistrar)
  └── _Shared/                 – Shared feature code
      ├── <Entity>.cs          – Entity/domain model
      ├── <DTO>.cs             – Data transfer objects
      ├── I<Service>.cs        – Service interface
      └── <Service>.cs         – Service implementation
```

### UI Feature Organization
```
Features/<Feature>/
  ├── <Feature>s.razor         – List/grid page (markup)
  ├── <Feature>s.razor.cs      – List page code-behind
  ├── CreateOrUpdate<Feature>.razor – Form dialog (markup)
  ├── CreateOrUpdate<Feature>.razor.cs – Form code-behind
  ├── <OtherPage>.razor        – Other feature pages
  ├── <OtherPage>.razor.cs
  └── _Shared/                 – Shared feature components
      ├── SingleSelect<Feature>DropDown.razor
      ├── MultiSelect<Feature>DropDown.razor
      └── <Feature>Validator.cs
```

### Backend Placement Rules

| Need | Location | Example |
|------|----------|---------|
| **Feature operation** | `Features/<Feature>/<Operation>.cs` | `Features/Users/CreateOrUpdateUser.cs` |
| **Entity** | `Features/<Feature>/_Shared/<Entity>.cs` | `Features/Users/_Shared/KrafterUser.cs` |
| **Service interface** | `Features/<Feature>/_Shared/I<Service>.cs` | `Features/Users/_Shared/IUserService.cs` |
| **Service implementation** | `Features/<Feature>/_Shared/<Service>.cs` | `Features/Users/_Shared/UserService.cs` |
| **Feature-specific DTOs** | Inside operation file or `_Shared/` | `Features/Users/_Shared/UserInfo.cs` |
| **Validators** | Inside operation file | `CreateOrUpdateUser.Validator` class |
| **Routes** | Inside operation file | `CreateOrUpdateUser.Route` class |
| **Shared utilities** | `Common/` | `Common/PasswordGenerator.cs` |
| **Shared DTOs** | `Common/Models/` | `Common/Models/Response.cs` |
| **Permissions** | `Common/Auth/Permissions/` | `KrafterPermissions.cs` |
| **EF configurations** | `Infrastructure/Persistence/Configurations/` | Entity type configurations |
| **Middleware** | `Api/Middleware/` or `Common/Middleware/` | `ExceptionMiddleware.cs` |
| **Cross-cutting services** | `Infrastructure/` | `Infrastructure/Persistence/`, `Infrastructure/BackgroundJobs/` |

### UI Placement Rules

| Need | Location | Example |
|------|----------|---------|
| **List page** | `Features/<Feature>/<Feature>s.razor[.cs]` | `Features/Users/Users.razor` |
| **Form page/dialog** | `Features/<Feature>/CreateOrUpdate<Feature>.razor[.cs]` | `Features/Users/CreateOrUpdateUser.razor` |
| **Other feature pages** | `Features/<Feature>/<PageName>.razor[.cs]` | `Features/Users/ChangePassword.razor` |
| **Feature-shared components** | `Features/<Feature>/_Shared/<Component>.razor` | `Features/Users/_Shared/SingleSelectUserDropDown.razor` |
| **Feature services** | `Features/<Feature>/_Shared/<Service>.cs` | `Features/Auth/_Shared/AuthenticationService.cs` |
| **Feature validators** | `Features/<Feature>/_Shared/<Validator>.cs` | `Features/Tenants/TenantValidator.cs` |
| **Layout components** | `Common/Components/Layout/` | `Common/Components/Layout/MainLayout.razor` |
| **Reusable UI components** | `Common/Components/<Category>/` | `Common/Components/Forms/DebouncedSearchInput.razor` |
| **Dialog components** | `Common/Components/Dialogs/` | `Common/Components/Dialogs/DeleteDialog.razor` |
| **Infrastructure services** | `Infrastructure/Services/` | `Infrastructure/Services/MenuService.cs` |
| **API services** | `Infrastructure/Api/` | `Infrastructure/Api/ClientSideApiService.cs` |
| **Auth providers** | `Infrastructure/Auth/` | `Infrastructure/Auth/UIAuthenticationStateProvider.cs` |
| **Shared models** | `Common/Models/` | `Common/Models/Response.cs` |
| **Permissions** | `Common/Permissions/` | `Common/Permissions/KrafterPermissions.cs` |
| **Constants** | `Common/Constants/` | `Common/Constants/KrafterRoute.cs` |
| **Extensions** | `Common/Extensions/` | `Common/Extensions/ClaimsPrincipalExtensions.cs` |

### Critical VSA Rules (Backend)
1. **One file per operation** - All related code (Request, Handler, Validator, Route) in single file
2. **Co-locate by feature** - Group by business capability, not technical layer
3. **Shared abstractions in _Shared** - Only extract to _Shared when used by multiple operations
4. **Response pattern** - ALL handlers return `Response<T>` or `Response`
5. **Auto-registration** - Use `IScopedHandler` marker for DI, `IRouteRegistrar` for endpoints
6. **Namespace convention** - `Backend.Features.<Feature>` and `Backend.Features.<Feature>._Shared`
7. **Produces attribute** - Always add `.Produces<T>()` to endpoint mappings for proper OpenAPI/Swagger documentation. This ensures that the API schema correctly reflects the response types, improving client generation and documentation accuracy. For example, `.Produces<Response<GenerateImageResponse>>()` should be included on all endpoints to specify the expected response model.

### Critical UI Rules
1. **Code-Behind separation** - Always split `.razor` (markup) and `.razor.cs` (logic)
2. **Feature-based organization** - Group by feature, not component type
3. **Shared components in _Shared** - Only extract when reused across multiple pages
4. **Permission-based rendering** - Use `MustHavePermission` attribute and `AuthorizeView`
5. **Kiota client** - Always use generated `KrafterClient` for API calls
6. **DialogService** - Use for modals/dialogs, not separate routes
7. **Primary constructor DI** - Inject services via primary constructors in code-behind
8. **RadzenDataGrid for lists** - Use `RadzenDataGrid` with `LoadData` event for pagination
9. **Update MenuService** - Always add new pages to `MenuService.cs`
10. **Mirror backend permissions** - UI permissions should match backend exactly

## 8. External Integration Pattern

### Backend External Integration
Example: External API integration (e.g., payment gateway)
1. **Operation file** → `Features/<Feature>/<Operation>.cs` with Request, Handler, Validator, Route
2. **External client** → `Features/<Feature>/_Shared/<External>Client.cs` with interface
3. **DTOs** → In operation file or `_Shared/` if reused
4. **Configuration** → `Api/Configuration/<Feature>Settings.cs`
5. **Permissions** → Add to `KrafterPermissions.cs`
6. **Telemetry** → Add activity spans in handler

**Example Structure:**
```
Features/Payments/
  ├── ProcessPayment.cs        – Operation slice
  ├── GetPaymentStatus.cs
  └── _Shared/
      ├── IPaymentGateway.cs   – External service interface
      ├── StripeClient.cs      – External implementation
      └── PaymentDto.cs        – Shared DTOs
```

### UI External Integration
Example: Integrating third-party component library
1. **Install package** → Add to `Krafter.UI.Web.Client.csproj`
2. **Register services** → Add to `RegisterUIServices.cs`
3. **Add imports** → Update `_Imports.razor` if needed
4. **Create wrapper** → `Common/Components/<Category>/<Wrapper>.razor` for reusable patterns
5. **Feature usage** → Use in feature pages

## 9. Authentication & Permissions

### Backend
- JWT Bearer + Google external auth
- Permission-based authorization via `MustHavePermission` attribute
- Multi-tenant context in JWT claims
- Permissions defined in `Common/Auth/Permissions/KrafterPermissions.cs`
- Permission format: `KrafterPermission.NameFor(action, resource)`

### UI
- `UIAuthenticationStateProvider` for managing auth state
- `IAuthenticationService` for login/logout operations
- Permission checks via:
  - `@attribute [MustHavePermission(KrafterAction.View, KrafterResource.Products)]` on pages
  - `<AuthorizeView Policy="@KrafterPermission.NameFor(...)">` in markup
- Token refresh handled automatically via `WebAssemblyAuthenticationHandler`
- Permissions cached in `IKrafterLocalStorageService`

## 10. UI Guidelines

### Page Structure Pattern (List Page)

**Markup (.razor):**
```razor
@using Krafter.UI.Web.Client.Common.Permissions
@attribute [Route(RoutePath)]
@attribute [MustHavePermission(KrafterAction.View, KrafterResource.Products)]

<AuthorizeView Policy="@KrafterPermission.NameFor(KrafterAction.Create, KrafterResource.Products)">
    <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" Gap="10px" JustifyContent="JustifyContent.End" class="mt-2 mb-4">
        <RadzenButton Size="ButtonSize.Small" ButtonStyle="ButtonStyle.Primary" Icon="add_circle_outline" Text="Product" Click="@AddProduct"/>
    </RadzenStack>
</AuthorizeView>

<RadzenDataGrid @ref="grid" 
                IsLoading=@IsLoading 
                Count=@(response?.Data?.TotalCount??0) 
                LoadData=@LoadData 
                AllowSorting=true 
                Data="@response.Data.Items" 
                AllowFiltering="true" 
                AllowPaging="true" 
                PageSize="@requestInput.MaxResultCount">
    <Columns>
        <RadzenDataGridColumn Property="Name" Title="Name" />
        <RadzenDataGridColumn Property="Price" Title="Price" />
        <RadzenDataGridColumn Title="Actions" Filterable="false" Sortable="false">
            <Template Context="data">
                <RadzenSplitButton Click=@(args => ActionClicked(args, data)) Text="Actions" Icon="settings_applications">
                    <ChildContent>
                        <AuthorizeView Policy="@(KrafterPermission.NameFor(KrafterAction.Update, KrafterResource.Products))">
                            <RadzenSplitButtonItem Text="Edit" Value="@KrafterAction.Update" Icon="edit" />
                        </AuthorizeView>
                        <AuthorizeView Policy="@(KrafterPermission.NameFor(KrafterAction.Delete, KrafterResource.Products))">
                            <RadzenSplitButtonItem Text="Delete" Value="@KrafterAction.Delete" Icon="delete" />
                        </AuthorizeView>
                    </ChildContent>
                </RadzenSplitButton>
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>
```

**Code-Behind (.razor.cs):**
```csharp
using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Krafter.UI.Web.Client.Common.Constants;
using Krafter.UI.Web.Client.Common.Permissions;
using Krafter.UI.Web.Client.Common.Models;
using Krafter.UI.Web.Client.Infrastructure.Services;
using Microsoft.Kiota.Abstractions;

namespace Krafter.UI.Web.Client.Features.Products;

public partial class Products(
    CommonService commonService,
    NavigationManager navigationManager,
    LayoutService layoutService,
    DialogService dialogService,
    KrafterClient krafterClient
    ) : ComponentBase, IDisposable
{
    public const string RoutePath = KrafterRoute.Products;
    private RadzenDataGrid<ProductDto> grid;
    private GetRequestInput requestInput = new();
    private ProductDtoPaginationResponseResponse? response = new ProductDtoPaginationResponseResponse
    {
        Data = new ProductDtoPaginationResponse()
    };
    private bool IsLoading = true;

    protected override async Task OnInitializedAsync()
    {
        dialogService.OnClose += Close;
        LocalAppSate.CurrentPageTitle = "Products";
        await GetListAsync();
    }

    private async Task LoadData(LoadDataArgs args)
    {
        IsLoading = true;
        await Task.Yield();
        requestInput.SkipCount = args.Skip ?? 0;
        requestInput.MaxResultCount = args.Top ?? 10;
        requestInput.Filter = args.Filter;
        requestInput.OrderBy = args.OrderBy;
        await GetListAsync();
    }

    private async Task GetListAsync(bool resetPaginationData = false)
    {
        IsLoading = true;
        if (resetPaginationData)
        {
            requestInput.SkipCount = 0;
        }
        response = await krafterClient.Products.Get.GetAsync(RequestConfiguration(requestInput), CancellationToken.None);
        IsLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    private Action<RequestConfiguration<GetRequestBuilder.GetRequestBuilderGetQueryParameters>>? RequestConfiguration(GetRequestInput requestInput)
    {
        return configuration => 
        {
            configuration.QueryParameters.SkipCount = requestInput.SkipCount;
            configuration.QueryParameters.MaxResultCount = requestInput.MaxResultCount;
            configuration.QueryParameters.Filter = requestInput.Filter;
            configuration.QueryParameters.OrderBy = requestInput.OrderBy;
        };
    }

    private async Task AddProduct()
    {
        await dialogService.OpenAsync<CreateOrUpdateProduct>($"Add New Product",
            new Dictionary<string, object>() { { "ProductInput", new ProductDto() } },
            new DialogOptions()
            {
                Width = "40vw",
                Resizable = true,
                Draggable = true,
                Top = "5vh"
            });
    }

    private async Task UpdateProduct(ProductDto product)
    {
        await dialogService.OpenAsync<CreateOrUpdateProduct>($"Update Product {product.Name}",
            new Dictionary<string, object>() { { "ProductInput", product } },
            new DialogOptions()
            {
                Width = "40vw",
                Resizable = true,
                Draggable = true,
                Top = "5vh"
            });
    }

    private async Task DeleteProduct(ProductDto product)
    {
        await commonService.Delete(new DeleteRequestInput()
        {
            Id = product.Id,
            DeleteReason = product.DeleteReason,
            EntityKind = (int)EntityKind.Product
        }, $"Delete Product {product.Name}");
    }

    private async void Close(dynamic result)
    {
        if (result == null || !result.Equals(true)) return;
        await GetListAsync();
    }

    private async Task ActionClicked(RadzenSplitButtonItem? item, ProductDto data)
    {
        if (item is { Value: KrafterAction.Update })
        {
            await UpdateProduct(data);
        }
        else if (item is { Value: KrafterAction.Delete })
        {
            await DeleteProduct(data);
        }
    }

    public void Dispose()
    {
        dialogService.OnClose -= Close;
        dialogService.Dispose();
    }
}
```

### Form Dialog Pattern

**Markup (.razor):**
```razor
@using Krafter.Api.Client.Models
<RadzenTemplateForm Data="@CreateProductRequest" Submit="@((CreateProductRequest __) => Submit(__))">
    <FluentValidationValidator Options="@(options => options.IncludeAllRuleSets())"/>
    <RadzenStack>
        <RadzenRow AlignItems="AlignItems.Center">
            <RadzenColumn Size="12" SizeMD="2">
                <RadzenLabel Text="Name" Component="Name" />
            </RadzenColumn>
            <RadzenColumn Size="12" SizeMD="10">
                <RadzenTextBox Style="width: 100%" @bind-Value="@CreateProductRequest.Name" Name="Name" />
            </RadzenColumn>
            <ValidationMessage style="font-size: 13px" For="@(() => CreateProductRequest.Name)" />
        </RadzenRow>

        <RadzenRow AlignItems="AlignItems.Center">
            <RadzenColumn Size="12" SizeMD="2">
                <RadzenLabel Text="Price" Component="Price" />
            </RadzenColumn>
            <RadzenColumn Size="12" SizeMD="10">
                <RadzenNumeric Style="width: 100%" @bind-Value="@CreateProductRequest.Price" Name="Price" />
            </RadzenColumn>
            <ValidationMessage style="font-size: 13px" For="@(() => CreateProductRequest.Price)" />
        </RadzenRow>
    </RadzenStack>
    
    <RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.Center" Gap="1rem" Class="rz-mt-8 rz-mb-4">
        <RadzenButton ButtonType="ButtonType.Submit" IsBusy="isBusy" Size="ButtonSize.Medium" Icon="save" Text="Save" />
        <RadzenButton ButtonStyle="ButtonStyle.Light" Variant="Variant.Flat" Size="ButtonSize.Medium" Icon="cancel" Text="Cancel" Click="@Cancel" />
    </RadzenStack>
</RadzenTemplateForm>
```

**Code-Behind (.razor.cs):**
```csharp
using Krafter.Api.Client;
using Krafter.Api.Client.Models;
using Mapster;

namespace Krafter.UI.Web.Client.Features.Products;

public partial class CreateOrUpdateProduct(
    DialogService dialogService,
    KrafterClient krafterClient
    ) : ComponentBase
{
    [Parameter]
    public ProductDto? ProductInput { get; set; } = new();
    CreateProductRequest CreateProductRequest = new();
    CreateProductRequest OriginalCreateProductRequest = new();
    private bool isBusy = false;

    protected override async Task OnInitializedAsync()
    {
        if (ProductInput is {})
        {
            CreateProductRequest = ProductInput.Adapt<CreateProductRequest>();
            OriginalCreateProductRequest = ProductInput.Adapt<CreateProductRequest>();
        }
    }

    async void Submit(CreateProductRequest input)
    {
        if (ProductInput is { })
        {
            isBusy = true;
            CreateProductRequest finalInput = new();
            if (string.IsNullOrWhiteSpace(input.Id))
            {
                finalInput = input;
            }
            else
            {
                finalInput.Id = input.Id;
                if (input.Name != OriginalCreateProductRequest.Name)
                {
                    finalInput.Name = input.Name;
                }
                if (input.Price != OriginalCreateProductRequest.Price)
                {
                    finalInput.Price = input.Price;
                }
            }

            var result = await krafterClient.Products.CreateOrUpdate.PostAsync(finalInput);
            isBusy = false;
            StateHasChanged();
            if (result is {} && result.IsError == false)
            {
                dialogService.Close(true);
            }
        }
        else
        {
            dialogService.Close(false);
        }
    }

    void Cancel()
    {
        dialogService.Close();
    }
}
```

### UI Component Guidelines

#### 1. Radzen Components
- **RadzenDataGrid**: For list/table views with pagination, sorting, filtering
- **RadzenTemplateForm**: For forms with validation
- **RadzenDialog**: Via `DialogService` for modals
- **RadzenButton**: For actions
- **RadzenTextBox, RadzenNumeric, RadzenDatePicker**: For inputs
- **RadzenStack**: For layouts (replaces manual flex/grid)
- **RadzenRow/RadzenColumn**: For responsive grid layouts

#### 2. Permission Checks
```razor
@* Page-level permission *@
@attribute [MustHavePermission(KrafterAction.View, KrafterResource.Products)]

@* Component-level permission *@
<AuthorizeView Policy="@KrafterPermission.NameFor(KrafterAction.Create, KrafterResource.Products)">
    <Authorized>
        <RadzenButton Text="Add Product" Click="@AddProduct"/>
    </Authorized>
</AuthorizeView>
```

#### 3. API Calls via Kiota
```csharp
// Always use KrafterClient (injected via DI)
var response = await krafterClient.Products.Get.GetAsync(
    configuration => 
    {
        configuration.QueryParameters.Filter = filter;
        configuration.QueryParameters.SkipCount = skip;
        configuration.QueryParameters.MaxResultCount = take;
    }, 
    CancellationToken.None);
```

#### 4. State Management
```csharp
// Use LayoutService for app-wide state
LocalAppSate.CurrentPageTitle = "Products";
LocalAppSate.Density = Density.Compact;

// Use IKrafterLocalStorageService for persistence
await localStorageService.SetItemAsync("key", value);
var value = await localStorageService.GetItemAsync<T>("key");
```

#### 5. Validation
```csharp
// Use FluentValidation
public class ProductValidator : AbstractValidator<CreateProductRequest>
{
    public ProductValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Price).GreaterThan(0);
    }
}
```

#### 6. Navigation
```csharp
// Use NavigationManager for routing
navigationManager.NavigateTo(KrafterRoute.Products);

// Use DialogService for modals
await dialogService.OpenAsync<CreateOrUpdateProduct>("Title", parameters, options);
dialogService.Close(result);
```

#### 7. Loading States
```csharp
private bool IsLoading = true;

// In markup
<RadzenDataGrid IsLoading=@IsLoading ... />

// In code-behind
IsLoading = true;
await DoWorkAsync();
IsLoading = false;
await InvokeAsync(StateHasChanged);
```

#### 8. Responsive Design
```razor
@* Use IFormFactor to detect device type *@
@inject IFormFactor FormFactor

@if (FormFactor.IsMobile())
{
    <MobileView />
}
else
{
    <DesktopView />
}

@* Or use cascading parameter from Routes *@
<CascadingValue Value="@IsMobileDevice">
    @* Component tree *@
</CascadingValue>
```

### Menu Registration
Update `Infrastructure/Services/MenuService.cs`:
```csharp
new Menu()
{
    Name = "Products",
    Path = KrafterRoute.Products,
    Icon = "product-icon-static",
    Permission = KrafterPermission.NameFor(KrafterAction.View, KrafterResource.Products),
    Title = "Product Management",
    Description = "Manage products, pricing, and inventory.",
    Tags = new[] { "products", "catalog", "inventory" }
}
```

### Route Registration
Add constant to `Common/Constants/KrafterRoute.cs`:
```csharp
public static class KrafterRoute
{
    public const string Products = "/products";
    // ... other routes
}
```

## 11. Standards & Patterns

### Backend Response Pattern (MANDATORY)
- **ALL handlers** return `Response<T>` or `Response`
- **Never** return raw types, throw unhandled exceptions, or return null
- Success: `Response<T>.Success(data)` or `Response.Success()`
- Failure: `Response<T>.Failure("message", statusCode)` or `Response.Failure("message", statusCode)`

```csharp
// Example Handler
internal sealed class Handler(UserManager<KrafterUser> userManager) : IScopedHandler
{
    public async Task<Response<UserDto>> CreateAsync(CreateUserRequest request)
    {
        var user = new KrafterUser { /* ... */ };
        var result = await userManager.CreateAsync(user, request.Password);
        
        if (!result.Succeeded)
        {
            return Response<UserDto>.Failure("Failed to create user", 400);
        }
        
        return Response<UserDto>.Success(new UserDto(user));
    }
}
```

### UI Response Handling Pattern (MANDATORY)
```csharp
// In code-behind
var response = await krafterClient.Products.Get.GetAsync(...);

// Check response status
if (response is { IsError: false, Data: not null })
{
    // Success - use response.Data
    this.products = response.Data.Items;
}
else
{
    // Error - response.Message contains error message
    // UI automatically shows notification via middleware
}
```

### Backend VSA Operation File Pattern (MANDATORY)
Each operation file contains these nested classes:

```csharp
namespace Backend.Features.Users;

public sealed class CreateOrUpdateUser
{
    // 1. Request DTO
    public sealed class CreateUserRequest
    {
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string Email { get; set; } = default!;
        // ... other properties
    }

    // 2. Handler (business logic)
    internal sealed class Handler(
        UserManager<KrafterUser> userManager,
        RoleManager<KrafterRole> roleManager) : IScopedHandler
    {
        public async Task<Response> CreateOrUpdateAsync(CreateUserRequest request)
        {
            // Business logic here
            return Response.Success();
        }
    }

    // 3. Validator (FluentValidation)
    internal sealed class Validator : AbstractValidator<CreateUserRequest>
    {
        public Validator()
        {
            RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    // 4. Route (endpoint registration)
    public sealed class Route : IRouteRegistrar
    {
        public void MapRoute(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(KrafterRoute.Users)
                .AddFluentValidationFilter();

            group.MapPost("/create-or-update", async (
                [FromBody] CreateUserRequest request,
                [FromServices] Handler handler) =>
            {
                var result = await handler.CreateOrUpdateAsync(request);
                return result.IsError 
                    ? Results.Json(result, statusCode: result.StatusCode)
                    : TypedResults.Ok(result);
            })
            .MustHavePermission(KrafterAction.Create, KrafterResource.Users);
        }
    }
}
```

### Auto Service Registration

#### Backend
- **Handlers**: Implement `IScopedHandler` for auto-registration
- **Services**: Implement `IScopedService` for auto-registration
- **Routes**: Implement `IRouteRegistrar` for auto-registration
- Registered via assembly scanning in `Program.cs`

#### UI
- **Services**: Register in `RegisterUIServices.cs`
```csharp
public static class RegisterUIServices
{
    public static void AddUIServices(this IServiceCollection service, string remoteHostUrl)
    {
        service.AddScoped<MenuService>();
        service.AddScoped<LayoutService>();
        service.AddScoped<CommonService>();
        service.AddScoped<IAuthenticationService, AuthenticationService>();
        // ... other services
    }
}
```

### Naming Convention

#### Backend
- **Files**: `<Operation>.cs` (e.g., `CreateOrUpdateUser.cs`, `GetUsers.cs`)
- **Namespaces**: `Backend.Features.<Feature>` or `Backend.Features.<Feature>._Shared`
- **Routes**: `lowercase-kebab` plural (`users`, `roles`, `tenants`)
- **Resources**: `PascalCase` plural (`Users`, `Roles`, `Tenants`)
- **Permissions**: `Verb + Resource` (`Create Users`, `View Roles`)
- **Entities**: Singular (`KrafterUser`, `Tenant`)
- **DbSets**: Plural (`Users`, `Tenants`)

#### UI
- **Pages**: `<Feature>s.razor` for lists, `<Operation><Feature>.razor` for forms
- **Code-Behind**: Match `.razor` file exactly (e.g., `Users.razor.cs`)
- **Components**: PascalCase (e.g., `DeleteDialog.razor`, `DebouncedSearchInput.razor`)
- **Namespaces**: `Krafter.UI.Web.Client.Features.<Feature>` or `Krafter.UI.Web.Client.Features.<Feature>._Shared`
- **Routes**: Same as backend (`/users`, `/roles`, `/tenants`)
- **Constants**: Defined in `KrafterRoute.cs` (e.g., `KrafterRoute.Users`)

## 12. Environment & Configuration

### Backend
- Dev: `dotnet user-secrets`
- Prod: environment variables
- Format: `Section__Property` (`Smtp__ApiKey`, `Database__ConnectionString`)
- Multi-database support: PostgreSQL, MySQL (configured in `DatabaseConfiguration.cs`)
- Never commit secrets

### UI
- Configuration in `appsettings.json` (Krafter.UI.Web project)
- API URL: `services:krafter-api:https:0` (Aspire orchestration)
- Remote Host URL: `RemoteHostUrl` configuration key
- Theme preferences stored in cookies via `RadzenCookieThemeService`
- User preferences stored in local storage via `IKrafterLocalStorageService`

## 13. Error Handling & Telemetry

### Backend Error Handling
- Global exception middleware: `ExceptionMiddleware.cs`
- All handlers return `Response<T>` pattern
- Map exceptions to appropriate HTTP status codes
- Never expose stack traces to clients

### Backend Telemetry
- OpenTelemetry via Aspire ServiceDefaults
- Activity spans for business operations
- Activity tags: `feature`, `operation`, `tenant.id`, `user.id`
- Redact sensitive data in logs

### UI Error Handling
- API errors automatically handled by Kiota client
- Response pattern ensures consistent error handling
- `NotificationService` for user-facing notifications
- Global error boundary in `Routes.razor`

### UI Telemetry
- SignalR for real-time notifications
- Console logging for debugging (development only)
- User actions tracked via menu navigation

## 14. Development Workflow

### Adding a New Backend Feature
1. Create feature folder: `Features/<Feature>/`
2. Create operation files: `Create<Feature>.cs`, `Get<Feature>s.cs`, etc.
3. Create `_Shared/` folder for entities and services
4. Add entity to `KrafterContext.cs` or `TenantDbContext.cs`
5. Create EF configuration in `Infrastructure/Persistence/Configurations/`
6. Run migration: `dotnet ef migrations add Add<Feature>`
7. Add permissions to `KrafterPermissions.cs`
8. Build and test endpoints

### Adding a New UI Feature
1. Create feature folder: `Features/<Feature>/`
2. Create list page: `<Feature>s.razor` and `<Feature>s.razor.cs`
3. Create form dialog: `CreateOrUpdate<Feature>.razor` and `.razor.cs`
4. Add route constant to `KrafterRoute.cs`
5. Add permissions to `KrafterPermissions.cs` (mirror backend)
6. Update `MenuService.cs` to add navigation item
7. Regenerate Kiota client if new endpoints added:
   
```bash
  # Navigate to UI.Web.Client project & Update Kiota client (if API changed)
  cd src/UI/Krafter.UI.Web.Client
  kiota update -o ./Client 
  ```

8. Build and test UI rendering and API integration

### Migration Workflow
```bash
# Add migration
dotnet ef migrations add Add<FeatureName> --project src/Backend --context KrafterContext

# Update database
dotnet ef database update --project src/Backend --context KrafterContext
```

### Kiota Client Regeneration
```bash
# Navigate to UI.Web.Client project
cd src/UI/Krafter.UI.Web.Client

# Update Kiota client (reads kiota-config.json)
kiota update -o ./Client

# Or generate from scratch
kiota generate -d https://localhost:7001/swagger/v1/swagger.json -c KrafterClient -n Krafter.Api.Client -o ./Client
```

### Commit Style
```
type(scope): summary

feat(users): add user creation endpoint
feat(ui-users): add user management page
fix(auth): resolve token refresh issue
refactor(tenants): consolidate tenant operations
ui(products): add product list page
```

### PR Checklist

#### Backend
- [ ] Build succeeds (`dotnet build`)
- [ ] Migration added (if schema change)
- [ ] Permissions added to `KrafterPermissions.cs`
- [ ] All handlers return `Response<T>/Response`
- [ ] Operation files follow VSA pattern
- [ ] No secrets committed
- [ ] Routes use `IRouteRegistrar` pattern
- [ ] FluentValidation added where needed

#### UI
- [ ] Build succeeds (`dotnet build`)
- [ ] Code-behind pattern used (`.razor` + `.razor.cs`)
- [ ] Permission checks on pages and actions
- [ ] Kiota client regenerated if API changed
- [ ] Menu updated in `MenuService.cs`
- [ ] Route constant added to `KrafterRoute.cs`
- [ ] Permissions added to `KrafterPermissions.cs`
- [ ] FluentValidation used for forms
- [ ] Loading states implemented
- [ ] Responsive design tested

## 15. AI Agent Rules

### General AI Agent Rules
1. **Restate assumptions** - Confirm feature requirements before coding
2. **Search existing patterns** - Use `Users`, `Roles`, `Tenants` as templates
3. **Follow namespace convention** - Backend: `Backend.Features.<Feature>`, UI: `Krafter.UI.Web.Client.Features.<Feature>`
4. **Enforce Response pattern** - All handlers return `Response<T>` or `Response`
5. **Show minimal diffs** - Only show new/modified code

### Backend-Specific AI Agent Rules
1. **One file per operation** - Consolidate Request, Handler, Validator, Route
2. **Use _Shared sparingly** - Only extract when code is reused across operations
3. **Auto-register services** - Use `IScopedHandler`, `IScopedService`, `IRouteRegistrar` markers

### UI-Specific AI Agent Rules
1. **Code-behind separation** - Always create both `.razor` and `.razor.cs` files
2. **Feature-based organization** - Group by feature, not component type
3. **Use Kiota client** - Always use `KrafterClient` for API calls, never HttpClient directly
4. **Permission-based rendering** - Add `MustHavePermission` attribute and `AuthorizeView` components
5. **DialogService for modals** - Use `DialogService.OpenAsync` for forms, not separate routes
6. **Primary constructor DI** - Inject services via primary constructors in code-behind
7. **RadzenDataGrid for lists** - Use `RadzenDataGrid` with `LoadData` event for pagination
8. **Update MenuService** - Always add new pages to `MenuService.cs`
9. **Mirror backend permissions** - UI permissions should match backend exactly

### VSA Decision Tree for AI Agents

**Backend:**
```
Question: Where should this code go?

Is it a feature operation (CRUD, business logic)?
  → Features/<Feature>/<Operation>.cs (single file)

Is it shared across multiple operations in same feature?
  → Features/<Feature>/_Shared/

Is it shared across multiple features?
  → Common/ (utilities) or Infrastructure/ (cross-cutting services)

Is it an entity?
  → Features/<Feature>/_Shared/<Entity>.cs

Is it middleware or configuration?
  → Api/Configuration/<Feature>Settings.cs

Is it database-related (DbContext, migrations)?
  → Infrastructure/Persistence/
```

**UI:**
```
Question: Where should this UI code go?

Is it a list/grid page?
  → Features/<Feature>/<Feature>s.razor + .razor.cs

Is it a form/dialog?
  → Features/<Feature>/CreateOrUpdate<Feature>.razor + .razor.cs

Is it a feature-specific page?
  → Features/<Feature>/<PageName>.razor + .razor.cs

Is it shared within a feature (dropdown, selector)?
  → Features/<Feature>/_Shared/<Component>.razor

Is it a feature service/validator?
  → Features/<Feature>/_Shared/<Service>.cs

Is it a layout component?
  → Common/Components/Layout/

Is it a reusable UI component?
  → Common/Components/<Category>/

Is it an infrastructure service?
  → Infrastructure/Services/ or Infrastructure/Api/

Is it a shared model/constant?
  → Common/Models/ or Common/Constants/
```

## 16. Security & Performance

### Backend Security Notes
- Multi-tenant isolation enforced at DbContext level
- JWT tokens include tenant context
- Permission-based authorization on all endpoints
- Never expose internal errors to clients
- Validate all inputs with FluentValidation
- Hash passwords with ASP.NET Core Identity

### UI Security Notes
- JWT tokens stored in local storage (with refresh token rotation)
- Automatic token refresh via `WebAssemblyAuthenticationHandler`
- Permission checks on routes and UI elements
- XSS protection via Blazor's built-in HTML encoding
- CSRF protection via antiforgery tokens
- Tenant context included in all API requests

### Backend Performance Tips
- Use `AsNoTracking()` for read-only queries
- Implement pagination for list operations
- Use cancellation tokens
- Background jobs for long-running operations (TickerQ)
- Cache reference data where appropriate

### UI Performance Tips
- Use `InteractiveWebAssembly` render mode for better UX
- Implement virtualization for large lists (`RadzenDataGrid` handles this)
- Lazy load feature modules when possible
- Cache API responses in `IKrafterLocalStorageService`
- Use `@key` directive for efficient list rendering
- Debounce search inputs (see `DebouncedSearchInput.razor`)
- Minimize `StateHasChanged()` calls

## 17. Prompting Guide & Future

### Prompting Suggestions (For Humans)

**Backend Feature:**
"Add Products feature to Backend project: fields (Id GUID, Name string 100 chars, Price decimal, IsActive bool), CRUD operations (Create, Update, Delete, GetAll, GetById), permissions (Products.View/Create/Update/Delete), multi-tenant, use VSA pattern like Users feature."

**UI Feature:**
"Add Products UI to Krafter.UI.Web.Client: list page with RadzenDataGrid, create/edit dialog with form validation, permissions (View/Create/Update/Delete Products), use pattern from Users feature, update MenuService."

**Full-Stack Feature:**
"Add Products feature to Krafter solution: Backend VSA (Id, Name, Price, IsActive, multi-tenant) + UI Blazor pages (list with grid, create/edit dialog), permissions, migrations, update menu."

### Future Enhancements (Backlog)

#### Backend
- Add Polly resilience policies for external services
- Implement audit trail for all operations
- Add rate limiting per tenant
- Implement soft delete pattern consistently
- Add integration tests for critical flows

#### UI
- Implement offline support with service workers
- Add advanced filtering UI component
- Implement real-time data updates via SignalR
- Add export to Excel functionality
- Implement drag-and-drop file uploads
- Add progressive web app (PWA) support
- Implement dark mode improvements

## 18. Quick Start

### Run the Solution
```bash
# Restore packages
dotnet restore

# Run with Aspire (recommended)
dotnet run --project aspire/Krafter.Aspire.AppHost/Krafter.Aspire.AppHost.csproj

# Run Backend directly (for API development)
dotnet run --project src/Backend/Backend.csproj

# Run UI directly (for UI development)
dotnet run --project src/UI/Krafter.UI.Web/Krafter.UI.Web.csproj
```

### Database Migrations
```bash
# Create migration
dotnet ef migrations add <MigrationName> --project src/Backend --context KrafterContext

# Update database
dotnet ef database update --project src/Backend --context KrafterContext
```

### Kiota Client
```bash
# Navigate to UI.Web.Client project
cd src/UI/Krafter.UI.Web.Client

# Update Kiota client (reads kiota-config.json)
kiota update -o ./Client

# Or generate from scratch
kiota generate -d https://localhost:7001/swagger/v1/swagger.json -c KrafterClient -n Krafter.Api.Client -o ./Client
```