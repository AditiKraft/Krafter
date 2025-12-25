namespace Backend.Common.Models;

public class PaginationResponse<T>(List<T> items, int count, int skipCount, int maxResultCount)
{
    public PaginationResponse() : this(new List<T>(), 0, 0, 0)
    {
        Items = new List<T>();
    }

    public List<T> Items { get; set; } = items;
    public int SkipCount { get; set; } = skipCount;
    public int TotalCount { get; set; } = count;
    public int MaxResultCount { get; set; } = maxResultCount;
}
