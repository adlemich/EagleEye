using EagleEye.ParentApp.Core.Abstractions;

namespace EagleEye.ParentApp.Core.Data;

/// <summary>
/// Key/value settings in <c>AppSettings</c>. US-002 uses <see cref="ThemeKey"/> =
/// <c>light</c> / <c>dark</c>; an absent (or unknown) value means "follow the OS".
/// </summary>
public sealed class SettingsStore(ParentDatabase database) : ISettingsStore
{
    /// <summary>Settings key of the theme.</summary>
    public const string ThemeKey = "appearance.theme";

    private const string LightValue = "light";
    private const string DarkValue = "dark";

    /// <inheritdoc />
    public async Task<ThemeMode?> GetThemeAsync()
    {
        return await GetAsync(ThemeKey).ConfigureAwait(false) switch
        {
            LightValue => ThemeMode.Light,
            DarkValue => ThemeMode.Dark,
            _ => null,
        };
    }

    /// <inheritdoc />
    public Task SetThemeAsync(ThemeMode theme)
    {
        return SetAsync(ThemeKey, theme == ThemeMode.Dark ? DarkValue : LightValue);
    }

    /// <summary>Returns the value, or <c>null</c> if the key is not set.</summary>
    internal Task<string?> GetAsync(string key)
    {
        return database.ExecuteAsync(async (connection, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Value FROM AppSettings WHERE Key = @key;";
            command.Parameters.AddWithValue("@key", key);
            return await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
        });
    }

    /// <summary>Stores or replaces the value.</summary>
    internal Task SetAsync(string key, string value)
    {
        return database.ExecuteAsync(async (connection, ct) =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO AppSettings (Key, Value) VALUES (@key, @value)
                ON CONFLICT (Key) DO UPDATE SET Value = excluded.Value;
                """;
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value);
            return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        });
    }
}
