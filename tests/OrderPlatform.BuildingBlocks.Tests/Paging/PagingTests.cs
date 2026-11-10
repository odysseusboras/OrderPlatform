using OrderPlatform.BuildingBlocks.Paging;

namespace OrderPlatform.BuildingBlocks.Tests.Paging;

public sealed class PagingTests
{
    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(3, 20, 40)]
    [InlineData(2, 100, 100)]
    public void PageRequest_Skip_IsThePreviousPagesTimesThePageSize(int pageNumber, int pageSize, int expected)
    {
        Assert.Equal(expected, new PageRequest(pageNumber, pageSize).Skip);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, PageRequest.MaxPageSize + 1)]
    public void PageRequest_OutsideTheLimits_Throws(int pageNumber, int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageRequest(pageNumber, pageSize));
    }

    [Theory]
    [InlineData(0, 20, 0, false)]
    [InlineData(20, 20, 1, false)]
    [InlineData(21, 20, 2, true)]
    public void Page_CountsPagesAndKnowsWhetherAnotherFollows(int totalCount, int pageSize, int totalPages, bool hasNext)
    {
        var page = new Page<int>([], 1, pageSize, totalCount);

        Assert.Equal(totalPages, page.TotalPages);
        Assert.Equal(hasNext, page.HasNextPage);
    }

    [Fact]
    public void Page_Map_KeepsThePagingInformation()
    {
        var mapped = new Page<int>([1, 2], 2, 2, 5).Map(number => number * 10);

        Assert.Equal([10, 20], mapped.Items);
        Assert.Equal((2, 2, 5), (mapped.PageNumber, mapped.PageSize, mapped.TotalCount));
    }
}
