using AditiKraft.Krafter.Contracts.Common.Enums;

namespace AditiKraft.Krafter.Contracts.Common.Models;

public class DeleteRequestInput
{
    public string Id { get; set; } = string.Empty;

    public string DeleteReason { get; set; } = string.Empty;
    public EntityKind EntityKind { get; set; }

    public int AssociatedEntityType { get; set; }
    public string? AssociationEntityId { get; set; }
}

public class UpdateRecordStateRequestInput
{
    public string Id { get; set; } = string.Empty;
    public RecordState? RecordState { get; set; }
    public string? RecordStateRemarks { get; set; }
}
