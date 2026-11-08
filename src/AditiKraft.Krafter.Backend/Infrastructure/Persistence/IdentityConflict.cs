using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AditiKraft.Krafter.Backend.Infrastructure.Persistence;

internal static class IdentityConflict
{
    public static bool IsUser(DbUpdateException exception) => exception.InnerException is PostgresException
    {
        SqlState: PostgresErrorCodes.UniqueViolation,
        ConstraintName: "IX_ApplicationUser_NormalizedEmail_TenantId" or "IX_ApplicationUser_NormalizedUserName_TenantId"
    };

    public static bool IsRole(DbUpdateException exception) => exception.InnerException is PostgresException
    {
        SqlState: PostgresErrorCodes.UniqueViolation,
        ConstraintName: "IX_ApplicationRole_NormalizedName_TenantId"
    };

    public static bool IsDuplicate(IdentityResult result) => result.Errors.Any(error =>
        error.Code is "DuplicateUserName" or "DuplicateEmail" or "DuplicateRoleName");

    public const string UserMessage = "This email or username is already in use, including retained deleted accounts. Choose a different identifier.";
    public const string RoleMessage = "This role name is already in use, including retained deleted roles. Choose a different name.";
}
