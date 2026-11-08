# Maintaining agent instructions

> **CRITICAL**: Agents.md files are LIVING DOCUMENTS that MUST evolve with the codebase.

## When to UPDATE Existing Agents.md

| Trigger | Action |
|---------|--------|
| New pattern discovered in code | Add to relevant Agents.md with code example |
| Existing pattern changes | Update the documentation immediately |
| AI agent makes repeated mistakes | Add to "Common Mistakes" section |
| New library/tool integrated | Document usage patterns |
| Code review reveals undocumented convention | Add to appropriate section |

## When to CREATE New Agents.md Files

| Trigger | Action |
|---------|--------|
| A feature grows to 5+ operations with unique patterns | Create `Features/<Feature>/Agents.md` |
| A new infrastructure area is added (e.g., messaging, caching) | Create `Infrastructure/<Area>/Agents.md` |
| CI/CD or deployment rules become complex | Create `.github/Agents.md` if that area needs AI-specific rules |
| Aspire orchestration has custom rules | Create `aspire/Agents.md` if orchestration patterns become project-specific |
| A sub-area has 3+ unique patterns not in parent | Create sub-directory Agents.md |

## When to SPLIT/BREAKDOWN Agents.md

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

## Hierarchy & Inheritance

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

## Template for New Agents.md

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

## AI Agent Responsibilities

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

## Version Tracking

Add to each Agents.md:
```markdown
---
Last Updated: YYYY-MM-DD
Verified Against: Directory.Packages.props, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiHostServiceRegistration.cs, [list key files checked]
---
```
