using FluentAssertions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DalSoft.Hosting.BackgroundQueue.Test
{
    public class MaxConcurrentCountTest
    {
        [Fact]
        public async Task ServiceShouldRunMaxConcurrentCountTaskWhenExistInQueue()
        {
            var tokenSource = new CancellationTokenSource();
            var queue = new BackgroundQueue(ex => throw ex, 10, 10); ;
            var queueService = new BackgroundQueueService(queue);
            var highwaterMark = 0;
            
            
            // queue background tasks
            for (var i = 0; i < 20; i++)
            {
                queue.Enqueue(async ct =>
                {
                    highwaterMark = Math.Max(queue.ConcurrentCount, highwaterMark);
                    await Task.Delay(5, ct);
                });
            }
            
            // process background tasks
            var runningService = Task.Run(async () => await queueService.StartAsync(tokenSource.Token), tokenSource.Token);
            
            // wait for all tasks to be processed
            while(queue.Count > 0)
            {
                await Task.Delay(20, tokenSource.Token);
            }
            
            // Check that tasks run concurrently up to the maxConcurrentCount.
            highwaterMark.Should().BeGreaterThan(1);
        }
    }
}
