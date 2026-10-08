using EagleEye.Service.Monitoring;
using Xunit;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class SessionStateTrackerTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Other = "S-1-5-21-1-2-3-1004";

    private readonly SessionStateTracker _tracker = new();

    public SessionStateTrackerTests()
    {
        _tracker.ApplySnapshot(
        [
            new SessionInfo(0, null, SessionConnectState.Disconnected, false),
            new SessionInfo(2, Kid, SessionConnectState.Active, false),
            new SessionInfo(3, Other, SessionConnectState.Disconnected, false),
        ]);
    }

    [Fact]
    public void IsInUse_ActiveUnlocked_True()
    {
        Assert.Equal((true, false, false, false), (_tracker.IsInUse(2), _tracker.IsInUse(3), _tracker.IsInUse(0), _tracker.IsInUse(9)));
    }

    [Fact]
    public void ApplyChange_Lock_NotInUse_Unlock_InUseAgain()
    {
        _tracker.ApplyChange(2, SessionChangeKind.Lock);
        var locked = _tracker.IsInUse(2);
        _tracker.ApplyChange(2, SessionChangeKind.Unlock);

        Assert.Equal((false, true), (locked, _tracker.IsInUse(2)));
    }

    [Theory]
    [InlineData(SessionChangeKind.ConsoleDisconnect)]
    [InlineData(SessionChangeKind.RemoteDisconnect)]
    public void ApplyChange_Disconnect_NotInUse(SessionChangeKind kind)
    {
        _tracker.ApplyChange(2, kind);

        Assert.Equal(SessionConnectState.Disconnected, _tracker.SessionsOf(Kid).Single().State);
    }

    [Theory]
    [InlineData(SessionChangeKind.ConsoleConnect)]
    [InlineData(SessionChangeKind.RemoteConnect)]
    [InlineData(SessionChangeKind.Logon)]
    public void ApplyChange_Connect_IsActive(SessionChangeKind kind)
    {
        _tracker.ApplyChange(3, kind);

        Assert.True(_tracker.IsInUse(3));
    }

    [Fact]
    public void ApplyChange_Logoff_RemovesSession()
    {
        _tracker.ApplyChange(2, SessionChangeKind.Logoff);

        Assert.Empty(_tracker.SessionsOf(Kid));
    }

    [Fact]
    public void ApplyChange_UnknownSession_Ignored()
    {
        _tracker.ApplyChange(9, SessionChangeKind.Logon);

        Assert.Equal(3, _tracker.Sessions.Count);
    }

    [Fact]
    public void ApplySnapshot_OverridesStaleState()
    {
        _tracker.ApplyChange(2, SessionChangeKind.Lock);

        _tracker.ApplySnapshot([new SessionInfo(2, Kid, SessionConnectState.Active, false)]);

        Assert.Equal((true, 1), (_tracker.IsInUse(2), _tracker.Sessions.Count));
    }

    [Fact]
    public void AccountInUse_RequiresSessionInUseAndObserved()
    {
        Assert.Equal(
            (true, false, false, false),
            (_tracker.AccountInUse(Kid, _ => true), _tracker.AccountInUse(Kid, _ => false),
             _tracker.AccountInUse(Other, _ => true), _tracker.AccountInUse("S-1-5-21-9", _ => true)));
    }

    [Fact]
    public void AccountInUse_AnyOfSeveralSessions()
    {
        _tracker.ApplySnapshot([new SessionInfo(2, Kid, SessionConnectState.Disconnected, false), new SessionInfo(4, Kid, SessionConnectState.Active, false)]);

        Assert.True(_tracker.AccountInUse(Kid.ToLowerInvariant(), id => id == 4));
    }

    [Fact]
    public void Guards_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _tracker.ApplySnapshot(null!));
        Assert.Throws<ArgumentNullException>(() => _tracker.SessionsOf(null!));
        Assert.Throws<ArgumentNullException>(() => _tracker.AccountInUse(Kid, null!));
    }

    [Theory]
    [InlineData(SessionConnectState.Active, false, true, true)]
    [InlineData(SessionConnectState.Active, true, false, true)]
    [InlineData(SessionConnectState.Disconnected, false, false, true)]
    [InlineData(SessionConnectState.Connected, false, false, false)]
    public void SessionInfo_InUseAndLoggedOn(SessionConnectState state, bool locked, bool inUse, bool loggedOn)
    {
        var session = new SessionInfo(2, Kid, state, locked);

        Assert.Equal((inUse, loggedOn), (session.IsInUse, session.IsLoggedOn));
    }

    [Fact]
    public void SessionInfo_NoUser_NotLoggedOn()
    {
        Assert.False(new SessionInfo(2, null, SessionConnectState.Active, false).IsLoggedOn);
    }
}
