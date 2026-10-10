using EagleEye.Service.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace EagleEye.Service.Tests.Data;

public sealed class TimeChangeFindingRepositoryTests : IAsyncLifetime
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 18, 30, 5, TimeSpan.Zero);

    private readonly ServiceDatabase _database = new("Data Source=:memory:");
    private readonly TimeChangeFindingRepository _repository;

    public TimeChangeFindingRepositoryTests()
    {
        _repository = new TimeChangeFindingRepository(_database);
    }

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Insert_StoresAllFields()
    {
        await _repository.InsertAsync(Finding(Now, Kid));

        Assert.Equal(
            [[Now.ToString("O"), "2026-10-10 11:30:05", "TimeZone", "W. Europe Standard Time (UTC+01:00)", "Pacific Standard Time (UTC-08:00)", "2", Kid, "kid1"]],
            await RowsAsync());
    }

    [Fact]
    public async Task Insert_WithoutSession_StoresNulls()
    {
        await _repository.InsertAsync(Finding(Now, null) with { Kind = "Clock" });

        var row = Assert.Single(await RowsAsync());
        Assert.Equal(("Clock", null, null, null), (row[2], row[5], row[6], row[7]));
    }

    [Fact]
    public async Task Insert_UnknownKind_RejectedByTheSchema()
    {
        await Assert.ThrowsAsync<SqliteException>(() => _repository.InsertAsync(Finding(Now, null) with { Kind = "Other" }));
    }

    [Fact]
    public async Task PurgeOlderThan_Day89KeptDay90Purged()
    {
        var cutoff = Now.AddDays(-90);
        await _repository.InsertAsync(Finding(cutoff.AddSeconds(-1), Kid));
        await _repository.InsertAsync(Finding(cutoff, Kid));

        var count = await _repository.PurgeOlderThanAsync(cutoff);

        Assert.Equal((1, cutoff.ToString("O")), (count, Assert.Single(await RowsAsync())[0]));
    }

    [Fact]
    public async Task PurgeAccountsNotIn_DeletesOtherSidsAndKeepsRowsWithoutAccount()
    {
        await _repository.InsertAsync(Finding(Now, Kid));
        await _repository.InsertAsync(Finding(Now, Kid2));
        await _repository.InsertAsync(Finding(Now, null));

        var count = await _repository.PurgeAccountsNotInAsync([Kid2.ToLowerInvariant()]);

        Assert.Equal((1, 2), (count, (await RowsAsync()).Count));
    }

    [Fact]
    public async Task Guards_Throw()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.InsertAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.PurgeAccountsNotInAsync(null!));
    }

    private static TimeChangeFinding Finding(DateTimeOffset detected, string? sid) => new(
        detected, new DateTime(2026, 10, 10, 11, 30, 5), "TimeZone", "W. Europe Standard Time (UTC+01:00)",
        "Pacific Standard Time (UTC-08:00)", sid is null ? null : 2, sid, sid is null ? null : "kid1");

    private Task<List<string?[]>> RowsAsync()
    {
        return _database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DetectedUtc, DetectedLocal, Kind, OldValue, NewValue, SessionId, AccountSid, UserName
                FROM TimeChangeFindings ORDER BY FindingId;
                """;
            using var reader = await command.ExecuteReaderAsync(token);
            var rows = new List<string?[]>();
            while (await reader.ReadAsync(token))
            {
                var values = new string?[reader.FieldCount];
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
                }

                rows.Add(values);
            }

            return rows;
        });
    }
}
