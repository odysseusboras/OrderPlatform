namespace OrderPlatform.BuildingBlocks.Paging;

/// <summary>One page of a list plus what a client needs to ask for the next one.</summary>
public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => PageNumber < TotalPages;

    public Page<TResult> Map<TResult>(Func<T, TResult> map) => new(Items.Select(map).ToList(), PageNumber, PageSize, TotalCount);
}
