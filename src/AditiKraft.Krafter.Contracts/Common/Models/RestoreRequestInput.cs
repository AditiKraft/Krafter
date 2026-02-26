namespace AditiKraft.Krafter.Contracts.Common.Models;

public class RestoreRequestInput
{
    public string Id { get; set; } = default!;
    public bool IsDeleted { get; set; }

    public int? AssociatedEntityType { get; set; }
    public string? AssociationEntityId { get; set; }
}
