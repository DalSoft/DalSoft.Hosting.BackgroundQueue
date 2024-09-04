using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Data;
using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Data.Entities;

namespace DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Endpoints;

public static class TestBackgroundQueueEndpoint
{
    public static void MapWeatherEndpoints(this WebApplication app)
    {
        app.MapPost("/backgroundtasks", (IBackgroundQueue backgroundQueue) =>
        {
            // queue background tasks
            for (var i = 0; i < 20; i++)
            {
                backgroundQueue.Enqueue(async ct =>
                {
                    await Task.Delay(60000, ct);
                });
            }
            return new { added = true };
        })
        .WithName("Add background Tasks")
        .WithOpenApi();
        
        // Also test injecting BackgroundQueue instead of IBackgroundQueue
        app.MapGet("/backgroundtasks", (BackgroundQueue backgroundQueue) => new { backgroundQueue.Count, backgroundQueue.ConcurrentCount })
            .WithName("Get Background Tasks")
            .WithOpenApi();

        app.MapPost("/backgroundtasks/dependencyinjection", (IBackgroundQueue backgroundQueue) =>
        {
            backgroundQueue.Enqueue(async (ct, serviceScope) =>
            {
                await Task.Delay(5000, ct);
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<PersonDbContext>();
                await dbContext.People.AddAsync(new Person
                {
                    FirstName = $"FirstName {Guid.NewGuid()}",
                    LastName = $"LastName {Guid.NewGuid()}"
                }, ct);
                await dbContext.SaveChangesAsync(ct);
            });
        })
        .WithName("Test Dependency Injection")
        .WithOpenApi();

        app.MapPost("/backgroundtasks/exception", (IBackgroundQueue backgroundQueue) =>
        {
            backgroundQueue.Enqueue(async ct => throw new Exception("Testing exceptions"));
        })
        .WithName("Test Exceptions Handling")
        .WithOpenApi();
    }
}
