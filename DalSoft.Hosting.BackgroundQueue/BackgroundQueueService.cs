using System.Threading;
using System.Threading.Tasks;

namespace DalSoft.Hosting.BackgroundQueue
{
    public class BackgroundQueueService : HostedService
    {
        private readonly BackgroundQueue _backgroundQueue;

        public BackgroundQueueService(BackgroundQueue backgroundQueue)
        {
            _backgroundQueue = backgroundQueue;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_backgroundQueue.TaskQueue.Count == 0 || _backgroundQueue.ConcurrentCount > _backgroundQueue.MaxConcurrentCount)
                    await Task.Delay(_backgroundQueue.MillisecondsToWaitBeforePickingUpTask, cancellationToken);
                else
                    await _backgroundQueue.Dequeue(cancellationToken);
            }
        }
    }
}
