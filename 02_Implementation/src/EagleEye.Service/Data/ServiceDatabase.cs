using EagleEye.Shared.Data;

namespace EagleEye.Service.Data;

/// <summary>
/// The service database <c>%ProgramData%\EagleEye\EagleEye.Service.db</c>. Migration 1 creates
/// <c>PairedDevices</c>; the token is stored only as its SHA-256 hash (ADR-004, ADR-008 §5).
/// Migration 2 creates <c>AccountSelections</c> (US-003): one row per account the parent has ticked
/// or unticked at least once, keyed by SID; no row means "not under parental control".
/// Migration 3 creates <c>AppRecords</c> (app inventory, kept without time limit), <c>AppInstances</c> (history)
/// and <c>DailyUsage</c> (seconds per app and local day) (US-004, ADR-012).
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
        """
        CREATE TABLE AppRecords (
            AppId         INTEGER PRIMARY KEY AUTOINCREMENT,
            AccountSid    TEXT NOT NULL,
            ProgramPath   TEXT NOT NULL COLLATE NOCASE,
            ProcessName   TEXT NOT NULL,
            DisplayName   TEXT NOT NULL,
            FirstSeenUtc  TEXT NOT NULL,
            LastSeenUtc   TEXT NOT NULL,
            UNIQUE (AccountSid, ProgramPath)
        );
        CREATE TABLE AppInstances (
            InstanceId    INTEGER PRIMARY KEY AUTOINCREMENT,
            AppId         INTEGER NOT NULL REFERENCES AppRecords (AppId) ON DELETE CASCADE,
            StartedUtc    TEXT NOT NULL,
            LastSeenUtc   TEXT NOT NULL,
            EndedUtc      TEXT NULL,
            EndReason     TEXT NULL
        );
        CREATE INDEX IX_AppInstances_StartedUtc ON AppInstances (StartedUtc);
        CREATE INDEX IX_AppInstances_Open ON AppInstances (EndedUtc) WHERE EndedUtc IS NULL;
        CREATE TABLE DailyUsage (
            AppId         INTEGER NOT NULL REFERENCES AppRecords (AppId) ON DELETE CASCADE,
            Day           TEXT NOT NULL,
            Seconds       INTEGER NOT NULL CHECK (Seconds >= 0),
            PRIMARY KEY (AppId, Day)
        );
        CREATE INDEX IX_DailyUsage_Day ON DailyUsage (Day);
        """,
    ];

    /// <inheritdoc />
    protected override IReadOnlyList<string> Migrations => SchemaMigrations;
}
