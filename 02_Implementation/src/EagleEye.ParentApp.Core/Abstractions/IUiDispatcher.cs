namespace EagleEye.ParentApp.Core.Abstractions;

/// <summary>Runs code on the UI thread. Coordinator events arrive on thread-pool threads.</summary>
public interface IUiDispatcher
{
    /// <summary>Queues the action on the UI thread.</summary>
    void Post(Action action);
}
