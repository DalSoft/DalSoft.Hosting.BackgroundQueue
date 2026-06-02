#nullable enable
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DalSoft.Hosting.BackgroundQueue.Scheduling;

/// <summary>
/// Drives the scheduler. Loads schedules from the store once at startup, then ticks against the in-memory
/// schedule only - the store/database is never polled while idle. Due jobs run in their own DI scope.
/// </summary>
internal sealed class SchedulerHostedService : BackgroundService
{
    private readonly JobScheduler _scheduler;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ISystemClock _clock;
    private readonly BackgroundJobsOptions _options;
    private readonly ConcurrentDictionary<Task, byte> _inFlight = new();

    public SchedulerHostedService(JobScheduler scheduler, IServiceScopeFactory serviceScopeFactory, ISystemClock clock, BackgroundJobsOptions options)
    {
        _scheduler = scheduler;
        _serviceScopeFactory = serviceScopeFactory;
        _clock = clock;
        _options = options;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // The single startup read of the store - hydrates the in-memory schedule.
        await _scheduler.ReloadFromStoreAsync(cancellationToken).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken serviceStopCancellationToken)
    {
        using PeriodicTimer timer = new(_options.TickInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(serviceStopCancellationToken))
            {
                RunDueJobs(serviceStopCancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on graceful shutdown.
        }
        finally
        {
            // Let running jobs drain (bounded by the host's shutdown timeout) rather than disposing their scopes underneath them.
            await Task.WhenAll(_inFlight.Keys.ToArray()).ConfigureAwait(false);
        }
    }

    private void RunDueJobs(CancellationToken serviceStopCancellationToken)
    {
        var now = _clock.UtcNow;

        foreach (var job in _scheduler.DueJobs(now))
        {
            if (!job.TryBeginRun())
            {
                continue; // already running - prevent overlap
            }

            // Advance immediately so the next occurrence is based on schedule, not on how long this run takes.
            job.AdvanceNextRun(now);

            var task = RunJobAsync(job, serviceStopCancellationToken);
            if (task.IsCompleted)
            {
                continue;
            }

            _inFlight[task] = 0;
            _ = task.ContinueWith(
                static (completed, state) => ((ConcurrentDictionary<Task, byte>)state!).TryRemove(completed, out _),
                _inFlight,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task RunJobAsync(ScheduledJob job, CancellationToken serviceStopCancellationToken)
    {
        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();

            var invocable = (IInvocable)(scope.ServiceProvider.GetService(job.ResolvedType)
                                         ?? ActivatorUtilities.CreateInstance(scope.ServiceProvider, job.ResolvedType));

            if (job.Payload is not null && invocable is IInvocableWithPayload withPayload)
            {
                withPayload.Payload = job.Payload;
            }

            await invocable.Invoke().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            try
            {
                _options.OnException?.Invoke(exception, job.Key);
            }
            catch
            {
                // The user's handler threw; swallow so it doesn't become an unobserved task exception.
            }
        }
        finally
        {
            job.EndRun();
        }
    }
}
