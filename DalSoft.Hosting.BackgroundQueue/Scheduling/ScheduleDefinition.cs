#nullable enable
namespace DalSoft.Hosting.BackgroundQueue.Scheduling;

/// <summary>
/// The durable representation of a scheduled job - a flat, serializable record an <see cref="IScheduleStore"/>
/// can persist (EF, Dapper, table storage, etc.). The job is identified by <see cref="InvocableType"/> rather
/// than a delegate so it can be rebuilt after a restart.
/// </summary>
public sealed record ScheduleDefinition
{
    /// <summary>Stable, unique identity used to reschedule or remove the job.</summary>
    public string Key { get; init; } = default!;

    /// <summary>A standard (5-field) or seconds-precision (6-field) cron expression.</summary>
    public string CronExpression { get; init; } = default!;

    /// <summary>The <see cref="IInvocable"/> type to run, as "Namespace.Type, AssemblyName" (version independent).</summary>
    public string InvocableType { get; init; } = default!;

    /// <summary>Optional data handed to the invocable if it implements <see cref="IInvocableWithPayload"/>.</summary>
    public string? Payload { get; init; }

    /// <summary>Optional time zone id the cron expression is evaluated in. Null means UTC.</summary>
    public string? TimeZoneId { get; init; }
}

/// <summary>A read-only snapshot of a live scheduled job, returned by <see cref="IJobScheduler.List"/>.</summary>
public sealed record ScheduledJobInfo(
    string Key,
    string CronExpression,
    string InvocableType,
    System.DateTime NextRunUtc,
    bool IsRunning);
