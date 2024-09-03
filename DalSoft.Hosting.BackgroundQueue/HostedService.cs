using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace DalSoft.Hosting.BackgroundQueue
{
    // Example untested base class code kindly provided by David Fowl: https://gist.github.com/davidfowl/a7dd5064d9dcf35b6eae1a7953d615e3
    public abstract class HostedService : IHostedService
    {
        private Task _executingTask;
        private CancellationTokenSource _cancellationTokenSource;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Create a linked token, so we can trigger cancellation outside of this token's cancellation
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Store the task we're executing
            _executingTask = ExecuteAsync(_cancellationTokenSource.Token);

            // If the task is completed, then return it, otherwise it's running
            return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Stop called without a start
            if (_executingTask == null)
            {
                return;
            }

            // Signal cancellation to the executing method
            _cancellationTokenSource.Cancel();

            // Wait until the task completes or the stop token triggers
            await Task.WhenAny(_executingTask, Task.Delay(-1, cancellationToken));

            // Throw if cancellation triggered
            cancellationToken.ThrowIfCancellationRequested();
        }

        // Derived classes should override this and execute a long-running method until 
        // cancellation is requested
        protected abstract Task ExecuteAsync(CancellationToken cancellationToken);
    }
}
