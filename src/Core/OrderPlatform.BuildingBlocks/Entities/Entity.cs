namespace OrderPlatform.BuildingBlocks.Entities;

/// <summary>Base class for persisted entities. The identifier is time ordered, so database indexes stay compact.</summary>
public abstract class Entity : IEntity
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
}
