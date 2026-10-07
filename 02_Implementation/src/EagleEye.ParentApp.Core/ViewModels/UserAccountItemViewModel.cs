using CommunityToolkit.Mvvm.ComponentModel;

namespace EagleEye.ParentApp.Core.ViewModels;

/// <summary>
/// One row of the account list (US-003 AC-10): shown name and the checkbox "Under parental control".
/// Only the binding (user input) goes through the <see cref="IsUnderParentalControl"/> setter and
/// triggers a write; snapshot updates use <see cref="ApplyStoredValue"/>, which never does
/// (no feedback loop between binding and broadcast).
/// </summary>
public sealed class UserAccountItemViewModel : ObservableObject
{
    private readonly Action<UserAccountItemViewModel, bool> _toggled;
    private string _displayName;
    private bool _isUnderParentalControl;
    private bool _isEnabled = true;

    /// <summary>Creates a row.</summary>
    /// <param name="sid">The account's SID.</param>
    /// <param name="displayName">The shown name.</param>
    /// <param name="isUnderParentalControl">The stored selection.</param>
    /// <param name="toggled">Called when the user changes the checkbox.</param>
    public UserAccountItemViewModel(string sid, string displayName, bool isUnderParentalControl, Action<UserAccountItemViewModel, bool> toggled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sid);
        ArgumentNullException.ThrowIfNull(displayName);
        Sid = sid;
        _displayName = displayName;
        _isUnderParentalControl = isUnderParentalControl;
        _toggled = toggled ?? throw new ArgumentNullException(nameof(toggled));
    }

    /// <summary>The account's SID.</summary>
    public string Sid { get; }

    /// <summary>The shown name, e.g. "Max Adler (max)".</summary>
    public string DisplayName
    {
        get => _displayName;
        internal set
        {
            if (SetProperty(ref _displayName, value))
            {
                OnPropertyChanged(nameof(CheckBoxAutomationName));
            }
        }
    }

    /// <summary>
    /// Accessible name of the checkbox, e.g. "Unter Elternkontrolle: eagleeye-kid" (ISSUE-006: the row
    /// has no visible label any more; the column header labels it visually).
    /// </summary>
    public string CheckBoxAutomationName => AppTexts.Format(AppTexts.AccountCheckBoxNameFormat, DisplayName);

    /// <summary>The checkbox. Setting a new value (user input) sends it to the service.</summary>
    public bool IsUnderParentalControl
    {
        get => _isUnderParentalControl;
        set
        {
            if (SetProperty(ref _isUnderParentalControl, value))
            {
                _toggled(this, value);
            }
        }
    }

    /// <summary>The checkbox can be changed; false while this row's write is pending (plan D-4).</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        internal set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>A write of this row is pending; snapshots do not change its shown value meanwhile.</summary>
    internal bool IsPending { get; set; }

    /// <summary>Shows the value stored in the service without triggering a write.</summary>
    internal void ApplyStoredValue(bool value)
    {
        SetProperty(ref _isUnderParentalControl, value, nameof(IsUnderParentalControl));
    }
}
