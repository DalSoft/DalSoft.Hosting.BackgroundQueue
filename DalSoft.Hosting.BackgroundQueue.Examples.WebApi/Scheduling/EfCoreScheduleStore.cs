using DalSoft.Hosting.BackgroundQueue.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Scheduling;

/// <summary>
/// A durable <see cref="IScheduleStore"/> backed by EF Core / SQL Server. Drop-in: register it before
/// AddBackgroundJobs and your schedules survive restarts.
///
/// Note it takes an <see cref="IDbContextFactory{TContext}"/>, not a DbContext. The scheduler resolves the
/// store as a singleton, and a singleton must not capture a scoped DbContext - the factory hands us a short
/// lived context per call instead. This store is only hit on startup load and on add/change/remove, never on
/// the scheduler's tick, so an idle (e.g. serverless) database is never polled.
/// </summary>
public sealed class EfCoreScheduleStore(IDbContextFactory<ScheduleStoreDbContext> contextFactory) : IScheduleStore
{
    public async Task<IReadOnlyCollection<ScheduleDefinition>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.Schedules.AsNoTracking().ToListAsync(cancellationToken);

        return rows.Select(row => new ScheduleDefinition
        {
            Key = row.Key,
            CronExpression = row.CronExpression,
            InvocableType = row.InvocableType,
            Payload = row.Payload,
            TimeZoneId = row.TimeZoneId
        }).ToArray();
    }

    public async Task UpsertAsync(ScheduleDefinition definition, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var row = await db.Schedules.FindAsync([definition.Key], cancellationToken);
        if (row is null)
        {
            db.Schedules.Add(new ScheduleRecord
            {
                Key = definition.Key,
                CronExpression = definition.CronExpression,
                InvocableType = definition.InvocableType,
                Payload = definition.Payload,
                TimeZoneId = definition.TimeZoneId
            });
        }
        else
        {
            row.CronExpression = definition.CronExpression;
            row.InvocableType = definition.InvocableType;
            row.Payload = definition.Payload;
            row.TimeZoneId = definition.TimeZoneId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);

        var row = await db.Schedules.FindAsync([key], cancellationToken);
        if (row is not null)
        {
            db.Schedules.Remove(row);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
