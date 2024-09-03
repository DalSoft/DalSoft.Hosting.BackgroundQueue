using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DalSoft.Hosting.BackgroundQueue
{
    public class BackgroundQueueService : HostedService
    {
        private readonly IBackgroundQueue _backgroundQueue;

        public BackgroundQueueService(IBackgroundQueue backgroundQueue)
        {
            _backgroundQueue = backgroundQueue;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_backgroundQueue.Count == 0 || _backgroundQueue.ConcurrentCount > _backgroundQueue.MaxConcurrentCount)
                {
                    await Task.Delay(_backgroundQueue.MillisecondsToWaitBeforePickingUpTask, cancellationToken);
                }
                else
                {
                    var concurrentTasks = new List<Task>();
                    while (_backgroundQueue.Count > 0 && _backgroundQueue.ConcurrentCount <= _backgroundQueue.MaxConcurrentCount)
                    {
                        concurrentTasks.Add(_backgroundQueue.Dequeue(cancellationToken));
                    }
                    await Task.WhenAll(concurrentTasks);
                }
            }
        }
    }
}
