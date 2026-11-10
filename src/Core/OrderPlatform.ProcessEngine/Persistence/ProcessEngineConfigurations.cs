using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderPlatform.ProcessEngine.Runs;

namespace OrderPlatform.ProcessEngine.Persistence;

internal static class ProcessSchema
{
    public const string Name = "process";
}

internal sealed class PipelineRunConfiguration : IEntityTypeConfiguration<PipelineRun>
{
    public void Configure(EntityTypeBuilder<PipelineRun> builder)
    {
        builder.ToTable("PipelineRuns", ProcessSchema.Name);
        builder.Property(run => run.PipelineName).HasMaxLength(100);
        builder.Property(run => run.Reason).HasMaxLength(2000);
        builder.Property(run => run.LockedBy).HasMaxLength(200);
        builder.Property(run => run.RowVersion).IsRowVersion();
        builder.HasIndex(run => new { run.PipelineName, run.BusinessKey }).IsUnique();
        builder.HasIndex(run => new { run.Status, run.NextAttemptAt });
        builder.HasMany(run => run.Steps).WithOne().HasForeignKey(step => step.RunId);
        builder.Navigation(run => run.Steps).HasField("_steps");
    }
}

internal sealed class StepExecutionConfiguration : IEntityTypeConfiguration<StepExecution>
{
    public void Configure(EntityTypeBuilder<StepExecution> builder)
    {
        builder.ToTable("StepExecutions", ProcessSchema.Name);
        builder.Property(step => step.StepKey).HasMaxLength(100);
        builder.Property(step => step.LastError).HasMaxLength(2000);
        builder.HasIndex(step => new { step.RunId, step.StepIndex, step.Kind }).IsUnique();
        builder.HasMany(step => step.Attempts).WithOne().HasForeignKey(attempt => attempt.StepExecutionId);
        builder.Navigation(step => step.Attempts).HasField("_attempts");
    }
}

internal sealed class StepAttemptConfiguration : IEntityTypeConfiguration<StepAttempt>
{
    public void Configure(EntityTypeBuilder<StepAttempt> builder)
    {
        builder.ToTable("StepAttempts", ProcessSchema.Name);
        builder.Property(attempt => attempt.Error).HasMaxLength(2000);
    }
}
