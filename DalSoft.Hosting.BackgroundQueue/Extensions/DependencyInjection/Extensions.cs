using System;
using Microsoft.Extensions.DependencyInjection;

namespace DalSoft.Hosting.BackgroundQueue.Extensions.DependencyInjection 
{
    public static class Extensions
    {
        public static void AddBackgroundQueue(this IServiceCollection services, Action<Exception, AsyncServiceScope> onException, int maxConcurrentCount = 1, int millisecondsToWaitBeforePickingUpTask = 10)
        {
            services.AddSingleton(new BackgroundQueue(onException, maxConcurrentCount, millisecondsToWaitBeforePickingUpTask));
            services.AddSingleton<IBackgroundQueue>(provider => provider.GetService<BackgroundQueue>());
            services.AddHostedService<BackgroundQueueService>();
        }
    }
}

// keep usage backwards compatible with < DalSoft.Hosting.BackgroundQueue v2.0.0
namespace DalSoft.Hosting.BackgroundQueue.DependencyInjection
{
    public static class Extensions
    {
        public static void AddBackgroundQueue(this IServiceCollection services, Action<Exception> onException, int maxConcurrentCount = 1, int millisecondsToWaitBeforePickingUpTask = 10)
        {
            // keep usage backwards compatible with < DalSoft.Hosting.BackgroundQueue v2.0.0
            services.AddSingleton(new BackgroundQueue((exception, _) => onException(exception), maxConcurrentCount, millisecondsToWaitBeforePickingUpTask));
            services.AddSingleton<IBackgroundQueue>(provider => provider.GetService<BackgroundQueue>());
            services.AddHostedService<BackgroundQueueService>();
        }
    }
}
