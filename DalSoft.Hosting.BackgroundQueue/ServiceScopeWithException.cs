using System;
using Microsoft.Extensions.DependencyInjection;

namespace DalSoft.Hosting.BackgroundQueue;

internal class ServiceScopeWithException : IServiceScope
{
    public const string CreateAsyncScopeFailedMessage = "The call to Microsoft.Extensions.DependencyInjection CreateAsyncScope failed. " +
        "Check your Dependency Injection / service configuration. No Background Tasks will be processed!";

    public void Dispose() { }

    public IServiceProvider ServiceProvider => throw new InvalidOperationException(CreateAsyncScopeFailedMessage);
}
