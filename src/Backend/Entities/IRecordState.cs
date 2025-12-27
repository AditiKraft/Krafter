using Krafter.Shared.Entities.Enums;

namespace Backend.Entities;

public interface IRecordState
{
    public RecordState RecordState { get; set; }
    public string? RecordStateRemarks { get; set; }
}
