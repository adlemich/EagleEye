using EagleEye.Shared.Data;

namespace EagleEye.ParentApp.Core.Data;

/// <summary>
/// The parent app database <c>EagleEye.ParentApp.db</c> in the app data folder. Migration 1
/// creates <c>ServerConnections</c> (at most one row in US-002), <c>AppSettings</c> and
/// <c>Secrets</c> (protected bytes; Windows: DPAPI CurrentUser).
/// </summary>
/// <param name="connectionString">SQLite connection string, see <see cref="SqliteDatabase.BuildConnectionString"/>.</param>
public sealed class ParentDatabase(string connectionString) : SqliteDatabase(connectionString)
{
    private static readonly string[] SchemaMigrations =
    [
        """
        CREATE TABLE ServerConnections (
            Host                  TEXT PRIMARY KEY NOT NULL,
            DeviceId              TEXT NOT NULL,
            DeviceName            TEXT NOT NULL,
            CertificateThumbprint TEXT NOT NULL,
            PairedAtUtc           TEXT NOT NULL
        );
        CREATE TABLE AppSettings (Key TEXT PRIMARY KEY NOT NULL, Value TEXT NOT NULL);
        CREATE TABLE Secrets (Key TEXT PRIMARY KEY NOT NULL, ProtectedValue BLOB NOT NULL);
        """,
    ];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Migrations => SchemaMigrations;
}
