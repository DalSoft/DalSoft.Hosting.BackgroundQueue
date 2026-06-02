#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DalSoft.Hosting.BackgroundQueue.Scheduling;

/// <summary>
/// In-memory cron scheduler with durable write-through. The <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// of jobs is the runtime source of truth; the tick loop only ever reads it. The <see cref="IScheduleStore"/>
/// is touched on load, on mutation, and on an explicit reload - never on the tick.
/// </summary>
internal sealed class JobScheduler : IJobScheduler
{
    private readonly ConcurrentDictionary<string, ScheduledJob> _jobs = new();
    private readonly IScheduleStore _store;
    private readonly ISystemClock _clock;

    public JobScheduler(IScheduleStore store, ISystemClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public Task ScheduleAsync<TInvocable>(string key, string cronExpression, string? payload = null, string? timeZoneId = null, CancellationToken cancellationToken = default)
        where TInvocable : IInvocable
        => ScheduleAsync(key, cronExpression, typeof(TInvocable), payload, timeZoneId, cancellationToken);

    public async Task ScheduleAsync(string key, string cronExpression, Type invocableType, string? payload = null, string? timeZoneId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));
        if (invocableType is null) throw new ArgumentNullException(nameof(invocableType));

        var definition = new ScheduleDefinition
        {
            Key = key,
            CronExpression = cronExpression,
            InvocableType = TypeName(invocableType),
            Payload = payload,
            TimeZoneId = timeZoneId
        };

        // Build first so an invalid cron / type / time zone throws before we persist anything.
        var job = ScheduledJob.Create(definition, _clock.UtcNow);

        // Store first, then memory: a crash between the two is recoverable on the next load.
        await _store.UpsertAsync(definition, cancellationToken).ConfigureAwait(false);
        _jobs[key] = job;
    }

    public async Task RescheduleAsync(string key, string cronExpression, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(key, out var existing))
        {
            throw new KeyNotFoundException($"No scheduled job with key '{key}'.");
        }

        var definition = existing.Definition with { CronExpression = cronExpression };
        var job = ScheduledJob.Create(definition, _clock.UtcNow);

        await _store.UpsertAsync(definition, cancellationToken).ConfigureAwait(false);
        _jobs[key] = job;
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _store.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        return _jobs.TryRemove(key, out _);
    }

    public bool Exists(string key) => _jobs.ContainsKey(key);

    public IReadOnlyCollection<ScheduledJobInfo> List() => _jobs.Values.Select(job => job.ToInfo()).ToArray();

    public async Task ReloadFromStoreAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;
        var keptKeys = new HashSet<string>();

        foreach (var definition in definitions)
        {
            keptKeys.Add(definition.Key);

            // Preserve a live job whose schedule is unchanged so we don't reset its NextRunUtc or running flag.
            if (_jobs.TryGetValue(definition.Key, out var existing) && existing.Definition == definition)
            {
                continue;
            }

            _jobs[definition.Key] = ScheduledJob.Create(definition, now);
        }

        // Drop jobs that no longer exist in the store.
        foreach (var key in _jobs.Keys.Where(key => !keptKeys.Contains(key)).ToArray())
        {
            _jobs.TryRemove(key, out _);
        }
    }

    // ----- consumed by SchedulerHostedService (same assembly); all in-memory, never touches the store -----

    internal IEnumerable<ScheduledJob> DueJobs(DateTime utcNow) => _jobs.Values.Where(job => job.IsDue(utcNow));

    private static string TypeName(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";
}
