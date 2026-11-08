# Shared Library AI Instructions

> **SCOPE**: API contracts (DTOs, Requests, Responses), shared constants, and common utilities used by Backend and UI.
> **PARENT**: See also: ../../AGENTS.md

## 1. Core Principles
- Contracts only; no business logic.
- Validators live in the same file as their request.
- Persisted text and text query parameters use `Common/Validation/TextValue.IsValid` to reject U+0000 and unpaired UTF-16 surrogates before provider work. Keep required/optional, length, email, tenant identifier and configured Identity username rules separate. Do not apply this storage rule to passwords or token proofs.
- No references to Backend or UI projects.
- Use `default!` for non-nullable properties set by deserialization.
- Use `CommonDtoProperty` for DTOs with audit fields.
- Keep XML doc style consistent with the file (only add if the file already uses XML docs).
- Use `Common/Auth/PasswordLimits.MaximumLength` for login, MFA password proof, and new passwords on change/reset. Keep the current-password field uncapped so an authenticated user can replace an older, longer password; never truncate a credential.
- `GetRequestInput` uses `Query` for literal text search and `Filter` for grid expressions. Keep the shared bounds (nonnegative skip, page size 1–200, bounded query text) and constructor defaults, which make omitted GET query parameters optional under `[AsParameters]`. Backend list handlers validate supported filter/sort operations before query execution.
- `UserInfo.Email` is empty when absent. Role-member and audit projections explicitly populate the account email; never introduce sample identity values as DTO defaults.
- An omitted or null `CreateOrUpdateRoleRequest.Permissions` means metadata-only update; an explicit empty list clears permissions. Keep omission distinct from empty selection.

## 2. Decision Tree
- Request DTO: `Contracts/<Feature>/<Name>Request.cs` (validator in same file)
- Response DTO: `Contracts/<Feature>/<Name>Dto.cs` or `<Name>Response.cs`
- Constants: `Contracts/<Feature>/<Name>Constant.cs`
- Permissions: `Common/Auth/Permissions/`
- Routes: `Common/ApiRoutes.cs`
- Common models: `Common/Models/`
- Shared validation predicates with multiple request consumers: `Common/Validation/`
- Enums: `Common/Enums/`
- Auth helpers: `Common/Auth/`
- Shared public URL model and tenant-domain helpers: `Common/AppUrls.cs`. Server configuration binding stays in the host. Keep `ServerApiBaseUrl` excluded from JSON; browsers receive public addresses and public reserved identifiers only. Share tenant identifier rules between request validation and URL resolution. Read [Configure application URLs](../../docs/url-configuration.md) before changing this contract.

## 3. Code Templates

### Request + Validator
```csharp
using FluentValidation;

namespace AditiKraft.Krafter.Contracts.Contracts.Users;

public class CreateUserRequest
{
    public string? Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(p => p.FirstName).NotEmpty();
        RuleFor(p => p.Email).NotEmpty().EmailAddress();
    }
}
```

### DTO
```csharp
using AditiKraft.Krafter.Contracts.Common.Models;

namespace AditiKraft.Krafter.Contracts.Contracts.Users;

public class UserDto : CommonDtoProperty
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
}
```

### Response Factory Methods
```csharp
return Response.NotFound("User not found");
return Response.Success("Done");
return Response<UserDto>.Success(dto);
```

## 4. Checklist
1. Add the contract file under `Contracts/<Feature>/` with `AditiKraft.Krafter.Contracts.Contracts.<Feature>` namespace.
2. Add a validator for request DTOs in the same file.
3. Use `default!` for non-nullable properties.
4. Build: `dotnet build src/AditiKraft.Krafter.Contracts/AditiKraft.Krafter.Contracts.csproj`.

## 5. Common Mistakes
- Putting business logic in Shared.
- Placing validators in separate files or projects.
- Adding Backend/UI references.
- Duplicating route or permission constants in UI.

## 6. Evolution Triggers
- New shared pattern discovered in code.
- Shared contracts or constants change structure.
- New permissions or routes added.

---
Last Updated: 2026-11-08
Verified Against: src/AditiKraft.Krafter.Contracts/Common/Validation/TextValue.cs, src/AditiKraft.Krafter.Contracts/Common/Models/GetRequestInput.cs, src/AditiKraft.Krafter.Contracts/Common/Models/UserInfo.cs, tests/AditiKraft.Krafter.Tests/Validation/PersistedTextEndpointTests.cs, tests/AditiKraft.Krafter.Tests/Queries/ListQueryEndpointTests.cs, src/AditiKraft.Krafter.Contracts/Contracts/Users/CreateUserRequest.cs, src/AditiKraft.Krafter.Contracts/Common/Models/Response.cs, src/AditiKraft.Krafter.Contracts/Common/Auth/Permissions/PermissionCatalog.cs, src/AditiKraft.Krafter.Contracts/Common/Auth/Permissions/PermissionDefinition.cs, src/AditiKraft.Krafter.Contracts/Common/ApiRoutes.cs, docs/url-configuration.md, src/AditiKraft.Krafter.Contracts/Common/AppUrls.cs, src/UI/AditiKraft.Krafter.UI.Web/Infrastructure/Hosting/UiUrlConfiguration.cs
---

