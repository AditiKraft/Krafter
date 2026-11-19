using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AditiKraft.Krafter.Backend.Features.Auth.Common;
using Microsoft.AspNetCore.DataProtection;

namespace AditiKraft.Krafter.Backend.Web.Authentication;

internal static class TwoFactorProtectionRegistration
{
    internal static void AddTwoFactorProtection(this WebApplicationBuilder builder)
    {
        const string applicationName = "AditiKraft.Krafter";
        const string key = TwoFactorProtectionOptions.ProviderKey;
        string? keyRing = builder.Configuration["TwoFactor:KeyRingPath"];
        string? certificatePath = builder.Configuration["TwoFactor:KeyEncryptionCertificatePath"];

        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            builder.Services.AddKeyedSingleton<X509Certificate2>(key, (_, _) =>
            {
                X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath,
                    builder.Configuration["TwoFactor:KeyEncryptionCertificatePassword"]);
                using RSA? rsa = certificate.GetRSAPublicKey();
                if (certificate.HasPrivateKey && rsa is not null) return certificate;
                certificate.Dispose();
                throw new InvalidOperationException("The two-factor key-encryption certificate must include an RSA private key.");
            });
        }

        // Identity reset links and antiforgery cookies keep their original host provider.
        builder.Services.AddKeyedSingleton<IDataProtectionProvider>(key, (services, _) =>
        {
            var certificate = services.GetKeyedService<X509Certificate2>(key);
            if (string.IsNullOrWhiteSpace(keyRing))
            {
                return certificate is null ? DataProtectionProvider.Create(applicationName)
                    : DataProtectionProvider.Create(applicationName, certificate);
            }

            return DataProtectionProvider.Create(new DirectoryInfo(keyRing), protection =>
            {
                protection.SetApplicationName(applicationName);
                if (certificate is not null) protection.ProtectKeysWithCertificate(certificate);
            });
        });

        builder.Services.AddOptions<TwoFactorProtectionOptions>()
            .Configure<IServiceProvider>((options, services) =>
            {
                // Resolve at startup so a misconfigured certificate fails before serving traffic.
                _ = services.GetRequiredKeyedService<IDataProtectionProvider>(key);
                options.EnrollmentAllowed = builder.Environment.IsDevelopment() ||
                    (!string.IsNullOrWhiteSpace(keyRing) && !string.IsNullOrWhiteSpace(certificatePath));
            }).ValidateOnStart();
    }
}
