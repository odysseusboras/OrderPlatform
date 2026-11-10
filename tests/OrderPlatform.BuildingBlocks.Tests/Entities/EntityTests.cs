using OrderPlatform.BuildingBlocks.Auditing;
using OrderPlatform.BuildingBlocks.Entities;

namespace OrderPlatform.BuildingBlocks.Tests.Entities;

public sealed class EntityTests
{
    [Fact]
    public void NewEntity_HasAUniqueNonEmptyId()
    {
        var first = new PlainEntity();
        var second = new PlainEntity();

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Ids_AreVersion7_SoTheyStartWithATimestamp()
    {
        Assert.Equal(7, new PlainEntity().Id.Version);
    }

    [Fact]
    public void AuditableEntity_IsAnEntity()
    {
        IEntity entity = new AuditedEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    private sealed class PlainEntity : Entity;

    private sealed class AuditedEntity : AuditableEntity;
}
