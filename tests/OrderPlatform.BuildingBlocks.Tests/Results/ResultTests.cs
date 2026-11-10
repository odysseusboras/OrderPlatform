using OrderPlatform.BuildingBlocks.Results;

namespace OrderPlatform.BuildingBlocks.Tests.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_CarriesTheValue_AndNoError()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_CarriesTheError_AndNoValue()
    {
        var error = new Error("orders.not_found", "The order does not exist.");

        var result = Result<string>.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Failure_WithoutError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(null!));
    }
}
