# Krafter AI Agent Instructions (Template Project)

> **READ THIS FIRST**: This is the entry point for AI agents working on the **Krafter template project** itself.
> This file is for **template developers** — it is NOT shipped in either template output.

## 1. Overview

Krafter is a .NET 10 full-stack **project template** that generates applications in two hosting variants:
- **Split Host** (`dotnet new krafter`): Separate Backend API + Blazor UI hosts
- **Single Host** (`dotnet new krafter-single`): Combined Backend + Blazor in one process

The template project itself contains both variants. Shared business code lives in `src/`, while hosting-specific files live in `src-single/` and `aspire-single/` (overlaid by the template engine).

**Stack**: ASP.NET Core Minimal APIs + Vertical Slice Architecture (VSA), Hybrid Blazor (WebAssembly + Server) + Radzen Components, .NET Aspire, OpenTelemetry, PostgreSQL/MySQL.

## 2. Which Instructions to Read?

```
┌──────────────────────────────────────────────────────────────────────┐
│                         DECISION TREE                                │
├──────────────────────────────────────────────────────────────────────┤
│ What are you working on?                                             │
│                                                                      │
│ ├── API endpoint, Handler, Entity, Database?                        │
│ │   └── READ: src/AditiKraft.Krafter.Backend/Agents.md              │
│ │                                                                    │
│ ├── Blazor page, Component, UI Service?                             │
│ │   └── READ: src/UI/Agents.md                                      │
│ │                                                                    │
│ ├── Shared DTOs, Requests, Responses (Contracts)?                   │
│ │   └── READ: src/AditiKraft.Krafter.Contracts/Agents.md            │
│ │                                                                    │
│ ├── Aspire orchestration, Docker, CI/CD?                            │
│ │   └── Use patterns from aspire/ and .github/workflows/            │
│ │                                                                    │
│ ├── Cross-cutting (affects both Backend + UI)?                      │
│ │   └── READ BOTH Backend + UI sub-files                            │
│ │                                                                    │
│ ├── Template configuration, overlay mechanism?                      │
│ │   └── See .template.config/ (split) and .template.config-single/  │
│ │       Single-host overlays: src-single/ and aspire-single/        │
│ │       READ: Section 4 (Template Mechanism) below                  │
│ │                                                                    │
│ └── Agents.md variant files (root-level AI instructions)?           │
│     └── Agents.md        — Template developers (this file)          │
│         Agents.split.md  — Shipped as Agents.md in krafter output   │
│         Agents.single.md — Shipped as Agents.md in krafter-single   │
│         READ: Section 5 (Agents.md Variant Strategy) below          │
└──────────────────────────────────────────────────────────────────────┘
```

## 2.1 New Feature Flow (Short Version)
1. If a feature-level `Agents.md` exists, read it first.
2. Add contracts + validators in `src/AditiKraft.Krafter.Contracts/Contracts/<Feature>/`.
3. Add permissions/routes in `src/AditiKraft.Krafter.Contracts/Common/`.
4. Add Backend operations in `src/AditiKraft.Krafter.Backend/Features/<Feature>/`.
5. Add UI Refit + pages in `src/UI/AditiKraft.Krafter.UI.Web.Client/`.

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
3. **WASM wwwroot overlay** (`src-single/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/` → `src/UI/AditiKraft.Krafter.UI.Web.Client/wwwroot/`) — replaces `appsettings.json` and `appsettings.Development.json` (removes the separate backend base URL since API is same-origin).
4. **Aspire overlay** (`aspire-single/AditiKraft.Krafter.Aspire.AppHost/` → `aspire/AditiKraft.Krafter.Aspire.AppHost/`) — replaces `Program.cs` and `.csproj` to register a single combined app resource instead of two separate ones.

The split-host template (`krafter`) simply excludes `src-single/`, `aspire-single/`, and `AditiKraft.Krafter.Single.slnx` — no overlays needed.

### 4.3 Overlay Rule

Files in `src-single/` and `aspire-single/` **replace** the corresponding files from `src/` and `aspire/` in the single-host template output. The root source excludes the original files, and the overlay sources copy replacements into the same target paths.

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
# Split-host (run via Aspire)
dotnet run --project aspire/AditiKraft.Krafter.Aspire.AppHost/AditiKraft.Krafter.Aspire.AppHost.csproj

# Single-host (run via Aspire)
dotnet run --project aspire-single/AditiKraft.Krafter.Aspire.AppHost/AditiKraft.Krafter.Aspire.AppHost.csproj

# Build split-host
dotnet build AditiKraft.Krafter.slnx

# Build single-host
dotnet build AditiKraft.Krafter.Single.slnx

# Database migrations
dotnet ef migrations add <Name> --project src/AditiKraft.Krafter.Backend --context KrafterContext

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

> **CRITICAL**: Agents.md files are LIVING DOCUMENTS that MUST evolve with the codebase.

### 10.1 When to UPDATE Existing Agents.md

| Trigger | Action |
|---------|--------|
| New pattern discovered in code | Add to relevant Agents.md with code example |
| Existing pattern changes | Update the documentation immediately |
| AI agent makes repeated mistakes | Add to "Common Mistakes" section |
| New library/tool integrated | Document usage patterns |
| Code review reveals undocumented convention | Add to appropriate section |

### 10.2 When to CREATE New Agents.md Files

| Trigger | Action |
|---------|--------|
| A feature grows to 5+ operations with unique patterns | Create `Features/<Feature>/Agents.md` |
| A new infrastructure area is added (e.g., messaging, caching) | Create `Infrastructure/<Area>/Agents.md` |
| CI/CD or deployment rules become complex | Create `.github/Agents.md` if that area needs AI-specific rules |
| Aspire orchestration has custom rules | Create `aspire/Agents.md` if orchestration patterns become project-specific |
| A sub-area has 3+ unique patterns not in parent | Create sub-directory Agents.md |

### 10.3 When to SPLIT/BREAKDOWN Agents.md

> **Split when a single Agents.md exceeds ~500 lines or covers too many concerns**

```
BEFORE (monolithic):
src/AditiKraft.Krafter.Backend/Agents.md (800+ lines covering everything)

AFTER (split by concern):
src/AditiKraft.Krafter.Backend/Agents.md (core patterns, ~200 lines)
├── Features/Auth/Agents.md (auth-specific patterns)
├── Features/Tenants/Agents.md (multi-tenant patterns)
├── Infrastructure/Persistence/Agents.md (EF Core patterns)
└── Infrastructure/Jobs/Agents.md (TickerQ patterns)
```

### 10.4 Hierarchy & Inheritance

```
Agents.md (ROOT - global rules)
    ↓ inherits
src/AditiKraft.Krafter.Backend/Agents.md (backend-specific)
    ↓ inherits
src/AditiKraft.Krafter.Backend/Features/Auth/Agents.md (auth-specific)
```

**Rules:**
- Child Agents.md inherits all rules from parent
- Child can OVERRIDE parent rules (document why)
- Child should only contain rules SPECIFIC to that area
- Always reference parent with the correct relative path (for example `> See also: ../../Agents.md`)

### 10.5 Template for New Agents.md

```markdown
# <Area> AI Instructions

> **SCOPE**: <What this file covers>
> **PARENT**: See also: <path to parent Agents.md>

## 1. Core Principles
- <Key rule 1>
- <Key rule 2>

## 2. Decision Tree
<Where does code go?>

## 3. Code Templates
<Copy-paste examples from ACTUAL code>

## 4. Checklist
<Step-by-step for new work>

## 5. Common Mistakes
<What AI agents get wrong>

## 6. Evolution Triggers
<When to update THIS file>
```

### 10.6 AI Agent Responsibilities

**When working on code:**
1. **Check** if current patterns match Agents.md
2. **Flag** any discrepancies found
3. **Suggest** updates when patterns evolve
4. **Propose** new Agents.md when complexity warrants

**When updating Agents.md:**
1. **Verify** against actual code (not assumptions)
2. **Include** real code snippets from codebase
3. **Reference** actual file paths
4. **Ask** user approval before creating new files

### 10.7 Version Tracking

Add to each Agents.md:
```markdown
---
Last Updated: YYYY-MM-DD
Verified Against: [list key files checked]
---
```

---
Last Updated: 2026-03-28
Verified Against: Agents.md, Agents.split.md, Agents.single.md, .template.config/template.json, .template.config-single/template.json, src/AditiKraft.Krafter.Backend/Agents.md, src/AditiKraft.Krafter.Backend/Infrastructure/Persistence/Agents.md, src/AditiKraft.Krafter.Backend/Infrastructure/Jobs/Agents.md, src/AditiKraft.Krafter.Backend/Features/Auth/Agents.md, src/AditiKraft.Krafter.Backend/Features/Users/Agents.md, src/AditiKraft.Krafter.Backend/Features/Roles/Agents.md, src/AditiKraft.Krafter.Backend/Features/Tenants/Agents.md, src/AditiKraft.Krafter.Contracts/Agents.md, src/UI/Agents.md, src/UI/AditiKraft.Krafter.UI.Web.Client/Infrastructure/Refit/Agents.md, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Auth/Agents.md, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Users/Agents.md, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Roles/Agents.md, src/UI/AditiKraft.Krafter.UI.Web.Client/Features/Tenants/Agents.md
---

