using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DalSoft.Hosting.BackgroundQueue
{
    public class BackgroundQueue
    {
        private readonly Action<Exception> _onException;

        internal readonly ConcurrentQueue<Func<CancellationToken, Task>> TaskQueue = new ConcurrentQueue<Func<CancellationToken, Task>>();
        internal readonly int MaxConcurrentCount;
        internal readonly int MillisecondsToWaitBeforePickingUpTask;
        internal int ConcurrentCount;

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
            TaskQueue.Enqueue(task);
        }

        internal async Task Dequeue(CancellationToken cancellationToken)
        {
            if (TaskQueue.TryDequeue(out var nextTaskAction))
            {
                Interlocked.Increment(ref ConcurrentCount);
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
                    Interlocked.Decrement(ref ConcurrentCount);
                }
            }

            await Task.CompletedTask;
        }
    }
}
