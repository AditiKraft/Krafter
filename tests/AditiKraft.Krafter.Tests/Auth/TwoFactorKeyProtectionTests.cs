using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using AditiKraft.Krafter.Backend.Features.Users.Common;
using AditiKraft.Krafter.Backend.Web.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorKeyProtectionTests
{
    [Fact]
    public async Task SharedMfaKeysPreserveExistingIdentityResetLinks()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("krafter-mfa-keys-");
        try
        {
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=Krafter-MFA-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            string certificatePath = Path.Combine(directory.FullName, "test.pfx");
            File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, "test-only-password"));

            using var harness = new MfaHarness();
            using MfaScope scope = harness.Scope();
            ApplicationUser user = await scope.AddUserAsync();
            string purpose = UserManager<ApplicationUser>.ResetPasswordTokenPurpose;
            await using WebApplication before = Host(directory);
            string reset = await before.Services.GetRequiredService<DataProtectorTokenProvider<ApplicationUser>>()
                .GenerateAsync(purpose, scope.Users, user);

            await using WebApplication first = Host(directory, certificatePath);
            Assert.True(await first.Services.GetRequiredService<DataProtectorTokenProvider<ApplicationUser>>()
                .ValidateAsync(purpose, reset, scope.Users, user));
            IDataProtectionProvider firstKeys = first.Services.GetRequiredKeyedService<IDataProtectionProvider>(TwoFactorProtectionOptions.ProviderKey);
            string secret = firstKeys.CreateProtector("Krafter.Totp.v1", "tenant-a", "user-a").Protect("test-authenticator-secret");
            await using WebApplication second = Host(directory, certificatePath);
            IDataProtectionProvider secondKeys = second.Services.GetRequiredKeyedService<IDataProtectionProvider>(TwoFactorProtectionOptions.ProviderKey);
            Assert.Equal("test-authenticator-secret", secondKeys.CreateProtector("Krafter.Totp.v1", "tenant-a", "user-a").Unprotect(secret));
            Assert.Throws<CryptographicException>(() => secondKeys.CreateProtector("Krafter.Totp.v1", "tenant-b", "user-a").Unprotect(secret));
            string keyFile = Assert.Single(Directory.GetFiles(Path.Combine(directory.FullName, "mfa"), "key-*.xml"));
            Assert.Contains("encryptedSecret", File.ReadAllText(keyFile));
        }
        finally { directory.Delete(recursive: true); }
    }

    private static WebApplication Host(DirectoryInfo directory, string? certificatePath = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDataProtection().PersistKeysToFileSystem(directory.CreateSubdirectory("identity"));
        builder.Services.AddTransient<DataProtectorTokenProvider<ApplicationUser>>();
        if (certificatePath is not null)
        {
            builder.Configuration["TwoFactor:KeyRingPath"] = directory.CreateSubdirectory("mfa").FullName;
            builder.Configuration["TwoFactor:KeyEncryptionCertificatePath"] = certificatePath;
            builder.Configuration["TwoFactor:KeyEncryptionCertificatePassword"] = "test-only-password";
            builder.AddTwoFactorProtection();
        }
        return builder.Build();
    }
}
