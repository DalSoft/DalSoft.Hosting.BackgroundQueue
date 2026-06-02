namespace DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Scheduling;

/// <summary>The persisted row for a schedule - a flat shape that maps 1:1 to ScheduleDefinition.</summary>
public class ScheduleRecord
{
    public string Key { get; set; } = default!;
    public string CronExpression { get; set; } = default!;
    public string InvocableType { get; set; } = default!;
    public string? Payload { get; set; }
    public string? TimeZoneId { get; set; }
}
