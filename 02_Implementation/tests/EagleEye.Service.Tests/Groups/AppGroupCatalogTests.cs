using EagleEye.Service.Data;
using EagleEye.Service.Groups;
using Xunit;

namespace EagleEye.Service.Tests.Groups;

public sealed class AppGroupCatalogTests
{
    private const string Kid = "S-1-5-21-1-2-3-1003";
    private const string Kid2 = "S-1-5-21-1-2-3-1004";
    private const string Notepad = @"C:\Apps\notepad.exe";
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    private readonly AppGroupCatalog _catalog = new();

    [Fact]
    public void See_UncontrolledAccount_NotListed_NoChange()
    {
        Assert.False(_catalog.See(new StoredCatalogApp(Kid, Notepad, "Editor", Now)));
        Assert.Empty(_catalog.Listed);
    }

    [Fact]
    public void SetControlled_SameSetIgnoringCase_NoChange()
    {
        _catalog.See(new StoredCatalogApp(Kid, Notepad, "Editor", Now));

        Assert.True(_catalog.SetControlled([Kid]));
        Assert.False(_catalog.SetControlled([Kid.ToLowerInvariant()]));
    }

    [Fact]
    public void See_SamePathOtherCasing_LatestRecordDecidesPathAndName()
    {
        _catalog.SetControlled([Kid, Kid2]);
        _catalog.See(new StoredCatalogApp(Kid, Notepad, "Editor", Now));

        Assert.True(_catalog.See(new StoredCatalogApp(Kid2, Notepad.ToUpperInvariant(), "Editor", Now.AddMinutes(1))));
        Assert.False(_catalog.See(new StoredCatalogApp(Kid, Notepad, "Editor", Now)));

        Assert.Equal(Notepad.ToUpperInvariant(), _catalog.Listed[Notepad].ProgramPath);
    }

    [Fact]
    public void RemoveAccountsNotIn_DropsTheRecords()
    {
        _catalog.SetControlled([Kid, Kid2]);
        _catalog.See(new StoredCatalogApp(Kid, Notepad, "Editor", Now));
        _catalog.See(new StoredCatalogApp(Kid2, @"C:\Apps\paint.exe", "Paint", Now));

        Assert.True(_catalog.RemoveAccountsNotIn([Kid]));
        Assert.False(_catalog.RemoveAccountsNotIn([Kid]));

        Assert.Equal([Notepad], _catalog.Listed.Keys);
    }

    [Fact]
    public void Guards()
    {
        Assert.Throws<ArgumentNullException>(() => _catalog.See(null!));
        Assert.Throws<ArgumentNullException>(() => _catalog.SetControlled(null!));
        Assert.Throws<ArgumentNullException>(() => _catalog.RemoveAccountsNotIn(null!));
    }
}
