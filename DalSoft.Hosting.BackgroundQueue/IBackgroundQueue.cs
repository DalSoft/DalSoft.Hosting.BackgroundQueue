using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace DalSoft.Hosting.BackgroundQueue;

public interface IBackgroundQueue
{
    int MaxConcurrentCount { get; }
    int MillisecondsToWaitBeforePickingUpTask { get; }
    int Count { get; }
    int ConcurrentCount { get; }
    void Enqueue(Func<CancellationToken, AsyncServiceScope, Task> task);
    void Enqueue(Func<CancellationToken, Task> task);
}