#nullable enable
using System;
using System.Threading;
using Cronos;

namespace DalSoft.Hosting.BackgroundQueue.Scheduling;

/// <summary>
/// The live, in-memory representation of a scheduled job. Created from a <see cref="ScheduleDefinition"/>
/// with its cron expression and target type pre-parsed/resolved so the tick loop does no parsing or I/O.
/// </summary>
internal sealed class ScheduledJob
{
    /// <summary>Sentinel for a cron expression that will never fire again (e.g. a one-off date in the past).</summary>
    public static readonly DateTime Never = DateTime.MaxValue;

    private int _running;

    private ScheduledJob(ScheduleDefinition definition, CronExpression cron, TimeZoneInfo timeZone, Type resolvedType)
    {
        Definition = definition;
        Cron = cron;
        TimeZone = timeZone;
        ResolvedType = resolvedType;
    }

    public ScheduleDefinition Definition { get; }
    public CronExpression Cron { get; }
    public TimeZoneInfo TimeZone { get; }
    public Type ResolvedType { get; }

    public string Key => Definition.Key;
    public string? Payload => Definition.Payload;

    public DateTime NextRunUtc { get; private set; }

    public bool IsRunning => Volatile.Read(ref _running) == 1;

    /// <summary>Atomically claims the job for a run; returns false if it's already running (overlap prevention).</summary>
    public bool TryBeginRun() => Interlocked.CompareExchange(ref _running, 1, 0) == 0;

    public void EndRun() => Volatile.Write(ref _running, 0);

    public bool IsDue(DateTime utcNow) => NextRunUtc <= utcNow;

    /// <summary>Advances <see cref="NextRunUtc"/> to the next occurrence strictly after <paramref name="fromUtc"/>.</summary>
    public void AdvanceNextRun(DateTime fromUtc)
    {
        var next = Cron.GetNextOccurrence(new DateTimeOffset(fromUtc, TimeSpan.Zero), TimeZone);
        NextRunUtc = next?.UtcDateTime ?? Never;
    }

    public ScheduledJobInfo ToInfo() => new(Key, Definition.CronExpression, Definition.InvocableType, NextRunUtc, IsRunning);

    /// <summary>
    /// Builds a runnable job from a stored definition: parses the cron (5 or 6 field), resolves the time zone
    /// and the <see cref="IInvocable"/> type, and computes the first <see cref="NextRunUtc"/> from now.
    /// Throws on an invalid cron expression, unknown time zone, or a type that isn't an <see cref="IInvocable"/>.
    /// </summary>
    public static ScheduledJob Create(ScheduleDefinition definition, DateTime utcNow)
    {
        var cron = ParseCron(definition.CronExpression);
        var timeZone = definition.TimeZoneId is null
            ? TimeZoneInfo.Utc
            : TimeZoneInfo.FindSystemTimeZoneById(definition.TimeZoneId);

        var type = Type.GetType(definition.InvocableType, throwOnError: true)!;
        if (!typeof(IInvocable).IsAssignableFrom(type))
        {
            throw new ArgumentException($"Type '{definition.InvocableType}' does not implement {nameof(IInvocable)}.", nameof(definition));
        }

        var job = new ScheduledJob(definition, cron, timeZone, type);
        job.AdvanceNextRun(utcNow);
        return job;
    }

    private static CronExpression ParseCron(string expression)
    {
        var fieldCount = expression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return fieldCount == 6
            ? CronExpression.Parse(expression, CronFormat.IncludeSeconds)
            : CronExpression.Parse(expression);
    }
}
