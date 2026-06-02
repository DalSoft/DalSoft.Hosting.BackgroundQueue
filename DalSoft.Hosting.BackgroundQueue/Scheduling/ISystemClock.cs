#nullable enable
using System;

namespace DalSoft.Hosting.BackgroundQueue.Scheduling;

/// <summary>Abstracts the current time so the scheduler is deterministically testable.</summary>
public interface ISystemClock
{
    DateTime UtcNow { get; }
}

internal sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
