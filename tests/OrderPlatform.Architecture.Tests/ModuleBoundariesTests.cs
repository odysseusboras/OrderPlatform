using System.Reflection;

namespace OrderPlatform.Architecture.Tests;

/// <summary>
/// The module boundaries of CLAUDE.md section 3, checked on the compiled assemblies: each project may use only
/// the platform projects listed for it. A forbidden reference fails the build pipeline instead of a code review.
/// </summary>
public sealed class ModuleBoundariesTests
{
    private const string Platform = "OrderPlatform.";
    private const string BuildingBlocks = "OrderPlatform.BuildingBlocks";
    private const string ProcessEngine = "OrderPlatform.ProcessEngine";
    private const string Persistence = "OrderPlatform.Persistence";
    private const string Catalog = "OrderPlatform.Catalog";
    private const string CatalogContracts = "OrderPlatform.Catalog.Contracts";
    private const string Pricing = "OrderPlatform.Pricing";
    private const string PricingContracts = "OrderPlatform.Pricing.Contracts";
    private const string Ordering = "OrderPlatform.Ordering";
    private const string Inventory = "OrderPlatform.Integrations.Inventory";
    private const string InventoryContracts = "OrderPlatform.Integrations.Inventory.Contracts";
    private const string Payments = "OrderPlatform.Integrations.Payments";
    private const string PaymentsContracts = "OrderPlatform.Integrations.Payments.Contracts";
    private const string Shipping = "OrderPlatform.Integrations.Shipping";
    private const string ShippingContracts = "OrderPlatform.Integrations.Shipping.Contracts";
    private const string Fakes = "OrderPlatform.FakeExternalSystems";

    public static TheoryData<string, string[]> AllowedReferences => new()
    {
        { BuildingBlocks, [] },
        { ProcessEngine, [BuildingBlocks] },
        { CatalogContracts, [BuildingBlocks] },
        { PricingContracts, [BuildingBlocks] },
        { InventoryContracts, [BuildingBlocks] },
        { PaymentsContracts, [BuildingBlocks] },
        { ShippingContracts, [BuildingBlocks] },
        { Catalog, [BuildingBlocks, CatalogContracts] },
        { Pricing, [BuildingBlocks, PricingContracts, CatalogContracts] },
        { Ordering, [BuildingBlocks, ProcessEngine, CatalogContracts, PricingContracts, InventoryContracts, PaymentsContracts, ShippingContracts] },
        { Inventory, [BuildingBlocks, InventoryContracts] },
        { Payments, [BuildingBlocks, PaymentsContracts] },
        { Shipping, [BuildingBlocks, ShippingContracts] },
        { Fakes, [BuildingBlocks, InventoryContracts, PaymentsContracts, ShippingContracts] },
        { Persistence, [BuildingBlocks, ProcessEngine, Catalog, Ordering] },
    };

    [Theory]
    [MemberData(nameof(AllowedReferences))]
    public void Project_ReferencesOnlyThePlatformProjectsItIsAllowedTo(string project, string[] allowed)
    {
        var forbidden = PlatformReferencesOf(project).Except(allowed).ToList();

        Assert.True(forbidden.Count == 0, $"{project} must not reference: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void EveryPlatformProject_HasARule_SoANewProjectCannotSlipInUnchecked()
    {
        // The hosts are composition roots and may reference everything; every project they pull in needs a rule.
        var hosts = new[] { "OrderPlatform.Api", "OrderPlatform.Worker" };
        var reachable = hosts.SelectMany(PlatformReferencesOf).Distinct().Except(hosts);

        var withoutRule = reachable.Except(AllowedReferences.Select(row => (string)row[0])).ToList();

        Assert.True(withoutRule.Count == 0, $"Add a rule for: {string.Join(", ", withoutRule)}");
    }

    [Fact]
    public void NoProject_ReferencesAHost() =>
        Assert.All(
            AllowedReferences.Select(row => (string)row[0]),
            project => Assert.DoesNotContain(PlatformReferencesOf(project), reference => reference is "OrderPlatform.Api" or "OrderPlatform.Worker"));

    private static List<string> PlatformReferencesOf(string project) =>
        Assembly.Load(project).GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith(Platform, StringComparison.Ordinal))
            .ToList();
}
