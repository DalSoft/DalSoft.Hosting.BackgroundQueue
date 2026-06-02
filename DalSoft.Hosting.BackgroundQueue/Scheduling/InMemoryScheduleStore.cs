#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DalSoft.Hosting.BackgroundQueue.Scheduling;

/// <summary>
/// Default <see cref="IScheduleStore"/>. Keeps schedules in process memory only, so dynamic changes are
/// lost on restart. Swap in your own <see cref="IScheduleStore"/> to make schedules durable.
/// </summary>
public sealed class InMemoryScheduleStore : IScheduleStore
{
    private readonly ConcurrentDictionary<string, ScheduleDefinition> _store = new();

    public Task<IReadOnlyCollection<ScheduleDefinition>> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<ScheduleDefinition>>(_store.Values.ToArray());

    public Task UpsertAsync(ScheduleDefinition definition, CancellationToken cancellationToken = default)
    {
        _store[definition.Key] = definition;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
