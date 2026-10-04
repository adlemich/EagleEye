namespace EagleEye.Service.Communication;

/// <summary>Thread-safe count of open tray connections.</summary>
public sealed class TrayConnectionTracker : ITrayConnectionTracker
{
    private int _count;

    /// <inheritdoc />
    public int Count => Volatile.Read(ref _count);

    /// <inheritdoc />
    public void Increment()
    {
        Interlocked.Increment(ref _count);
    }

    /// <inheritdoc />
    public void Decrement()
    {
        Interlocked.Decrement(ref _count);
    }
}
