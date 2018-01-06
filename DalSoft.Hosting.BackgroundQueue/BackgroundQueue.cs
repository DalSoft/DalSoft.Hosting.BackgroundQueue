using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DalSoft.Hosting.BackgroundQueue
{
    public class BackgroundQueue
    {
        private readonly Action<Exception> _onException;
        private readonly ConcurrentQueue<Func<Task>> _taskQueue = new ConcurrentQueue<Func<Task>>();
        private readonly int _maxConcurrentCount;
        private readonly int _millisecondsToWaitBeforePickingUpTask;
        private int _concurrentCount;
        
        public BackgroundQueue(Action<Exception> onException, int maxConcurrentCount, int millisecondsToWaitBeforePickingUpTask)
        {
            if (millisecondsToWaitBeforePickingUpTask < 500) throw new ArgumentException("< 500 Milliseconds will eat the CPU", nameof(millisecondsToWaitBeforePickingUpTask));
            if (maxConcurrentCount < 1) throw new ArgumentException("maxConcurrentCount must be at least 1", nameof(maxConcurrentCount));

            _onException = onException ?? (exception => { }); 
            _maxConcurrentCount = maxConcurrentCount;
            _millisecondsToWaitBeforePickingUpTask = millisecondsToWaitBeforePickingUpTask;
        }

        public void Enqueue(Func<Task> task)
        {
            _taskQueue.Enqueue(task);
        }

        internal Task Dequeue(CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                if (_taskQueue.Count==0 || _concurrentCount > _maxConcurrentCount)
                {
                    await Task.Delay(_millisecondsToWaitBeforePickingUpTask, cancellationToken); ;
                }

                if (_taskQueue.TryDequeue(out var nextTaskAction))
                {
                    Interlocked.Increment(ref _concurrentCount);
                    try
                    {
                        await nextTaskAction();
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
            }, cancellationToken);
        }
    }
}
