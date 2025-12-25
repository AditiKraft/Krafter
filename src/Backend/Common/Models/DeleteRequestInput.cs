using Backend.Entities.Enums;

namespace Backend.Common.Models;

public class DeleteRequestInput
{
    public string Id { get; set; }

    public string DeleteReason { get; set; }
    public EntityKind EntityKind { get; set; }

    public int AssociatedEntityType { get; set; }
    public string? AssociationEntityId { get; set; }
}

public class UpdateRecordStateRequestInput
{
    public string Id { get; set; }
    public RecordState? RecordState { get; set; }
    public string? RecordStateRemarks { get; set; }
}
