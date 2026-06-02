using FluentAssertions;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;


namespace DalSoft.Hosting.BackgroundQueue.Test;

public class BackgroundQueueServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // Polls a condition instead of sleeping a fixed amount, so the tests don't race the timer under load.
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < Timeout)
        {
            await Task.Delay(10, CancellationToken.None);
        }
    }

    [Fact]
    public async Task Queue20BackgroundTasks_WhenBackgroundTasksExistInQueue_ConcurrentCountIsGreaterThan1()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var gate = new TaskCompletionSource();

        var queue = new BackgroundQueue
        (
            onException: (ex, _) => throw ex,
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
        );

        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);

        // Tasks park on the gate so they stay in-flight - concurrency climbs deterministically rather than
        // depending on whether a short Task.Delay happens to overlap the timer tick.
        for (var i = 0; i < 20; i++)
        {
            queue.Enqueue(async cancellationToken => await gate.Task.WaitAsync(cancellationToken));
        }

        await queueService.StartAsync(serviceStopCancellationToken.Token);

        await WaitUntilAsync(() => queue.ConcurrentCount > 1);

        // Check that tasks run concurrently up to the maxConcurrentCount.
        queue.ConcurrentCount.Should().BeGreaterThan(1);

        gate.TrySetResult();
        serviceStopCancellationToken.Cancel();
    }

    [Fact(DisplayName = "Check That background tasks are processed up to the MaxConcurrentCount")]
    public async Task Queue20BackgroundTasks_WhenMaxConcurrentCount10_ConcurrentCountShouldBe10AndCountShouldBe10()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var gate = new TaskCompletionSource();

        var queue = new BackgroundQueue
        (
            onException: (ex, _) => throw ex,
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
        );

        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);

        // Gated tasks stay running, so once 10 are picked up the service stops dequeuing (at MaxConcurrentCount)
        // leaving exactly 10 in the queue - a stable state to assert against.
        for (var i = 0; i < 20; i++)
        {
            queue.Enqueue(async cancellationToken => await gate.Task.WaitAsync(cancellationToken));
        }

        queue.Count.Should().Be(20);

        await queueService.StartAsync(serviceStopCancellationToken.Token);

        await WaitUntilAsync(() => queue.ConcurrentCount == 10);

        queue.ConcurrentCount.Should().Be(10);
        queue.Count.Should().Be(10);

        gate.TrySetResult();
        serviceStopCancellationToken.Cancel();
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

        // Stop before the service ever ticks, so nothing is dequeued.
        serviceStopCancellationToken.Cancel();
        await queueService.StartAsync(serviceStopCancellationToken.Token);

        // give a stopped service a chance to (incorrectly) pick anything up
        await Task.Delay(50, CancellationToken.None);
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

        await queueService.StartAsync(serviceStopCancellationToken.Token);

        await WaitUntilAsync(() => exceptionThrown);

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

        await queueService.StartAsync(serviceStopCancellationToken.Token);

        await WaitUntilAsync(() => exceptionThrown);

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

        await queueService.StartAsync(serviceStopCancellationToken.Token);

        await WaitUntilAsync(() => exceptionThrown);

        exceptionThrown.Should().Be(true);
        actualExceptionMessage.Should().Be(expectedExceptionMessage);
    }

    [Fact]
    public async Task QueueBackgroundTask_ThrowsException_ICanGetServicesViaOnException()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var onExceptionCalled = new TaskCompletionSource();

        var queue = new BackgroundQueue
        (
            onException: (_, serviceScope) =>
            {
                var scopeServiceProvider = serviceScope.ServiceProvider;
                onExceptionCalled.TrySetResult();
            },
            maxConcurrentCount: 10,
            millisecondsToWaitBeforePickingUpTask: 10,
            fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
        );

        var queueService = new BackgroundQueueService(queue, mockServiceScopeFactory.Object);

        queue.Enqueue(_ => throw new Exception());

        await queueService.StartAsync(serviceStopCancellationToken.Token);

        await onExceptionCalled.Task.WaitAsync(Timeout);

        mockServiceScope.Verify(serviceScope => serviceScope.ServiceProvider, Times.Once);
    }

    [Fact]
    public async Task QueueBackgroundTask_WhenDequeued_ServiceStopCancellationTokenIsPassedToBackgroundTask()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var taskPickedUp = new TaskCompletionSource();

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
            taskPickedUp.TrySetResult();
            while (!taskCancelled)
            {
                await Task.Delay(10, CancellationToken.None);
            }
        });

        await queueService.StartAsync(serviceStopCancellationToken.Token);

        // wait until the task is actually running before stopping the service
        await taskPickedUp.Task.WaitAsync(Timeout);
        serviceStopCancellationToken.Cancel();

        await WaitUntilAsync(() => taskCancelled);
        taskCancelled.Should().Be(true);
    }

    [Fact]
    public async Task QueueBackgroundTask_WhenDequeued_ICanGetServices()
    {
        var serviceStopCancellationToken = new CancellationTokenSource();
        var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        var mockServiceScope = new Mock<IServiceScope>();
        var ran = new TaskCompletionSource();

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
            ran.TrySetResult();
            return Task.CompletedTask;
        });

        await queueService.StartAsync(serviceStopCancellationToken.Token);

        await ran.Task.WaitAsync(Timeout);

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

        await queueService.StartAsync(serviceStopCancellationToken.Token);

        await WaitUntilAsync(() => queue.Count == 0);

        queue.Count.Should().Be(0);
    }
}
