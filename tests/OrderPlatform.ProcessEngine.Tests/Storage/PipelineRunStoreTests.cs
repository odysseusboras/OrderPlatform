using Microsoft.EntityFrameworkCore;
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

namespace OrderPlatform.ProcessEngine.Tests.Storage;

/// <summary>
/// Runs the engine's own mapping on an in-memory SQLite database: a run, its step executions and attempts
/// survive a save and a reload in a new unit of work, as they must for the worker to continue after a restart.
/// </summary>
public sealed class PipelineRunStoreTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new(typeof(PipelineRun).Assembly);
    private readonly ServiceProvider _services;
    private readonly StepJournal _journal = new();

    public PipelineRunStoreTests()
    {
        var two = new StepTwo(_journal);
        two.ThenExecute(StepResult.Retryable("busy"));

        _services = _database.Register(new ServiceCollection())
            .AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero)))
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(_journal)
            .AddSingleton(two)
            .AddPipeline(TestPipeline.Standard())
            .BuildServiceProvider();

        SqliteTestDatabase.Create(_services);
    }

    [Fact]
    public async Task Start_ThenSave_StoresTheRunPinnedToItsVersionWithItsContext()
    {
        var orderId = Guid.CreateVersion7();
        await InScopeAsync(async store =>
        {
            store.Start(TestPipeline.Standard(), orderId, new FlowContext { Name = "order" });
            await store.SaveChangesAsync(CancellationToken.None);
        });

        var run = await InScopeAsync(store => store.FindByBusinessKeyAsync(TestPipeline.PipelineName, orderId, CancellationToken.None));

        Assert.NotNull(run);
        Assert.Equal(1, run.PipelineVersion);
        Assert.Equal(RunStatus.Running, run.Status);
        Assert.Equal("order", EngineHarness.ContextOf(run).Name);
    }

    [Fact]
    public async Task Start_DoesNotSave_SoTheCallerSavesItWithItsOwnChanges()
    {
        var orderId = Guid.CreateVersion7();
        await InScopeAsync(store =>
        {
            store.Start(TestPipeline.Standard(), orderId, new FlowContext());
            return Task.CompletedTask;
        });

        var run = await InScopeAsync(store => store.FindByBusinessKeyAsync(TestPipeline.PipelineName, orderId, CancellationToken.None));

        Assert.Null(run);
    }

    [Fact]
    public async Task AdvanceAndSave_InSeparateUnitsOfWork_KeepsStepExecutionsAndAttempts()
    {
        var runId = await StartRunAsync();

        // Each call is a new unit of work, as in the worker: load, advance one step, save.
        for (var call = 0; call < 2; call++)
        {
            await InScopeAsync(async (store, executor) =>
            {
                var run = await store.FindAsync(runId, CancellationToken.None);
                await executor.AdvanceAsync(run!, CancellationToken.None);
                await store.SaveChangesAsync(CancellationToken.None);
            });
        }

        var stored = await InScopeAsync(store => store.FindAsync(runId, CancellationToken.None));

        Assert.NotNull(stored);
        Assert.Equal(1, stored.NextStepIndex);
        Assert.NotNull(stored.NextAttemptAt);
        Assert.Equal(StepExecutionStatus.Succeeded, stored.FindExecution(0, StepExecutionKind.Execute)!.Status);
        var retried = stored.FindExecution(1, StepExecutionKind.Execute)!;
        Assert.Equal(1, retried.AttemptCount);
        Assert.Equal("busy", retried.Attempts.Single().Error);
        Assert.Equal(["one"], EngineHarness.ContextOf(stored).Visited);
    }

    [Fact]
    public async Task Start_SecondRunForTheSameBusinessKey_IsRejectedByTheDatabase()
    {
        var orderId = Guid.CreateVersion7();
        await InScopeAsync(async store =>
        {
            store.Start(TestPipeline.Standard(), orderId, new FlowContext());
            await store.SaveChangesAsync(CancellationToken.None);
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => InScopeAsync(async store =>
        {
            store.Start(TestPipeline.Standard(), orderId, new FlowContext());
            await store.SaveChangesAsync(CancellationToken.None);
        }));
    }

    [Fact]
    public async Task FindAsync_UnknownRun_ReturnsNull() =>
        Assert.Null(await InScopeAsync(store => store.FindAsync(Guid.CreateVersion7(), CancellationToken.None)));

    public void Dispose()
    {
        _services.Dispose();
        _database.Dispose();
    }

    private async Task<Guid> StartRunAsync()
    {
        var runId = Guid.Empty;
        await InScopeAsync(async store =>
        {
            runId = store.Start(TestPipeline.Standard(), Guid.CreateVersion7(), new FlowContext()).Id;
            await store.SaveChangesAsync(CancellationToken.None);
        });
        return runId;
    }

    private async Task<T> InScopeAsync<T>(Func<IPipelineRunStore, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IPipelineRunStore>());
    }

    private async Task InScopeAsync(Func<IPipelineRunStore, Task> action)
    {
        await using var scope = _services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IPipelineRunStore>());
    }

    private async Task InScopeAsync(Func<IPipelineRunStore, IPipelineExecutor, Task> action)
    {
        await using var scope = _services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IPipelineRunStore>(), scope.ServiceProvider.GetRequiredService<IPipelineExecutor>());
    }
}
