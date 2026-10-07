using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace EagleEye.Shared.Logging;

/// <summary>
/// EagleEye file logging provider (ADR-002 implementation note: .NET has no built-in file
/// provider). All categories write to one set of rolling files, see <see cref="RollingFileOptions"/>.
/// Never throws into the application on write errors.
/// </summary>
[ProviderAlias("File")]
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly RollingFileWriter _writer;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, RollingFileLogger> _loggers = new(StringComparer.Ordinal);

    /// <summary>Creates the provider. No file is opened before the first entry.</summary>
    public RollingFileLoggerProvider(RollingFileOptions options, TimeProvider timeProvider)
    {
        _writer = new RollingFileWriter(options, timeProvider);
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ArgumentNullException.ThrowIfNull(categoryName);
        return _loggers.GetOrAdd(categoryName, name => new RollingFileLogger(name, _writer, _timeProvider));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _writer.Dispose();
    }
}
