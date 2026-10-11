using EagleEye.ParentApp.Core.Abstractions;
using EagleEye.ParentApp.Core.Accounts;
using EagleEye.ParentApp.Core.Communication;
using EagleEye.ParentApp.Core.Data;
using EagleEye.ParentApp.Core.Reports;
using EagleEye.ParentApp.Core.Rules;
using EagleEye.ParentApp.Core.ViewModels;
using EagleEye.ParentApp.Services;
using EagleEye.ParentApp.Views;
using EagleEye.Shared.Data;

namespace EagleEye.ParentApp;

/// <summary>Builds the MAUI app and its dependency injection container.</summary>
public static class MauiProgram
{
    /// <summary>Creates the app.</summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
#if WINDOWS
        // US-005 (coding guidelines §16.1): WinUI gives a CheckBox a minimum width of about 120; the Rules table needs
        // narrow checkbox columns (AC-4, 150 % scaling).
        Microsoft.Maui.Handlers.CheckBoxHandler.Mapper.AppendToMapping("EagleEyeNoMinWidth", (handler, _) => handler.PlatformView.MinWidth = 0);
#endif

        var services = builder.Services;
        var paths = new MauiAppDataPaths();
        services.AddSingleton<IAppDataPaths>(paths);
        services.AddSingleton(TimeProvider.System);

        // Storage (ParentApp.Core).
        services.AddSingleton(_ => new ParentDatabase(SqliteDatabase.BuildConnectionString(paths.DatabasePath)));
#if WINDOWS
        services.AddSingleton<ISecretProtector, Platforms.Windows.DpapiSecretProtector>();
        services.AddSingleton<ISecretStore, ProtectedSecretStore>();
#else
        services.AddSingleton<ISecretStore, MauiSecureStorageSecretStore>();
#endif
        services.AddSingleton<IPairingStore, PairingStore>();
        services.AddSingleton<ISettingsStore, SettingsStore>();

        // Communication (ParentApp.Core).
        services.AddSingleton<IParentHubClientFactory, ParentHubClientFactory>();
        services.AddSingleton<ParentHubGateway>();
        services.AddSingleton<IParentHubGateway>(sp => sp.GetRequiredService<ParentHubGateway>());
        services.AddSingleton<IPairedConnectionSink>(sp => sp.GetRequiredService<ParentHubGateway>());
        services.AddSingleton<IConnectionCoordinator, ConnectionCoordinator>();

        // State area "UserAccounts" (US-003, ADR-010).
        services.AddSingleton<IUserAccountsModel, UserAccountsModel>();

        // Usage areas (US-004, ADR-012).
        services.AddSingleton<IAccountUsageModel, AccountUsageModel>();

        // State area "AccountRules" (US-005, ADR-010).
        services.AddSingleton<IAccountRulesModel, AccountRulesModel>();

        // Platform services.
        services.AddSingleton<IThemeService, MauiThemeService>();
        services.AddSingleton<IDialogService, MauiDialogService>();
        services.AddSingleton<IUiDispatcher, MauiUiDispatcher>();
        services.AddSingleton<HeadingBarColorService>();

        // View models and views.
        services.AddSingleton<AppearanceViewModel>();
        services.AddSingleton<ServerConnectionViewModel>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<UserAccountsViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<RulesViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsView>();
        services.AddSingleton<ReportsView>();
        services.AddSingleton<RulesView>();
        services.AddSingleton<StatusBarView>();
        services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
