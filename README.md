### `If you find this repo / package useful all I ask is you please star it ⭐`
> ### Do you or the company you work for benefit from the tools I build? <br /> If so please consider [Becoming a Sponsor](https://github.com/sponsors/dalsoft) it would be greatly appreciated ❤️ 

# DalSoft.Hosting.BackgroundQueue

DalSoft.Hosting.BackgroundQueue is a very lightweight .NET Core replacement for [HostingEnvironment.QueueBackgroundWorkItem](https://www.hanselman.com/blog/HowToRunBackgroundTasksInASPNET.aspx) it has no extra dependencies!

For those of you that haven't used HostingEnvironment.QueueBackgroundWorkItem it was a simple way in .NET 4.5.2 to safely run a background task on a webhost, for example, sending an email when a user registers. 

Yes there are loads of great options (hangfire, Azure Web Jobs/Functions) for doing this, but nothing in ASP.NET Core to replace the simplicity of the classic one-liner ```HostingEnvironment.QueueBackgroundWorkItem(cancellationToken => DoWork())```.

#### Most backgrounder code examples don't work properly when injecting scoped services like EF (see FAQ). This use case is taken care of and battle tested in production environments.

For me, I needed something simple akin to HostingEnvironment.QueueBackgroundWorkItem, so I will continue to support and improve this package.

This package has over 345k downloads and is used in many production environments, but should you run into a problem please raise an issue.

## Supported Platforms

v2.0.0 DalSoft.Hosting.BackgroundQueue uses [BackgroundService](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-8.0&tabs=visual-studio), and works with .NET 6.0 and above.

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
 
**maxConcurrentCount (optional)**
maxConcurrentCount is the number of Tasks allowed to run in the background concurrently. maxConcurrentCount defaults to 1. Setting maxConcurrentCount lower than 1 throws an exception.

**millisecondsToWaitBeforePickingUpTask (optional)**
The delay before a background Task is picked up. If the number of background Tasks exceeds the maxConcurrentCount, then millisecondsToWaitBeforePickingUpTask is used to delay picking up Tasks until the current Task is completed. millisecondsToWaitBeforePickingUpTask defaults to 1000, setting millisecondsToWaitBeforePickingUpTask lower than 500 throws an exception.

 **onException (required)**
You are running tasks in the background on a different thread you need to know when an exception occurred. This is done using the ```Action<Exception, IServiceScopeFactory>``` parameter passed to onException. onException is called any time a Task throws an exception. 

> As you would expect exceptions only affect the Task causing the exception, all other Tasks are processed as normal.
> You can get your services from the IServiceScopeFactory parameter i.e. `serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>()`.

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

## FAQ

### I'm getting a System.ObjectDisposedException
If your getting: <br />
`System.ObjectDisposedException: Cannot access a disposed object. A common cause of this error is disposing a context that was resolved from dependency injection...`

Your getting your service from your controller instead of Enqueue (make your code like the examples above).

### Thread Safety 
DalSoft.Hosting.BackgroundQueue uses a [ConcurrentQueue](https://msdn.microsoft.com/en-us/library/dd267265(v=vs.110).aspx) and [interlocked operations](https://docs.microsoft.com/en-us/dotnet/standard/threading/interlocked-operations) so is completely thread safe, just watch out for [Access to Modified Closure](https://weblogs.asp.net/fbouma/linq-beware-of-the-access-to-modified-closure-demon) issues.

### Brief History

 **Update September 2024** 
 Version 2.0 is here with full DI support, and support for minimal APIs. Version 2.0 API is fully backwards compatible with versions 1.x.x.

**Update August 2021**
Although [Microsoft.NET.Sdk.Worker](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-5.0&tabs=visual-studio) works well,but you end up with a lot of boilerplate code and have to solve things like exception handling and concurrency. [MS are leaving it up to the end user](https://github.com/dotnet/extensions/issues/805) to decide how to implement (which makes sense rather than trying to implement every scenario).

**Update December 2019**
ASP.NET Core 3.1 finally supports background tasks using Microsoft.NET.Sdk.Worker.
