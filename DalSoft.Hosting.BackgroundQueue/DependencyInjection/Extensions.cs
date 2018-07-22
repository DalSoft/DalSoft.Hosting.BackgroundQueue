using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DalSoft.Hosting.BackgroundQueue.DependencyInjection
{
    public static class Extensions
    {
        public static void AddBackgroundQueue(this IServiceCollection services, Action<Exception> onException, int maxConcurrentCount=1, int millisecondsToWaitBeforePickingUpTask = 1000)
        {
            services.AddSingleton(serivceProvider => new BackgroundQueue(((provider, exception) => onException(exception)), maxConcurrentCount, millisecondsToWaitBeforePickingUpTask, serivceProvider));
            services.AddSingleton<IHostedService, BackgroundQueueService>();
        }

        public static void AddBackgroundQueue(this IServiceCollection services, Action<IServiceProvider, Exception> onException, int maxConcurrentCount = 1, int millisecondsToWaitBeforePickingUpTask = 1000)
        {
            services.AddSingleton(serivceProvider => new BackgroundQueue(onException, maxConcurrentCount, millisecondsToWaitBeforePickingUpTask, serivceProvider));
            services.AddSingleton<IHostedService, BackgroundQueueService>();
        }
    }
}
