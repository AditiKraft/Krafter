# Krafter AI Agent Instructions (Template Project)

> **READ THIS FIRST**: This is the entry point for AI agents working on the **Krafter template project** itself.
> This file is for **template developers** — it is NOT shipped in either template output.

## 1. Overview

Krafter is a .NET 10 full-stack **project template** that generates applications in two hosting variants:
- **Split Host** (`dotnet new krafter`): Separate Backend API + Blazor UI hosts
- **Single Host** (`dotnet new krafter-single`): Combined Backend + Blazor in one process

The template project itself contains both variants. Shared business code lives in `src/`, while hosting-specific files live in `src-single/` and `aspire-single/` (overlaid by the template engine).

**Stack**: ASP.NET Core Minimal APIs + Vertical Slice Architecture (VSA), Hybrid Blazor (WebAssembly + Server) + Radzen Components, .NET Aspire, OpenTelemetry, PostgreSQL.

## 2. Which Instructions to Read?

- API endpoints, handlers, entities, or database: read [Backend instructions](src/AditiKraft.Krafter.Backend/Agents.md).
- Blazor pages, components, or UI services: read [UI instructions](src/UI/Agents.md).
- Shared DTOs, requests, responses, or validators: read [Contracts instructions](src/AditiKraft.Krafter.Contracts/Agents.md).
- Cross-cutting changes: read both Backend and UI guidance, plus the applicable feature instructions.
- Aspire, Docker, or CI/CD: follow existing `aspire/` and `.github/workflows/` patterns.
- Template overlays: see section 4 and both `.template.config` directories.
- Root instruction variants: see section 5 before changing `Agents.split.md` or `Agents.single.md`.


For URL settings, hosting-mode routing, tenant domains, CORS, or Google callbacks, read [Configure application URLs](docs/url-configuration.md) before changing code or configuration.

## 2.1 New Feature Flow (Short Version)
1. If a feature-level `Agents.md` exists, read it first.
2. Add contracts + validators in `src/AditiKraft.Krafter.Contracts/Contracts/<Feature>/`.
3. Add permissions/routes in `src/AditiKraft.Krafter.Contracts/Common/`.
4. Add Backend operations in `src/AditiKraft.Krafter.Backend/Features/<Feature>/`.
5. Add UI pages and `I<Feature>Api.cs` together in `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/<Feature>/`.

## File Placement

- Package versions: root `Directory.Packages.props`. Add `PackageReference` entries without a `Version` in project files.
- `Directory.Build.props` audits direct and transitive dependencies and treats NU1901–NU1904 as errors. Fix affected parent packages or documented security floors; do not suppress advisories with `NoWarn` or `WarningsNotAsErrors`. See `docs/dependency-security.md` and verify source plus both generated hosting variants.
- Shared server UI registration: `src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiHostServiceRegistration.cs`. Keep host-specific authentication and endpoint selection in `Program.cs`; reusable BFF mappers live in server `Infrastructure/Auth/`.

- Backend operations: `src/AditiKraft.Krafter.Backend/Features/<Feature>/<Operation>.cs`.
- UI pages, dialogs, and API interface: `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/<Feature>/`.
- Shared requests, responses, and validators: `src/AditiKraft.Krafter.Contracts/Contracts/<Feature>/`.
- Follow `src/AditiKraft.Krafter.Backend/Agents.md` and `src/UI/Agents.md` for infrastructure placement.
- Match file names to their main types and namespaces to folders. Keep each operation's Handler + Route together and each request's validator with its request.
- After moving files, update imports, file links, and instruction references. Template developers must also verify the `src-single` file links and both generated hosting variants.

## 2.2 Deep Dives
- Backend persistence: `src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/Agents.md`
- Backend background jobs: `src/AditiKraft.Krafter.Backend/Infrastructure/Jobs/Agents.md`
- Backend auth: `src/AditiKraft.Krafter.Backend/Features/Auth/Agents.md`
- Backend Users feature: `src/AditiKraft.Krafter.Backend/Features/Users/Agents.md`
- Backend roles: `src/AditiKraft.Krafter.Backend/Features/Roles/Agents.md`
- Backend tenants: `src/AditiKraft.Krafter.Backend/Features/Tenants/Agents.md`
- UI Refit: `src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/Agents.md`
- UI auth: `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Agents.md`
- UI users: `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/Agents.md`
- UI roles: `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/Agents.md`
- UI tenants: `src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/Agents.md`

## 2.3 Bugfix Fast Path
1. Locate the closest matching feature `Agents.md` and read it.
2. Find the exact operation file (one file per operation) and trace request/response types in Shared.
3. Apply minimal diff; verify response shape and route/permission usage.

## 3. Solution Structure

```
AditiKraft.Krafter/
├── Agents.md                    ← YOU ARE HERE (template-project version)
├── Agents.split.md              ← Split-host variant (→ Agents.md in krafter output)
├── Agents.single.md             ← Single-host variant (→ Agents.md in krafter-single output)
├── .template.config/            # Split-host template config
│   └── template.json
├── .template.config-single/     # Single-host template config
│   └── template.json
├── aspire/                      # Split-host Aspire (2 app resources)
│   ├── AditiKraft.Krafter.Aspire.AppHost/
│   └── AditiKraft.Krafter.Aspire.ServiceDefaults/
├── aspire-single/               # Single-host Aspire overlay (1 combined resource)
│   └── AditiKraft.Krafter.Aspire.AppHost/
├── src/                         # Shared business code (used by BOTH templates)
│   ├── AditiKraft.Krafter.Contracts/
│   │   ├── Agents.md
│   │   ├── Contracts/
│   │   └── Common/
│   ├── AditiKraft.Krafter.Backend/
│   │   ├── Agents.md
│   │   ├── Web/                 # Includes HostingExtensions.cs (reusable by single-host)
│   │   ├── Features/
│   │   ├── Infrastructure/
│   │   ├── Common/
│   │   ├── Errors/
│   │   └── Migrations/
│   ├── AditiKraft.Krafter.Backend.Migrator/
│   └── UI/
│       ├── Agents.md
│       ├── AditiKraft.Krafter.UI.Web.Client/
│       └── AditiKraft.Krafter.UI.Web/   # Split-host version
├── src-single/                  # Single-host overlay files
│   └── UI/
│       ├── AditiKraft.Krafter.UI.Web/   # Overlays src/UI/AditiKraft.Krafter.UI.Web/
│       │   ├── Program.cs               # Combined host composition root
│       │   ├── *.csproj                 # Refs Backend project
│       │   └── appsettings*.json
│       └── AditiKraft.Krafter.UI.Web.Client/
│           └── wwwroot/appsettings*.json
├── AditiKraft.Krafter.slnx      # Split-host solution
├── AditiKraft.Krafter.Single.slnx # Single-host solution
├── AditiKraft.Krafter.Templates.csproj
└── pack-and-install.cmd
```

The template packaging project keeps its own `Microsoft.TemplateEngine.Tasks` reference. It opts out of central package versions; do not move its floating tool version into the application package list.

## 4. Template Mechanism

### 4.1 Two Template Configs

| Config | Template | Short Name |
|--------|----------|------------|
| `.template.config/template.json` | Split-host (separate Backend + UI) | `krafter` |
| `.template.config-single/template.json` | Single-host (combined process) | `krafter-single` |

Both configs set `sourceName: "AditiKraft.Krafter"` — the .NET template engine replaces this token with the user's chosen project name in all file names, folder names, namespaces, and file content.

### 4.2 Single-Host Overlay (4-Source Strategy)

The single-host template (`krafter-single`) uses four `sources` entries in its `template.json`:

1. **Root (`./` → `./`)** — copies everything except files that will be overlaid, plus excludes `src-single/`, `aspire-single/`, and split-only files (`AditiKraft.Krafter.slnx`). Renames `AditiKraft.Krafter.Single.slnx` → `AditiKraft.Krafter.slnx`.
2. **UI.Web overlay** (`src-single/UI/AditiKraft.Krafter.UI.Web/` → `src/UI/AditiKraft.Krafter.UI.Web/`) — replaces `Program.cs`, `.csproj`, and `appsettings*.json` with the combined-host versions.
3. **WASM wwwroot overlay** (`src-single/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/` → `src/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/`) — replaces `appsettings.json` and `appsettings.Development.json` (contains public non-URL settings; browser URLs come from the UI host).
4. **Aspire overlay** (`aspire-single/AditiKraft.Krafter.Aspire.AppHost/` → `aspire/AditiKraft.Krafter.Aspire.AppHost/`) — replaces `Program.cs` and `.csproj` to register a single combined app resource instead of two separate ones.

The split-host template (`krafter`) simply excludes `src-single/`, `aspire-single/`, and `AditiKraft.Krafter.Single.slnx` — no overlays needed.

The source single-host AppHost selects the `src-single` UI project when it exists; generated projects select `src/UI`. Source-only launch profiles and AppHost settings are physical files because Aspire reads them from each project directory. Keep these copies aligned with the shared files in `src/UI` and `aspire`. The overlay sources exclude these support files, so generated projects retain the shared root copies.

`AditiKraft.Krafter.Single.slnx` describes generated paths. In the source repository, build `AditiKraft.Krafter.Dev.slnx` or the exact `aspire-single` AppHost project to verify the combined host.

### 4.3 Overlay Rule

Files in `src-single/` and `aspire-single/` **replace** the corresponding files from `src/` and `aspire/` in the single-host template output. The root source excludes the original files, and the overlay sources copy replacements into the same target paths.

In generated single-host projects, `UI.Web` owns the server configuration. Exclude Backend `appsettings*.json` files along with its `Program.cs`, because Backend runs as a library.

## 5. Agents.md Variant Strategy

Three root-level Agents.md files ensure correct AI instructions per context:

| File | Audience | Shipped in template output? |
|------|----------|----------------------------|
| `Agents.md` | Template developers working on this repo | **No** — excluded by both template configs |
| `Agents.split.md` | Users of generated split-host projects | **Yes** — renamed to `Agents.md` via template rename in `krafter` output |
| `Agents.single.md` | Users of generated single-host projects | **Yes** — renamed to `Agents.md` via template rename in `krafter-single` output |

All **14 sub-level Agents.md files** (inside `src/`) are mode-agnostic and ship identically in both template outputs. Only the root-level file differs per hosting variant.

## 6. Global Coding Conventions
| Rule | Requirement |
|------|-------------|
| **Nullable** | Enabled. Use `default!` for non-nullable properties. |
| **Async** | Use `Async` suffix. No `async void` except event handlers. |
| **DI** | Primary constructors preferred. |
| **Secrets** | NEVER commit. Use `dotnet user-secrets` locally. |
| **Namespaces** | File-scoped. Match folder structure. |

## 7. Development Commands
```bash
# Split-host (run via Aspire — starts Backend + UI + infrastructure)
dotnet run --project aspire/AditiKraft.Krafter.Aspire.AppHost/AditiKraft.Krafter.Aspire.AppHost.csproj

# Single-host (run via Aspire — starts combined app + infrastructure)
# NOTE: In the template repo, aspire-single/ uses a conditional reference to src-single/UI.Web.
# In generated output from `dotnet new krafter-single`, aspire/ points to the overlaid src/UI.Web.
dotnet run --project aspire-single/AditiKraft.Krafter.Aspire.AppHost/AditiKraft.Krafter.Aspire.AppHost.csproj

# Build split-host
dotnet build AditiKraft.Krafter.slnx

# Build the source single-host AppHost and its combined UI host
dotnet build aspire-single/AditiKraft.Krafter.Aspire.AppHost/AditiKraft.Krafter.Aspire.AppHost.csproj

# Run authentication and permission regression tests
dotnet test tests/AditiKraft.Krafter.Tests/AditiKraft.Krafter.Tests.csproj

# Database migrations
dotnet ef migrations add <Name> --project src/AditiKraft.Krafter.Backend --context ApplicationDbContext

# Pack and install templates
pack-and-install.cmd
```

## 8. Commit Convention
```
type(scope): summary

feat(users): add user creation endpoint
feat(ui-users): add user management page
fix(auth): resolve token refresh issue
refactor(tenants): consolidate tenant operations
```

## 9. AI Agent Rules (CRITICAL)
1. **Restate Assumptions**: Before coding, confirm feature requirements.
2. **Search First**: Look at `Features/Users` or `Features/Roles` for patterns.
3. **Follow Existing Patterns**: Copy structure from similar features.
4. **Minimal Diffs**: Only modify what is strictly necessary.
5. **Test Build**: Always verify `dotnet build` succeeds.

## 10. Agents.md Evolution & Maintenance

Keep instructions accurate and feature-specific; update examples and references when code moves.
Read [instruction maintenance](docs/agent-instructions-maintenance.md) before changing guidance.
Ask user approval before creating new instruction files. Prefer updating existing guidance and linking detailed docs.

See [architecture guidelines](docs/architecture-guidelines.md) for operation cohesion, shared invariants, and validation.

---
Last Updated: 2026-11-08
Verified Against: Directory.Packages.props, AditiKraft.Krafter.Dev.slnx, .template.config/template.json, .template.config-single/template.json, docs/architecture-guidelines.md, docs/agent-instructions-maintenance.md
---

