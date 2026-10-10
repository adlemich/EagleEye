using EagleEye.Service.Monitoring;
using Xunit;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class AgentPlanTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Parent = "S-1-5-21-1-2-3-1001";

    private static readonly HashSet<string> Controlled = new(StringComparer.OrdinalIgnoreCase) { Kid };

    [Fact]
    public void Compute_StartsOnlyForLoggedOnControlledSessions()
    {
        SessionInfo[] sessions =
        [
            new(0, null, SessionConnectState.Disconnected, false),
            new(1, Parent, SessionConnectState.Active, false),
            new(2, Kid, SessionConnectState.Active, true),
            new(3, Kid2, SessionConnectState.Active, false),
            new(4, Kid, SessionConnectState.Disconnected, false),
            new(5, Kid, SessionConnectState.Connected, false),
            new(6, null, SessionConnectState.Active, false),
            new(0, Kid, SessionConnectState.Active, false),
        ];

        var plan = AgentPlan.Compute(sessions, Controlled, new Dictionary<int, AgentSlotState>());

        Assert.Equal([2, 4], plan.Start.Select(s => s.SessionId));
        Assert.Empty(plan.Stop);
    }

    [Fact]
    public void Compute_RunningAgentForEligibleSession_NothingToDo()
    {
        var plan = AgentPlan.Compute([Session(2)], Controlled, new Dictionary<int, AgentSlotState> { [2] = new(Kid, true, false) });

        Assert.Equal((0, 0), (plan.Start.Count, plan.Stop.Count));
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void Compute_ExitedAgent_RestartsOnlyWhenDue(bool due, int starts)
    {
        var plan = AgentPlan.Compute([Session(2)], Controlled, new Dictionary<int, AgentSlotState> { [2] = new(Kid, false, due) });

        Assert.Equal(starts, plan.Start.Count);
    }

    [Fact]
    public void Compute_Unticked_StopsRunningAgent()
    {
        var plan = AgentPlan.Compute([Session(2)], new HashSet<string>(), new Dictionary<int, AgentSlotState> { [2] = new(Kid, true, false) });

        Assert.Equal([2], plan.Stop);
    }

    [Fact]
    public void Compute_LoggedOff_StopsRunningAgent()
    {
        var plan = AgentPlan.Compute([], Controlled, new Dictionary<int, AgentSlotState> { [2] = new(Kid, true, false) });

        Assert.Equal([2], plan.Stop);
    }

    [Fact]
    public void Compute_OtherUserInSameSession_StopsRunningAgent()
    {
        var controlled = new HashSet<string>(Controlled, StringComparer.OrdinalIgnoreCase) { Kid2 };
        var plan = AgentPlan.Compute([Session(2, Kid2)], controlled, new Dictionary<int, AgentSlotState> { [2] = new(Kid, true, false) });

        Assert.Equal([2], plan.Stop);
        Assert.Empty(plan.Start);
    }

    [Fact]
    public void Compute_NotRunningSlotOfIneligibleSession_NotStopped()
    {
        var plan = AgentPlan.Compute([], Controlled, new Dictionary<int, AgentSlotState> { [2] = new(Kid, false, true) });

        Assert.Empty(plan.Stop);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 30)]
    [InlineData(9, 30)]
    public void RestartDelay_BackOff1_5_30(int failures, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), AgentPlan.RestartDelay(failures));
    }

    [Fact]
    public void RestartDelay_Zero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AgentPlan.RestartDelay(0));
    }

    [Theory]
    [InlineData(3, 59, 4)]
    [InlineData(3, 60, 1)]
    [InlineData(0, 0, 1)]
    public void NextFailureCount_ResetsAfterStableMinute(int previous, int runSeconds, int expected)
    {
        Assert.Equal(expected, AgentPlan.NextFailureCount(previous, TimeSpan.FromSeconds(runSeconds)));
    }

    [Fact]
    public void Guards_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AgentPlan.IsEligible(null!, Controlled));
        Assert.Throws<ArgumentNullException>(() => AgentPlan.IsEligible(Session(2), null!));
        Assert.Throws<ArgumentNullException>(() => AgentPlan.Compute(null!, Controlled, new Dictionary<int, AgentSlotState>()));
        Assert.Throws<ArgumentNullException>(() => AgentPlan.Compute([], Controlled, null!));
    }

    private static SessionInfo Session(int id, string sid = Kid) => new(id, sid, SessionConnectState.Active, false);
}
