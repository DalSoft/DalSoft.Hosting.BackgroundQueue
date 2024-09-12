using System;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace DalSoft.Hosting.BackgroundQueue.Test;

public class BackgroundQueueTests
{
    [Fact]
    public void Ctor_MaxConcurrentCountIsLessThanOne_ThrowsArgumentException()
    {
        var ex = Record.Exception(() =>
        {
            var mockServiceScope = new Mock<IServiceScope>();
            
            var queue = new BackgroundQueue
            (
                onException: (ex, _) => throw ex,
                maxConcurrentCount: 0,
                millisecondsToWaitBeforePickingUpTask: 10,
                fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
            );
        });
        ex.Should().NotBeNull();
        ex!.Message.Should().Contain(BackgroundQueue.MaxConcurrentCountExceptionMessage);
        ex.As<ArgumentException>()?.ParamName.Should().Be("maxConcurrentCount");
    }
    
    [Fact]
    public void Ctor_MillisecondsToWaitBeforePickingUpTaskLessThan10_ThrowsArgumentException()
    {
        var ex = Record.Exception(() =>
        {
            var mockServiceScope = new Mock<IServiceScope>();
            
            var queue = new BackgroundQueue
            (
                onException: (ex, _) => throw ex,
                maxConcurrentCount: 10,
                millisecondsToWaitBeforePickingUpTask: 9,
                fakeCreateAsyncScope: () => new AsyncServiceScope(mockServiceScope.Object)
            );
        });
        ex.Should().NotBeNull();
        ex!.Message.Should().Contain(BackgroundQueue.MillisecondsToWaitBeforePickingUpTaskExceptionMessage);
        ex.As<ArgumentException>()?.ParamName.Should().Be("millisecondsToWaitBeforePickingUpTask");
    }
}
