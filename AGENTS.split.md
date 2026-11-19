# Krafter AI Agent Instructions

> **READ THIS FIRST**: This is the entry point for AI agents working on the Krafter project.

## 1. Overview
Krafter is a .NET 10 full-stack application with separate Backend API and Blazor UI hosts:
- **Backend**: ASP.NET Core Minimal APIs + Vertical Slice Architecture (VSA) — runs as its own process
- **UI**: Hybrid Blazor (WebAssembly + Server) + Radzen Components — runs as a separate process with BFF proxy
- **Infrastructure**: .NET Aspire (orchestrates both hosts), OpenTelemetry, PostgreSQL

## 1.1 Hosting Architecture (Split-Host)
This project uses a **split-host** topology — Backend and UI run as independent processes:

| Component | Role |
|-----------|------|
| **Backend API** (`AditiKraft.Krafter.Backend`) | Standalone API host. Runs on its own port. All routes are prefixed with `/api`. |
| **UI.Web** (`AditiKraft.Krafter.UI.Web`) | Blazor server host + BFF auth proxy. Serves the Blazor app and proxies **only auth/token cookie-management endpoints** to Backend. |
| **UI.Web.Client** (`AditiKraft.Krafter.UI.Web.Client`) | WASM client. Auth calls (`IAuthApi`) go through the BFF on UI.Web; feature APIs (`IUsersApi`, `IRolesApi`, `ITenantsApi`, etc.) call Backend directly. |
| **Aspire AppHost** | Orchestrates Backend + UI.Web as separate resources. Provides service discovery so UI.Web can locate Backend by name. |

**Request flow**: Feature APIs: Browser (WASM) → Backend API (`/api/...`) directly. Auth APIs: Browser → UI.Web BFF → Backend API.
**Server connections**: UI.Web resolves the Backend address in `UiUrlConfiguration`: explicit server override, then Aspire service discovery, then the public API address. Server-side Refit sends the tenant identifier in a header.

## 2. Which Instructions to Read?

```
┌─────────────────────────────────────────────────────────────┐
│                    DECISION TREE                            │
├─────────────────────────────────────────────────────────────┤
│ What are you working on?                                    │
│                                                             │
│ ├── API endpoint, Handler, Entity, Database?               │
│ │   └── READ: src/AditiKraft.Krafter.Backend/AGENTS.md                        │
│ │                                                           │
│ ├── Blazor page, Component, UI Service?                    │
│ │   └── READ: src/UI/AGENTS.md                             │
│ │                                                           │
│ ├── Shared DTOs, Requests, Responses (Contracts)?          │
│ │   └── READ: src/AditiKraft.Krafter.Contracts/AGENTS.md                 │
│ │                                                           │
│ ├── Aspire orchestration, Docker, CI/CD?                   │
│ │   └── Use patterns from aspire/ and .github/workflows/   │
│ │                                                           │
│ └── Cross-cutting (affects both Backend + UI)?             │
│     └── READ BOTH Backend + UI sub-files                   │
└─────────────────────────────────────────────────────────────┘
```

For URL settings, hosting-mode routing, tenant domains, CORS, or Google callbacks, read [Configure application URLs](docs/url-configuration.md) before changing code or configuration.

## 2.1 New Feature Flow (Short Version)
1. If a feature-level `AGENTS.md` exists, read it first.
2. Add contracts + validators in `src/AditiKraft.Krafter.Contracts/Contracts/<Feature>/`.
3. Add permissions/routes in `src/AditiKraft.Krafter.Contracts/Common/`.
4. Add Backend operations in `src/AditiKraft.Krafter.Backend/Features/<Feature>/`.
5. Add UI pages and `I<Feature>Api.cs` together in `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/<Feature>/`.

## File Placement

- Package versions: root `Directory.Packages.props`. Add `PackageReference` entries without a `Version` in project files.
- Shared server UI registration: `src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiHostServiceRegistration.cs`. Keep host-specific authentication and endpoint selection in `Program.cs`; reusable BFF mappers live in server `Infrastructure/Auth/`.

- Backend operations: `src/AditiKraft.Krafter.Backend/Features/<Feature>/<Operation>.cs`.
- UI pages, dialogs, and API interface: `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/<Feature>/`.
- Shared requests, responses, and validators: `src/AditiKraft.Krafter.Contracts/Contracts/<Feature>/`.
- Follow `src/AditiKraft.Krafter.Backend/AGENTS.md` and `src/UI/AGENTS.md` for infrastructure placement.
- Match file names to their main types and namespaces to folders. Keep each operation's Handler + Route together and each request's validator with its request.
- After moving files, update imports, file links, and instruction references. Template developers must also verify the `src-single` file links and both generated hosting variants.

## 2.2 Deep Dives
- Backend persistence: `src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/AGENTS.md`
- Backend background jobs: `src/AditiKraft.Krafter.Backend/Infrastructure/Jobs/AGENTS.md`
- Backend auth: `src/AditiKraft.Krafter.Backend/Features/Auth/AGENTS.md`
- Backend Users feature: `src/AditiKraft.Krafter.Backend/Features/Users/AGENTS.md`
- Backend roles: `src/AditiKraft.Krafter.Backend/Features/Roles/AGENTS.md`
- Backend tenants: `src/AditiKraft.Krafter.Backend/Features/Tenants/AGENTS.md`
- UI Refit: `src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/AGENTS.md`
- UI auth: `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/AGENTS.md`
- UI users: `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/AGENTS.md`
- UI roles: `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/AGENTS.md`
- UI tenants: `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/AGENTS.md`

## 2.3 Bugfix Fast Path
1. Locate the closest matching feature `AGENTS.md` and read it.
2. Find the exact operation file (one file per operation) and trace request/response types in Shared.
3. Apply minimal diff; verify response shape and route/permission usage.

## 3. Solution Structure
```
AditiKraft.Krafter/
├── AGENTS.md                    ← YOU ARE HERE
├── aspire/                      # Aspire orchestration (2 app resources)
│   ├── AditiKraft.Krafter.Aspire.AppHost/
│   └── AditiKraft.Krafter.Aspire.ServiceDefaults/
├── src/
│   ├── AditiKraft.Krafter.Contracts/          # Shared contracts library
│   │   ├── AGENTS.md
│   │   ├── Contracts/           # API DTOs, Requests, Responses
│   │   └── Common/              # Shared utilities, models
│   ├── AditiKraft.Krafter.Backend/                 # API host (separate process)
│   │   ├── AGENTS.md
│   │   ├── Web/                 # HTTP pipeline (routes, middleware, auth config)
│   │   ├── Features/            # Vertical slices (business logic)
│   │   ├── Infrastructure/      # Persistence, jobs, notifications, realtime
│   │   ├── Common/              # Auth, tenants, entities, extensions
│   │   ├── Errors/              # Exception types
│   │   └── Migrations/          # EF Core migrations
│   ├── AditiKraft.Krafter.Backend.Migrator/        # Short-lived EF migration runner
│   └── UI/                      # Blazor (See src/UI/AGENTS.md)
│       ├── AGENTS.md
│       ├── AditiKraft.Krafter.UI.Web.Client/  # WASM client
│       └── AditiKraft.Krafter.UI.Web/         # Server host + BFF proxy
```

## 4. Global Coding Conventions
| Rule | Requirement |
|------|-------------|
| **Nullable** | Enabled. Use `default!` for non-nullable properties. |
| **Async** | Use `Async` suffix. No `async void` except event handlers. |
| **DI** | Primary constructors preferred. |
| **Secrets** | NEVER commit. Use `dotnet user-secrets` locally. |
| **Namespaces** | File-scoped. Match folder structure. |

## 5. Development Commands
```bash
# Run entire solution via Aspire (recommended — starts Backend + UI + infrastructure)
dotnet run --project aspire/AditiKraft.Krafter.Aspire.AppHost/AditiKraft.Krafter.Aspire.AppHost.csproj

# Run Backend API only
dotnet run --project src/AditiKraft.Krafter.Backend/AditiKraft.Krafter.Backend.csproj

# Run UI only (requires Backend running separately)
dotnet run --project src/UI/AditiKraft.Krafter.UI.Web/AditiKraft.Krafter.UI.Web.csproj

# Database migrations
dotnet ef migrations add <Name> --project src/AditiKraft.Krafter.Backend --context ApplicationDbContext
dotnet run --project aspire/AditiKraft.Krafter.Aspire.AppHost/AditiKraft.Krafter.Aspire.AppHost.csproj

# Build solution
dotnet build AditiKraft.Krafter.slnx
```

## 6. Commit Convention
```
type(scope): summary

feat(users): add user creation endpoint
feat(ui-users): add user management page
fix(auth): resolve token refresh issue
refactor(tenants): consolidate tenant operations
```

## 7. AI Agent Rules (CRITICAL)
1. **Restate Assumptions**: Before coding, confirm feature requirements.
2. **Search First**: Look at `Features/Users` or `Features/Roles` for patterns.
3. **Follow Existing Patterns**: Copy structure from similar features.
4. **Minimal Diffs**: Only modify what is strictly necessary.
5. **Test Build**: Always verify `dotnet build` succeeds.

## 8. AGENTS.md Evolution & Maintenance

Keep instructions accurate and feature-specific; update examples and references when code moves.
Read [instruction maintenance](docs/agent-instructions-maintenance.md) before changing guidance.
Ask user approval before creating new instruction files. Prefer updating existing guidance and linking detailed docs.

See [architecture guidelines](docs/architecture-guidelines.md) for operation cohesion, shared invariants, and validation.

---
Last Updated: 2026-09-12
Verified Against: Directory.Packages.props, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiHostServiceRegistration.cs, AGENTS.split.md, src/AditiKraft.Krafter.Backend/AGENTS.md, src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/AGENTS.md, src/AditiKraft.Krafter.Contracts/AGENTS.md, src/UI/AGENTS.md, docs/url-configuration.md, src/AditiKraft.Krafter.Contracts/Common/AppUrls.cs, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiUrlConfiguration.cs
---
