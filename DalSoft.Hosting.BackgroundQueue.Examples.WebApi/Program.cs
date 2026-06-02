using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Data;
using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Endpoints;
using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Extensions;
using DalSoft.Hosting.BackgroundQueue.Examples.WebApi.Scheduling;
using DalSoft.Hosting.BackgroundQueue.Extensions.DependencyInjection;
using DalSoft.Hosting.BackgroundQueue.Scheduling;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
builder.Logging.ClearProviders().AddConsole();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<PersonDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString(nameof(PersonDbContext))));

builder.Services.AddBackgroundQueue
(
    onException: (exception, serviceScope) =>
    {
        serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>()
        .Log(LogLevel.Error, exception, exception.Message);
    },
    maxConcurrentCount: 10
);

// Durable cron scheduler. Register the EF Core store BEFORE AddBackgroundJobs so it's used instead of the
// in-memory default. AddDbContextFactory keeps it singleton-safe (the store is resolved as a singleton).
builder.Services.AddDbContextFactory<ScheduleStoreDbContext>(options =>
    options.UseSqlServer(configuration.GetConnectionString(nameof(ScheduleStoreDbContext))));
builder.Services.AddSingleton<IScheduleStore, EfCoreScheduleStore>();
builder.Services.AddBackgroundJobs();

var app = builder.Build();

app.CreateDbIfNotExists();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapWeatherEndpoints();
app.MapSchedulingEndpoints();

app.Run();

