using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DalSoft.Hosting.BackgroundQueue;

public class BackgroundQueueService : BackgroundService
{
    private readonly BackgroundQueue _backgroundQueue;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public BackgroundQueueService(BackgroundQueue backgroundQueue, IServiceScopeFactory serviceScopeFactory)
    {
        _backgroundQueue = backgroundQueue;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken serviceStopCancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(_backgroundQueue.MillisecondsToWaitBeforePickingUpTask));

        try
        {
            // WaitForNextTickAsync observes the stop token, so we don't need to re-check IsCancellationRequested.
            while (await timer.WaitForNextTickAsync(serviceStopCancellationToken))
            {
                if (_backgroundQueue.ConcurrentCount < _backgroundQueue.MaxConcurrentCount)
                {
                    // ExecuteAsync is long-running while the background service is running, so we can't use default dependency injection behaviour.
                    // To prevent open resources and instances - scope services per run.
                    _backgroundQueue.Dequeue(serviceStopCancellationToken, _serviceScopeFactory);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on graceful shutdown when the stop token is cancelled.
        }
        finally
        {
            // Don't abandon tasks that are still running - let them drain (bounded by the host's shutdown timeout)
            // so their service scopes aren't disposed underneath them.
            await _backgroundQueue.WhenAllInFlightComplete();
        }
    }
}
