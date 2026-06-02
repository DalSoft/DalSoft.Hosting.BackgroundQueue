### `If you find this repo / package useful all I ask is you please star it ⭐`
> ### Do you or the company you work for benefit from the tools I build? <br /> If so please consider [Becoming a Sponsor](https://github.com/sponsors/dalsoft) it would be greatly appreciated ❤️ 

# DalSoft.Hosting.BackgroundQueue

DalSoft.Hosting.BackgroundQueue is a very lightweight .NET background-jobs library - a focused, low-dependency alternative to Hangfire for in-memory, single-instance work. It gives you two things:

* **A background task queue** - the original, a very lightweight replacement for [HostingEnvironment.QueueBackgroundWorkItem](https://www.hanselman.com/blog/HowToRunBackgroundTasksInASPNET.aspx).
* **A dynamic cron scheduler** (since v2.1.0) - add, reschedule and remove cron jobs **at runtime, with no restart**, with an optional bring-your-own durable store.

The two compose: schedule *when* with the scheduler, run *how* (throttled, scoped) on the queue.

For those of you that haven't used HostingEnvironment.QueueBackgroundWorkItem it was a simple way in .NET 4.5.2 to safely run a background task on a webhost, for example, sending an email when a user registers. 

Yes there are loads of great options (hangfire, Azure Web Jobs/Functions) for doing this, but nothing in ASP.NET Core to replace the simplicity of the classic one-liner ```HostingEnvironment.QueueBackgroundWorkItem(cancellationToken => DoWork())```.

#### Most backgrounder code examples don't work properly when injecting scoped services like EF (see FAQ). This use case is taken care of and battle tested in production environments.

For me, I needed something simple akin to HostingEnvironment.QueueBackgroundWorkItem, so I will continue to support and improve this package.

This package has over 345k downloads and is used in many production environments, but should you run into a problem, please raise an issue.

## Supported Platforms

v2.0.0+ DalSoft.Hosting.BackgroundQueue uses [BackgroundService](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-8.0&tabs=visual-studio), and works with .NET 6.0 and above (the package multi-targets net6.0 and net8.0).

v1.1.1 DalSoft.Hosting.BackgroundQueue uses [IHostedService](https://blogs.msdn.microsoft.com/cesardelatorre/2017/11/18/implementing-background-tasks-in-microservices-with-ihostedservice-and-the-backgroundservice-class-net-core-2-x/) and works with any .NET Core 2.0 or higher IWebHost i.e. a server that supports ASP.NET Core. v.1.1.1 doesn't support DI (you don't get the serviceScope parameter). 

DalSoft.Hosting.BackgroundQueue also works with .NET Core's lighter-weight [IHost](https://blogs.msdn.microsoft.com/cesardelatorre/2017/11/18/implementing-background-tasks-in-microservices-with-ihostedservice-and-the-backgroundservice-class-net-core-2-x/) - i.e. just services no ASP.NET Core, ideal for microservices.

## Getting Started
```bash
dotnet add package DalSoft.Hosting.BackgroundQueue
```
In your ASP.NET Core Startup.cs:
```cs
builder.Services.AddBackgroundQueue
(
   maxConcurrentCount: 1, millisecondsToWaitBeforePickingUpTask: 1000,
   onException: (exception, serviceScope) =>
   {
       serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>()
           .Log(LogLevel.Error, exception, exception.Message);
   }
);
```
> This setups DalSoft.Hosting.BackgroundQueue using .NET Core's DI container. If you're using a different DI container, you need to register BackgroundQueue, IBackgroundQueue and BackgroundQueueService as singletons.

**onException (required)** <br />
You are running tasks in the background on a different thread you need to know when an exception occurred. This is done using the ```Action<Exception, AsyncServiceScope>``` parameter passed to onException. onException is called any time a Task throws an exception. 

**maxConcurrentCount (optional)** <br />
maxConcurrentCount is the number of Tasks allowed to run in the background concurrently. maxConcurrentCount defaults to 1. Setting maxConcurrentCount lower than 1 throws an exception.

**millisecondsToWaitBeforePickingUpTask (optional)** <br />
millisecondsToWaitBeforePickingUpTask is the delay before a background Task is added to the queue - defaults to 10 milliseconds.  
Setting millisecondsToWaitBeforePickingUpTask lower than 10 throws an exception. In most cases you shouldn't need to change this setting it's useful if you have to ['warm up'](https://payodatechnologyinc.medium.com/cache-warming-and-its-importance-5724148ab5f5) or need more throttling before hitting the maxConcurrentCount.

> As you would expect exceptions only affect the Task causing the exception, all other Tasks are processed as normal.
> You can get your services from the AsyncServiceScope parameter i.e. `serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>()`.

## Queuing a Background Task

To queue a background Task just add ```IBackgroundQueue``` to your controller's constructor and call ```Enqueue```.

Using a controller:
```cs
public EmailController(IBackgroundQueue backgroundQueue)
{
   _backgroundQueue = backgroundQueue;
}

[HttpPost, Route("/")]
public IActionResult SendEmail([FromBody]emailRequest)
{
   _backgroundQueue.Enqueue(async (cancellationToken, serviceScope) =>
   {
       var smtp = serviceScope.ServiceProvider.GetRequiredService<ISmtp>()
       await smtp.SendMailAsync(emailRequest.From, emailRequest.To, request.Body, cancellationToken);
   });

   return Ok();
}
```

Equivalent code using minimal API
```cs
app.MapPost("/", (IBackgroundQueue backgroundQueue) =>
{
    backgroundQueue.Enqueue(async (cancellationToken, serviceScope) =>
    {
        var smtp = serviceScope.ServiceProvider.GetRequiredService<ISmtp>()
        await smtp.SendMailAsync(emailRequest.From, emailRequest.To, request.Body, cancellationToken);
    });
})
```

> **Note** services are scoped to the Enqueue Task you provide i.e. per run, this is by design.

A fully working ASP.NET example targeting net8.0 can be found [here](https://github.com/DalSoft/DalSoft.Hosting.BackgroundQueue/tree/master/DalSoft.Hosting.BackgroundQueue.Examples.WebApi). 

## Scheduling cron jobs (since v2.1.0)

The scheduler lets you run jobs on a cron schedule and - unlike most schedulers - **add, reschedule and remove those jobs at runtime without restarting your app**. It's completely separate from the queue: adding it changes nothing about existing queue usage.

Register it (in addition to, or instead of, `AddBackgroundQueue`):
```cs
builder.Services.AddBackgroundJobs();
```

Write your job as an `IInvocable`. It's resolved from DI in a fresh scope per run, so it can take scoped dependencies (a `DbContext`, repositories, or `IBackgroundQueue`) via the constructor:
```cs
public class SendDigestEmails : IInvocable
{
    private readonly IBackgroundQueue _queue;
    public SendDigestEmails(IBackgroundQueue queue) => _queue = queue;

    public Task Invoke()
    {
        // Schedule decides WHEN; hand the heavy work to the throttled queue to decide HOW it runs.
        _queue.Enqueue(async (ct, scope) =>
        {
            var mailer = scope.ServiceProvider.GetRequiredService<IMailer>();
            await mailer.SendDigestsAsync(ct);
        });
        return Task.CompletedTask;
    }
}
```

Add, change and remove schedules at runtime by resolving `IJobScheduler` anywhere (a controller, a minimal API, another job):
```cs
app.MapPost("/schedules/digest", async (IJobScheduler scheduler) =>
{
    await scheduler.ScheduleAsync<SendDigestEmails>("daily-digest", "0 8 * * *"); // every day at 08:00 (UTC)
});

app.MapPut("/schedules/digest", async (IJobScheduler scheduler, string cron) =>
{
    await scheduler.RescheduleAsync("daily-digest", cron); // takes effect on the next tick - no restart
});

app.MapDelete("/schedules/digest", (IJobScheduler scheduler) => scheduler.RemoveAsync("daily-digest"));
```

* Cron is **standard 5-field** (`m h dom mon dow`) or **6-field with seconds** (`s m h dom mon dow`), parsed by [Cronos](https://github.com/HangfireIO/Cronos).
* Pass a `timeZoneId` to evaluate the expression in a specific time zone (defaults to UTC).
* Optional `payload` is handed to jobs that implement `IInvocableWithPayload` - keeps schedules durable by passing parameters as data rather than a captured closure.

### Durable schedules (bring your own store)

By default schedules live in memory and are lost on restart. Implement `IScheduleStore` to persist them (EF Core, Dapper, table storage, …) and register it before/after `AddBackgroundJobs`:
```cs
builder.Services.AddSingleton<IScheduleStore, MySqlScheduleStore>();
builder.Services.AddBackgroundJobs();
```

**The scheduler never polls your database.** The store is read once at startup, written through only when a schedule is added/changed/removed, and otherwise the per-second tick runs entirely against the in-memory schedule. This is deliberate so a pay-per-use / serverless database (e.g. serverless Azure SQL) is never hit while the app is idle. If another process changes schedules directly in the store, call `IJobScheduler.ReloadFromStoreAsync()` to pick them up - ideally from inside a scheduled "sync" job, so even that read happens on your terms.

A complete, drop-in **EF Core / SQL Server `IScheduleStore`** plus runtime add/reschedule/remove endpoints can be found in the [example WebApi](https://github.com/DalSoft/DalSoft.Hosting.BackgroundQueue/tree/master/DalSoft.Hosting.BackgroundQueue.Examples.WebApi) (`Scheduling/EfCoreScheduleStore.cs`). Note it's resolved via `IDbContextFactory<T>` because the store is a singleton and must not capture a scoped `DbContext`.

### How this compares to Hangfire

This is a *lightweight* alternative, not a replacement for everything Hangfire does. Reach for it when you want in-memory, single-instance scheduling/queuing with minimal dependencies and runtime-editable schedules. Hangfire is the better choice when you need built-in durable storage, automatic retries, a dashboard, or distributed processing across many servers - none of which this library provides out of the box (persistence is bring-your-own, and there's no retry/dashboard/clustering).

## FAQ

### I'm getting a System.ObjectDisposedException
If you're getting: <br />
`System.ObjectDisposedException: Cannot access a disposed object. A common cause of this error is disposing a context that was resolved from dependency injection...`

You're getting your service from your controller instead of Enqueue (make your code like the examples above).

### Thread Safety 
DalSoft.Hosting.BackgroundQueue uses a [ConcurrentQueue](https://msdn.microsoft.com/en-us/library/dd267265(v=vs.110).aspx) and [interlocked operations](https://docs.microsoft.com/en-us/dotnet/standard/threading/interlocked-operations) so is completely thread safe, watch out for [Access to Modified Closure](https://weblogs.asp.net/fbouma/linq-beware-of-the-access-to-modified-closure-demon) issues.

### Brief History

 **Update June 2026**
 Version 2.1.0 adds a dynamic cron scheduler (`AddBackgroundJobs` / `IJobScheduler`) with add/reschedule/remove at runtime, per-job DI scope, and a pluggable `IScheduleStore` for durable schedules that never polls the database. The queue's graceful shutdown was also hardened so in-flight tasks drain instead of being abandoned. Fully backwards compatible - the existing queue API is unchanged - and now multi-targets net6.0 and net8.0.

 **Update September 2024** 
 Version 2.0 is here with full DI support, and support for minimal APIs. Version 2.0 API is fully backwards compatible with versions 1.x.x.

**Update August 2021**
Although [Microsoft.NET.Sdk.Worker](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-5.0&tabs=visual-studio) works well, you end up with a lot of boilerplate code and have to solve things like exception handling and concurrency. [MS are leaving it up to the end user](https://github.com/dotnet/extensions/issues/805) to decide how to implement (which makes sense rather than trying to implement every scenario).

**Update December 2019**
ASP.NET Core 3.1 finally supports background tasks using Microsoft.NET.Sdk.Worker.
