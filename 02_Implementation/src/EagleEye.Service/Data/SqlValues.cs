using System.Globalization;
using Microsoft.Data.Sqlite;

namespace EagleEye.Service.Data;

/// <summary>
/// Value formats and helpers of the US-005 repositories: times as ISO 8601 UTC (<c>O</c> format, so they compare
/// as text), local times as <c>yyyy-MM-dd HH:mm:ss</c> (for readability only), and <c>IN (…)</c> parameter lists.
/// </summary>
internal static class SqlValues
{
    private const string LocalFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>A UTC time as stored text.</summary>
    public static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    /// <summary>A local time as stored text.</summary>
    public static string Local(DateTime value) => value.ToString(LocalFormat, CultureInfo.InvariantCulture);

    /// <summary>Adds one parameter per value and returns the parameter list for an <c>IN (…)</c> clause.</summary>
    public static string InList(SqliteCommand command, IEnumerable<string> values)
    {
        var names = new List<string>();
        foreach (var value in values)
        {
            var name = "@p" + names.Count.ToString(CultureInfo.InvariantCulture);
            command.Parameters.AddWithValue(name, value);
            names.Add(name);
        }

        return string.Join(", ", names);
    }
}
