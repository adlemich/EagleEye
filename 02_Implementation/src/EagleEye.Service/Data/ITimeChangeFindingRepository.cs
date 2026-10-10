namespace EagleEye.Service.Data;

/// <summary>Data access for <c>TimeChangeFindings</c> (ADR-013 §9, AC-37, FR-SVC-026).</summary>
public interface ITimeChangeFindingRepository
{
    /// <summary>Stores a finding.</summary>
    Task InsertAsync(TimeChangeFinding finding, CancellationToken ct = default);

    /// <summary>Deletes findings detected before <paramref name="cutoffUtc"/>; returns their number.</summary>
    Task<int> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken ct = default);

    /// <summary>
    /// Deletes the findings of accounts that are not in <paramref name="existingSids"/>; findings without an account
    /// are kept. Returns their number.
    /// </summary>
    Task<int> PurgeAccountsNotInAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default);
}
