using Microsoft.Extensions.Logging;

namespace EagleEye.Service.Tests;

/// <summary>Records log entries (level, formatted message, exception) for assertions.</summary>
internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get
        {
            lock (_entries)
            {
                return _entries.ToList();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_entries)
        {
            _entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

    /// <summary>Whether an entry with the level and exactly this exception exists.</summary>
    public bool Has(LogLevel level, Exception exception) => Entries.Any(e => e.Level == level && ReferenceEquals(e.Exception, exception));

    /// <summary>The messages of all entries with the level.</summary>
    public IReadOnlyList<string> Messages(LogLevel level) => Entries.Where(e => e.Level == level).Select(e => e.Message).ToList();
}
