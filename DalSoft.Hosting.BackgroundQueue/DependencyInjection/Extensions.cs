using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DalSoft.Hosting.BackgroundQueue.DependencyInjection
{
    public static class Extensions
    {
        public static void AddBackgroundQueue(this IServiceCollection services, Action<Exception> onException, int maxConcurrentCount=1, int millisecondsToWaitBeforePickingUpTask = 1000)
        {
            services.AddSingleton<IBackgroundQueue>(new BackgroundQueue(onException, maxConcurrentCount, millisecondsToWaitBeforePickingUpTask));
            services.AddSingleton<IHostedService, BackgroundQueueService>();
        }
    }
}
