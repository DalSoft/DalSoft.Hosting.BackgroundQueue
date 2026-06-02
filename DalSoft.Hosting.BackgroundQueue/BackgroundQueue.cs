using System;
using System.Collections.Concurrent;
using System.Linq;
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

    // Tracks the tasks currently being processed so the host can drain them on shutdown.
    private readonly ConcurrentDictionary<Task, byte> _inFlight = new();

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
            throw new ArgumentException(MaxConcurrentCountExceptionMessage, nameof(maxConcurrentCount));
        }

        if (millisecondsToWaitBeforePickingUpTask < 10)
        {
            throw new ArgumentException(MillisecondsToWaitBeforePickingUpTaskExceptionMessage, nameof(millisecondsToWaitBeforePickingUpTask));
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

    internal void Dequeue(CancellationToken serviceStopCancellationToken, IServiceScopeFactory serviceScopeFactory)
    {
        if (!_taskQueue.TryDequeue(out var nextTaskAction))
        {
            return;
        }

        // Kick off processing and track it so the service can wait for in-flight work to drain on shutdown.
        var processing = ProcessBackgroundTask(serviceStopCancellationToken, serviceScopeFactory, nextTaskAction);

        if (processing.IsCompleted)
        {
            return;
        }

        _inFlight[processing] = 0;
        _ = processing.ContinueWith(
            static (completed, state) => ((ConcurrentDictionary<Task, byte>)state!).TryRemove(completed, out _),
            _inFlight,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    // Awaited by the host on shutdown so in-flight tasks aren't abandoned (and their scopes disposed underneath them).
    internal Task WhenAllInFlightComplete() => Task.WhenAll(_inFlight.Keys.ToArray());

    private async Task ProcessBackgroundTask(CancellationToken serviceStopCancellationToken, IServiceScopeFactory serviceScopeFactory, Func<CancellationToken, AsyncServiceScope, Task> nextTaskAction)
    {
        Interlocked.Increment(ref _concurrentCount);
        try
        {
            try
            {
                await using var asyncScope = _fakeCreateAsyncScope?.Invoke() ?? serviceScopeFactory.CreateAsyncScope();
                try
                {
                    await nextTaskAction(serviceStopCancellationToken, asyncScope);
                }
                catch (Exception e)
                {
                    SafeOnException(e, asyncScope);
                }
            }
            catch (Exception e)
            {
                // *very* unlikely to happen, but if serviceScopeFactory.CreateAsyncScope() ever fails, we still let the user know their task failed.
                SafeOnException(e, new AsyncServiceScope(new ServiceScopeWithException()));
            }
        }
        finally
        {
            Interlocked.Decrement(ref _concurrentCount);
        }
    }

    private void SafeOnException(Exception exception, AsyncServiceScope scope)
    {
        try
        {
            _onException(exception, scope);
        }
        catch
        {
            // The user's onException handler threw. There's nowhere left to surface this, and we must not
            // let it escape onto the processing task (where it would become an unobserved task exception).
        }
    }
}
