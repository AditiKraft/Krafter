# Feature organization and readability

Krafter organizes code around use cases. A reader should be able to find an operation by name,
inspect its route and contract, follow its business logic, and locate the relevant tests.
The same predictable navigation helps human developers and coding agents.

## Default operation shape

- Backend: `Features/<Feature>/<Operation>.cs` contains a nested `Handler : IScopedHandler` and `Route`.
- Contracts: `Contracts/<Feature>/<Request>.cs` keeps the request and its validator together.
  Give standalone response and entity types filenames that match their names.
- UI: pages and their feature API interface stay under `Features/<Feature>/`.
  Add a code-behind file when it improves readability; a small cohesive component can keep its logic inline.
- Tests: describe observable behavior and boundaries, using real infrastructure where it establishes the guarantee.

These are repository conventions for discoverability and registration. Vertical slices do not require a mediator,
an interface for every handler, or a repository around each EF Core query. Use direct dependencies when they are clear.
The separate Contracts project serves both the backend and WebAssembly client.

## Shared logic and exceptions

Share rules with actual consumers. Handlers may delegate to a concrete feature-local service when several operations
must enforce the same invariant. `UserMutationService`, `RolePermissionService` and `TwoFactorService` have this role.
Keep independent use-case behavior in its operation; extract shared rules when repeated behavior warrants it.

For MFA, operation files expose authorization and contracts while `TwoFactorService` owns account locking,
challenge binding, factor consumption and session revocation. Keep these transitions atomic. A transaction owns its
DbContext unit of work, reloads state under the account lock, and returns credentials only after commit.
Request cancellation reaches queries, saves, transport calls and lock waits. Check it before commit; once commit starts,
finish commit independently of caller cancellation and do not report cancellation after a successful commit.
Identity methods without cancellation parameters remain bounded by explicit checks around their work.

Route groups centralize shared HTTP policies, while each operation declares its authorization.
`SessionValidator` belongs to Auth because HTTP, refresh and realtime share its rule.
The feature-owned `TwoFactorCleanup` worker creates its own scope for each maintenance iteration.
TickerQ retains its separate Basic authentication; Krafter's session filter applies only to application-owned hubs.

## Changes and validation

Use ordinary control flow, visible state changes and explicit interface obligations. Keep dependencies and namespaces
aligned with the feature. Update links when moving types. Source-file moves do not require database migrations.

Verify changed route metadata and behavior, cancellation propagation, and transaction rollback where relevant.
Use a dedicated PostgreSQL test database for locking tests. Build source and fresh split/single template outputs after
changes that affect shared code or hosting. Test the actual boundary rather than asserting the chosen folder layout.

Keep agent instructions as a map to the applicable feature and deeper documentation.
See [instruction maintenance](agent-instructions-maintenance.md) for updating existing guidance.

## List query boundary

The Users, Roles, Tenants, and role-member lists share `GetRequestInput`. `SkipCount` must be nonnegative;
`MaxResultCount` is 1–200 and defaults to 10. The 200-row ceiling leaves room for the existing Radzen grid and
dropdown pages while bounding response size. Constructor defaults also apply when GET query parameters are omitted.
`Query` is literal, case-insensitive name search (first/last name for users; name for roles; name/identifier for tenants),
limited to 256 characters. Dropdowns send their search text in `Query`.
List identifiers, query text, ordering, and parsed filter literals reject U+0000 and unpaired UTF-16 surrogates,
which the PostgreSQL text/UTF-8 boundary cannot accept. Valid Unicode surrogate pairs, including emoji, remain supported.

`Filter` is a Radzen grid expression, limited to 2,048 characters. `TryApplyListQuery` parses it and checks the expression
tree against the operation's explicit projected-property allowlist. Supported operations include comparisons, boolean
combinations, null checks and `np`, and string `Contains`, `StartsWith`, `EndsWith`, `Equals`, `ToLower`, `ToUpper`,
`IsNullOrEmpty`, and `IsNullOrWhiteSpace`. String comparisons use literal text (optionally case-folded), as grid filters do.
Other methods, navigation through unlisted members, and constructed objects are rejected before EF execution.
Method receivers must be supported projected text/null-propagation expressions or nonnull literal text; arbitrary
computed constants are rejected so EF cannot evaluate a null method call while preparing a database query.
Expression classification is memoized and bounded, including nested conditional receivers.
`OrderBy` is at most 256 characters and accepts comma-separated allowed properties with optional `asc`/`desc` directions.
Malformed or unsupported expressions return 400; callers should send literal search text in `Query` rather than `Filter`.
Keep these rules aligned with the actual grid columns when adding filters. Do not infer expression syntax from substrings
inside search text. `ListQueryEndpointTests` exercises production GET binding and real Radzen-generated filters;
`ListQueryPostgresTests` checks SQL translation and rejects invalid requests before a database command.

## References

- [Jimmy Bogard: Vertical Slice Architecture](https://www.jimmybogard.com/vertical-slice-architecture/)
- [ASP.NET Core Minimal API route handlers and groups](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0)
- [Codex instruction discovery](https://developers.openai.com/codex/guides/agents-md/)
- [OpenAI: Harness engineering](https://openai.com/index/harness-engineering/)

These sources explain principles and mechanisms; the precise file layout above is Krafter's chosen convention.
