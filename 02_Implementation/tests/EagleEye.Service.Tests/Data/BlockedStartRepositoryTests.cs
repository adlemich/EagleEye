using EagleEye.Service.Data;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Data;

public sealed class BlockedStartRepositoryTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 18, 10, 2, TimeSpan.Zero);

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly BlockedStartRepository _repository;

    public BlockedStartRepositoryTests()
    {
        _repository = new BlockedStartRepository(_database);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Insert_StoresAllFieldsWithOpenOutcome()
    {
        var id = await _repository.InsertAsync(Record(Kid, Now));

        Assert.Equal(
            [
                "S-1-5-21-1-2-3-1003", "kid1", Now.AddSeconds(-2).ToString("O"), "2026-10-10 20:10:00", Now.ToString("O"), "Editor",
                "notepad.exe", @"C:\Windows\System32\notepad.exe", "5120", "app start", "3", "1200", "1439", "31", "Sa",
                null, null, null, null,
            ],
            await RowAsync(id));
    }

    [Fact]
    public async Task Complete_StoresOutcome()
    {
        var id = await _repository.InsertAsync(Record(Kid, Now));

        await _repository.CompleteAsync(id, "closed gracefully", 0.4, "shown", Now.AddSeconds(1));

        Assert.Equal(["closed gracefully", "0.4", "shown", Now.AddSeconds(1).ToString("O")], (await RowAsync(id))[15..]);
    }

    [Fact]
    public async Task Complete_UnknownSeconds_StoresNull()
    {
        var id = await _repository.InsertAsync(Record(Kid, Now));

        await _repository.CompleteAsync(id, "terminated by force", null, "not shown (tray client not connected)", Now);

        Assert.Null((await RowAsync(id))[16]);
    }

    [Fact]
    public async Task CompleteDangling_OnlyOpenRecords()
    {
        var done = await _repository.InsertAsync(Record(Kid, Now));
        await _repository.CompleteAsync(done, "closed gracefully", 1, "shown", Now);
        var open = await _repository.InsertAsync(Record(Kid, Now));

        var count = await _repository.CompleteDanglingAsync("unknown (service stopped)", Now.AddHours(1));

        Assert.Equal((1, "closed gracefully", "unknown (service stopped)"), (count, (await RowAsync(done))[15], (await RowAsync(open))[15]));
    }

    [Fact]
    public async Task PurgeOlderThan_Day89KeptDay90Purged()
    {
        var cutoff = Now.AddDays(-90);
        var old = await _repository.InsertAsync(Record(Kid, cutoff.AddSeconds(-1)));
        var kept = await _repository.InsertAsync(Record(Kid, cutoff));

        var count = await _repository.PurgeOlderThanAsync(cutoff);

        Assert.Equal((1, 0, 19), (count, (await RowAsync(old)).Length, (await RowAsync(kept)).Length));
    }

    [Fact]
    public async Task PurgeAccountsNotIn_DeletesOtherSids()
    {
        await _repository.InsertAsync(Record(Kid, Now));
        await _repository.InsertAsync(Record(Kid, Now));
        var kept = await _repository.InsertAsync(Record(Kid2, Now));

        var count = await _repository.PurgeAccountsNotInAsync([Kid2.ToLowerInvariant()]);

        Assert.Equal((2, 19), (count, (await RowAsync(kept)).Length));
    }

    [Fact]
    public async Task Guards_Throw()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.InsertAsync(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.CompleteAsync(1, " ", 1, "shown", Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.CompleteAsync(1, "closed", 1, " ", Now));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _repository.CompleteDanglingAsync(" ", Now));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.PurgeAccountsNotInAsync(null!));
    }

    private static BlockedStartRecord Record(string sid, DateTimeOffset detected) => new(
        sid, "kid1", detected.AddSeconds(-2), new DateTime(2026, 10, 10, 20, 10, 0), detected, "Editor", "notepad.exe",
        @"C:\Windows\System32\notepad.exe", 5120, "app start", new BreakTimeEntryDto(3, true, 1200, 1439, (BreakTimeDays)31), "Sa");

    private Task<string?[]> RowAsync(long id)
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT AccountSid, UserName, StartedUtc, StartedLocal, DetectedUtc, DisplayName, ProcessName, ProgramPath, ProcessId,
                    Trigger, EntryId, EntryStartMinute, EntryEndMinute, EntryDays, Weekday, Outcome, SecondsUntilGone, MessageState, CompletedUtc
                FROM BlockedStarts WHERE BlockedStartId = @id;
                """;
            command.Parameters.AddWithValue("@id", id);
            using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                return [];
            }

            var values = new string?[reader.FieldCount];
            for (var i = 0; i < values.Length; i++)
            {
                values[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
            }

            return values;
        });
    }
}
