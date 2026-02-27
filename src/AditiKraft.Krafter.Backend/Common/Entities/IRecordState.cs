using AditiKraft.Krafter.Contracts.Common.Enums;

namespace AditiKraft.Krafter.Backend.Common.Entities;

public interface IRecordState
{
    public RecordState RecordState { get; set; }
    public string? RecordStateRemarks { get; set; }
}

