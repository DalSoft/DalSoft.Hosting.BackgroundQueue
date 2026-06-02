#nullable enable
using System;
using DalSoft.Hosting.BackgroundQueue.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Same namespace as AddBackgroundQueue so both are discoverable from one using directive.
namespace DalSoft.Hosting.BackgroundQueue.Extensions.DependencyInjection
{
    public static class BackgroundJobsServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the dynamic cron scheduler. Resolve <see cref="IJobScheduler"/> to add, reschedule or remove
        /// jobs at runtime. Register your own <see cref="IScheduleStore"/> before or after this call to make
        /// schedules durable; otherwise an in-memory store is used.
        /// </summary>
        /// <remarks>
        /// Additive and independent of AddBackgroundQueue - calling this does not change queue behaviour, and
        /// the queue can be used without the scheduler. A common pattern is a scheduled <see cref="IInvocable"/>
        /// that injects <see cref="IBackgroundQueue"/> and enqueues the heavy work onto the throttled queue.
        /// </remarks>
        public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, Action<BackgroundJobsOptions>? configure = null)
        {
            var options = new BackgroundJobsOptions();
            configure?.Invoke(options);

            services.AddSingleton(options);
            services.TryAddSingleton<ISystemClock, SystemClock>();
            services.TryAddSingleton<IScheduleStore, InMemoryScheduleStore>();
            services.AddSingleton<JobScheduler>();
            services.AddSingleton<IJobScheduler>(provider => provider.GetRequiredService<JobScheduler>());
            services.AddHostedService<SchedulerHostedService>();

            return services;
        }
    }
}
