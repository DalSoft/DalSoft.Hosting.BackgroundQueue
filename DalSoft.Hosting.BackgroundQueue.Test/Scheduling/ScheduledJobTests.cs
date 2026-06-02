using System;
using DalSoft.Hosting.BackgroundQueue.Scheduling;
using FluentAssertions;
using Xunit;

namespace DalSoft.Hosting.BackgroundQueue.Test.Scheduling;

public class ScheduledJobTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static ScheduledJob NewJob(string cron = "* * * * * *") => ScheduledJob.Create(
        new ScheduleDefinition
        {
            Key = "k",
            CronExpression = cron,
            InvocableType = $"{typeof(RecordingInvocable).FullName}, {typeof(RecordingInvocable).Assembly.GetName().Name}"
        },
        T0);

    [Fact]
    public void TryBeginRun_PreventsOverlap_UntilEndRun()
    {
        var job = NewJob();

        job.TryBeginRun().Should().BeTrue();   // first claim wins
        job.TryBeginRun().Should().BeFalse();  // already running - overlap blocked
        job.IsRunning.Should().BeTrue();

        job.EndRun();

        job.IsRunning.Should().BeFalse();
        job.TryBeginRun().Should().BeTrue();   // can run again after finishing
    }

    [Fact]
    public void AdvanceNextRun_MovesToNextOccurrenceAfterTheGivenTime()
    {
        var job = NewJob("0 0 * * *"); // daily midnight; created at T0 (midnight) => next is the following day
        job.NextRunUtc.Should().Be(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        job.AdvanceNextRun(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
        job.NextRunUtc.Should().Be(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Create_ResolvesTimeZone_AndEvaluatesCronInIt()
    {
        // 30 minutes past midnight, evaluated in a +05:30 zone, is 19:00 UTC the previous day-relative.
        var tz = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "India Standard Time" : "Asia/Kolkata");
        var job = ScheduledJob.Create(
            new ScheduleDefinition
            {
                Key = "tz",
                CronExpression = "0 0 * * *", // local midnight in IST
                InvocableType = $"{typeof(RecordingInvocable).FullName}, {typeof(RecordingInvocable).Assembly.GetName().Name}",
                TimeZoneId = tz.Id
            },
            T0);

        // IST midnight on 2026-01-01 is 2025-12-31 18:30 UTC (already past T0), so next is 2026-01-01 18:30 UTC.
        job.NextRunUtc.Should().Be(new DateTime(2026, 1, 1, 18, 30, 0, DateTimeKind.Utc));
    }
}
