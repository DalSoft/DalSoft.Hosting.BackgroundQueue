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

        while (!serviceStopCancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(serviceStopCancellationToken))
        {
            if (_backgroundQueue.ConcurrentCount < _backgroundQueue.MaxConcurrentCount)
            {
                // ExecuteAsync is a long-running while the background service is running, so we can't use default dependency injection behaviour.
                // To prevent open resources and instances - scope services per run */
                // Create scope, so we get request services
                _backgroundQueue.Dequeue(serviceStopCancellationToken, _serviceScopeFactory);
            }
        }
    }
}
