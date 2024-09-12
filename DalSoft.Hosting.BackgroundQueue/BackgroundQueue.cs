using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace DalSoft.Hosting.BackgroundQueue;

public class BackgroundQueue : IBackgroundQueue
{
    public int MaxConcurrentCount { get; }
    public int MillisecondsToWaitBeforePickingUpTask { get; }
    public int Count => _taskQueue.Count;
    public int ConcurrentCount => _concurrentCount;

    private int _concurrentCount;
    private readonly Action<Exception, AsyncServiceScope > _onException;
    private readonly ConcurrentQueue<Func<CancellationToken, AsyncServiceScope, Task>> _taskQueue = new();
    
    // This is needed to keep things testable as CreateAsyncScope is at extension method.
    private readonly Func<AsyncServiceScope> _fakeCreateAsyncScope;
    internal BackgroundQueue(Action<Exception, AsyncServiceScope> onException, int maxConcurrentCount, int millisecondsToWaitBeforePickingUpTask, Func<AsyncServiceScope> fakeCreateAsyncScope): 
        this(onException, maxConcurrentCount, millisecondsToWaitBeforePickingUpTask)
    {
        _fakeCreateAsyncScope = fakeCreateAsyncScope;
    }
    
    internal const string MaxConcurrentCountExceptionMessage = "maxConcurrentCount must be at least 1";
    internal const string MillisecondsToWaitBeforePickingUpTaskExceptionMessage = "millisecondsToWaitBeforePickingUpTask cannot be < 10 Milliseconds";
    
    public BackgroundQueue(Action<Exception, AsyncServiceScope> onException, int maxConcurrentCount, int millisecondsToWaitBeforePickingUpTask)
    {
        if (maxConcurrentCount < 1)
        {
            throw new ArgumentException("maxConcurrentCount must be at least 1", nameof(maxConcurrentCount));
        }
        
        if (millisecondsToWaitBeforePickingUpTask < 10)
        {
            throw new ArgumentException("millisecondsToWaitBeforePickingUpTask cannot be < 10 Milliseconds", nameof(millisecondsToWaitBeforePickingUpTask));
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
            try
            {
                await ProcessBackgroundTask(serviceStopCancellationToken, serviceScopeFactory, nextTaskAction);
            }
            catch (Exception e)
            {
                // *very* unlikely to happen, but if serviceScopeFactory.CreateAsyncScope() ever fails, we have no way letting the user know their task failed.
                _onException(e, new AsyncServiceScope(new ServiceScopeWithException()));
            }
            finally
            {
                Interlocked.Decrement(ref _concurrentCount);
            }
        }

        await Task.CompletedTask;
    }

    private async Task ProcessBackgroundTask(CancellationToken serviceStopCancellationToken, IServiceScopeFactory serviceScopeFactory, Func<CancellationToken, AsyncServiceScope, Task> nextTaskAction)
    {
        Interlocked.Increment(ref _concurrentCount);
        await using var asyncScope = _fakeCreateAsyncScope?.Invoke() ?? serviceScopeFactory.CreateAsyncScope();
        try
        {
            await nextTaskAction(serviceStopCancellationToken, asyncScope);
        }
        catch (Exception e)
        {
            _onException(e, asyncScope);
        }
    }
}
