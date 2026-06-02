#nullable enable
using System;

namespace DalSoft.Hosting.BackgroundQueue.Scheduling;

/// <summary>Options for the cron scheduler, configured via AddBackgroundJobs.</summary>
public sealed class BackgroundJobsOptions
{
    /// <summary>
    /// How often the scheduler checks the in-memory schedule for due jobs. Defaults to one second.
    /// This is an in-memory check only - it never touches the <see cref="IScheduleStore"/> / database.
    /// Keep it at or below the smallest precision you schedule (e.g. 1s if you use seconds-precision cron).
    /// </summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Called when a scheduled invocable throws. The string is the job key. If null, exceptions from
    /// invocables are swallowed (other jobs continue), mirroring the queue's per-task isolation.
    /// </summary>
    public Action<Exception, string>? OnException { get; set; }
}
