using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace EagleEye.Shared.Logging;

/// <summary>
/// Formats one log entry as <c>yyyy-MM-dd HH:mm:ss.fff zzz [INF] Category: message</c>, followed
/// by <see cref="Exception.ToString"/> on the next lines if an exception is given.
/// </summary>
internal static class LogLineFormatter
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";

    /// <summary>Returns the entry including the trailing line break.</summary>
    public static string Format(DateTimeOffset timestamp, LogLevel level, string category, string message, Exception? exception)
    {
        var builder = new StringBuilder()
            .Append(timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture))
            .Append(" [").Append(GetLevelCode(level)).Append("] ")
            .Append(category).Append(": ")
            .Append(message)
            .Append(Environment.NewLine);
        if (exception is not null)
        {
            builder.Append(exception).Append(Environment.NewLine);
        }

        return builder.ToString();
    }

    /// <summary>The three-letter code of a level.</summary>
    public static string GetLevelCode(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???",
    };
}
