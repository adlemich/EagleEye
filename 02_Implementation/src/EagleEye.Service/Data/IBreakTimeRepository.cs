using EagleEye.Shared.Models;

namespace EagleEye.Service.Data;

/// <summary>Data access for <c>BreakTimeEntries</c> and <c>AccountDisplayTexts</c> (US-005, migration 4).</summary>
public interface IBreakTimeRepository
{
    /// <summary>Loads all entries (creation order) and all changed display texts.</summary>
    Task<StoredRules> LoadAllAsync(CancellationToken ct = default);

    /// <summary>Stores a new entry of the account and returns its id.</summary>
    Task<long> InsertEntryAsync(string accountSid, BreakTimeEntryDto values, DateTimeOffset nowUtc, CancellationToken ct = default);

    /// <summary>Stores the values of an existing entry; <c>false</c> if the entry of that account does not exist.</summary>
    Task<bool> UpdateEntryAsync(string accountSid, BreakTimeEntryDto entry, DateTimeOffset nowUtc, CancellationToken ct = default);

    /// <summary>Deletes an entry; <c>false</c> if the entry of that account does not exist.</summary>
    Task<bool> DeleteEntryAsync(string accountSid, long entryId, CancellationToken ct = default);

    /// <summary>Stores the display text of the account; <c>null</c> deletes it (default text).</summary>
    Task SetDisplayTextAsync(string accountSid, string? text, DateTimeOffset nowUtc, CancellationToken ct = default);

    /// <summary>Deletes entries and texts of all accounts that are not in <paramref name="existingSids"/>.</summary>
    Task<IReadOnlyList<RulesPurge>> PurgeAccountsNotInAsync(IReadOnlyCollection<string> existingSids, CancellationToken ct = default);
}
