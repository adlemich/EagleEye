namespace EagleEye.ParentApp.Core.Data;

/// <summary>Stores <b>the</b> pairing: connection data in SQLite, the token in the secret store.</summary>
public interface IPairingStore
{
    /// <summary>Returns the pairing, or <c>null</c> if the app is not paired.</summary>
    Task<StoredPairing?> LoadAsync();

    /// <summary>Stores the pairing, replacing any earlier one.</summary>
    Task SaveAsync(StoredPairing pairing);

    /// <summary>Deletes the pairing and its token.</summary>
    Task DeleteAsync();
}
