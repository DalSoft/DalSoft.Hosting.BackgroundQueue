using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Data;
using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Data.Entities;
using DalSoft.Hosting.BackgroundQueue.Scheduling;

namespace DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Jobs;

/// <summary>
/// A scheduled job. It's resolved from DI per run, so it can take dependencies via the constructor -
/// here <see cref="IBackgroundQueue"/>. The scheduler decides WHEN this runs; the actual work is handed to
/// the throttled queue, which decides HOW it runs (its own scope, concurrency limit, exception handling).
/// </summary>
public sealed class AddRandomPersonJob(IBackgroundQueue backgroundQueue) : IInvocable
{
    public Task Invoke()
    {
        backgroundQueue.Enqueue(async (cancellationToken, serviceScope) =>
        {
            var dbContext = serviceScope.ServiceProvider.GetRequiredService<PersonDbContext>();
            await dbContext.People.AddAsync(new Person
            {
                FirstName = $"Scheduled {Guid.NewGuid()}",
                LastName = "Person"
            }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        });

        return Task.CompletedTask;
    }
}
