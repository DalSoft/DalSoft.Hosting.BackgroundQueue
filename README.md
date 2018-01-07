# DalSoft.Hosting.BackgroundQueue

> This is used in production environments, however the test coverage isn't where it needs to be, should you run into a problem please raise an issue

DalSoft.Hosting.BackgroundQueue is a very lightweight .NET Core replacement for [HostingEnvironment.QueueBackgroundWorkItem](https://www.hanselman.com/blog/HowToRunBackgroundTasksInASPNET.aspx) it has no extra dependancies!

For those of you that haven't used HostingEnvironment.QueueBackgroundWorkItem it was a simple way in .NET 4.5.2 to safely run a background task on a webhost, for example sending an email when a user registers. 

Yes there are loads of  good options (hangfire, Azure Web Jobs/Functions) for doing this, but nothing in ASP.NET Core to replace the simplicity of the classic one liner ```HostingEnvironment.QueueBackgroundWorkItem(cancellationToken => DoWork())```.

## Supported Platforms

DalSoft.Hosting.BackgroundQueue uses [IHostedService](https://blogs.msdn.microsoft.com/cesardelatorre/2017/11/18/implementing-background-tasks-in-microservices-with-ihostedservice-and-the-backgroundservice-class-net-core-2-x/) and works with any .NET Core 2.0 IWebHost i.e. a server that supports ASP.NET Core.

DalSoft.Hosting.BackgroundQueue also works with .NET Core's 2.1 lighter-weight [IHost](https://blogs.msdn.microsoft.com/cesardelatorre/2017/11/18/implementing-background-tasks-in-microservices-with-ihostedservice-and-the-backgroundservice-class-net-core-2-x/) - i.e. just services no ASP.NET Core, ideal for microservices.

## Getting Started
```dos
PM> Install-Package DalSoft.Hosting.BackgroundQueue
```
In your ASP.NET Core Startup.cs:
```cs
public void ConfigureServices(IServiceCollection services)
{
   services.AddBackgroundQueue(maxConcurrentCount:1, millisecondsToWaitBeforePickingUpTask:1000, 
      onException:exception =>
      {
                   
      });
}
```
> This setups DalSoft.Hosting.BackgroundQueue using .NET Core's DI container. If your using a different DI container you need to register BackgroundQueue and BackgroundQueueService as singletons.
 
**maxConcurrentCount (optional)**
maxConcurrentCount is the number of Tasks allowed to run in the background concurrently. maxConcurrentCount defaults to 1.

**millisecondsToWaitBeforePickingUpTask (optional)**
If the number of background Tasks exceeds the maxConcurrentCount then millisecondsToWaitBeforePickingUpTask is used to delay picking up Tasks until the current Task is completed.  millisecondsToWaitBeforePickingUpTask defaults to 1000, setting millisecondsToWaitBeforePickingUpTask lower than 500 throws an exception.

 **onException (required)**
You are running tasks in the background on a different thread you need to know when an exception occurred. This is done using the ```Action<Exception>``` passed to onException.  onException is called any time a Task throws an exception. 
 
> As you would expect exceptions only affect the Task causing the exception, all other Tasks are processed as normal.

## Queuing a Background Task

To queue a background Task just add ```BackgroundQueue``` to your controller's constructor and call ```Enqueue```.

```cs
public EmailController(BackgroundQueue backgroundQueue)
{
   _backgroundQueue = backgroundQueue;
}

[HttpPost, Route("/")]
public IActionResult SendEmail([FromBody]emailRequest)
{
   _backgroundQueue.Enqueue(async () =>
   {
      await _smtp.SendMailAsync(emailRequest.From, emailRequest.To, request.Body);
   });

   return Ok();
}
```

## Thread Safety 
DalSoft.Hosting.BackgroundQueue uses a [ConcurrentQueue](https://msdn.microsoft.com/en-us/library/dd267265(v=vs.110).aspx) and [interlocked operations](https://docs.microsoft.com/en-us/dotnet/standard/threading/interlocked-operations) so is completely thread safe, just watch out for [Access to Modified Closure](https://weblogs.asp.net/fbouma/linq-beware-of-the-access-to-modified-closure-demon) issues.

## Standing on the Shoulders of Giants

DalSoft.Hosting.BackgroundQueue is inspired by and gives credit to:

* [Steve Gordon's ASP.NET Core 2.0 IHostedService](https://www.stevejgordon.co.uk/asp-net-core-2-ihostedservice)
* [David Fowl's HostedService base class](https://gist.github.com/davidfowl/a7dd5064d9dcf35b6eae1a7953d615e3)
* [Yuliang's .NET C# ConcurrentQueueWorker](https://dingyuliang.me/net-async-tasks-execution-c-concurrentqueueworker/)
