using EagleEye.ParentApp.Core.ViewModels;
using Xunit;

namespace EagleEye.ParentApp.Tests.ViewModels;

public sealed class UserAccountItemViewModelTests
{
    private readonly List<bool> _toggles = [];
    private readonly UserAccountItemViewModel _item;

    public UserAccountItemViewModelTests()
    {
        _item = new UserAccountItemViewModel("S-1-5-21-1-2-3-1001", "max", false, (_, value) => _toggles.Add(value));
    }

    [Fact]
    public void Setter_NewValue_RaisesToggled()
    {
        _item.IsUnderParentalControl = true;

        Assert.Equal([true], _toggles);
    }

    [Fact]
    public void Setter_SameValue_DoesNotRaise()
    {
        _item.IsUnderParentalControl = false;

        Assert.Empty(_toggles);
    }

    [Fact]
    public void ApplyStoredValue_ChangesValueAndNotifiesWithoutToggle()
    {
        var notified = new List<string?>();
        _item.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        _item.ApplyStoredValue(true);

        Assert.Equal((true, 0), (_item.IsUnderParentalControl, _toggles.Count));
        Assert.Equal([nameof(UserAccountItemViewModel.IsUnderParentalControl)], notified);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        Assert.Equal(("S-1-5-21-1-2-3-1001", "max", false, true), (_item.Sid, _item.DisplayName, _item.IsUnderParentalControl, _item.IsEnabled));
    }

    [Fact]
    public void Constructor_Guards()
    {
        Assert.ThrowsAny<ArgumentException>(() => new UserAccountItemViewModel(" ", "max", false, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => new UserAccountItemViewModel("S-1", null!, false, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => new UserAccountItemViewModel("S-1", "max", false, null!));
    }
}
