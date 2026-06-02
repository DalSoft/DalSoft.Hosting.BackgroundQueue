#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DalSoft.Hosting.BackgroundQueue.Scheduling;

namespace DalSoft.Hosting.BackgroundQueue.Test.Scheduling;

/// <summary>A clock the test drives directly so scheduling is deterministic regardless of the real tick cadence.</summary>
internal sealed class FakeClock : ISystemClock
{
    private long _ticks;

    public FakeClock(DateTime start) => _ticks = start.Ticks;

    public DateTime UtcNow => new(Volatile.Read(ref _ticks), DateTimeKind.Utc);

    public void Set(DateTime utc) => Volatile.Write(ref _ticks, utc.Ticks);

    public void Advance(TimeSpan delta) => Volatile.Write(ref _ticks, Volatile.Read(ref _ticks) + delta.Ticks);
}

/// <summary>Wraps an in-memory store and counts each call so tests can assert the store is never polled on the tick.</summary>
internal sealed class CountingScheduleStore : IScheduleStore
{
    private readonly InMemoryScheduleStore _inner = new();

    public int LoadCount;
    public int UpsertCount;
    public int RemoveCount;

    public Task<IReadOnlyCollection<ScheduleDefinition>> LoadAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref LoadCount);
        return _inner.LoadAsync(cancellationToken);
    }

    public Task UpsertAsync(ScheduleDefinition definition, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref UpsertCount);
        return _inner.UpsertAsync(definition, cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref RemoveCount);
        return _inner.RemoveAsync(key, cancellationToken);
    }
}

/// <summary>Shared, DI-injected sink so invocables can record what ran across per-run scopes.</summary>
public sealed class JobRecorder
{
    private int _runs;
    private readonly object _gate = new();
    private readonly List<string> _payloads = new();

    public int Runs => Volatile.Read(ref _runs);
    public IReadOnlyList<string> Payloads { get { lock (_gate) return _payloads.ToArray(); } }

    public void Record(string? payload)
    {
        Interlocked.Increment(ref _runs);
        if (payload is not null)
        {
            lock (_gate) _payloads.Add(payload);
        }
    }
}

public sealed class RecordingInvocable : IInvocable
{
    private readonly JobRecorder _recorder;
    public RecordingInvocable(JobRecorder recorder) => _recorder = recorder;

    public Task Invoke()
    {
        _recorder.Record(null);
        return Task.CompletedTask;
    }
}

public sealed class RecordingInvocableWithPayload : IInvocable, IInvocableWithPayload
{
    private readonly JobRecorder _recorder;
    public RecordingInvocableWithPayload(JobRecorder recorder) => _recorder = recorder;

    public string? Payload { get; set; }

    public Task Invoke()
    {
        _recorder.Record(Payload);
        return Task.CompletedTask;
    }
}
