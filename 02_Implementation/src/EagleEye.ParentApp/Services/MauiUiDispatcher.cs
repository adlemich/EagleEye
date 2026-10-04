using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Services;

/// <summary>Marshals coordinator events (thread-pool threads) to the UI thread.</summary>
public sealed class MauiUiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Post(Action action) => MainThread.BeginInvokeOnMainThread(action);
}
