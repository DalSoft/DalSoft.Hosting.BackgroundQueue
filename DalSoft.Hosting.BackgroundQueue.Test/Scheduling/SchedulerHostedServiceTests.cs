using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DalSoft.Hosting.BackgroundQueue.Extensions.DependencyInjection;
using DalSoft.Hosting.BackgroundQueue.Scheduling;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DalSoft.Hosting.BackgroundQueue.Test.Scheduling;

public class SchedulerHostedServiceTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static (IServiceProvider provider, IJobScheduler scheduler, JobRecorder recorder, CountingScheduleStore store, FakeClock clock) Build()
    {
        var clock = new FakeClock(T0);
        var store = new CountingScheduleStore();
        var recorder = new JobRecorder();

        var services = new ServiceCollection();
        services.AddSingleton<ISystemClock>(clock);     // registered before AddBackgroundJobs so TryAdd keeps the fake
        services.AddSingleton<IScheduleStore>(store);
        services.AddSingleton(recorder);
        services.AddBackgroundJobs(options => options.TickInterval = TimeSpan.FromMilliseconds(20));

        var provider = services.BuildServiceProvider();
        return (provider, provider.GetRequiredService<IJobScheduler>(), recorder, store, clock);
    }

    private static SchedulerHostedService HostedService(IServiceProvider provider)
        => provider.GetServices<IHostedService>().OfType<SchedulerHostedService>().Single();

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (condition()) return true;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        return condition();
    }

    [Fact]
    public async Task ScheduledJob_Runs_WhenItBecomesDue()
    {
        var (provider, scheduler, recorder, _, clock) = Build();
        await scheduler.ScheduleAsync<RecordingInvocable>("everysecond", "* * * * * *"); // due at T0 + 1s

        var service = HostedService(provider);
        await service.StartAsync(CancellationToken.None);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(2)); // now past the first occurrence
            (await WaitUntilAsync(() => recorder.Runs >= 1, TimeSpan.FromSeconds(3))).Should().BeTrue();
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Tick_NeverPollsTheStore_OnlyLoadsOnceAtStartup()
    {
        var (provider, scheduler, recorder, store, clock) = Build();
        await scheduler.ScheduleAsync<RecordingInvocable>("everysecond", "* * * * * *");
        store.UpsertCount.Should().Be(1); // the schedule write-through

        var service = HostedService(provider);
        await service.StartAsync(CancellationToken.None); // single startup load
        try
        {
            // Advance the clock repeatedly so the every-second job becomes due several times,
            // exercising many tick iterations in between.
            for (var i = 0; i < 4; i++)
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                await Task.Delay(60, TestContext.Current.CancellationToken);
            }
            await WaitUntilAsync(() => recorder.Runs >= 3, TimeSpan.FromSeconds(2));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        recorder.Runs.Should().BeGreaterThanOrEqualTo(3); // fired on multiple ticks
        store.LoadCount.Should().Be(1);   // ONLY the startup load - the per-tick loop never touched the store
        store.UpsertCount.Should().Be(1); // no writes happened on the tick either
    }

    [Fact]
    public async Task ScheduledJob_ReceivesPayload_WhenItImplementsIInvocableWithPayload()
    {
        var (provider, scheduler, recorder, _, clock) = Build();
        await scheduler.ScheduleAsync<RecordingInvocableWithPayload>("withpayload", "* * * * * *", payload: "hello-from-store");

        var service = HostedService(provider);
        await service.StartAsync(CancellationToken.None);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(2));
            (await WaitUntilAsync(() => recorder.Payloads.Count >= 1, TimeSpan.FromSeconds(3))).Should().BeTrue();
            recorder.Payloads.Should().Contain("hello-from-store");
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task DynamicallyRemovedJob_StopsRunning_WithoutRestart()
    {
        var (provider, scheduler, recorder, _, clock) = Build();
        await scheduler.ScheduleAsync<RecordingInvocable>("everysecond", "* * * * * *");

        var service = HostedService(provider);
        await service.StartAsync(CancellationToken.None);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(2));
            (await WaitUntilAsync(() => recorder.Runs >= 1, TimeSpan.FromSeconds(3))).Should().BeTrue();

            await scheduler.RemoveAsync("everysecond");
            var runsAtRemoval = recorder.Runs;

            // Advance further; with the job removed it must not run again.
            clock.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(150, TestContext.Current.CancellationToken);

            recorder.Runs.Should().Be(runsAtRemoval);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }
}
