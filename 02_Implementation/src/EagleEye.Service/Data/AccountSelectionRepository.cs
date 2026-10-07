using System.Globalization;
using System.Text;

namespace EagleEye.Service.Data;

/// <summary>
/// Parameterized SQL access to <c>AccountSelections</c> (coding guidelines §8.2). SIDs are stored as
/// Windows reports them (<see cref="UserAccounts.LocalAccountInfo.Sid"/>).
/// </summary>
public sealed class AccountSelectionRepository(ServiceDatabase database) : IAccountSelectionRepository
{
    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, bool>> LoadAllAsync(CancellationToken ct = default)
    {
        return database.ExecuteAsync<IReadOnlyDictionary<string, bool>>(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Sid, UnderParentalControl FROM AccountSelections;";
            using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            var selections = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                selections[reader.GetString(0)] = reader.GetInt64(1) == 1;
            }

            return selections;
        }, ct);
    }

    /// <inheritdoc />
    public Task SetAsync(
        string sid, string userName, bool isUnderParentalControl, DateTimeOffset changedAtUtc, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sid);
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        return database.ExecuteAsync(async (connection, token) =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO AccountSelections (Sid, UserName, UnderParentalControl, ChangedAtUtc)
                VALUES (@sid, @userName, @value, @changedAt)
                ON CONFLICT (Sid) DO UPDATE SET
                    UserName = excluded.UserName,
                    UnderParentalControl = excluded.UnderParentalControl,
                    ChangedAtUtc = excluded.ChangedAtUtc;
                """;
            command.Parameters.AddWithValue("@sid", sid);
            command.Parameters.AddWithValue("@userName", userName);
            command.Parameters.AddWithValue("@value", isUnderParentalControl ? 1 : 0);
            command.Parameters.AddWithValue("@changedAt", changedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }, ct);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> DeleteMissingAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existingSids);

        return database.ExecuteAsync<IReadOnlyList<string>>(async (connection, token) =>
        {
            // One statement (atomic without an explicit transaction); one parameter per existing SID.
            using var command = connection.CreateCommand();
            var parameters = new StringBuilder();
            var index = 0;
            foreach (var sid in existingSids)
            {
                var name = "@p" + index.ToString(CultureInfo.InvariantCulture);
                parameters.Append(index++ == 0 ? string.Empty : ", ").Append(name);
                command.Parameters.AddWithValue(name, sid);
            }

            command.CommandText = $"DELETE FROM AccountSelections WHERE Sid NOT IN ({parameters}) RETURNING Sid;";
            using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            var deleted = new List<string>();
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                deleted.Add(reader.GetString(0));
            }

            return deleted;
        }, ct);
    }
}
