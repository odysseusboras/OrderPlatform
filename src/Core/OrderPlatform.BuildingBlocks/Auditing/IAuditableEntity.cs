using OrderPlatform.BuildingBlocks.Entities;

namespace OrderPlatform.BuildingBlocks.Auditing;

/// <summary>An entity whose creation and last modification are recorded automatically when it is saved.</summary>
public interface IAuditableEntity : IEntity
{
    DateTimeOffset CreatedAt { get; }

    Guid CreatedBy { get; }

    DateTimeOffset? ModifiedAt { get; }

    Guid? ModifiedBy { get; }

    void MarkCreated(DateTimeOffset at, Guid by);

    void MarkModified(DateTimeOffset at, Guid by);
}
