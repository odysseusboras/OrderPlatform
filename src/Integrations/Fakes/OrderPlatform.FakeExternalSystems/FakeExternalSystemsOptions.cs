namespace OrderPlatform.FakeExternalSystems;

/// <summary>
/// Failure scenarios of the fake external systems, set in configuration (section <c>FakeExternalSystems</c>) or in tests.
/// They live here and never in the real catalog.
/// </summary>
public sealed class FakeExternalSystemsOptions
{
    public const string SectionName = "FakeExternalSystems";

    public FakeInventoryOptions Inventory { get; set; } = new();
}

public sealed class FakeInventoryOptions
{
    /// <summary>Reserving any of these SKUs fails for good: out of stock.</summary>
    public IList<string> OutOfStockSkus { get; set; } = [];

    /// <summary>Reserving any of these SKUs fails for now: the inventory system is unavailable.</summary>
    public IList<string> UnavailableSkus { get; set; } = [];
}
