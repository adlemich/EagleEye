using EagleEye.Service.Data;
using EagleEye.Shared.Data;
using EagleEye.Shared.Models;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EagleEye.Service.Tests.Data;

public sealed class AppGroupRepositoryTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Notepad = @"C:\Windows\notepad.exe";
    private const string Paint = @"C:\Windows\mspaint.exe";
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly AppGroupRepository _repository;

    public AppGroupRepositoryTests()
    {
        _repository = new AppGroupRepository(_database);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task LoadAll_Empty_NoGroupsAndEmptyCatalog()
    {
        var stored = await _repository.LoadAllAsync();

        Assert.Equal((0, 0, 0, 0), (stored.Groups.Count, stored.Members.Count, stored.Rules.Count, stored.Catalog.Count));
    }

    [Fact]
    public async Task LoadAll_ReturnsGroupsMembersRulesAndCatalog()
    {
        await new UsageRepository(_database).GetOrCreateAppAsync(Kid, Notepad, "notepad.exe", "Editor", Now);
        var id = await _repository.CreateGroupAsync("Games", "GAMES", Now);
        await _repository.SetMemberAsync(Notepad, id, Now);
        await _repository.SetRuleAsync(id, Kid, 60, BreakTimeDays.Saturday, Now);

        var stored = await _repository.LoadAllAsync();

        Assert.Equal(new StoredAppGroup(id, "Games"), stored.Groups.Single());
        Assert.Equal(new StoredAppGroupMember(Notepad, id), stored.Members.Single());
        Assert.Equal(new StoredAppGroupRule(id, Kid, 60, BreakTimeDays.Saturday), stored.Rules.Single());
        Assert.Equal(new StoredCatalogApp(Kid, Notepad, "Editor", Now), stored.Catalog.Single());
    }

    [Fact]
    public async Task CreateGroup_DuplicateKey_Throws()
    {
        await _repository.CreateGroupAsync("Games", "GAMES", Now);

        await Assert.ThrowsAsync<SqliteException>(() => _repository.CreateGroupAsync("games", "GAMES", Now));
    }

    [Fact]
    public async Task RenameGroup_ExistingTrue_GoneFalse()
    {
        var id = await _repository.CreateGroupAsync("Games", "GAMES", Now);

        Assert.True(await _repository.RenameGroupAsync(id, "Gaming", "GAMING", Now));
        Assert.False(await _repository.RenameGroupAsync(id + 1, "X", "X", Now));
        Assert.Equal("Gaming", (await _repository.LoadAllAsync()).Groups.Single().Name);
    }

    [Fact]
    public async Task DeleteGroup_CascadesMembersAndRules()
    {
        var id = await _repository.CreateGroupAsync("Games", "GAMES", Now);
        await _repository.SetMemberAsync(Notepad, id, Now);
        await _repository.SetRuleAsync(id, Kid, 60, BreakTimeDays.All, Now);

        Assert.True(await _repository.DeleteGroupAsync(id));
        Assert.False(await _repository.DeleteGroupAsync(id));

        var stored = await _repository.LoadAllAsync();
        Assert.Equal((0, 0, 0), (stored.Groups.Count, stored.Members.Count, stored.Rules.Count));
    }

    [Fact]
    public async Task SetMember_OneGroupPerPath_IgnoringCase()
    {
        var games = await _repository.CreateGroupAsync("Games", "GAMES", Now);
        var school = await _repository.CreateGroupAsync("School", "SCHOOL", Now);

        await _repository.SetMemberAsync(Notepad, games, Now);
        await _repository.SetMemberAsync(Notepad.ToUpperInvariant(), school, Now);

        Assert.Equal(school, (await _repository.LoadAllAsync()).Members.Single().GroupId);
    }

    [Fact]
    public async Task SetMember_Null_RemovesTheMembership()
    {
        var games = await _repository.CreateGroupAsync("Games", "GAMES", Now);
        await _repository.SetMemberAsync(Notepad, games, Now);
        await _repository.SetMemberAsync(Paint, games, Now);

        await _repository.SetMemberAsync(Notepad, null, Now);

        Assert.Equal(Paint, (await _repository.LoadAllAsync()).Members.Single().ProgramPath);
    }

    [Fact]
    public async Task SetMember_UnknownGroup_Throws()
    {
        await Assert.ThrowsAsync<SqliteException>(() => _repository.SetMemberAsync(Notepad, 99, Now));
    }

    [Fact]
    public async Task SetRule_UpsertsAndDefaultDeletes()
    {
        var id = await _repository.CreateGroupAsync("Games", "GAMES", Now);

        await _repository.SetRuleAsync(id, Kid, 60, BreakTimeDays.Saturday, Now);
        await _repository.SetRuleAsync(id, Kid.ToLowerInvariant(), 90, BreakTimeDays.All, Now);
        Assert.Equal((90, BreakTimeDays.All), ((await _repository.LoadAllAsync()).Rules.Single().BudgetMinutes, (await _repository.LoadAllAsync()).Rules.Single().Days));

        await _repository.SetRuleAsync(id, Kid, 0, BreakTimeDays.None, Now);
        Assert.Empty((await _repository.LoadAllAsync()).Rules);
    }

    [Fact]
    public async Task PurgeRulesNotIn_DeletesOnlyOtherAccountsRules()
    {
        var games = await _repository.CreateGroupAsync("Games", "GAMES", Now);
        var school = await _repository.CreateGroupAsync("School", "SCHOOL", Now);
        await _repository.SetMemberAsync(Notepad, games, Now);
        await _repository.SetRuleAsync(games, Kid, 60, BreakTimeDays.All, Now);
        await _repository.SetRuleAsync(school, Kid2, 60, BreakTimeDays.All, Now);
        await _repository.SetRuleAsync(games, Kid2, 30, BreakTimeDays.All, Now);

        var purged = await _repository.PurgeRulesNotInAsync([Kid.ToLowerInvariant()]);

        Assert.Equal([Kid2], purged);
        var stored = await _repository.LoadAllAsync();
        Assert.Equal((2, 1, Kid), (stored.Groups.Count, stored.Members.Count, stored.Rules.Single().AccountSid));
    }

    [Fact]
    public async Task Guards_Throw()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.CreateGroupAsync(" ", "K", Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.CreateGroupAsync("N", " ", Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.RenameGroupAsync(1, " ", "K", Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.RenameGroupAsync(1, "N", " ", Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.SetMemberAsync(" ", 1, Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.SetRuleAsync(1, " ", 1, BreakTimeDays.All, Now));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.PurgeRulesNotInAsync(null!));
    }

    [Fact]
    public async Task Migration6_OnVersion5File_KeepsDataAndHasNoGroups()
    {
        var file = Path.Combine(Path.GetTempPath(), "eagleeye-tests", Guid.NewGuid().ToString("N") + ".db");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try
        {
            await CreateVersion5FileAsync(file);

            await using var database = new ServiceDatabase(ServiceDatabase.BuildConnectionString(file));
            await database.InitializeAsync();

            Assert.Equal(7, await database.GetSchemaVersionAsync());
            Assert.True((await new AccountSelectionRepository(database).LoadAllAsync())[Kid]);
            Assert.Equal(605, (await new UsageRepository(database).GetUsageAsync(Kid, new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 30))).Single().Seconds);
            Assert.Equal((45, BreakTimeDays.All), ((await new AppRuleRepository(database).LoadAllAsync()).Apps.Single().BudgetMinutes, (await new AppRuleRepository(database).LoadAllAsync()).Apps.Single().Days));
            var groups = await new AppGroupRepository(database).LoadAllAsync();
            Assert.Equal((0, 0, 0, 1), (groups.Groups.Count, groups.Members.Count, groups.Rules.Count, groups.Catalog.Count));
            Assert.Empty(await new UsageRepository(database).GetSpansAsync(Kid, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
            Assert.Equal(new string?[] { "budget used up", "45", null, null }, await HistoryAsync(database));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static async Task CreateVersion5FileAsync(string file)
    {
        await using (var version5 = new Version5Database(ServiceDatabase.BuildConnectionString(file)))
        {
            await version5.InitializeAsync();
            await version5.ExecuteAsync(async (connection, token) =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"""
                    INSERT INTO AccountSelections VALUES ('{Kid}', 'kid1', 1, '2026-10-07T12:00:00.0000000+00:00');
                    INSERT INTO AppRecords VALUES (1, '{Kid}', '{Notepad}', 'notepad.exe', 'Editor', '2026-10-30T08:00:00.0000000+00:00', '2026-10-30T08:10:00.0000000+00:00');
                    INSERT INTO DailyUsage VALUES (1, '2026-10-30', 605);
                    INSERT INTO AppRules VALUES (1, 45, 127, '2026-10-30T08:00:00.0000000+00:00');
                    INSERT INTO BlockedStarts (Kind, Reason, AccountSid, UserName, StartedUtc, StartedLocal, DetectedUtc, DisplayName, ProcessName,
                        ProgramPath, ProcessId, Trigger, BudgetMinutes, BudgetDays, UsedSeconds, Weekday)
                        VALUES ('blocked start', 'budget used up', '{Kid}', 'kid1', '2026-10-30T18:10:00.0000000+00:00', '2026-10-30 19:10:00',
                        '2026-10-30T18:10:02.0000000+00:00', 'Editor', 'notepad.exe', '{Notepad}', 5120, 'app start', 45, 127, 2700, 'Fr');
                    """;
                return await command.ExecuteNonQueryAsync(token);
            });
        }

        SqliteConnection.ClearAllPools();
    }

    private static Task<string?[]> HistoryAsync(ServiceDatabase database)
    {
        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Reason, BudgetMinutes, GroupId, GroupName FROM BlockedStarts;";
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

    /// <summary>The service database as it was in 0.7.0: migrations 1 to 5.</summary>
    private sealed class Version5Database(string connectionString) : SqliteDatabase(connectionString)
    {
        protected override IReadOnlyList<string> Migrations => [.. ServiceDatabase.AllMigrations.Take(5)];
    }
}
