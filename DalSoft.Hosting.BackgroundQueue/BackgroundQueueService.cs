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

        protected override Task ExecuteAsync(CancellationToken serviceStopCancellationToken)
        {
            var timer = new System.Timers.Timer(_backgroundQueue.MillisecondsToWaitBeforePickingUpTask);
            timer.Elapsed += (sender, args) =>
            {
                if (serviceStopCancellationToken.IsCancellationRequested)
                {
                    timer.Stop();
                }

                if (!serviceStopCancellationToken.IsCancellationRequested && _backgroundQueue.ConcurrentCount < _backgroundQueue.MaxConcurrentCount) 
                {
                    _backgroundQueue.Dequeue(serviceStopCancellationToken);
                }
            };
            timer.AutoReset = true;
            timer.Start();
            
            return Task.CompletedTask;
        }
    }
}
