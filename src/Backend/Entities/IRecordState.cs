using Krafter.Shared.Common.Enums;

namespace Backend.Entities;

public interface IRecordState
{
    public RecordState RecordState { get; set; }
    public string? RecordStateRemarks { get; set; }
}
