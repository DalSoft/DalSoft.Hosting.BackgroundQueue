using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DalSoft.Hosting.BackgroundQueue
{
    public class BackgroundQueue : IBackgroundQueue
    {
        private readonly Action<Exception> _onException;

        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _taskQueue = new ConcurrentQueue<Func<CancellationToken, Task>>();
        public int MaxConcurrentCount { get; }
        public int MillisecondsToWaitBeforePickingUpTask { get; }

        public int Count => _taskQueue.Count;

        public int ConcurrentCount => _concurrentCount;

        private int _concurrentCount;

        public BackgroundQueue(Action<Exception> onException, int maxConcurrentCount, int millisecondsToWaitBeforePickingUpTask)
        {
            //if (millisecondsToWaitBeforePickingUpTask < 500) throw new ArgumentException("< 500 Milliseconds will eat the CPU", nameof(millisecondsToWaitBeforePickingUpTask));
            if (maxConcurrentCount < 1) throw new ArgumentException("maxConcurrentCount must be at least 1", nameof(maxConcurrentCount));

            _onException = onException ?? (exception => { }); 
            MaxConcurrentCount = maxConcurrentCount;
            MillisecondsToWaitBeforePickingUpTask = millisecondsToWaitBeforePickingUpTask;
        }

        public void Enqueue(Func<CancellationToken, Task> task)
        {
            _taskQueue.Enqueue(task);
        }

        public async Task Dequeue(CancellationToken cancellationToken)
        {
            if (_taskQueue.TryDequeue(out var nextTaskAction))
            {
                Interlocked.Increment(ref _concurrentCount);
                try
                {
                    await nextTaskAction(cancellationToken);
                }
                catch (Exception e)
                {
                    _onException(e);
                }
                finally
                {
                    Interlocked.Decrement(ref _concurrentCount);
                }
            }

            await Task.CompletedTask;
        }
    }
}
