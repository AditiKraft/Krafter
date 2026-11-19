# Maintaining agent instructions

> **CRITICAL**: AGENTS.md files are LIVING DOCUMENTS that MUST evolve with the codebase.

Use the exact filename `AGENTS.md` for automatic Codex discovery on case-sensitive filesystems.
The root `AGENTS.split.md` and `AGENTS.single.md` files are template inputs: rename the applicable variant to
`AGENTS.md` in generated output, excluding the template-developer root instructions and the other variant.
Keep nested instruction files and their links consistent in source and both generated applications.

## When to UPDATE Existing AGENTS.md

| Trigger | Action |
|---------|--------|
| New pattern discovered in code | Add to relevant AGENTS.md with code example |
| Existing pattern changes | Update the documentation immediately |
| AI agent makes repeated mistakes | Add to "Common Mistakes" section |
| New library/tool integrated | Document usage patterns |
| Code review reveals undocumented convention | Add to appropriate section |

## When to CREATE New AGENTS.md Files

| Trigger | Action |
|---------|--------|
| A feature grows to 5+ operations with unique patterns | Create `Features/<Feature>/AGENTS.md` |
| A new infrastructure area is added (e.g., messaging, caching) | Create `Infrastructure/<Area>/AGENTS.md` |
| CI/CD or deployment rules become complex | Create `.github/AGENTS.md` if that area needs AI-specific rules |
| Aspire orchestration has custom rules | Create `aspire/AGENTS.md` if orchestration patterns become project-specific |
| A sub-area has 3+ unique patterns not in parent | Create sub-directory AGENTS.md |

## When to SPLIT/BREAKDOWN AGENTS.md

> **Split when a single AGENTS.md exceeds ~500 lines or covers too many concerns**

```
BEFORE (monolithic):
src/AditiKraft.Krafter.Backend/AGENTS.md (800+ lines covering everything)

AFTER (split by concern):
src/AditiKraft.Krafter.Backend/AGENTS.md (core patterns, ~200 lines)
├── Features/Auth/AGENTS.md (auth-specific patterns)
├── Features/Tenants/AGENTS.md (multi-tenant patterns)
├── Infrastructure/Persistence/AGENTS.md (EF Core patterns)
└── Infrastructure/Jobs/AGENTS.md (TickerQ patterns)
```

## Hierarchy & Inheritance

```
AGENTS.md (ROOT - global rules)
    ↓ inherits
src/AditiKraft.Krafter.Backend/AGENTS.md (backend-specific)
    ↓ inherits
src/AditiKraft.Krafter.Backend/Features/Auth/AGENTS.md (auth-specific)
```

**Rules:**
- Child AGENTS.md inherits all rules from parent
- Child can OVERRIDE parent rules (document why)
- Child should only contain rules SPECIFIC to that area
- Always reference parent with the correct relative path (for example `> See also: ../../AGENTS.md`)

## Template for New AGENTS.md

```markdown
# <Area> AI Instructions

> **SCOPE**: <What this file covers>
> **PARENT**: See also: <path to parent AGENTS.md>

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

## AI Agent Responsibilities

**When working on code:**
1. **Check** if current patterns match AGENTS.md
2. **Flag** any discrepancies found
3. **Suggest** updates when patterns evolve
4. **Propose** new AGENTS.md when complexity warrants

**When updating AGENTS.md:**
1. **Verify** against actual code (not assumptions)
2. **Include** real code snippets from codebase
3. **Reference** actual file paths
4. **Ask** user approval before creating new files

## Version Tracking

Add to each AGENTS.md:
```markdown
---
Last Updated: YYYY-MM-DD
Verified Against: Directory.Packages.props, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiHostServiceRegistration.cs, [list key files checked]
---
```
