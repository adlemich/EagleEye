namespace EagleEye.Service.SessionAgent;

/// <summary>
/// The "Apps" rule of ADR-011 §4: which top-level windows make their process an app as Task Manager's
/// "Apps" group shows it. Pure; every case is documented by unit tests.
/// </summary>
public static class AppWindowRule
{
    /// <summary><c>DWM_CLOAKED_SHELL</c>: cloaked by the shell (e.g. on another virtual desktop); still counts.</summary>
    public const int CloakedByShell = 0x2;

    /// <summary><c>WS_EX_TOOLWINDOW</c>.</summary>
    public const long ToolWindowStyle = 0x80;

    /// <summary><c>WS_EX_APPWINDOW</c>.</summary>
    public const long AppWindowStyle = 0x40000;

    /// <summary>Class of the frame window of Store apps.</summary>
    public const string StoreFrameClass = "ApplicationFrameWindow";

    /// <summary>Class of File Explorer windows.</summary>
    public const string FileExplorerClass = "CabinetWClass";

    /// <summary>File name of the Windows shell process.</summary>
    public const string ExplorerProcessName = "explorer.exe";

    /// <summary>The shell's own window classes (taskbar, desktop, Start, Search, notification area, task view).</summary>
    public static readonly IReadOnlySet<string> ShellClasses = new HashSet<string>(StringComparer.Ordinal)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "Progman",
        "WorkerW",
        "Windows.UI.Core.CoreWindow",
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland",
        "Xaml_WindowedPopupClass",
        "ApplicationManager_ImmersiveShellWindow",
        "MultitaskingViewFrame",
        "ForegroundStaging",
        "TaskListThumbnailWnd",
        "XamlExplorerHostIslandWindow",
        "Shell_InputSwitchTopLevelWindow",
        "EdgeUiInputTopWndClass",
    };

    /// <summary>Returns the app the window stands for, or <c>null</c> if the window does not make an app.</summary>
    public static AgentApp? Classify(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!window.IsVisible || window.Width <= 0 || window.Height <= 0
            || (window.Cloaked & ~CloakedByShell) != 0
            || ShellClasses.Contains(window.ClassName))
        {
            return null;
        }

        if (window.ClassName == StoreFrameClass)
        {
            return window.HostedProcessId is { } hosted ? new AgentApp(hosted, AgentAppKind.StoreApp, window.ProcessId) : null;
        }

        if ((window.ExStyle & AppWindowStyle) == 0 && (window.HasOwner || (window.ExStyle & ToolWindowStyle) != 0))
        {
            return null;
        }

        if (string.Equals(window.ProcessName, ExplorerProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return new AgentApp(window.ProcessId, window.ClassName == FileExplorerClass ? AgentAppKind.FileExplorer : AgentAppKind.ExplorerWindow);
        }

        return new AgentApp(window.ProcessId, AgentAppKind.Window);
    }
}
