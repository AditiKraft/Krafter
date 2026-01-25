# Krafter AI Agent Instructions

> **READ THIS FIRST**: This is the entry point for AI agents working on the Krafter project.

## 1. Overview
Krafter is a .NET 10 full-stack platform with:
- **Backend**: ASP.NET Core Minimal APIs + Vertical Slice Architecture (VSA)
- **UI**: Hybrid Blazor (WebAssembly + Server) + Radzen Components
- **Infrastructure**: .NET Aspire, OpenTelemetry, PostgreSQL/MySQL

## 2. Which Instructions to Read?

```
┌─────────────────────────────────────────────────────────────┐
│                    DECISION TREE                            │
├─────────────────────────────────────────────────────────────┤
│ What are you working on?                                    │
│                                                             │
│ ├── API endpoint, Handler, Entity, Database?               │
│ │   └── READ: src/Backend/Agents.md                        │
│ │                                                           │
│ ├── Blazor page, Component, UI Service?                    │
│ │   └── READ: src/UI/Agents.md                             │
│ │                                                           │
│ ├── Shared DTOs, Requests, Responses (Contracts)?          │
│ │   └── READ: src/Krafter.Shared/Agents.md                 │
│ │                                                           │
│ ├── Aspire orchestration, Docker, CI/CD?                   │
│ │   └── Use patterns from aspire/ and .github/workflows/   │
│ │                                                           │
│ └── Cross-cutting (affects both Backend + UI)?             │
│     └── READ BOTH Backend + UI sub-files                   │
└─────────────────────────────────────────────────────────────┘
```

## 3. Solution Structure
```
Krafter/
├── Agents.md                    ← YOU ARE HERE
├── aspire/                      # Aspire orchestration
│   ├── Krafter.Aspire.AppHost/
│   └── Krafter.Aspire.ServiceDefaults/
├── src/
│   ├── Krafter.Shared/          # Shared contracts library
│   │   ├── Agents.md            ← Shared-specific rules
│   │   ├── Contracts/           # API DTOs, Requests, Responses
│   │   │   ├── Auth/
│   │   │   ├── Users/
│   │   │   ├── Roles/
│   │   │   └── Tenants/
│   │   └── Common/              # Shared utilities, models
│   ├── Backend/                 # API (See src/Backend/Agents.md)
│   │   ├── Agents.md            ← Backend-specific rules
│   │   ├── Features/            # Vertical slices (business logic)
│   │   ├── Infrastructure/      # Persistence, Jobs
│   │   └── Common/              # Backend-specific utilities
│   └── UI/                      # Blazor (See src/UI/Agents.md)
│       ├── Agents.md            ← UI-specific rules
│       ├── Krafter.UI.Web.Client/  # WASM client
│       └── Krafter.UI.Web/         # Server host
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
# Run entire solution (recommended)
dotnet run --project aspire/Krafter.Aspire.AppHost/Krafter.Aspire.AppHost.csproj

# Run Backend only
dotnet run --project src/Backend/Backend.csproj

# Run UI only
dotnet run --project src/UI/Krafter.UI.Web/Krafter.UI.Web.csproj

# Database migrations
dotnet ef migrations add <Name> --project src/Backend --context KrafterContext
dotnet ef database update --project src/Backend --context KrafterContext

# Build solution
dotnet build Krafter.slnx
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

## 8. Agents.md Evolution & Maintenance

> **CRITICAL**: Agents.md files are LIVING DOCUMENTS that MUST evolve with the codebase.

### 8.1 When to UPDATE Existing Agents.md

| Trigger | Action |
|---------|--------|
| New pattern discovered in code | Add to relevant Agents.md with code example |
| Existing pattern changes | Update the documentation immediately |
| AI agent makes repeated mistakes | Add to "Common Mistakes" section |
| New library/tool integrated | Document usage patterns |
| Code review reveals undocumented convention | Add to appropriate section |

### 8.2 When to CREATE New Agents.md Files

| Trigger | Action |
|---------|--------|
| A feature grows to 5+ operations with unique patterns | Create `Features/<Feature>/Agents.md` |
| A new infrastructure area is added (e.g., messaging, caching) | Create `Infrastructure/<Area>/Agents.md` |
| CI/CD or deployment rules become complex | Create `.github/Agents.md` |
| Aspire orchestration has custom rules | Create `aspire/Agents.md` |
| A sub-area has 3+ unique patterns not in parent | Create sub-directory Agents.md |

### 8.3 When to SPLIT/BREAKDOWN Agents.md

> **Split when a single Agents.md exceeds ~500 lines or covers too many concerns**

```
BEFORE (monolithic):
src/Backend/Agents.md (800+ lines covering everything)

AFTER (split by concern):
src/Backend/Agents.md (core patterns, ~200 lines)
├── Features/Auth/Agents.md (auth-specific patterns)
├── Features/Tenants/Agents.md (multi-tenant patterns)
├── Infrastructure/Persistence/Agents.md (EF Core patterns)
└── Infrastructure/BackgroundJobs/Agents.md (TickerQ patterns)
```

### 8.4 Hierarchy & Inheritance

```
Agents.md (ROOT - global rules)
    ↓ inherits
src/Backend/Agents.md (backend-specific)
    ↓ inherits
src/Backend/Features/Auth/Agents.md (auth-specific)
```

**Rules:**
- Child Agents.md inherits all rules from parent
- Child can OVERRIDE parent rules (document why)
- Child should only contain rules SPECIFIC to that area
- Always reference parent: `> See also: ../Agents.md`

### 8.5 Template for New Agents.md

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

### 8.6 AI Agent Responsibilities

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

### 8.7 Version Tracking

Add to each Agents.md:
```markdown
---
Last Updated: YYYY-MM-DD
Verified Against: [list key files checked]
---
```

---
Last Updated: 2026-01-25
Verified Against: Agents.md, src/Backend/Agents.md, src/Krafter.Shared/Agents.md, src/UI/Agents.md
---
