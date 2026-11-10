using OrderPlatform.BuildingBlocks.Auditing;
using OrderPlatform.BuildingBlocks.Users;

namespace OrderPlatform.BuildingBlocks.Tests.Auditing;

public sealed class AuditableEntityTests
{
    private static readonly DateTimeOffset Created = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Modified = Created.AddHours(1);

    [Fact]
    public void MarkCreated_SetsCreatedFields_AndLeavesModifiedEmpty()
    {
        var user = Guid.NewGuid();
        var entity = new TestEntity();

        entity.MarkCreated(Created, user);

        Assert.Equal(Created, entity.CreatedAt);
        Assert.Equal(user, entity.CreatedBy);
        Assert.Null(entity.ModifiedAt);
        Assert.Null(entity.ModifiedBy);
    }

    [Fact]
    public void MarkModified_SetsModifiedFields_AndKeepsCreatedFields()
    {
        var creator = Guid.NewGuid();
        var editor = Guid.NewGuid();
        var entity = new TestEntity();
        entity.MarkCreated(Created, creator);

        entity.MarkModified(Modified, editor);

        Assert.Equal(Created, entity.CreatedAt);
        Assert.Equal(creator, entity.CreatedBy);
        Assert.Equal(Modified, entity.ModifiedAt);
        Assert.Equal(editor, entity.ModifiedBy);
    }

    [Fact]
    public void SystemUser_HasAFixedIdThatIsNotEmpty()
    {
        Assert.Equal(new Guid("00000000-0000-0000-0000-000000000001"), SystemUser.Id);
    }

    [Fact]
    public void SystemCurrentUser_IsTheSystemUser()
    {
        Assert.Equal(SystemUser.Id, new SystemCurrentUser().UserId);
    }

    private sealed class TestEntity : AuditableEntity;
}
