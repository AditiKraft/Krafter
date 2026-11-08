using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace AditiKraft.Krafter.Tests.Auth;

public sealed class TwoFactorKeyProtectionTests
{
    [Fact]
    public void CertificateProtectedKeysWorkAcrossReplicasAndRejectAnotherTenant()
    {
        string directory = Directory.CreateTempSubdirectory("krafter-mfa-keys-").FullName;
        try
        {
            using RSA rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=Krafter-MFA-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
            string certificatePath = Path.Combine(directory, "test.pfx");
            File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, "test-only-password"));
            using X509Certificate2 loaded = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, "test-only-password");
            using ServiceProvider first = Services(directory, loaded);
            string protectedValue = first.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("Krafter.Totp.v1", "tenant-a", "user-a").Protect("test-authenticator-secret");
            using ServiceProvider second = Services(directory, loaded);
            IDataProtectionProvider provider = second.GetRequiredService<IDataProtectionProvider>();
            Assert.Equal("test-authenticator-secret", provider.CreateProtector("Krafter.Totp.v1", "tenant-a", "user-a").Unprotect(protectedValue));
            Assert.Throws<CryptographicException>(() => provider.CreateProtector("Krafter.Totp.v1", "tenant-b", "user-a").Unprotect(protectedValue));
            string keyFile = Assert.Single(Directory.GetFiles(directory, "key-*.xml"));
            Assert.Contains("encryptedSecret", File.ReadAllText(keyFile));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static ServiceProvider Services(string directory, X509Certificate2 certificate)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().SetApplicationName("Krafter-MFA-test")
            .PersistKeysToFileSystem(new DirectoryInfo(directory)).ProtectKeysWithCertificate(certificate);
        return services.BuildServiceProvider();
    }
}
