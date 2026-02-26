namespace AditiKraft.Krafter.Contracts.Common.Models;

public class UserInfo : IIdDto
{
    public string Id { get; set; } = string.Empty;
    public string? FirstName { get; set; }

    public string? LastName { get; set; }
    public DateTime CreatedOn { get; set; }

    ////Email
    public string Email { get; set; } = "iambipinpaul@outlook.com";

    ////permissions

    public List<string> Permissions { get; set; } = new();

    ////roles
    public List<string> Roles { get; set; } = new();

    ////tokenExpiryTime
    public DateTime TokenExpiryTime { get; set; }
}
