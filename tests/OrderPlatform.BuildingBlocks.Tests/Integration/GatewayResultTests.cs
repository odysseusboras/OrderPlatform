using OrderPlatform.BuildingBlocks.Integration;

namespace OrderPlatform.BuildingBlocks.Tests.Integration;

public sealed class GatewayResultTests
{
    [Fact]
    public void Success_IsSuccess_WithoutError()
    {
        var result = GatewayResult.Success();

        Assert.True(result.IsSuccess);
        Assert.Equal(GatewayOutcome.Succeeded, result.Outcome);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Transient_IsNotSuccess_AndCarriesTheError()
    {
        var result = GatewayResult.Transient("timeout");

        Assert.False(result.IsSuccess);
        Assert.Equal(GatewayOutcome.TransientFailure, result.Outcome);
        Assert.Equal("timeout", result.Error);
    }

    [Fact]
    public void Permanent_IsNotSuccess_AndCarriesTheError()
    {
        var result = GatewayResult.Permanent("card declined");

        Assert.False(result.IsSuccess);
        Assert.Equal(GatewayOutcome.PermanentFailure, result.Outcome);
        Assert.Equal("card declined", result.Error);
    }

    [Fact]
    public void GenericSuccess_CarriesTheValue()
    {
        var result = GatewayResult<string>.Success("RES-1");

        Assert.True(result.IsSuccess);
        Assert.Equal("RES-1", result.Value);
    }

    [Fact]
    public void GenericTransient_HasNoValue()
    {
        var result = GatewayResult<string>.Transient("unavailable");

        Assert.Equal(GatewayOutcome.TransientFailure, result.Outcome);
        Assert.Null(result.Value);
        Assert.Equal("unavailable", result.Error);
    }

    [Fact]
    public void GenericPermanent_HasNoValue()
    {
        var result = GatewayResult<string>.Permanent("out of stock");

        Assert.Equal(GatewayOutcome.PermanentFailure, result.Outcome);
        Assert.Null(result.Value);
        Assert.Equal("out of stock", result.Error);
    }
}
