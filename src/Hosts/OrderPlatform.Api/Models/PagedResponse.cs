using OrderPlatform.BuildingBlocks.Paging;

namespace OrderPlatform.Api.Models;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount, int TotalPages, bool HasNextPage)
{
    public static PagedResponse<T> From(Page<T> page) =>
        new(page.Items, page.PageNumber, page.PageSize, page.TotalCount, page.TotalPages, page.HasNextPage);
}
