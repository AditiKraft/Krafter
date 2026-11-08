using FluentValidation;

namespace AditiKraft.Krafter.Contracts.Common.Models;

public class GetRequestInput : IPagedResultRequest
{
    public const int MaximumPageSize = 200;

    // Constructor defaults are visible to Minimal API [AsParameters] binding.
    public GetRequestInput(string? id = null, bool history = false, bool isDeleted = false,
        int maxResultCount = 10, int skipCount = 0, string? query = null,
        string? orderBy = "CreatedOn desc", string? filter = null)
    {
        Id = id;
        History = history;
        IsDeleted = isDeleted;
        MaxResultCount = maxResultCount;
        SkipCount = skipCount;
        Query = query;
        OrderBy = orderBy;
        Filter = filter;
    }

    public string? Id { get; set; }
    public bool History { get; set; }
    public bool IsDeleted { get; set; }
    public int MaxResultCount { get; set; } = 10;
    public int SkipCount { get; set; }
    public string? Query { get; set; }
    public string? OrderBy { get; set; } = "CreatedOn desc";
    public string? Filter { get; set; }
}

public sealed class GetRequestInputValidator : AbstractValidator<GetRequestInput>
{
    public GetRequestInputValidator()
    {
        RuleFor(request => request.SkipCount).GreaterThanOrEqualTo(0);
        RuleFor(request => request.MaxResultCount).InclusiveBetween(1, GetRequestInput.MaximumPageSize);
        RuleFor(request => request.Id).Must(IsValidText).WithMessage("Id must contain valid Unicode text without null characters.");
        RuleFor(request => request.Query).MaximumLength(256)
            .Must(IsValidText).WithMessage("Query must contain valid Unicode text without null characters.");
        RuleFor(request => request.Filter).MaximumLength(2048)
            .Must(IsValidText).WithMessage("Filter must contain valid Unicode text without null characters.");
        RuleFor(request => request.OrderBy).MaximumLength(256)
            .Must(IsValidText).WithMessage("OrderBy must contain valid Unicode text without null characters.");
    }

    public static bool IsValidText(string? value)
    {
        if (value is null) return true;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character == '\0') return false;
            if (!char.IsSurrogate(character)) continue;
            if (!char.IsHighSurrogate(character) || index + 1 == value.Length ||
                !char.IsLowSurrogate(value[++index])) return false;
        }
        return true;
    }
}

public interface IPagedResultRequest
{
    public int SkipCount { get; set; }
    public int MaxResultCount { get; set; }
}
