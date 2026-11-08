using AditiKraft.Krafter.Contracts.Contracts.Auth;
using AditiKraft.Krafter.Contracts.Contracts.Roles;
using AditiKraft.Krafter.Contracts.Contracts.Tenants;
using AditiKraft.Krafter.Contracts.Contracts.Users;

namespace AditiKraft.Krafter.Tests.Validation;

public sealed class PersistedTextValidationTests
{
    [Theory]
    [InlineData("nul")]
    [InlineData("high")]
    [InlineData("low")]
    public void ProfileFieldsRejectTextTheDatabaseCannotEncode(string kind)
    {
        string invalid = InvalidText(kind);
        foreach (string property in new[] { "FirstName", "LastName", "PhoneNumber", "Email", "UserName" })
        {
            var request = User();
            typeof(CreateUserRequest).GetProperty(property)!.SetValue(request,
                property == "Email" ? $"a{invalid}b@example.test" : $"a{invalid}b");
            var validation = new CreateUserRequestValidator().Validate(request);
            Assert.Contains(validation.Errors, failure => failure.PropertyName == property);
        }
        var memberships = User();
        memberships.Roles = [$"a{invalid}b"];
        Assert.Contains(new CreateUserRequestValidator().Validate(memberships).Errors,
            failure => failure.PropertyName == "Roles[0]");
    }

    [Theory]
    [InlineData("nul")]
    [InlineData("high")]
    [InlineData("low")]
    public void RoleAndTenantFieldsRejectTextTheDatabaseCannotEncode(string kind)
    {
        string invalid = InvalidText(kind);
        foreach (string property in new[] { "Name", "Description" })
        {
            var role = new CreateOrUpdateRoleRequest { Name = "Editor", Description = "An editor" };
            typeof(CreateOrUpdateRoleRequest).GetProperty(property)!.SetValue(role, $"a{invalid}b");
            Assert.Contains(new CreateOrUpdateRoleRequestValidator().Validate(role).Errors,
                failure => failure.PropertyName == property);
        }
        foreach (string property in new[] { "Name", "Identifier", "AdminEmail", "TablesToCopy" })
        {
            var tenant = Tenant();
            typeof(CreateOrUpdateTenantRequest).GetProperty(property)!.SetValue(tenant,
                property == "AdminEmail" ? $"a{invalid}b@example.test" : $"a{invalid}b");
            Assert.Contains(new CreateOrUpdateTenantRequestValidator().Validate(tenant).Errors,
                failure => failure.PropertyName == property);
        }
    }

    [Fact]
    public void DisplayTextRetainsEmojiAndOptionalValuesRemainOptional()
    {
        var user = User();
        user.FirstName = "First😀";
        user.LastName = "Last😀";
        user.PhoneNumber = null;
        Assert.True(new CreateUserRequestValidator().Validate(user).IsValid);
        Assert.True(new CreateOrUpdateRoleRequestValidator().Validate(new CreateOrUpdateRoleRequest
        { Name = "Editor😀", Description = "Description😀" }).IsValid);
        var tenant = Tenant();
        tenant.Name = "Tenant😀";
        tenant.TablesToCopy = "schema😀";
        Assert.True(new CreateOrUpdateTenantRequestValidator().Validate(tenant).IsValid);
    }

    [Fact]
    public void PasswordProofIsNotSubjectToPersistedProfileTextRules()
    {
        // Passwords are hashed; their accepted alphabet must not inherit a database text restriction.
        Assert.True(new TokenRequestValidator().Validate(new TokenRequest
        { Email = "account@example.test", Password = "Password\0😀1!" }).IsValid);
    }

    private static CreateUserRequest User() => new()
    { FirstName = "First", LastName = "Last", Email = "user@example.test", UserName = "user@example.test", PhoneNumber = "123" };

    // Custom-attribute strings are UTF-8 encoded; construct unpaired surrogates at runtime.
    private static string InvalidText(string kind) => new(kind switch
    { "nul" => '\0', "high" => (char)0xd800, "low" => (char)0xdc00, _ => throw new ArgumentOutOfRangeException(nameof(kind)) }, 1);

    private static CreateOrUpdateTenantRequest Tenant() => new()
    {
        Identifier = "blue", Name = "Blue", AdminEmail = "owner@example.test", IsActive = true,
        ValidUpto = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };
}
