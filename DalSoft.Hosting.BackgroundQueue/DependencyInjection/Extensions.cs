using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DalSoft.Hosting.BackgroundQueue.DependencyInjection
{
    public static class Extensions
    {
        public static void AddBackgroundQueue(this IServiceCollection services, Action<Exception> onException, int maxConcurrentCount=1, int throttlingInMilliseconds = 1000)
        {
            services.AddSingleton(new BackgroundQueue(onException, maxConcurrentCount, throttlingInMilliseconds));
            services.AddSingleton<IHostedService, BackgroundQueueService>();
        }
    }
}
