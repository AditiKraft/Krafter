namespace Krafter.Shared.Common.Models;

public class GetRequestInput : IPagedResultRequest
{
    public string? Id { get; set; }
    public bool History { get; set; }
    public bool IsDeleted { get; set; }
    public int MaxResultCount { get; set; } = 10;
    public int SkipCount { get; set; }
    public string? Query { get; set; }
    public string? OrderBy { get; set; } = "CreatedOn desc";
    public string? Filter { get; set; }
}

public interface IPagedResultRequest
{
    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; }
}
