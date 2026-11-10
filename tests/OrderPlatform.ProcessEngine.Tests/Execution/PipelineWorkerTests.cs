using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using OrderPlatform.ProcessEngine.Execution;
using OrderPlatform.ProcessEngine.Runs;
using OrderPlatform.ProcessEngine.Steps;
using OrderPlatform.ProcessEngine.Storage;
using OrderPlatform.ProcessEngine.Tests.Support;
using OrderPlatform.TestUtilities.Database;

namespace OrderPlatform.ProcessEngine.Tests.Execution;

/// <summary>The background worker on a real (SQLite) store: it finds due runs, locks them, moves them and saves every move.</summary>
public sealed class PipelineWorkerTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new(typeof(PipelineRun).Assembly);
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
    private readonly StepJournal _journal = new();
    private readonly StepTwo _two;
    private readonly ServiceProvider _services;

    public PipelineWorkerTests()
    {
        _two = new StepTwo(_journal);
        _services = _database.Register(new ServiceCollection())
            .AddSingleton<TimeProvider>(_clock)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(_journal)
            .AddSingleton(_two)
            .AddPipeline(TestPipeline.Standard())
            .BuildServiceProvider();
        SqliteTestDatabase.Create(_services);
    }

    [Fact]
    public async Task RunOnce_DueRun_RunsItToTheEnd_AndReleasesTheLock()
    {
        var runId = await StartAsync();

        var moved = await Worker().RunOnceAsync(CancellationToken.None);

        var run = await LoadAsync(runId);
        Assert.Equal(1, moved);
        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(["execute one", "execute two", "execute three", "execute four", "execute five"], _journal.Entries);
        Assert.Null(run.LockedBy);
    }

    [Fact]
    public async Task RunOnce_DueRun_RunsItsStepsInsideOneTrace()
    {
        var traces = new List<ActivityTraceId?>();
        _two.ThenExecute(_ =>
        {
            traces.Add(Activity.Current?.TraceId);
            return StepResult.Success();
        });
        await StartAsync();

        await Worker().RunOnceAsync(CancellationToken.None);

        Assert.NotNull(Assert.Single(traces));
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task RunOnce_StepHasToBeRetried_StopsThere_AndContinuesWhenTheRetryIsDue()
    {
        _two.ThenExecute(StepResult.Retryable("busy"));
        var runId = await StartAsync();

        await Worker().RunOnceAsync(CancellationToken.None);
        var notDueYet = await Worker().RunOnceAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(1));
        await Worker().RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, notDueYet);
        Assert.Equal(RunStatus.Completed, (await LoadAsync(runId)).Status);
    }

    [Fact]
    public async Task RunOnce_FatalError_UndoesTheSucceededSteps()
    {
        _two.ThenExecute(StepResult.Fatal("out of stock"));
        var runId = await StartAsync();

        await Worker().RunOnceAsync(CancellationToken.None);

        Assert.Equal(RunStatus.Compensated, (await LoadAsync(runId)).Status);
        Assert.Equal(["execute one", "execute two", "compensate one"], _journal.Entries);
    }

    [Fact]
    public async Task RunOnce_RunHeldBySomeoneElse_IsLeftAlone_UntilTheLockExpires()
    {
        var runId = await StartAsync(run => run.Lock("inline", _clock.GetUtcNow().AddSeconds(30)));

        var whileLocked = await Worker().RunOnceAsync(CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(30));
        var afterExpiry = await Worker().RunOnceAsync(CancellationToken.None);

        Assert.Equal(0, whileLocked);
        Assert.Equal(1, afterExpiry);
        Assert.Equal(RunStatus.Completed, (await LoadAsync(runId)).Status);
    }

    [Fact]
    public async Task RunOnce_NothingDue_DoesNothing() =>
        Assert.Equal(0, await Worker().RunOnceAsync(CancellationToken.None));

    [Fact]
    public async Task FindDueIds_ReturnsOnlyRunsThatAreDueNow_OldestFirst()
    {
        var first = await StartAsync();
        _clock.Advance(TimeSpan.FromSeconds(1));
        var second = await StartAsync();
        var later = await StartAsync(run => run.ScheduleAt(_clock.GetUtcNow().AddMinutes(5)));
        var finished = await StartAsync(run => run.Complete());

        await using var scope = _services.CreateAsyncScope();
        var due = await scope.ServiceProvider.GetRequiredService<IPipelineRunStore>().FindDueIdsAsync(_clock.GetUtcNow(), 10, CancellationToken.None);

        Assert.Equal([first, second], due);
        Assert.DoesNotContain(later, due);
        Assert.DoesNotContain(finished, due);
    }

    [Fact]
    public async Task FindDueIds_TakesAtMostTheRequestedNumber()
    {
        await StartAsync();
        await StartAsync();

        await using var scope = _services.CreateAsyncScope();
        var due = await scope.ServiceProvider.GetRequiredService<IPipelineRunStore>().FindDueIdsAsync(_clock.GetUtcNow(), 1, CancellationToken.None);

        Assert.Single(due);
    }

    public void Dispose()
    {
        _services.Dispose();
        _database.Dispose();
    }

    private PipelineWorker Worker() =>
        new(_services.GetRequiredService<IServiceScopeFactory>(), _clock, NullLogger<PipelineWorker>.Instance);

    private async Task<Guid> StartAsync(Action<PipelineRun>? change = null)
    {
        await using var scope = _services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPipelineRunStore>();
        var run = store.Start(TestPipeline.Standard(), Guid.CreateVersion7(), new FlowContext());
        change?.Invoke(run);
        await store.SaveChangesAsync(CancellationToken.None);
        return run.Id;
    }

    private async Task<PipelineRun> LoadAsync(Guid runId)
    {
        await using var scope = _services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IPipelineRunStore>().FindAsync(runId, CancellationToken.None))!;
    }
}
