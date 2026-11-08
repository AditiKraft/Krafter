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

## References

- [Jimmy Bogard: Vertical Slice Architecture](https://www.jimmybogard.com/vertical-slice-architecture/)
- [ASP.NET Core Minimal API route handlers and groups](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/route-handlers?view=aspnetcore-10.0)
- [Codex instruction discovery](https://developers.openai.com/codex/guides/agents-md/)
- [OpenAI: Harness engineering](https://openai.com/index/harness-engineering/)

These sources explain principles and mechanisms; the precise file layout above is Krafter's chosen convention.
