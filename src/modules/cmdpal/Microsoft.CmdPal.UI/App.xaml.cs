// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Bookmarks;
using Microsoft.CmdPal.Ext.Calc;
using Microsoft.CmdPal.Ext.ClipboardHistory;
using Microsoft.CmdPal.Ext.Indexer;
using Microsoft.CmdPal.Ext.Registry;
using Microsoft.CmdPal.Ext.RemoteDesktop;
using Microsoft.CmdPal.Ext.Shell;
using Microsoft.CmdPal.Ext.System;
using Microsoft.CmdPal.Ext.TimeDate;
using Microsoft.CmdPal.Ext.WebSearch;
using Microsoft.CmdPal.Ext.WindowsServices;
using Microsoft.CmdPal.Ext.WindowsSettings;
using Microsoft.CmdPal.Ext.WindowsTerminal;
using Microsoft.CmdPal.Ext.WindowWalker;
using Microsoft.CmdPal.Ext.WinGet;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Helpers.MarkdownImageProviders;
using Microsoft.CmdPal.UI.Pages;
using Microsoft.CmdPal.UI.Services;
using Microsoft.CmdPal.UI.Settings;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;
using Microsoft.CmdPal.UI.ViewModels.MainPage;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Microsoft.CmdPal.UI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private readonly ILogger _logger;
    private readonly GlobalErrorHandler _globalErrorHandler;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use.
    /// </summary>
    public static new App Current => (App)Application.Current;

    public Window? AppWindow { get; private set; }

    public ETWTrace EtwTrace { get; private set; } = new ETWTrace();

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App(ILogger logger)
    {
        _logger = logger;
        _globalErrorHandler = new((CmdPalLogger)logger);

#if !CMDPAL_DISABLE_GLOBAL_ERROR_HANDLER
        _globalErrorHandler.Register(this);
#endif

        _services = ConfigureServices();

        this.InitializeComponent();

        // Ensure types used in XAML are preserved for AOT compilation
        TypePreservation.PreserveTypes();

        NativeEventWaiter.WaitForEventLoop(
            "Local\\PowerToysCmdPal-ExitEvent-eb73f6be-3f22-4b36-aee3-62924ba40bfd", () =>
            {
                EtwTrace?.Dispose();
                AppWindow?.Close();
                Environment.Exit(0);
            });
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        AppWindow = _services.GetRequiredService<MainWindow>();

        var activatedEventArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
        ((MainWindow)AppWindow).HandleLaunchNonUI(activatedEventArgs);
    }

    /// <summary>
    /// Configures the services for the application
    /// </summary>
    private ServiceProvider ConfigureServices()
    {
        // TODO: It's in the Labs feed, but we can use Sergio's AOT-friendly source generator for this: https://github.com/CommunityToolkit/Labs-Windows/discussions/463
        ServiceCollection services = new();

        // Root services
        services.AddSingleton<ILogger>(_logger);
        services.AddSingleton(TaskScheduler.FromCurrentSynchronizationContext());

        // Register settings & app state services first
        // because other services depend on them
        services.AddSingleton<SettingsService>();
        services.AddSingleton<AppStateService>();

        AddCoreServices(services);

        AddBuiltInCommands(services);

        AddUIServices(services);

        return services.BuildServiceProvider();
    }

    private void AddCoreServices(ServiceCollection services)
    {
        // Core services
        services.AddSingleton<IExtensionService, BuiltInExtensionService>();
        services.AddSingleton<IExtensionService, WinRTExtensionService>();

        services.AddSingleton<IRunHistoryService, RunHistoryService>();
        services.AddSingleton<IRecentCommandsManager, RecentCommandsManager>();
        services.AddSingleton<AppExtensionHost, CommandPaletteHost>();
        services.AddSingleton<IRootPageService, PowerToysRootPageService>();
        services.AddSingleton<IAppHostService, PowerToysAppHostService>();
        services.AddSingleton<ITelemetryService, TelemetryForwarder>();

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<IPageViewModelFactoryService, CommandPalettePageViewModelFactory>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<CommandBarViewModel>();
        services.AddSingleton<ContextMenuViewModel>();
        services.AddSingleton<SettingsWindowViewModel>();

        // Controls
        services.AddSingleton<ContextMenu>();
        services.AddSingleton<CommandBar>();
        services.AddSingleton<SearchBar>();
        services.AddTransient<ScreenPreview>();
        services.AddTransient<FallbackRanker>();
        services.AddTransient<FallbackRankerDialog>();
        services.AddSingleton<ImageProvider>();

        // Windows & Pages
        services.AddSingleton<MainWindow>();
        services.AddSingleton<SettingsWindow>();
        services.AddSingleton<MainListPage>();
        services.AddSingleton<ShellPage>();
        services.AddSingleton<GeneralPage>();
        services.AddSingleton<AppearancePage>();
        services.AddSingleton<ExtensionsPage>();
        services.AddSingleton<ExtensionPage>();
    }

    private void AddBuiltInCommands(ServiceCollection services)
    {
        // Built-in Commands. Order matters - this is the order they'll be presented by default.
        var allApps = new AllAppsCommandProvider();
        var files = new IndexerCommandsProvider();
        files.SuppressFallbackWhen(ShellCommandsProvider.SuppressFileFallbackIf);
        services.AddSingleton<ICommandProvider>(allApps);

        services.AddSingleton<ICommandProvider, ShellCommandsProvider>();
        services.AddSingleton<ICommandProvider, CalculatorCommandProvider>();
        services.AddSingleton<ICommandProvider>(files);
        services.AddSingleton<ICommandProvider, BookmarksCommandProvider>(_ => BookmarksCommandProvider.CreateWithDefaultStore());

        services.AddSingleton<ICommandProvider, WindowWalkerCommandsProvider>();
        services.AddSingleton<ICommandProvider, WebSearchCommandsProvider>();
        services.AddSingleton<ICommandProvider, ClipboardHistoryCommandsProvider>();

        // GH #38440: Users might not have WinGet installed! Or they might have
        // a ridiculously old version. Or might be running as admin.
        // We shouldn't explode in the App ctor if we fail to instantiate an
        // instance of PackageManager, which will happen in the static ctor
        // for WinGetStatics
        try
        {
            var winget = new WinGetExtensionCommandsProvider();
            winget.SetAllLookup(
                query => allApps.LookupAppByPackageFamilyName(query, requireSingleMatch: true),
                query => allApps.LookupAppByProductCode(query, requireSingleMatch: true));
            services.AddSingleton<ICommandProvider>(winget);
        }
        catch (Exception ex)
        {
            Log_FailedToLoadWinget(ex);
        }

        services.AddSingleton<ICommandProvider, WindowsTerminalCommandsProvider>();
        services.AddSingleton<ICommandProvider, WindowsSettingsCommandsProvider>();
        services.AddSingleton<ICommandProvider, RegistryCommandsProvider>();
        services.AddSingleton<ICommandProvider, WindowsServicesCommandsProvider>();
        services.AddSingleton<ICommandProvider, BuiltInsCommandProvider>();
        services.AddSingleton<ICommandProvider, TimeDateCommandsProvider>();
        services.AddSingleton<ICommandProvider, SystemCommandExtensionProvider>();
        services.AddSingleton<ICommandProvider, RemoteDesktopCommandProvider>();
    }

    private void AddUIServices(ServiceCollection services)
    {
        // Services
        services.AddSingleton<TopLevelCommandManager>();
        services.AddSingleton<AliasManager>();
        services.AddSingleton<HotkeyManager>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<TrayIconService>();

        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ResourceSwapper>();
        services.AddTransient<LocalKeyboardListener>();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Couldn't load winget")]
    partial void Log_FailedToLoadWinget(Exception ex);
}
