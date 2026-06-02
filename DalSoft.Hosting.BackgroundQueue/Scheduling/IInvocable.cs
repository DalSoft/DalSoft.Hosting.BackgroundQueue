#nullable enable
using System.Threading.Tasks;

namespace DalSoft.Hosting.BackgroundQueue.Scheduling;

/// <summary>
/// A unit of scheduled work. Implementations are resolved from the DI container (a fresh scope per run),
/// so they can take scoped dependencies (DbContext, repositories, <see cref="IBackgroundQueue"/>, etc.)
/// via constructor injection.
/// </summary>
public interface IInvocable
{
    Task Invoke();
}

/// <summary>
/// Optional companion to <see cref="IInvocable"/>. If a scheduled invocable also implements this,
/// the scheduler sets <see cref="Payload"/> from the persisted schedule before calling
/// <see cref="IInvocable.Invoke"/>. Keeps schedules durable: parameters travel as data, not as a closure.
/// </summary>
public interface IInvocableWithPayload
{
    string? Payload { get; set; }
}
