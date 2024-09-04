using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace DalSoft.Hosting.BackgroundQueue;

public class BackgroundQueue : IBackgroundQueue
{
    private readonly Action<Exception, AsyncServiceScope > _onException;

    private readonly ConcurrentQueue<Func<CancellationToken, AsyncServiceScope, Task>> _taskQueue = new();
    public int MaxConcurrentCount { get; }
    public int MillisecondsToWaitBeforePickingUpTask { get; }

    public int Count => _taskQueue.Count;

    public int ConcurrentCount => _concurrentCount;

    private int _concurrentCount;

    public BackgroundQueue(Action<Exception, AsyncServiceScope> onException, int maxConcurrentCount, int millisecondsToWaitBeforePickingUpTask)
    {
        if (maxConcurrentCount < 1)
        {
            throw new ArgumentException("maxConcurrentCount must be at least 1", nameof(maxConcurrentCount));
        }
        
        if (millisecondsToWaitBeforePickingUpTask < 500)
        {
            throw new ArgumentException("millisecondsToWaitBeforePickingUpTask cannot be < 500 Milliseconds", nameof(millisecondsToWaitBeforePickingUpTask));
        }

        _onException = onException ?? ((ex, scope) => { }); 
        MaxConcurrentCount = maxConcurrentCount;
        MillisecondsToWaitBeforePickingUpTask = millisecondsToWaitBeforePickingUpTask;
    }

    public void Enqueue(Func<CancellationToken, AsyncServiceScope, Task> task)
    {
        _taskQueue.Enqueue(task);
    }
        
    public void Enqueue(Func<CancellationToken, Task> task)
    {
        // keep usage backwards compatible with < DalSoft.Hosting.BackgroundQueue v2.0.0
        _taskQueue.Enqueue((token, _) => task(token));
    }
        
    internal async Task Dequeue(CancellationToken serviceStopCancellationToken, IServiceScopeFactory serviceScopeFactory)
    {
        if (_taskQueue.TryDequeue(out var nextTaskAction))
        {
            Interlocked.Increment(ref _concurrentCount);
            await using var asyncScope = serviceScopeFactory.CreateAsyncScope();
            try
            {
                await nextTaskAction(serviceStopCancellationToken, asyncScope);
            }
            catch (Exception e)
            {
                _onException(e, asyncScope);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentCount);
            }
        }

        await Task.CompletedTask;
    }
}
