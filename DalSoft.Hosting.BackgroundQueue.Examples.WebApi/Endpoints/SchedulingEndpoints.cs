using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Jobs;
using DalSoft.Hosting.BackgroundQueue.Scheduling;

namespace DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Endpoints;

public static class SchedulingEndpoints
{
    public static void MapSchedulingEndpoints(this WebApplication app)
    {
        // List the currently scheduled jobs (in-memory snapshot, no database access).
        app.MapGet("/schedules", (IJobScheduler scheduler) => scheduler.List())
            .WithName("List schedules")
            .WithOpenApi();

        // Add a schedule at runtime. e.g. cron = "*/10 * * * * *" (every 10 seconds, 6-field) or "0 8 * * *" (08:00 daily).
        // The schedule is written through to the EF Core store, so it survives a restart.
        app.MapPost("/schedules/{key}", async (string key, string cron, IJobScheduler scheduler) =>
        {
            await scheduler.ScheduleAsync<AddRandomPersonJob>(key, cron);
            return Results.Ok(new { key, cron });
        })
        .WithName("Add or replace a schedule")
        .WithOpenApi();

        // Change an existing schedule's cron - takes effect on the next tick, no restart.
        app.MapPut("/schedules/{key}", async (string key, string cron, IJobScheduler scheduler) =>
        {
            await scheduler.RescheduleAsync(key, cron);
            return Results.Ok(new { key, cron });
        })
        .WithName("Reschedule")
        .WithOpenApi();

        // Remove a schedule - takes effect on the next tick, no restart.
        app.MapDelete("/schedules/{key}", async (string key, IJobScheduler scheduler) =>
        {
            var removed = await scheduler.RemoveAsync(key);
            return removed ? Results.Ok() : Results.NotFound();
        })
        .WithName("Remove a schedule")
        .WithOpenApi();
    }
}
