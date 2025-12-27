using Krafter.Shared.Entities.Enums;

namespace Krafter.Shared.Common.Models;

public interface ICommonDtoProperty
{
    public bool IsDeleted { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public DateTime? PeriodStart { get; set; }
    public UserInfo? CreatedBy { get; set; }
    public string? CreatedById { get; set; }
    public UserInfo? UpdatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedById { get; set; }
    public string? DeleteReason { get; set; }
    public RecordState RecordState { get; set; }
    public string? RecordStateRemarks { get; set; }
}

public interface IIdDto
{
    public string Id { get; set; }
}

public class CommonDtoProperty : ICommonDtoProperty
{
    public string Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public DateTime? PeriodStart { get; set; }
    public UserInfo? CreatedBy { get; set; }
    public string? CreatedById { get; set; }
    public UserInfo? UpdatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedById { get; set; }

    public string? DeleteReason { get; set; }
    public RecordState RecordState { get; set; } = RecordState.Active;
    public string? RecordStateRemarks { get; set; }
}
