# Shared Library AI Instructions

> **SCOPE**: API contracts (DTOs, Requests, Responses), shared constants, and common utilities used by Backend and UI.
> **PARENT**: See also: ../../Agents.md

## 1. Core Principles
- Contracts only; no business logic.
- Validators live in the same file as their request.
- No references to Backend or UI projects.
- Use `default!` for non-nullable properties set by deserialization.
- Use `CommonDtoProperty` for DTOs with audit fields.
- Keep XML doc style consistent with the file (only add if the file already uses XML docs).

## 2. Decision Tree
- Request DTO: `Contracts/<Feature>/<Name>Request.cs` (validator in same file)
- Response DTO: `Contracts/<Feature>/<Name>Dto.cs` or `<Name>Response.cs`
- Constants: `Contracts/<Feature>/<Name>Constant.cs`
- Permissions: `Common/Auth/Permissions/`
- Routes: `Common/KrafterRoute.cs`
- Common models: `Common/Models/`
- Enums: `Common/Enums/`
- Auth helpers: `Common/Auth/`

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
Last Updated: 2026-01-26
Verified Against: Contracts/Users/CreateUserRequest.cs, Common/Models/Response.cs, Common/Auth/Permissions/KrafterPermissions.cs, Common/KrafterRoute.cs
---

