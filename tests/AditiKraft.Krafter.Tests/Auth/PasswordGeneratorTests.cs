using AditiKraft.Krafter.Backend.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Contracts.Common.Auth;
using Microsoft.AspNetCore.Identity;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class PasswordGeneratorTests
{
    [Fact]
    public void InitialPasswordsDefaultToSixteenCharacters() =>
        Assert.Equal(16, PasswordGenerator.GeneratePassword().Length);

    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(PasswordLimits.MaximumLength)]
    public async Task SupportedLengthsKeepEveryRequiredCharacterClassAndPassIdentity(int length)
    {
        string password = PasswordGenerator.GeneratePassword(length);
        Assert.Equal(length, password.Length);
        Assert.Contains(password, character => character is >= 'A' and <= 'Z');
        Assert.Contains(password, character => character is >= 'a' and <= 'z');
        Assert.Contains(password, character => character is >= '0' and <= '9');
        Assert.Contains(password, character => "!@#$%^&*()".Contains(character));

        using var harness = new MfaHarness();
        using MfaScope scope = harness.Scope();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), UserName = "generated@example.test", Email = "generated@example.test",
            IsActive = true
        };
        IdentityResult result = await scope.Users.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join(';', result.Errors.Select(error => error.Description)));
        Assert.True(await scope.Users.CheckPasswordAsync(user, password));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(7)]
    [InlineData(PasswordLimits.MaximumLength + 1)]
    [InlineData(int.MaxValue)]
    public void UnsupportedLengthsAreRejectedInsteadOfSilentlyChangingTheLength(int length) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PasswordGenerator.GeneratePassword(length));
}
