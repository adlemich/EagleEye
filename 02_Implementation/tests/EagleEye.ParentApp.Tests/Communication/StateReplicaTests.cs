using EagleEye.ParentApp.Core.Communication;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.ParentApp.Tests.Communication;

public sealed class StateReplicaTests
{
    private readonly StateReplica<UserAccountListDto> _replica = new(s => s.Revision);

    [Fact]
    public void New_IsEmptyWithRevision0()
    {
        Assert.Equal((null, 0L), (_replica.Current, _replica.Revision));
    }

    [Fact]
    public void TryApply_HigherRevision_Applies()
    {
        var snapshot = Snapshot(3);

        Assert.True(_replica.TryApply(snapshot));
        Assert.Equal((snapshot, 3L), (_replica.Current, _replica.Revision));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(4)]
    public void TryApply_EqualOrLowerRevision_IsIgnored(long revision)
    {
        var current = Snapshot(5);
        _replica.TryApply(current);

        Assert.False(_replica.TryApply(Snapshot(revision)));
        Assert.Same(current, _replica.Current);
    }

    [Fact]
    public void Reset_ForgetsSnapshotAndAcceptsAnyRevisionAgain()
    {
        _replica.TryApply(Snapshot(9));

        _replica.Reset();
        var afterReset = (_replica.Current, _replica.Revision);

        Assert.Equal((null, 0L), afterReset);
        Assert.True(_replica.TryApply(Snapshot(1)));
    }

    [Fact]
    public void TryApply_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _replica.TryApply(null!));
    }

    [Fact]
    public void Constructor_NullRevisionOf_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new StateReplica<UserAccountListDto>(null!));
    }

    private static UserAccountListDto Snapshot(long revision) => new(revision, null, []);
}
