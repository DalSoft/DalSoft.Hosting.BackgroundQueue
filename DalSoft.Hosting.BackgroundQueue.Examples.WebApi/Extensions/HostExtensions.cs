using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Data;
using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Extensions;

public static class HostExtensions
{
    public static void CreateDbIfNotExists(this IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<PersonDbContext>();
            context.Database.EnsureCreated();

            // The schedule store uses its own database (see ScheduleStoreDbContext), created independently.
            using var scheduleContext = services.GetRequiredService<IDbContextFactory<ScheduleStoreDbContext>>().CreateDbContext();
            scheduleContext.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred creating the DB.");
        }
    }
}
