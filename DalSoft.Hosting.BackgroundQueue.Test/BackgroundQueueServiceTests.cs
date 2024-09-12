using FluentAssertions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;


namespace DalSoft.Hosting.BackgroundQueue.Test;

public class BackgroundQueueServiceTests
{
    [Fact]
    public async Task Queue20BackgroundTasks_WhenBackgroundTasksExistInQueue_ConcurrentCountIsGreaterThan1()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();

        var queue = new BackgroundQueue
        (
            onException: (ex, _) => throw ex,
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
        );

        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);
        var highwaterMark = 0;

        // queue background tasks
        for (var i = 0; i < 20; i++)
        {
            queue.Enqueue(async cancellationToken =>
            {
                highwaterMark = Math.Max(queue.ConcurrentCount, highwaterMark);
                await Task.Delay(5, cancellationToken);
            });
        }
            
        // process background tasks
        var runningService = Task.Run(async () => await queueService.StartAsync(serviceStopCancellationToken.Token), TestContext.Current.CancellationToken);
            
        // wait for all tasks to be processed
        while(queue.Count > 0)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
            
        // Check that tasks run concurrently up to the maxConcurrentCount.
        highwaterMark.Should().BeGreaterThan(1);
    }
    
    [Fact(DisplayName = "Check That background tasks are processed up to the MaxConcurrentCount")]
    public async Task Queue20BackgroundTasks_WhenMaxConcurrentCount10_ConcurrentCountShouldBe10AndCountShouldBe10()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();

        var queue = new BackgroundQueue
        (
            onException: (ex, _) => throw ex,
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
        );

        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);
        // queue background tasks
        for (var i = 0; i < 20; i++)
        {
            queue.Enqueue(async cancellationToken => await Task.Delay(200, cancellationToken));
        }

        // process background tasks
        var runningService = Task.Run(async () => await queueService.StartAsync(serviceStopCancellationToken.Token), TestContext.Current.CancellationToken);
        queue.Count.Should().Be(20);
        // wait for tasks to be picked up off the queue
        await Task.Delay(200, serviceStopCancellationToken.Token);
        queue.Count.Should().Be(10);
        queue.ConcurrentCount.Should().Be(10);
    }
    
    [Fact]
    public async Task Queue20BackgroundTasks_WhenMaxConcurrentCount10AndServiceIsStoppedViaToken_ConcurrentCountShouldBeAndCountShould20()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();

        var queue = new BackgroundQueue
        (
            onException: (ex, _) => throw ex,
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
        );

        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);
        // queue background tasks
        for (var i = 0; i < 20; i++)
        {
            queue.Enqueue( _ => Task.CompletedTask);
        }

        // process background tasks
        var runningService = Task.Run(async () => await queueService.StartAsync(serviceStopCancellationToken.Token), TestContext.Current.CancellationToken);
        serviceStopCancellationToken.Cancel();
        // wait for tasks to be picked up off the queue
        await Task.Delay(200, TestContext.Current.CancellationToken);
        queue.Count.Should().Be(20);
        queue.ConcurrentCount.Should().Be(0);
    }

    [Fact]
    public async Task QueueBackgroundTask_CreateAsyncScopeThrowsException_OnExceptionIsCalled()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();

        var exceptionThrown = false;
        const string expectedExceptionMessage = "Call to CreateAsyncScopeThrowsException failed";
        string actualExceptionMessage = null;
        var queue = new BackgroundQueue
        (
            onException: (ex, _) =>
            {
                exceptionThrown = true;
                actualExceptionMessage = ex.Message;
            },
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => throw new Exception(expectedExceptionMessage));
        
        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);
        
        queue.Enqueue(async _ => await Task.CompletedTask);
        
        // process background tasks
        var runningService = Task.Run(async () => await queueService.StartAsync(serviceStopCancellationToken.Token), TestContext.Current.CancellationToken);

        // wait for an attempt to pick a task up off the queue
        await Task.Delay(30, TestContext.Current.CancellationToken);
        
        exceptionThrown.Should().Be(true);
        actualExceptionMessage.Should().Be(expectedExceptionMessage);
    }
    
    [Fact]
    public async Task QueueBackgroundTask_CreateAsyncScopeThrowsExceptionAndITryToAccessServiceProviderViaOnException_ExceptionIsThrowTellingUserCreateAsyncScopeFailed()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();

        var exceptionThrown = false;
        string actualExceptionMessage = null;
        var queue = new BackgroundQueue
        (
            onException: (_, serviceScope) =>
            {
                try
                {
                    // The Service we are getting doesn't matter here we are expecting failure.
                    serviceScope.ServiceProvider.GetService<BackgroundQueue>();
                }
                catch (Exception ex)
                {
                    exceptionThrown = true;
                    actualExceptionMessage = ex.Message;
                }
            },
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => throw new Exception());
        
        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);
        
        queue.Enqueue(async _ => await Task.CompletedTask);
        
        // process background tasks
        var runningService = Task.Run(async () => await queueService.StartAsync(serviceStopCancellationToken.Token), TestContext.Current.CancellationToken);

        // wait for an attempt to pick a task up off the queue
        await Task.Delay(30, TestContext.Current.CancellationToken);
        
        exceptionThrown.Should().Be(true);
        actualExceptionMessage.Should().Be(ServiceScopeWithException.CreateAsyncScopeFailedMessage);
    }
    
    [Fact]
    public async Task QueueBackgroundTask_ThrowsException_OnExceptionIsCalled()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();

        var exceptionThrown = false;
        const string expectedExceptionMessage = "Queued Background failed";
        string actualExceptionMessage = null;
        var queue = new BackgroundQueue
        (
            onException: (ex, _) =>
            {
                exceptionThrown = true;
                actualExceptionMessage = ex.Message;
            },
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
        );
        
        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);
        
        queue.Enqueue(_ => throw new Exception(expectedExceptionMessage));
        
        // process background tasks
        var runningService = Task.Run(async () => await queueService.StartAsync(serviceStopCancellationToken.Token), serviceStopCancellationToken.Token);

        // wait for an attempt to pick a task up off the queue
        await Task.Delay(30, serviceStopCancellationToken.Token);
        
        exceptionThrown.Should().Be(true);
        actualExceptionMessage.Should().Be(expectedExceptionMessage);
    }
    
    [Fact]
    public async Task QueueBackgroundTask_ThrowsException_ICanGetServicesViaOnException()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();

        var queue = new BackgroundQueue
        (
            onException: (_, serviceScope) =>
            {
                var scopeServiceProvider = serviceScope.ServiceProvider;
            },
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
        );
        
        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);
        
        queue.Enqueue(_ => throw new Exception());
        
        // process background tasks
        var runningService = Task.Run(async () => await queueService.StartAsync(serviceStopCancellationToken.Token), serviceStopCancellationToken.Token);

        // wait for an attempt to pick a task up off the queue
        await Task.Delay(30, serviceStopCancellationToken.Token);
        
        mockServiceScope.Verify(serviceScope => serviceScope.ServiceProvider, Times.Once);
    }
    
    [Fact]
    public async Task QueueBackgroundTask_WhenDequeued_ServiceStopCancellationTokenIsPassedToBackgroundTask()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();

        var queue = new BackgroundQueue
        (
            onException: (ex, _) => throw ex,
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
        );
        
        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);
        var taskCancelled = false;
        serviceStopCancellationToken.Token.Register(() =>
        { 
            taskCancelled = true;
        });
        queue.Enqueue(async (cancellationToken, _) =>
        {
            while (!taskCancelled)
            {
                await Task.Delay(10, CancellationToken.None);
            }
        });
        
        // process background tasks
        var runningService = Task.Run(async () => await queueService.StartAsync(serviceStopCancellationToken.Token), serviceStopCancellationToken.Token);
        // wait for an attempt to pick a task up off the queue
        await Task.Delay(30, CancellationToken.None);
        serviceStopCancellationToken.Cancel();
        taskCancelled.Should().Be(true);
    }
    
    [Fact]
    public async Task QueueBackgroundTask_WhenDequeued_ICanGetServices()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();

        var queue = new BackgroundQueue
        (
            onException: (ex, _) => throw ex,
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
        );
        
        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);
        
        queue.Enqueue( (_, serviceScope) =>
        {
            var serviceProvider = serviceScope.ServiceProvider;
            return Task.CompletedTask;
        });
        
        // process background tasks
        var runningService = Task.Run(async () => await queueService.StartAsync(serviceStopCancellationToken.Token), serviceStopCancellationToken.Token);

        // wait for an attempt to pick a task up off the queue
        await Task.Delay(30, serviceStopCancellationToken.Token);
        
        mockServiceScope.Verify(serviceScope => serviceScope.ServiceProvider, Times.Once);
    }
    
    [Fact]
    public async Task QueueBackgroundTask_WhenQueuedAndDeQueued_CountIsCorrect()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();

        var queue = new BackgroundQueue
        (
            onException: (ex, _) => throw ex,
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
        );
        
        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);
        
        queue.Enqueue( (_, serviceScope) => Task.CompletedTask);

        queue.Count.Should().Be(1);
        
        // process background tasks
        var runningService = Task.Run(async () => await queueService.StartAsync(serviceStopCancellationToken.Token), serviceStopCancellationToken.Token);

        // wait for an attempt to pick a task up off the queue
        await Task.Delay(30, serviceStopCancellationToken.Token);
        
        queue.Count.Should().Be(0);
    }
}
