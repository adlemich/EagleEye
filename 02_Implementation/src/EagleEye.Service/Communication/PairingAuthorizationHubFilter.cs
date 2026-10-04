using System.Reflection;
using EagleEye.Shared.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace EagleEye.Service.Communication;

/// <summary>
/// Default-deny filter for <see cref="ParentHub"/> (ADR-008 §4): methods without
/// <see cref="AllowUnpairedAttribute"/> require a paired connection, and the pairing methods are
/// callable only by unpaired connections.
/// </summary>
public sealed class PairingAuthorizationHubFilter : IHubFilter
{
    /// <summary>Message for a protected method called by an unpaired connection.</summary>
    public const string NotPairedMessage = "Not paired.";

    /// <summary>Message for a pairing method called by a paired connection.</summary>
    public const string AlreadyPairedMessage = "Already paired.";

    private static readonly HashSet<string> UnpairedOnlyMethods =
        new([nameof(IParentHub.StartPairing), nameof(IParentHub.SubmitPairingCode)], StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        ArgumentNullException.ThrowIfNull(invocationContext);
        ArgumentNullException.ThrowIfNull(next);

        var method = invocationContext.HubMethod;
        var isPaired = ParentConnectionState.IsPaired(invocationContext.Context);

        if (!isPaired && method.GetCustomAttribute<AllowUnpairedAttribute>() is null)
        {
            throw new HubException(NotPairedMessage);
        }

        if (isPaired && UnpairedOnlyMethods.Contains(method.Name))
        {
            throw new HubException(AlreadyPairedMessage);
        }

        return next(invocationContext);
    }
}
