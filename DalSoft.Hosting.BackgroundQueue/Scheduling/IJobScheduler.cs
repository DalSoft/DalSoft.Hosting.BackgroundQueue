#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DalSoft.Hosting.BackgroundQueue.Scheduling;

/// <summary>
/// Add, change and remove cron schedules at runtime - no restart required. Mutations write through to the
/// configured <see cref="IScheduleStore"/>; the running schedule lives in memory and is what the tick loop reads.
/// </summary>
public interface IJobScheduler
{
    /// <summary>Schedules <typeparamref name="TInvocable"/> under <paramref name="key"/>. Replaces any existing job with the same key.</summary>
    Task ScheduleAsync<TInvocable>(string key, string cronExpression, string? payload = null, string? timeZoneId = null, CancellationToken cancellationToken = default)
        where TInvocable : IInvocable;

    /// <summary>Schedules <paramref name="invocableType"/> (must implement <see cref="IInvocable"/>) under <paramref name="key"/>.</summary>
    Task ScheduleAsync(string key, string cronExpression, Type invocableType, string? payload = null, string? timeZoneId = null, CancellationToken cancellationToken = default);

    /// <summary>Changes the cron expression of an existing job. Takes effect on the next tick.</summary>
    Task RescheduleAsync(string key, string cronExpression, CancellationToken cancellationToken = default);

    /// <summary>Removes a job. Returns false if no job with that key existed. Takes effect on the next tick.</summary>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>True if a job with the given key is currently scheduled (in-memory; no store access).</summary>
    bool Exists(string key);

    /// <summary>A snapshot of the currently scheduled jobs (in-memory; no store access).</summary>
    IReadOnlyCollection<ScheduledJobInfo> List();

    /// <summary>
    /// Re-reads every schedule from the <see cref="IScheduleStore"/> and replaces the in-memory schedule.
    /// This is the only way to pick up schedule changes made directly in the store by another process.
    /// Call it sparingly (e.g. from inside a scheduled "sync" job) so an idle database is never polled.
    /// </summary>
    Task ReloadFromStoreAsync(CancellationToken cancellationToken = default);
}
