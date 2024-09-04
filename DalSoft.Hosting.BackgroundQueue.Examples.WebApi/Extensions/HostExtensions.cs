using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Data;

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
                
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred creating the DB.");
        }
    }
}
