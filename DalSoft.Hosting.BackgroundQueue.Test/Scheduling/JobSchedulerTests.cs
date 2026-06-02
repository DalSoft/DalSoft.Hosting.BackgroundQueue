using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DalSoft.Hosting.BackgroundQueue.Scheduling;
using FluentAssertions;
using Xunit;

namespace DalSoft.Hosting.BackgroundQueue.Test.Scheduling;

public class JobSchedulerTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static (JobScheduler scheduler, CountingScheduleStore store, FakeClock clock) Create()
    {
        var store = new CountingScheduleStore();
        var clock = new FakeClock(T0);
        return (new JobScheduler(store, clock), store, clock);
    }

    [Fact]
    public async Task ScheduleAsync_AddsJob_ListsIt_AndWritesThroughToStore()
    {
        var (scheduler, store, _) = Create();

        await scheduler.ScheduleAsync<RecordingInvocable>("daily", "0 0 * * *");

        scheduler.Exists("daily").Should().BeTrue();
        scheduler.List().Should().ContainSingle(job => job.Key == "daily");
        store.UpsertCount.Should().Be(1);
        // next occurrence after 2026-01-01 00:00:00 for "daily at midnight" is the next day
        scheduler.List().Single().NextRunUtc.Should().Be(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ScheduleAsync_InvalidCron_Throws_AndDoesNotTouchStore()
    {
        var (scheduler, store, _) = Create();

        var act = () => scheduler.ScheduleAsync<RecordingInvocable>("bad", "not a cron");

        await act.Should().ThrowAsync<Exception>();
        store.UpsertCount.Should().Be(0);
        scheduler.Exists("bad").Should().BeFalse();
    }

    [Fact]
    public async Task ScheduleAsync_TypeThatIsNotInvocable_Throws()
    {
        var (scheduler, _, _) = Create();

        var act = () => scheduler.ScheduleAsync("oops", "* * * * *", typeof(string));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ScheduleAsync_SupportsSecondsPrecisionCron()
    {
        var (scheduler, _, _) = Create();

        await scheduler.ScheduleAsync<RecordingInvocable>("everysecond", "* * * * * *");

        scheduler.List().Single().NextRunUtc.Should().Be(T0.AddSeconds(1));
    }

    [Fact]
    public async Task RescheduleAsync_ChangesCron_RecomputesNextRun_AndWritesThrough()
    {
        var (scheduler, store, _) = Create();
        await scheduler.ScheduleAsync<RecordingInvocable>("job", "0 0 * * *"); // midnight -> 2026-01-02 00:00

        await scheduler.RescheduleAsync("job", "0 12 * * *"); // noon -> 2026-01-01 12:00

        scheduler.List().Single().NextRunUtc.Should().Be(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        store.UpsertCount.Should().Be(2);
    }

    [Fact]
    public async Task RescheduleAsync_UnknownKey_Throws()
    {
        var (scheduler, _, _) = Create();

        var act = () => scheduler.RescheduleAsync("nope", "0 0 * * *");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task RemoveAsync_RemovesFromMemoryAndStore()
    {
        var (scheduler, store, _) = Create();
        await scheduler.ScheduleAsync<RecordingInvocable>("job", "0 0 * * *");

        var removed = await scheduler.RemoveAsync("job");

        removed.Should().BeTrue();
        scheduler.Exists("job").Should().BeFalse();
        store.RemoveCount.Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_UnknownKey_ReturnsFalse()
    {
        var (scheduler, _, _) = Create();

        (await scheduler.RemoveAsync("nope")).Should().BeFalse();
    }

    [Fact]
    public async Task ReloadFromStoreAsync_RehydratesFromStore_AndDropsRemoved()
    {
        // Seed a store directly (as if a previous process / another writer persisted these).
        var store = new CountingScheduleStore();
        var clock = new FakeClock(T0);
        var seeder = new JobScheduler(store, clock);
        await seeder.ScheduleAsync<RecordingInvocable>("a", "0 0 * * *");
        await seeder.ScheduleAsync<RecordingInvocable>("b", "0 0 * * *");

        // A fresh scheduler instance starts empty, then loads from the same store.
        var scheduler = new JobScheduler(store, clock);
        scheduler.List().Should().BeEmpty();

        await scheduler.ReloadFromStoreAsync();
        scheduler.List().Select(j => j.Key).Should().BeEquivalentTo(new[] { "a", "b" });

        // Remove one from the store, reload, and confirm it's dropped in memory.
        await store.RemoveAsync("a");
        await scheduler.ReloadFromStoreAsync();
        scheduler.List().Select(j => j.Key).Should().BeEquivalentTo(new[] { "b" });
    }

    [Fact]
    public async Task DueJobs_ReturnsOnlyJobsWhoseNextRunHasPassed()
    {
        var (scheduler, _, clock) = Create();
        await scheduler.ScheduleAsync<RecordingInvocable>("everysecond", "* * * * * *"); // due at T0+1s

        scheduler.DueJobs(clock.UtcNow).Should().BeEmpty();          // nothing due at T0
        scheduler.DueJobs(T0.AddSeconds(1)).Select(j => j.Key).Should().ContainSingle().Which.Should().Be("everysecond");
    }
}
