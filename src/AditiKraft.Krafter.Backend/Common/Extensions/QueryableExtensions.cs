using AditiKraft.Krafter.Contracts.Common.Models;

namespace AditiKraft.Krafter.Backend.Common.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<T> PageBy<T>(this IQueryable<T> query, int skipCount, int maxResultCount)
    {
        if (query == null)
        {
            throw new ArgumentNullException("query");
        }

        return query.Skip(skipCount).Take(maxResultCount);
    }

    public static IQueryable<T> PageBy<T>(this IQueryable<T> query, IPagedResultRequest pagedResultRequest) =>
        query.PageBy(pagedResultRequest.SkipCount, pagedResultRequest.MaxResultCount);
}
