using System.Security.Principal;
using EagleEye.Service.Pairing;
using EagleEye.Service.Rules;
using EagleEye.Service.Statistics;
using EagleEye.Service.UserAccounts;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Contracts;
using EagleEye.Shared.Models;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// SignalR hub for parent apps on the TLS endpoint (ADR-008). A connection that presents a known
/// token (<c>Authorization: Bearer</c>) is paired; all other connections may only pair
/// (enforced by <see cref="PairingAuthorizationHubFilter"/>). Thin routing layer: the pairing
/// logic lives in <see cref="IPairingManager"/>. Clients only see <see cref="HubException"/>s with
/// safe messages (coding guidelines §7.4).
/// </summary>
public sealed class ParentHub(
    IPairingManager pairing,
    IParentConnectionRegistry registry,
    IUserAccountService userAccounts,
    IUsageService usage,
    IBreakTimeService rules,
    ILogger<ParentHub> logger) : Hub<IParentClientCallback>, IParentHub
{
    /// <summary>Group of all paired parent connections.</summary>
    public const string ParentsGroup = "Parents";

    internal const string StartFailedMessage = "Pairing could not be started.";
    internal const string SubmitFailedMessage = "Pairing failed.";
    internal const string RemoveFailedMessage = "The device could not be removed.";
    internal const string UnknownDeviceMessage = "Unknown device.";
    internal const string AccountsUnavailableMessage = "The accounts are not available.";
    internal const string InvalidRequestMessage = "Invalid request.";
    internal const string UnknownAccountMessage = "Unknown account.";
    internal const string SaveFailedMessage = "The change could not be saved.";
    internal const string UsageUnavailableMessage = "The usage is not available.";
    internal const string RulesUnavailableMessage = "The rules are not available.";
    internal const string EntryNotFoundMessage = "The entry no longer exists.";
    internal const string EndNotAfterStartMessage = "The end time must be later than the start time.";
    internal const string NoDaySelectedMessage = "Select at least one day.";
    internal const string TooManyEntriesMessage = "Too many entries.";

    /// <inheritdoc />
    public async Task<AccountRulesDto> GetAccountRules(string accountSid)
    {
        if (!IsValidSid(accountSid))
        {
            throw new HubException(InvalidRequestMessage);
        }

        try
        {
            return await rules.GetAsync(accountSid);
        }
        catch (UnknownAccountException)
        {
            throw new HubException(UnknownAccountMessage);
        }
        catch (Exception ex) when (ex is not HubException)
        {
            logger.LogError(ex, "Reading the rules of {AccountSid} failed.", accountSid);
            throw new HubException(RulesUnavailableMessage);
        }
    }

    /// <inheritdoc />
    public Task<StateWriteAckDto> AddBreakTimeEntry(Guid requestId, string accountSid) =>
        WriteRulesAsync(requestId, accountSid, true, device => rules.AddEntryAsync(requestId, accountSid, device));

    /// <inheritdoc />
    public Task<StateWriteAckDto> DeleteBreakTimeEntry(Guid requestId, string accountSid, long entryId) =>
        WriteRulesAsync(requestId, accountSid, true, device => rules.DeleteEntryAsync(requestId, accountSid, entryId, device));

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetBreakTimeEntryActive(Guid requestId, string accountSid, long entryId, bool isActive) =>
        WriteRulesAsync(requestId, accountSid, true, device => rules.SetActiveAsync(requestId, accountSid, entryId, isActive, device));

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetBreakTimeEntryTime(Guid requestId, string accountSid, long entryId, BreakTimeBoundary boundary, int minute) =>
        WriteRulesAsync(
            requestId, accountSid, Enum.IsDefined(boundary) && BreakTimeRules.IsValidMinute(minute),
            device => rules.SetTimeAsync(requestId, accountSid, entryId, boundary, minute, device));

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetBreakTimeEntryDay(Guid requestId, string accountSid, long entryId, DayOfWeek day, bool isSelected) =>
        WriteRulesAsync(
            requestId, accountSid, Enum.IsDefined(day),
            device => rules.SetDayAsync(requestId, accountSid, entryId, day, isSelected, device));

    /// <inheritdoc />
    public Task<StateWriteAckDto> SetDisplayText(Guid requestId, string accountSid, string text) =>
        WriteRulesAsync(
            requestId, accountSid, text is not null && BreakTimeRules.IsValidDisplayText(BreakTimeRules.NormalizeLineBreaks(text)),
            device => rules.SetDisplayTextAsync(requestId, accountSid, text!, device)); // Checked for null in the condition.

    /// <inheritdoc />
    public async Task<AccountUsageDto> GetAccountUsage(string accountSid)
    {
        if (!IsValidSid(accountSid))
        {
            throw new HubException(InvalidRequestMessage);
        }

        try
        {
            return await usage.GetAccountUsageAsync(accountSid);
        }
        catch (UnknownAccountException)
        {
            throw new HubException(UnknownAccountMessage);
        }
        catch (Exception ex) when (ex is not HubException)
        {
            logger.LogError(ex, "Reading the usage of {AccountSid} failed.", accountSid);
            throw new HubException(UsageUnavailableMessage);
        }
    }

    /// <inheritdoc />
    public async Task<UserAccountListDto> GetUserAccounts()
    {
        try
        {
            return await userAccounts.GetSnapshotAsync();
        }
        catch (Exception ex) when (ex is not HubException)
        {
            logger.LogError(ex, "Reading the user accounts failed for connection {ConnectionId}.", Context.ConnectionId);
            throw new HubException(AccountsUnavailableMessage);
        }
    }

    /// <inheritdoc />
    public async Task<StateWriteAckDto> SetParentalControl(Guid requestId, string accountSid, bool isUnderParentalControl)
    {
        if (requestId == Guid.Empty || !IsValidSid(accountSid))
        {
            throw new HubException(InvalidRequestMessage);
        }

        try
        {
            // The default-deny filter only lets paired connections in, and they always carry a device name.
            var deviceName = ParentConnectionState.GetDeviceName(Context)!;
            return await userAccounts.SetParentalControlAsync(requestId, accountSid, isUnderParentalControl, deviceName);
        }
        catch (UnknownAccountException)
        {
            throw new HubException(UnknownAccountMessage);
        }
        catch (Exception ex) when (ex is not HubException)
        {
            logger.LogError(ex, "Setting parental control for {AccountSid} failed.", accountSid);
            throw new HubException(SaveFailedMessage);
        }
    }

    /// <inheritdoc />
    [AllowUnpaired]
    public Task<PairingStatusDto> GetPairingStatus()
    {
        var deviceId = ParentConnectionState.GetDeviceId(Context);
        return Task.FromResult(new PairingStatusDto(deviceId is not null, deviceId, ParentConnectionState.GetDeviceName(Context)));
    }

    /// <inheritdoc />
    [AllowUnpaired]
    public async Task StartPairing()
    {
        try
        {
            await pairing.StartPairingAsync(Context.ConnectionId);
        }
        catch (Exception ex) when (ex is not HubException)
        {
            logger.LogError(ex, "Starting pairing failed for connection {ConnectionId}.", Context.ConnectionId);
            throw new HubException(StartFailedMessage);
        }
    }

    /// <inheritdoc />
    [AllowUnpaired]
    public async Task<PairingResultDto> SubmitPairingCode(string code, string deviceName)
    {
        try
        {
            return await pairing.SubmitAsync(Context.ConnectionId, code, deviceName);
        }
        catch (Exception ex) when (ex is not HubException)
        {
            logger.LogError(ex, "Pairing failed for connection {ConnectionId}.", Context.ConnectionId);
            throw new HubException(SubmitFailedMessage);
        }
    }

    /// <inheritdoc />
    public async Task RemovePairedDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new HubException(UnknownDeviceMessage);
        }

        bool removed;
        try
        {
            removed = await pairing.RemoveDeviceAsync(deviceId);
        }
        catch (Exception ex) when (ex is not HubException)
        {
            logger.LogError(ex, "Removing parent device {DeviceId} failed.", deviceId);
            throw new HubException(RemoveFailedMessage);
        }

        if (!removed)
        {
            throw new HubException(UnknownDeviceMessage);
        }

        registry.AbortAll(deviceId, Context.ConnectionId);
        if (deviceId == ParentConnectionState.GetDeviceId(Context))
        {
            ParentConnectionState.Clear(Context);
            registry.Unregister(deviceId, Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ParentsGroup);
        }
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var token = BearerToken.Parse(Context.GetHttpContext()?.Request.Headers.Authorization.ToString());
        var device = await pairing.AuthenticateAsync(token);
        if (device is null)
        {
            logger.LogInformation("Unpaired parent app connected: {ConnectionId}.", Context.ConnectionId);
        }
        else
        {
            ParentConnectionState.SetPaired(Context, device.DeviceId, device.DeviceName);
            await Groups.AddToGroupAsync(Context.ConnectionId, ParentsGroup);
            registry.Register(device.DeviceId, Context);
            logger.LogInformation(
                "Paired parent device {DeviceId} ({DeviceName}) connected: {ConnectionId}.",
                device.DeviceId, device.DeviceName, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var deviceId = ParentConnectionState.GetDeviceId(Context);
        if (deviceId is not null)
        {
            registry.Unregister(deviceId, Context.ConnectionId);
        }

        logger.LogInformation(exception, "Parent app disconnected: {ConnectionId}.", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Common part of the rules writes (US-005, ADR-010): input check, the device name from the connection,
    /// and the mapping of the service's exceptions to safe texts (coding guidelines §7.4).
    /// </summary>
    private async Task<StateWriteAckDto> WriteRulesAsync(
        Guid requestId, string accountSid, bool argumentsValid, Func<string, Task<StateWriteAckDto>> write)
    {
        if (requestId == Guid.Empty || !IsValidSid(accountSid) || !argumentsValid)
        {
            throw new HubException(InvalidRequestMessage);
        }

        try
        {
            // The default-deny filter only lets paired connections in, and they always carry a device name.
            return await write(ParentConnectionState.GetDeviceName(Context)!);
        }
        catch (Exception ex) when (ex is not HubException)
        {
            throw new HubException(RulesWriteFailure(ex, accountSid));
        }
    }

    /// <summary>The safe client text for a failed rules write; unexpected failures are logged.</summary>
    private string RulesWriteFailure(Exception ex, string accountSid)
    {
        switch (ex)
        {
            case UnknownAccountException:
                return UnknownAccountMessage;
            case EntryNotFoundException:
                return EntryNotFoundMessage;
            case TooManyEntriesException:
                return TooManyEntriesMessage;
            case RuleValidationException validation:
                return validation.Violation == RuleViolation.NoDaySelected ? NoDaySelectedMessage : EndNotAfterStartMessage;
            default:
                logger.LogError(ex, "Changing the rules of {AccountSid} failed.", accountSid);
                return SaveFailedMessage;
        }
    }

    /// <summary>Whether the text is a syntactically valid SID.</summary>
    internal static bool IsValidSid(string? sid)
    {
        if (string.IsNullOrWhiteSpace(sid))
        {
            return false;
        }

        try
        {
            _ = new SecurityIdentifier(sid);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
