#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DalSoft.Hosting.BackgroundQueue.Scheduling;

/// <summary>
/// Durable persistence for schedules - implement this to back schedules with your own store
/// (EF Core, Dapper, table storage, etc.). The default registration is an in-memory store.
/// </summary>
/// <remarks>
/// IMPORTANT: this is only touched at three points - never on the scheduler's tick:
/// <list type="number">
/// <item>once on startup, via <see cref="LoadAsync"/>;</item>
/// <item>when a schedule is added/changed/removed, via <see cref="UpsertAsync"/> / <see cref="RemoveAsync"/>;</item>
/// <item>when you explicitly call <see cref="IJobScheduler.ReloadFromStoreAsync"/> (ideally from inside a scheduled job).</item>
/// </list>
/// The per-tick scheduling loop runs entirely against the in-memory schedule, so a serverless/pay-per-use
/// database is never polled while idle.
/// </remarks>
public interface IScheduleStore
{
    /// <summary>Loads all persisted schedules. Called once at startup (and on an explicit reload).</summary>
    Task<IReadOnlyCollection<ScheduleDefinition>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Inserts or updates a schedule (write-through when a schedule is added or rescheduled).</summary>
    Task UpsertAsync(ScheduleDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>Removes a schedule by key (write-through when a schedule is removed).</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
