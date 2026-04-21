namespace CrmSales.SharedKernel.Application;

public sealed class CursorPaginationResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }

    public static CursorPaginationResult<T> Create(IReadOnlyList<T> items, string? nextCursor)
    {
        return new CursorPaginationResult<T>
        {
            Items = items,
            NextCursor = nextCursor,
            HasMore = !string.IsNullOrEmpty(nextCursor)
        };
    }
}