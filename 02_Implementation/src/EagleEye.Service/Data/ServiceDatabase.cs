using EagleEye.Shared.Data;

namespace EagleEye.Service.Data;

/// <summary>
/// The service database <c>%ProgramData%\EagleEye\EagleEye.Service.db</c>. Migration 1 creates
/// <c>PairedDevices</c>; the token is stored only as its SHA-256 hash (ADR-004, ADR-008 §5).
/// Migration 2 creates <c>AccountSelections</c> (US-003): one row per account the parent has ticked
/// or unticked at least once, keyed by SID; no row means "not under parental control".
/// </summary>
/// <param name="connectionString">SQLite connection string, see <see cref="SqliteDatabase.BuildConnectionString"/>.</param>
public sealed class ServiceDatabase(string connectionString) : SqliteDatabase(connectionString)
{
    private static readonly string[] SchemaMigrations =
    [
        """
        CREATE TABLE PairedDevices (
            DeviceId     TEXT PRIMARY KEY NOT NULL,
            DeviceName   TEXT NOT NULL,
            TokenHash    BLOB NOT NULL UNIQUE,
            PairedAtUtc  TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE AccountSelections (
            Sid                    TEXT PRIMARY KEY NOT NULL,
            UserName               TEXT NOT NULL,
            UnderParentalControl   INTEGER NOT NULL CHECK (UnderParentalControl IN (0, 1)),
            ChangedAtUtc           TEXT NOT NULL
        );
        """,
    ];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Migrations => SchemaMigrations;
}
