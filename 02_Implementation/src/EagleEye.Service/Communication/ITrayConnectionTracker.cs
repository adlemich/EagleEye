namespace EagleEye.Service.Communication;

/// <summary>Counts the open tray client connections.</summary>
public interface ITrayConnectionTracker
{
    /// <summary>Number of open tray connections.</summary>
    int Count { get; }

    /// <summary>Records an opened connection.</summary>
    void Increment();

    /// <summary>Records a closed connection.</summary>
    void Decrement();
}
