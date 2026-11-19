using AditiKraft.Krafter.Contracts.Common.Models;
using AditiKraft.Krafter.Backend.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace AditiKraft.Krafter.Backend.Features.Users.Common;

internal static class UserIdentityValidation
{
    public static async Task<Response> ValidateAsync(UserManager<ApplicationUser> users, ApplicationUser user,
        CancellationToken cancellationToken)
    {
        Response validation = Validate(users, user.Email, user.UserName);
        if (validation.IsError)
            return validation;
        foreach (IUserValidator<ApplicationUser> validator in users.UserValidators)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IdentityResult result = await validator.ValidateAsync(users, user);
            if (!result.Succeeded)
                return IdentityConflict.IsDuplicate(result) ? Response.Conflict(IdentityConflict.UserMessage) :
                    Response.BadRequest(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
        return Response.Success();
    }

    // Email is also the login username for tenant owners and email changes.
    // Respect deployments that customize Identity's username alphabet.
    public static Response Validate(UserManager<ApplicationUser> users, string? email, string? userName)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(userName) ||
            email.Length > 256 || userName.Length > 256 ||
            users.NormalizeEmail(email)?.Length > 256 || users.NormalizeName(userName)?.Length > 256)
            return Response.BadRequest("Email and username are required and cannot exceed 256 characters.");

        string? allowed = users.Options.User.AllowedUserNameCharacters;
        if (!string.IsNullOrEmpty(allowed) && userName.Any(character => !allowed.Contains(character)))
            return Response.BadRequest("The login email or username contains characters not allowed by the configured identity policy.");

        return Response.Success();
    }
}
