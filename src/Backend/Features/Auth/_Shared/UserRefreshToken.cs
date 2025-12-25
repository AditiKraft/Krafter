using Backend.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Features.Auth._Shared;

public class UserRefreshToken : ITenant
{
    public string UserId { get; set; }
    public string TenantId { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }

    [NotMapped] public DateTime TokenExpiryTime { get; set; }
}
