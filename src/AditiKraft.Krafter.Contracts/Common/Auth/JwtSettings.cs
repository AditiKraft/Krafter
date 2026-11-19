using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AditiKraft.Krafter.Contracts.Common.Auth;

public class JwtSettings : IValidatableObject
{
    private const string DevelopmentSigningKey = "ziKPKoP2YWMztBmxlNNCVRojuZWxjHlDAB6sPC6Uc8";

    public string Key { get; set; } = string.Empty;

    public int TokenExpirationInMinutes { get; set; }

    public int RefreshTokenExpirationInDays { get; set; }

    public bool HasPrivateSigningKey() => !string.IsNullOrWhiteSpace(Key)
        && !string.Equals(Key.Trim(), DevelopmentSigningKey, StringComparison.Ordinal)
        && Encoding.UTF8.GetByteCount(Key) >= 32;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Key))
        {
            yield return new ValidationResult("No Key defined in JwtSettings config", new[] { nameof(Key) });
        }
    }
}
