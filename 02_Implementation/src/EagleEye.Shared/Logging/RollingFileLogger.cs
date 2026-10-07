using Microsoft.Extensions.Logging;

namespace EagleEye.Shared.Logging;

/// <summary>
/// <see cref="ILogger"/> of one category that writes formatted lines to the shared
/// <see cref="RollingFileWriter"/>. Level filtering is done by the logging configuration; scopes
/// are not supported.
/// </summary>
internal sealed class RollingFileLogger(string category, RollingFileWriter writer, TimeProvider timeProvider) : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return NoScope.Instance;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        writer.Write(LogLineFormatter.Format(timeProvider.GetLocalNow(), logLevel, category, message ?? string.Empty, exception));
    }

    private sealed class NoScope : IDisposable
    {
        public static readonly NoScope Instance = new();

        public void Dispose()
        {
            // Scopes are not supported; there is nothing to end.
        }
    }
}
