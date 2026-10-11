using EagleEye.Service.Data;
using EagleEye.Service.Rules;
using EagleEye.Shared.Constants;
using EagleEye.Shared.Models;
using Xunit;

namespace EagleEye.Service.Tests.Rules;

public sealed class RulesSnapshotTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private static readonly BreakTimeEntryDto First = new(1, true, 1200, 1439, BreakTimeDays.All);
    private static readonly BreakTimeEntryDto Second = new(2, false, 0, 540, BreakTimeDays.Saturday);

    [Fact]
    public void Empty_ForAnyAccount_DefaultTextNoEntries()
    {
        var rules = RulesSnapshot.Empty.For(Kid);

        Assert.Equal((0, BreakTimeRules.DefaultDisplayText, true, false, true), (rules.Entries.Count, rules.DisplayText, rules.IsDefaultText, rules.HasActiveEntry, rules.IsEmpty));
        Assert.Empty(RulesSnapshot.Empty.AccountSids);
    }

    [Fact]
    public void From_GroupsEntriesInOrderAndTexts()
    {
        var stored = new StoredRules(
            [new StoredBreakTimeEntry(Kid, First), new StoredBreakTimeEntry(Kid2, Second), new StoredBreakTimeEntry(Kid.ToLowerInvariant(), Second)],
            [new StoredDisplayText(Kid2, "Anna \U0001F60A"), new StoredDisplayText("S-1-5-21-1-2-3-1005", "only text")]);

        var snapshot = RulesSnapshot.From(stored);

        Assert.Equal([First, Second], snapshot.For(Kid).Entries);
        Assert.Equal(("Anna \U0001F60A", false, false), (snapshot.For(Kid2).DisplayText, snapshot.For(Kid2).IsDefaultText, snapshot.For(Kid2).HasActiveEntry));
        Assert.Equal(3, snapshot.AccountSids.Count());
    }

    [Fact]
    public void With_ReplacesAndLeavesTheOriginalUnchanged()
    {
        var original = RulesSnapshot.Empty.With(Kid, new AccountRules([First], null));

        var changed = original.With(Kid, new AccountRules([First, Second], "x"));

        Assert.Equal((1, 2, "x"), (original.For(Kid).Entries.Count, changed.For(Kid).Entries.Count, changed.For(Kid).DisplayText));
    }

    [Fact]
    public void With_EmptyRules_RemovesTheAccount()
    {
        var snapshot = RulesSnapshot.Empty.With(Kid, new AccountRules([First], null)).With(Kid, AccountRules.Empty);

        Assert.Empty(snapshot.AccountSids);
    }

    [Fact]
    public void Without_RemovesAccounts()
    {
        var snapshot = RulesSnapshot.Empty.With(Kid, new AccountRules([First], null)).With(Kid2, new AccountRules([], "t"));

        Assert.Equal([Kid2], snapshot.Without([Kid.ToLowerInvariant()]).AccountSids);
    }

    [Fact]
    public void AccountRules_Find()
    {
        var rules = new AccountRules([First, Second], null);

        Assert.Equal((Second, (BreakTimeEntryDto?)null), (rules.Find(2), rules.Find(9)));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => RulesSnapshot.From(null!));
        Assert.Throws<ArgumentNullException>(() => RulesSnapshot.Empty.For(null!));
        Assert.Throws<ArgumentNullException>(() => RulesSnapshot.Empty.With(null!, AccountRules.Empty));
        Assert.Throws<ArgumentNullException>(() => RulesSnapshot.Empty.With(Kid, null!));
        Assert.Throws<ArgumentNullException>(() => RulesSnapshot.Empty.Without(null!));
    }
}
