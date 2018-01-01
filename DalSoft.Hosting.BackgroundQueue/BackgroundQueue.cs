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
        private int _concurrentCount;

        public int MaxConcurrentCount { get; private set; }
        public int ThrottlingInMilliseconds { get; private set; }

        public BackgroundQueue(Action<Exception> onException, int maxConcurrentCount, int throttlingInMilliseconds)
        {
            _onException = onException;
            MaxConcurrentCount = maxConcurrentCount;
            ThrottlingInMilliseconds = throttlingInMilliseconds;
        }

        public void Enqueue(Func<Task> task)
        {
            _taskQueue.Enqueue(task);
        }

        internal Task Dequeue()
        {
            return Task.Run(async () =>
            {
                if ( !(_taskQueue.Count > 0 && MaxConcurrentCount > _concurrentCount) )
                    return;

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
                        if (ThrottlingInMilliseconds > 0)
                            await Task.Delay(ThrottlingInMilliseconds);

                        Interlocked.Decrement(ref _concurrentCount);
                    }
                }
            });
        }
    }
}
