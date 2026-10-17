using EagleEye.Service.Data;
using EagleEye.Shared.Data;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Data;

public sealed class AppRuleRepositoryTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 18, 0, 0, TimeSpan.Zero);

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly AppRuleRepository _repository;
    private readonly UsageRepository _usage;

    public AppRuleRepositoryTests()
    {
        _repository = new AppRuleRepository(_database);
        _usage = new UsageRepository(_database);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task LoadAll_AppWithoutRule_HasTheDefaultRule()
    {
        var app = await AppAsync(Kid, @"C:\Windows\notepad.exe");

        var stored = await _repository.LoadAllAsync();

        Assert.Equal(new StoredAppRecord(Kid, app, @"C:\Windows\notepad.exe", "Editor", 0, BreakTimeDays.None), stored.Apps.Single());
        Assert.Empty(stored.BudgetTexts);
    }

    [Fact]
    public async Task SetRule_StoresAndUpdates()
    {
        var app = await AppAsync(Kid, "a.exe");

        Assert.True(await _repository.SetRuleAsync(Kid, app, 45, BreakTimeDays.Monday, Now));
        Assert.True(await _repository.SetRuleAsync(Kid.ToLowerInvariant(), app, 180, BreakTimeDays.All, Now));

        var stored = (await _repository.LoadAllAsync()).Apps.Single();
        Assert.Equal((180, BreakTimeDays.All), (stored.BudgetMinutes, stored.Days));
    }

    [Fact]
    public async Task SetRule_DefaultRule_DeletesTheRow()
    {
        var app = await AppAsync(Kid, "a.exe");
        await _repository.SetRuleAsync(Kid, app, 45, BreakTimeDays.Monday, Now);

        Assert.True(await _repository.SetRuleAsync(Kid, app, 0, BreakTimeDays.None, Now));

        Assert.Equal(0, await CountAsync("AppRules"));
    }

    [Fact]
    public async Task SetRule_ZeroBudgetWithDays_IsStored()
    {
        var app = await AppAsync(Kid, "a.exe");

        await _repository.SetRuleAsync(Kid, app, 0, BreakTimeDays.Sunday, Now);

        Assert.Equal(1, await CountAsync("AppRules"));
    }

    [Fact]
    public async Task SetRule_UnknownAppOrOtherAccount_False()
    {
        var app = await AppAsync(Kid, "a.exe");

        Assert.False(await _repository.SetRuleAsync(Kid, app + 1, 45, BreakTimeDays.Monday, Now));
        Assert.False(await _repository.SetRuleAsync(Kid2, app, 45, BreakTimeDays.Monday, Now));
        Assert.Equal(0, await CountAsync("AppRules"));
    }

    [Fact]
    public async Task Schema_RejectsOutOfRangeValues()
    {
        var app = await AppAsync(Kid, "a.exe");

        await Assert.ThrowsAnyAsync<Exception>(() => _repository.SetRuleAsync(Kid, app, 1440, BreakTimeDays.Monday, Now));
        await Assert.ThrowsAnyAsync<Exception>(() => _repository.SetRuleAsync(Kid, app, 10, (BreakTimeDays)128, Now));
    }

    [Fact]
    public async Task AppRecordDeleted_RuleCascades()
    {
        var app = await AppAsync(Kid, "a.exe");
        await _repository.SetRuleAsync(Kid, app, 45, BreakTimeDays.Monday, Now);

        await _usage.PurgeAccountsNotInAsync([Kid2]);

        Assert.Equal(0, await CountAsync("AppRules"));
    }

    [Fact]
    public async Task BudgetText_SetReplaceAndDelete()
    {
        await _repository.SetBudgetTextAsync(Kid, "Schluss \U0001F60A", Now);
        await _repository.SetBudgetTextAsync(Kid, "Ende\nZeile", Now);
        await _repository.SetBudgetTextAsync(Kid2, "x", Now);
        Assert.Equal(
            [new StoredDisplayText(Kid, "Ende\nZeile"), new StoredDisplayText(Kid2, "x")],
            (await _repository.LoadAllAsync()).BudgetTexts);

        await _repository.SetBudgetTextAsync(Kid, null, Now);

        Assert.Equal([new StoredDisplayText(Kid2, "x")], (await _repository.LoadAllAsync()).BudgetTexts);
    }

    [Fact]
    public async Task PurgeBudgetTextsNotIn_ReturnsPurgedSids()
    {
        await _repository.SetBudgetTextAsync(Kid, "a", Now);
        await _repository.SetBudgetTextAsync(Kid2, "b", Now);

        var purged = await _repository.PurgeBudgetTextsNotInAsync([Kid2.ToLowerInvariant()]);

        Assert.Equal([Kid], purged);
        Assert.Equal([new StoredDisplayText(Kid2, "b")], (await _repository.LoadAllAsync()).BudgetTexts);
    }

    [Fact]
    public async Task Guards_Throw()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.SetRuleAsync(" ", 1, 1, BreakTimeDays.All, Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.SetBudgetTextAsync(" ", "x", Now));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.PurgeBudgetTextsNotInAsync(null!));
    }

    [Fact]
    public async Task Migration5_OnVersion4File_KeepsDataAndEveryAppIsDefault()
    {
        var file = Path.Combine(Path.GetTempPath(), "eagleeye-tests", Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try
        {
            await CreateVersion4FileAsync(file);

            await using var database = new ServiceDatabase(ServiceDatabase.BuildConnectionString(file));
            await database.InitializeAsync();

            Assert.Equal(5, await database.GetSchemaVersionAsync());
            Assert.NotNull(await new PairedDeviceRepository(database).FindByTokenHashAsync(new byte[32]));
            Assert.True((await new AccountSelectionRepository(database).LoadAllAsync())[Kid]);
            Assert.Equal(605, (await new UsageRepository(database).GetUsageAsync(Kid, new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 9))).Single().Seconds);
            var breaks = await new BreakTimeRepository(database).LoadAllAsync();
            Assert.Equal((1, "Pause"), (breaks.Entries.Count, breaks.Texts.Single().Text));
            var rules = await new AppRuleRepository(database).LoadAllAsync();
            Assert.Equal(2, rules.Apps.Count);
            Assert.All(rules.Apps, a => Assert.Equal((0, BreakTimeDays.None), (a.BudgetMinutes, a.Days)));
            Assert.Null(await new WarningSettingsRepository(database).LoadAsync());
            Assert.Equal(
                ["blocked start", "break time", "0", "kid1", "app start", "3", "1200", "1439", "31", "Sa", "closed gracefully", "0.4", "shown"],
                await HistoryRowAsync(database));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static async Task CreateVersion4FileAsync(string file)
    {
        await using (var version4 = new Version4Database(ServiceDatabase.BuildConnectionString(file)))
        {
            await version4.InitializeAsync();
            await version4.ExecuteAsync(async (connection, token) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"""
                    INSERT INTO PairedDevices VALUES ('device-1', 'Dad', zeroblob(32), '2026-10-04T12:00:00.0000000+00:00');
                    INSERT INTO AccountSelections VALUES ('{Kid}', 'kid1', 1, '2026-10-07T12:00:00.0000000+00:00');
                    INSERT INTO AppRecords VALUES (1, '{Kid}', 'C:\Windows\notepad.exe', 'notepad.exe', 'Editor', '2026-10-09T08:00:00.0000000+00:00', '2026-10-09T08:10:00.0000000+00:00');
                    INSERT INTO AppRecords VALUES (2, '{Kid}', 'C:\Windows\mspaint.exe', 'mspaint.exe', 'Paint', '2026-10-09T08:00:00.0000000+00:00', '2026-10-09T08:10:00.0000000+00:00');
                    INSERT INTO DailyUsage VALUES (1, '2026-10-09', 605);
                    INSERT INTO BreakTimeEntries (AccountSid, IsActive, StartMinute, EndMinute, Days, CreatedUtc, ChangedUtc)
                        VALUES ('{Kid}', 1, 1200, 1439, 31, '2026-10-10T12:00:00.0000000+00:00', '2026-10-10T12:00:00.0000000+00:00');
                    INSERT INTO AccountDisplayTexts VALUES ('{Kid}', 'Pause', '2026-10-10T12:00:00.0000000+00:00');
                    INSERT INTO BlockedStarts (AccountSid, UserName, StartedUtc, StartedLocal, DetectedUtc, DisplayName, ProcessName, ProgramPath,
                        ProcessId, Trigger, EntryId, EntryStartMinute, EntryEndMinute, EntryDays, Weekday, Outcome, SecondsUntilGone, MessageState, CompletedUtc)
                        VALUES ('{Kid}', 'kid1', '2026-10-10T18:10:00.0000000+00:00', '2026-10-10 20:10:00', '2026-10-10T18:10:02.0000000+00:00',
                        'Editor', 'notepad.exe', 'C:\Windows\notepad.exe', 5120, 'app start', 3, 1200, 1439, 31, 'Sa', 'closed gracefully', 0.4, 'shown',
                        '2026-10-10T18:10:03.0000000+00:00');
                    """;
                return await command.ExecuteNonQueryAsync(token);
            });
        }

        SqliteConnectionPoolClear();
    }

    private static void SqliteConnectionPoolClear() => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

    private static Task<string?[]> HistoryRowAsync(ServiceDatabase database)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Kind, Reason, AfterGrace, UserName, Trigger, EntryId, EntryStartMinute, EntryEndMinute, EntryDays, Weekday, Outcome,
                    SecondsUntilGone, MessageState FROM BlockedStarts;
                """;
            using var reader = await command.ExecuteReaderAsync(token);
            await reader.ReadAsync(token);
            var values = new string?[reader.FieldCount];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
            }

            return values;
        });
    }

    private async Task<long> AppAsync(string sid, string path) =>
        (await _usage.GetOrCreateAppAsync(sid, path, Path.GetFileName(path), "Editor", Now)).AppId;

    private Task<long> CountAsync(string table)
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table};";
            return (long)(await command.ExecuteScalarAsync(token))!;
        });
    }

    /// <summary>The service database as it was in 0.5.0: migrations 1 to 4.</summary>
    private sealed class Version4Database(string connectionString) : SqliteDatabase(connectionString)
    {
        protected override IReadOnlyList<string> Migrations => [.. ServiceDatabase.AllMigrations.Take(4)];
    }
}
