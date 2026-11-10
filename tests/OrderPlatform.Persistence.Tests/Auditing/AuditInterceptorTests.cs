using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using OrderPlatform.BuildingBlocks.Auditing;
using OrderPlatform.BuildingBlocks.Users;
using OrderPlatform.Persistence.Auditing;

namespace OrderPlatform.Persistence.Tests.Auditing;

public sealed class AuditInterceptorTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeTimeProvider _time = new(Start);
    private readonly TestCurrentUser _user = new(Guid.NewGuid());

    public AuditInterceptorTests()
    {
        _connection.Open();
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    [Fact]
    public void SaveChanges_OnAdd_StampsCreatedFields()
    {
        using var context = NewContext();
        var entity = new Note { Text = "first" };
        context.Notes.Add(entity);

        context.SaveChanges();

        Assert.Equal(Start, entity.CreatedAt);
        Assert.Equal(_user.UserId, entity.CreatedBy);
        Assert.Null(entity.ModifiedAt);
        Assert.Null(entity.ModifiedBy);
    }

    [Fact]
    public async Task SaveChangesAsync_OnUpdate_StampsModifiedFields_AndKeepsCreatedFields()
    {
        var creator = _user.UserId;
        Guid id;
        await using (var context = NewContext())
        {
            var entity = new Note { Text = "first" };
            context.Notes.Add(entity);
            await context.SaveChangesAsync(CancellationToken.None);
            id = entity.Id;
        }

        _time.Advance(TimeSpan.FromMinutes(5));
        _user.UserId = Guid.NewGuid();
        await using (var context = NewContext())
        {
            var entity = await context.Notes.SingleAsync(note => note.Id == id, CancellationToken.None);
            entity.Text = "second";
            await context.SaveChangesAsync(CancellationToken.None);

            Assert.Equal(Start, entity.CreatedAt);
            Assert.Equal(creator, entity.CreatedBy);
            Assert.Equal(Start.AddMinutes(5), entity.ModifiedAt);
            Assert.Equal(_user.UserId, entity.ModifiedBy);
        }
    }

    [Fact]
    public void SaveChanges_ForUnchangedEntities_StampsNothing()
    {
        Guid id;
        using (var context = NewContext())
        {
            var entity = new Note { Text = "first" };
            context.Notes.Add(entity);
            context.SaveChanges();
            id = entity.Id;
        }

        _time.Advance(TimeSpan.FromMinutes(5));
        using (var context = NewContext())
        {
            var entity = context.Notes.Single(note => note.Id == id);
            context.SaveChanges();

            Assert.Null(entity.ModifiedAt);
        }
    }

    public void Dispose() => _connection.Dispose();

    private NotesContext NewContext() =>
        new(new DbContextOptionsBuilder<NotesContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditInterceptor(_time, _user))
            .Options);

    private sealed class Note : AuditableEntity
    {
        public string Text { get; set; } = string.Empty;
    }

    private sealed class NotesContext(DbContextOptions<NotesContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; set; } = userId;
    }
}
