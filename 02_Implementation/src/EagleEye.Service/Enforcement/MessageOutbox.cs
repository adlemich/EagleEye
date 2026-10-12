using EagleEye.Service.Communication;
using EagleEye.Shared.Models;

namespace EagleEye.Service.Enforcement;

/// <summary>
/// The warnings and grace messages of one session in one tick (US-006 Decision 6, plan D-4): only the most urgent is
/// sent (grace &gt; final &gt; early; break &gt; app at the same level, the first on a tie); the others are logged as
/// "not shown (replaced in the same tick)". The log entry of the sent message is written when the tray answered.
/// </summary>
internal sealed class MessageOutbox
{
    private readonly List<(int Priority, KidMessageDto Message, Action<string> Log)> _candidates = [];

    /// <summary>The priority of a reason: higher is more urgent.</summary>
    public static int PriorityOf(KidMessageReason reason) => reason switch
    {
        KidMessageReason.BreakGracePeriod => 6,
        KidMessageReason.AppGracePeriod => 5,
        KidMessageReason.BreakFinalWarning => 4,
        KidMessageReason.AppFinalWarning => 3,
        KidMessageReason.BreakEarlyWarning => 2,
        _ => 1,
    };

    /// <summary>Adds a candidate; <paramref name="log"/> receives the message state.</summary>
    public void Add(KidMessageDto message, Action<string> log) => _candidates.Add((PriorityOf(message.Reason), message, log));

    /// <summary>Sends the most urgent candidate (not awaited) and logs the others as replaced.</summary>
    public void Send(IKidMessenger messenger, int sessionId)
    {
        if (_candidates.Count == 0)
        {
            return;
        }

        var chosen = _candidates.MaxBy(c => c.Priority);
        foreach (var other in _candidates.Where(c => !ReferenceEquals(c.Message, chosen.Message)))
        {
            other.Log(KidMessageStates.ReplacedInTick);
        }

        _ = LogWhenAnsweredAsync(messenger.ShowAsync(sessionId, chosen.Message, CancellationToken.None), chosen.Log);
    }

    private static async Task LogWhenAnsweredAsync(Task<string> state, Action<string> log) => log(await state.ConfigureAwait(false));
}
