using System.ComponentModel.DataAnnotations.Schema;
using AditiKraft.Krafter.Backend.Common.Entities;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public class UserRefreshToken : ITenant
{
    public string UserId { get; set; } = null!;
    public string TenantId { get; set; } = null!;
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }

    /// <summary>
    /// The refresh token value immediately prior to the last rotation. Retained so that a
    /// concurrent refresh which raced against the rotation (and therefore still presents the
    /// just-superseded token) can be honoured within a short grace window instead of being
    /// forcibly logged out. See <see cref="RefreshTokenRotatedAt"/>.
    /// </summary>
    public string? PreviousRefreshToken { get; set; }

    /// <summary>
    /// UTC timestamp of the last rotation, used to bound the grace window during which
    /// <see cref="PreviousRefreshToken"/> is still accepted.
    /// </summary>
    public DateTime? RefreshTokenRotatedAt { get; set; }

    [NotMapped] public DateTime TokenExpiryTime { get; set; }
}



