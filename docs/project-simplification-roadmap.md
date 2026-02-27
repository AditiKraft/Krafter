# Krafter Project Simplification Roadmap

## Goal
Make the project structure and naming easy to understand at a glance for both AI agents and human contributors.

## Principles
1. Prefer explicit names over generic names.
2. Keep one operation per file in backend features.
3. Avoid deep folder nesting unless it clearly adds value.
4. Keep behavior unchanged during structural refactors.
5. Build after each phase.

## Target Naming Conventions
### Backend
1. Feature folders remain plural: `Users`, `Roles`, `Tenants`, `Auth`.
2. Operation files use explicit names: `CreateUser.cs`, `UpdateUser.cs`, `GetUsers.cs`, `DeleteUser.cs`.
3. Replace ambiguous/shared folder names:
   - `_Shared` -> `Common`
   - `Api` -> `Web`
   - `Application` -> split by concern (`Context`, `Jobs`, `Notifications`, `Errors`)
   - `Hubs` -> `Realtime`
4. Keep route registrars inside operation files.

## Recommended VSA Starter Structure (Current)
```text
src/AditiKraft.Krafter.Backend/
|__ Web/                    # HTTP pipeline (routes, middleware, auth config)
|__ Features/               # Vertical slices (Auth, Users, Roles, Tenants, ...)
|__ Infrastructure/         # Technical plumbing
|   |__ Persistence/
|   |__ BackgroundJobs/
|   |__ Jobs/
|   |__ Notifications/
|   |__ Realtime/
|__ Common/                 # Cross-cutting domain/app code
|   |__ Context/
|   |__ Entities/
|   |__ Interfaces/
|   |__ Extensions/
|__ Errors/
|__ Migrations/
|__ Program.cs
```

### UI
1. Keep feature folders but use explicit page/dialog names where practical.
2. Fix typos and inconsistent names (`RestPassword` -> `ResetPassword`).
3. Keep API/Refit naming aligned with backend operation names.

## Phased Plan
## Phase 1: Safe Naming Cleanup (No Behavior Change)
1. Fix inconsistent/unclear class and file names.
2. Fix typo-based names.
3. Keep current folders and runtime behavior intact.
4. Update references and namespaces.
5. Build and verify.

## Phase 2: Simplify Cross-Cutting Backend Structure
1. Move `Application/Auth` and `Application/Multitenant` to `Context`.
2. Move `Application/BackgroundJobs` to `Jobs`.
3. Move `Application/Notifications` to `Notifications`.
4. Move `Application/Common/KrafterException.cs` to `Errors`.
5. Remove or reduce unused generic folders.
6. Build and verify.

## Phase 3: Feature Naming Normalization
1. Replace `CreateOrUpdate*` operations with explicit `Create*` and `Update*` operations.
2. Ensure permissions match operation intent (`Create` vs `Update`).
3. Keep request/response contracts stable unless necessary.
4. Build and verify.

## Phase 4: Documentation and Agent Rules Sync
1. Update root and sub `Agents.md` paths and examples.
2. Ensure examples match actual file names and folders.
3. Add a short "how to find code" map for AI and humans.

## Current Execution Scope
This pass starts with Phase 1 and the first part of Phase 2 (Application area simplification), then updates docs and builds.

## Progress (This Pass)
- Completed:
  - Added this roadmap.
  - Split `Application` area into explicit folders:
    - `Context` (`Auth`, `Tenants`)
    - `Jobs`
    - `Notifications`
    - `Errors`
  - Updated namespaces/usings after moves.
  - Removed old `Application` folder.
  - Renamed auth operation naming for clarity:
    - `GetToken` -> `Login`
    - `TokenRoute` -> `Route`
  - Renamed tenant list operation for clarity:
    - `GetTenant` -> `GetTenants`
  - Fixed UI typo naming:
    - `RestPassword.*` -> `ResetPassword.*`
  - Synced multiple `Agents.md` files with corrected project paths and renamed files.
- Pending:
  - None for the current naming passes.

## Progress (Current Pass)
- Completed:
  - Renamed feature support folders from `_Shared` to `Common`:
    - Backend: `Features/Auth|Roles|Tenants|Users/Common`
    - UI: `Features/Auth|Roles|Users/Common`
  - Updated all related namespaces/usings and references.
  - Renamed backend realtime folder:
    - `Hubs` -> `Realtime`
  - Updated backend wiring (`Program.cs`) and realtime hub namespace.
  - Renamed backend HTTP composition folder:
    - `Api` -> `Web`
  - Updated namespaces/usings from `AditiKraft.Krafter.Backend.Api.*` to `AditiKraft.Krafter.Backend.Web.*`.
  - Consolidated backend top-level folders for simpler navigation:
    - `Context` -> `Common/Context`
    - `Entities` -> `Common/Entities`
    - `Jobs` -> `Infrastructure/Jobs`
    - `Notifications` -> `Infrastructure/Notifications`
    - `Realtime` -> `Infrastructure/Realtime`
  - Updated namespaces/usings for all moved folders and references.

## Progress (Phase 3)
- Completed:
  - Split combined backend upsert operations into explicit operation files:
    - Users: `CreateUser.cs`, `UpdateUser.cs`
    - Roles: `CreateRole.cs`, `UpdateRole.cs`
    - Tenants: `CreateTenant.cs`, `UpdateTenant.cs`
  - Removed old combined files:
    - `CreateOrUpdateUser.cs`
    - `CreateOrUpdateRole.cs`
    - `CreateOrUpdate.cs` (Tenants)
  - Updated UI Refit APIs for explicit create/update calls:
    - `IUsersApi`, `IRolesApi`, `ITenantsApi`
  - Updated UI dialog submit logic to call create vs update endpoint based on `Id`.
  - Updated related `Agents.md` references to match the new operation files.

## Validation Checklist
1. `dotnet build AditiKraft.Krafter.slnx` succeeds.
2. No broken namespaces/usings.
3. Feature discovery is clearer by folder/file name.
4. Updated docs reflect real paths.
