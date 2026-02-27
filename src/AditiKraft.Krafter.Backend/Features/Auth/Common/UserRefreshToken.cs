using System.ComponentModel.DataAnnotations.Schema;
using AditiKraft.Krafter.Backend.Entities;

namespace AditiKraft.Krafter.Backend.Features.Auth.Common;

public class UserRefreshToken : ITenant
{
    public string UserId { get; set; } = null!;
    public string TenantId { get; set; } = null!;
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }

    [NotMapped] public DateTime TokenExpiryTime { get; set; }
}


