using AditiKraft.Krafter.Backend.Features.Users.Common;

namespace AditiKraft.Krafter.Backend.Common.Entities;

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
    public string Id { get; set; } = null!;
    public string? DeleteReason { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedOn { get; set; }
    public KrafterUser? CreatedBy { get; set; }
#pragma warning disable CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).
    public string CreatedById { get; set; } = null!;
#pragma warning restore CS8767 // Nullability of reference types in type of parameter doesn't match implicitly implemented member (possibly because of nullability attributes).

    public DateTime? UpdatedOn { get; set; }

    public KrafterUser? UpdatedBy { get; set; }

    public string? UpdatedById { get; set; }
    public string TenantId { get; set; } = null!;
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



