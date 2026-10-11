using EagleEye.Service.Enforcement;
using Xunit;

namespace EagleEye.Service.Tests.Enforcement;

public sealed class OpenAppsViewTests
{
    [Fact]
    public void Of_BeforePublish_Empty()
    {
        Assert.Empty(new OpenAppsView().Of("S-1"));
    }

    [Fact]
    public void Publish_ReplacesAndIsCaseInsensitiveBySid()
    {
        var view = new OpenAppsView();
        view.Publish(new Dictionary<string, IReadOnlySet<string>> { ["S-1-A"] = new HashSet<string> { "a" } });
        view.Publish(new Dictionary<string, IReadOnlySet<string>> { ["S-1-B"] = new HashSet<string> { "b" } });

        Assert.Equal((0, 1), (view.Of("S-1-A").Count, view.Of("s-1-b").Count));
    }

    [Fact]
    public void Guards_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new OpenAppsView().Publish(null!));
        Assert.Throws<ArgumentNullException>(() => new OpenAppsView().Of(null!));
    }
}
