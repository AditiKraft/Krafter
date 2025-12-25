using Backend.Features.Users._Shared;

namespace Backend.Entities;

public interface ITenant
{
    public string TenantId { get; set; }
}

public interface ISoftDelete
{
    public bool IsDeleted { get; set; }
    public string? DeleteReason { get; set; }
}

public class CommonEntityProperty : ICommonEntityProperty
{
    public string Id { get; set; }
    public string? DeleteReason { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedOn { get; set; }
    public KrafterUser? CreatedBy { get; set; }
    public string CreatedById { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public KrafterUser? UpdatedBy { get; set; }

    public string? UpdatedById { get; set; }
    public string TenantId { get; set; }
}

public interface ICommonEntityProperty : ICommonAuthEntityProperty
{
    public string Id { get; set; }
}

public interface ICommonAuthEntityProperty : ITenant, ISoftDelete, IHistory
{
}

public interface IHistory
{
    public KrafterUser? CreatedBy { get; set; }
    public KrafterUser? UpdatedBy { get; set; }
    public DateTime CreatedOn { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedById { get; set; }
}
