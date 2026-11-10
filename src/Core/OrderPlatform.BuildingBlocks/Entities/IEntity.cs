namespace OrderPlatform.BuildingBlocks.Entities;

/// <summary>Anything persisted with its own identity.</summary>
public interface IEntity
{
    Guid Id { get; }
}
