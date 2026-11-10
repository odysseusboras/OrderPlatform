using OrderPlatform.BuildingBlocks.Entities;

namespace OrderPlatform.BuildingBlocks.Auditing;

/// <summary>
/// Base class for entities with audit fields. The audit fields are filled by the persistence layer
/// when the entity is saved, never by business code.
/// </summary>
public abstract class AuditableEntity : Entity, IAuditableEntity
{
    public DateTimeOffset CreatedAt { get; private set; }

    public Guid CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public void MarkCreated(DateTimeOffset at, Guid by)
    {
        CreatedAt = at;
        CreatedBy = by;
    }

    public void MarkModified(DateTimeOffset at, Guid by)
    {
        ModifiedAt = at;
        ModifiedBy = by;
    }
}
