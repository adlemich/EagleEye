using EagleEye.Service.SessionAgent;
using Xunit;

namespace EagleEye.Service.Tests.SessionAgent;

/// <summary>The close command and its answer (US-005 Decision 5): <see cref="AgentProtocol"/>, <see cref="AgentCommandReader"/>.</summary>
public sealed class AgentCommandTests
{
    private static readonly CloseCommand Command = new(17, [new CloseTarget(4711, 133420000000000000), new CloseTarget(812, 5)]);

    [Fact]
    public void SerializeCloseCommand_WritesCompactLine()
    {
        Assert.Equal(
            """{"cmd":"close","id":17,"targets":[{"pid":4711,"created":133420000000000000},{"pid":812,"created":5}]}""",
            AgentProtocol.SerializeCloseCommand(Command));
    }

    [Fact]
    public void Command_RoundTrip()
    {
        Assert.True(AgentCommandReader.TryParse(AgentProtocol.SerializeCloseCommand(Command), out var parsed));
        Assert.Equal(Command.Id, parsed.Id);
        Assert.Equal(Command.Targets, parsed.Targets);
    }

    [Fact]
    public void TryParse_MaxTargets_Valid()
    {
        var targets = Enumerable.Range(1, AgentCommandReader.MaxTargets).Select(i => new CloseTarget(i, i)).ToList();

        Assert.True(AgentCommandReader.TryParse(AgentProtocol.SerializeCloseCommand(new CloseCommand(1, targets)), out _));
    }

    [Fact]
    public void TryParse_TooManyTargets_Rejected()
    {
        var targets = Enumerable.Range(1, AgentCommandReader.MaxTargets + 1).Select(i => new CloseTarget(i, i)).ToList();

        Assert.False(AgentCommandReader.TryParse(AgentProtocol.SerializeCloseCommand(new CloseCommand(1, targets)), out _));
    }

    [Fact]
    public void TryParse_TooLong_Rejected()
    {
        var line = """{"cmd":"close","id":1,"targets":[{"pid":1,"created":1}]}""" + new string(' ', AgentCommandReader.MaxLineLength);

        Assert.False(AgentCommandReader.TryParse(line, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("""{"cmd":"kill","id":1,"targets":[{"pid":1,"created":1}]}""")]
    [InlineData("""{"cmd":"close","id":-1,"targets":[{"pid":1,"created":1}]}""")]
    [InlineData("""{"cmd":"close","id":1,"targets":[]}""")]
    [InlineData("""{"cmd":"close","id":1,"targets":[null]}""")]
    [InlineData("""{"cmd":"close","id":1,"targets":[{"pid":0,"created":1}]}""")]
    [InlineData("""{"cmd":"close","id":1,"targets":[{"pid":-4,"created":1}]}""")]
    [InlineData("""{"cmd":"close","id":1,"targets":[{"pid":1,"created":0}]}""")]
    [InlineData("""{"cmd":"close","id":1,"targets":[{"pid":1}]}""")]
    [InlineData("""{"cmd":"close","id":1,"targets":[{"pid":1,"created":1,"x":1}]}""")]
    [InlineData("""{"cmd":"close","targets":[{"pid":1,"created":1}]}""")]
    [InlineData("""{"cmd":"close","id":1,"id":2,"targets":[{"pid":1,"created":1}]}""")]
    [InlineData("""{"cmd":"close","id":1,"targets":[{"pid":1,"created":1}],"extra":true}""")]
    [InlineData("""{"seq":1,"truncated":false,"apps":[]}""")]
    public void TryParse_Malformed_Rejected(string? line)
    {
        Assert.False(AgentCommandReader.TryParse(line, out _));
    }

    [Fact]
    public void SerializeCloseAnswer_WritesCompactLine()
    {
        Assert.Equal("""{"closed":{"id":17,"windows":2,"missing":0}}""", AgentProtocol.SerializeCloseAnswer(new CloseAnswer(17, 2, 0)));
    }

    [Fact]
    public void Answer_RoundTrip()
    {
        Assert.True(AgentProtocol.TryParseCloseAnswer(AgentProtocol.SerializeCloseAnswer(new CloseAnswer(3, 1, 1)), out var answer));
        Assert.Equal(new CloseAnswer(3, 1, 1), answer);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{\"closed\":")]
    [InlineData("null")]
    [InlineData("""{"closed":null}""")]
    [InlineData("""{"closed":{"id":-1,"windows":0,"missing":0}}""")]
    [InlineData("""{"closed":{"id":1,"windows":-1,"missing":0}}""")]
    [InlineData("""{"closed":{"id":1,"windows":0,"missing":-1}}""")]
    [InlineData("""{"closed":{"id":1,"windows":0}}""")]
    [InlineData("""{"closed":{"id":1,"windows":0,"missing":0},"x":1}""")]
    public void TryParseCloseAnswer_Malformed_Rejected(string? line)
    {
        Assert.False(AgentProtocol.TryParseCloseAnswer(line, out _));
    }

    [Theory]
    [InlineData("""{"closed":{"id":1,"windows":0,"missing":0}}""", true)]
    [InlineData("""{"seq":1,"truncated":false,"apps":[]}""", false)]
    [InlineData(null, false)]
    public void IsCloseAnswer_TellsAnswersFromReports(string? line, bool expected)
    {
        Assert.Equal(expected, AgentProtocol.IsCloseAnswer(line));
    }

    [Fact]
    public void Report_IsNotAnAnswerAndAnswerIsNotAReport()
    {
        Assert.False(AgentProtocol.TryParseCloseAnswer("""{"seq":1,"truncated":false,"apps":[]}""", out _));
        Assert.False(AgentProtocol.TryParse("""{"closed":{"id":1,"windows":0,"missing":0}}""", out _));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => AgentProtocol.SerializeCloseCommand(null!));
        Assert.Throws<ArgumentNullException>(() => AgentProtocol.SerializeCloseAnswer(null!));
    }
}

