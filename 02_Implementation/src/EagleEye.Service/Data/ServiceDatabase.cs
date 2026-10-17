using EagleEye.Shared.Data;

namespace EagleEye.Service.Data;

/// <summary>
/// The service database <c>%ProgramData%\EagleEye\EagleEye.Service.db</c>. Migration 1 creates
/// <c>PairedDevices</c>; the token is stored only as its SHA-256 hash (ADR-004, ADR-008 §5).
/// Migration 2 creates <c>AccountSelections</c> (US-003): one row per account the parent has ticked
/// or unticked at least once, keyed by SID; no row means "not under parental control".
/// Migration 3 creates <c>AppRecords</c> (app inventory, kept without time limit), <c>AppInstances</c> (history)
/// and <c>DailyUsage</c> (seconds per app and local day) (US-004, ADR-012).
/// Migration 4 creates <c>BreakTimeEntries</c> and <c>AccountDisplayTexts</c> (the rules of US-005, no row = default
/// text), <c>BlockedStarts</c> (history of blocked starts, 90 days, ADR-013 §7) and <c>TimeChangeFindings</c>
/// (time-zone and clock changes, 90 days, ADR-013 §9). History rows have no foreign keys: entries may be deleted later.
/// Migration 5 (US-006, ADR-015 §8) creates <c>AppRules</c> (no row = default rule 0:00, no day: deny by default, no data
/// copy), <c>AccountBudgetTexts</c> (no row = default text, ADR-016) and <c>WarningSettings</c> (at most one row; no row =
/// 5 / 1 minutes and default texts), and rebuilds <c>BlockedStarts</c> with nullable entry columns plus kind, reason and
/// app-rule columns (old rows: kind "blocked start", reason "break time").
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
        """
        CREATE TABLE BreakTimeEntries (
            EntryId      INTEGER PRIMARY KEY AUTOINCREMENT,
            AccountSid   TEXT NOT NULL,
            IsActive     INTEGER NOT NULL CHECK (IsActive IN (0, 1)),
            StartMinute  INTEGER NOT NULL CHECK (StartMinute BETWEEN 0 AND 1438),
            EndMinute    INTEGER NOT NULL CHECK (EndMinute BETWEEN 1 AND 1439),
            Days         INTEGER NOT NULL CHECK (Days BETWEEN 1 AND 127),
            CreatedUtc   TEXT NOT NULL,
            ChangedUtc   TEXT NOT NULL,
            CHECK (EndMinute > StartMinute)
        );
        CREATE INDEX IX_BreakTimeEntries_Account ON BreakTimeEntries (AccountSid, EntryId);
        CREATE TABLE AccountDisplayTexts (
            AccountSid   TEXT PRIMARY KEY NOT NULL,
            Text         TEXT NOT NULL,
            ChangedUtc   TEXT NOT NULL
        );
        CREATE TABLE BlockedStarts (
            BlockedStartId   INTEGER PRIMARY KEY AUTOINCREMENT,
            AccountSid       TEXT NOT NULL,
            UserName         TEXT NOT NULL,
            StartedUtc       TEXT NOT NULL,
            StartedLocal     TEXT NOT NULL,
            DetectedUtc      TEXT NOT NULL,
            DisplayName      TEXT NOT NULL,
            ProcessName      TEXT NOT NULL,
            ProgramPath      TEXT NOT NULL,
            ProcessId        INTEGER NOT NULL,
            Trigger          TEXT NOT NULL,
            EntryId          INTEGER NOT NULL,
            EntryStartMinute INTEGER NOT NULL,
            EntryEndMinute   INTEGER NOT NULL,
            EntryDays        INTEGER NOT NULL,
            Weekday          TEXT NOT NULL,
            Outcome          TEXT NULL,
            SecondsUntilGone REAL NULL,
            MessageState     TEXT NULL,
            CompletedUtc     TEXT NULL
        );
        CREATE INDEX IX_BlockedStarts_DetectedUtc ON BlockedStarts (DetectedUtc);
        CREATE INDEX IX_BlockedStarts_Account ON BlockedStarts (AccountSid);
        CREATE TABLE TimeChangeFindings (
            FindingId     INTEGER PRIMARY KEY AUTOINCREMENT,
            DetectedUtc   TEXT NOT NULL,
            DetectedLocal TEXT NOT NULL,
            Kind          TEXT NOT NULL CHECK (Kind IN ('TimeZone', 'Clock')),
            OldValue      TEXT NOT NULL,
            NewValue      TEXT NOT NULL,
            SessionId     INTEGER NULL,
            AccountSid    TEXT NULL,
            UserName      TEXT NULL
        );
        CREATE INDEX IX_TimeChangeFindings_DetectedUtc ON TimeChangeFindings (DetectedUtc);
        """,
        """
        CREATE TABLE AppRules (
            AppId          INTEGER PRIMARY KEY NOT NULL REFERENCES AppRecords (AppId) ON DELETE CASCADE,
            BudgetMinutes  INTEGER NOT NULL CHECK (BudgetMinutes BETWEEN 0 AND 1439),
            Days           INTEGER NOT NULL CHECK (Days BETWEEN 0 AND 127),
            ChangedUtc     TEXT NOT NULL
        );
        CREATE TABLE AccountBudgetTexts (
            AccountSid   TEXT PRIMARY KEY NOT NULL,
            Text         TEXT NOT NULL,
            ChangedUtc   TEXT NOT NULL
        );
        CREATE TABLE WarningSettings (
            Id            INTEGER PRIMARY KEY NOT NULL CHECK (Id = 1),
            EarlyMinutes  INTEGER NOT NULL CHECK (EarlyMinutes BETWEEN 1 AND 59),
            EarlyText     TEXT NULL,
            FinalMinutes  INTEGER NOT NULL CHECK (FinalMinutes BETWEEN 1 AND 59),
            FinalText     TEXT NULL,
            ChangedUtc    TEXT NOT NULL,
            CHECK (EarlyMinutes > FinalMinutes)
        );
        CREATE TABLE BlockedStarts_v5 (
            BlockedStartId   INTEGER PRIMARY KEY AUTOINCREMENT,
            Kind             TEXT NOT NULL CHECK (Kind IN ('blocked start', 'running app')),
            Reason           TEXT NOT NULL CHECK (Reason IN ('break time', 'not allowed today', 'budget used up')),
            AfterGrace       INTEGER NOT NULL DEFAULT 0 CHECK (AfterGrace IN (0, 1)),
            AccountSid       TEXT NOT NULL,
            UserName         TEXT NOT NULL,
            StartedUtc       TEXT NOT NULL,
            StartedLocal     TEXT NOT NULL,
            DetectedUtc      TEXT NOT NULL,
            DisplayName      TEXT NOT NULL,
            ProcessName      TEXT NOT NULL,
            ProgramPath      TEXT NOT NULL,
            ProcessId        INTEGER NOT NULL,
            Trigger          TEXT NOT NULL,
            EntryId          INTEGER NULL,
            EntryStartMinute INTEGER NULL,
            EntryEndMinute   INTEGER NULL,
            EntryDays        INTEGER NULL,
            BudgetMinutes    INTEGER NULL,
            BudgetDays       INTEGER NULL,
            UsedSeconds      INTEGER NULL,
            Weekday          TEXT NOT NULL,
            Outcome          TEXT NULL,
            SecondsUntilGone REAL NULL,
            MessageState     TEXT NULL,
            CompletedUtc     TEXT NULL
        );
        INSERT INTO BlockedStarts_v5 (BlockedStartId, Kind, Reason, AfterGrace, AccountSid, UserName, StartedUtc, StartedLocal,
            DetectedUtc, DisplayName, ProcessName, ProgramPath, ProcessId, Trigger, EntryId, EntryStartMinute, EntryEndMinute,
            EntryDays, Weekday, Outcome, SecondsUntilGone, MessageState, CompletedUtc)
        SELECT BlockedStartId, 'blocked start', 'break time', 0, AccountSid, UserName, StartedUtc, StartedLocal, DetectedUtc,
            DisplayName, ProcessName, ProgramPath, ProcessId, Trigger, EntryId, EntryStartMinute, EntryEndMinute, EntryDays,
            Weekday, Outcome, SecondsUntilGone, MessageState, CompletedUtc FROM BlockedStarts;
        DROP TABLE BlockedStarts;
        ALTER TABLE BlockedStarts_v5 RENAME TO BlockedStarts;
        CREATE INDEX IX_BlockedStarts_DetectedUtc ON BlockedStarts (DetectedUtc);
        CREATE INDEX IX_BlockedStarts_Account ON BlockedStarts (AccountSid);
        """,
    ];

    /// <summary>All migrations (exposed so tests can build a database of an earlier version).</summary>
    internal static IReadOnlyList<string> AllMigrations => SchemaMigrations;

    /// <inheritdoc />
    protected override IReadOnlyList<string> Migrations => SchemaMigrations;
}
