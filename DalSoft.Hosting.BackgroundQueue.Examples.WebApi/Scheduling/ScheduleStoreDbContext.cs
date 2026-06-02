using Microsoft.EntityFrameworkCore;

namespace DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Scheduling;

/// <summary>
/// A dedicated context for schedules. It uses its own database so this sample can call EnsureCreated()
/// independently of the Person example (EnsureCreated is all-or-nothing per database).
/// </summary>
public class ScheduleStoreDbContext(DbContextOptions<ScheduleStoreDbContext> options) : DbContext(options)
{
    public DbSet<ScheduleRecord> Schedules => Set<ScheduleRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<ScheduleRecord>().HasKey(record => record.Key);
}
