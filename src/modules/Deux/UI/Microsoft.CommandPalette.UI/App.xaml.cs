// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.UI.Helpers;
using Microsoft.CommandPalette.UI.Pages;
using Microsoft.CommandPalette.UI.Services;
using Microsoft.CommandPalette.UI.Services.Extensions;
using Microsoft.CommandPalette.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;

namespace Microsoft.CommandPalette.UI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private readonly ILogger logger;
    private readonly GlobalErrorHandler _globalErrorHandler;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use.
    /// </summary>
    public static new App Current => (App)Application.Current;

    public Window? AppWindow { get; private set; }

    public ETWTrace EtwTrace { get; private set; } = new ETWTrace();

    public App(ILogger logger)
    {
        this.logger = logger;
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

    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register root services
        services.AddSingleton(logger);
        services.AddSingleton(TaskScheduler.FromCurrentSynchronizationContext());

        // Register settings & app state services first
        // because other services depend on them
        services.AddSingleton<SettingsService>();
        services.AddSingleton<AppStateService>();

        // Register extension services
        // We do these before other services so that they are available
        // during initialization of other services. Technically, they should
        // be registered before other services require them, but this is
        // a simple way to ensure that.
        services.AddSingleton<IExtensionService, BuiltInExtensionService>();
        services.AddSingleton<IExtensionService, WinRTExtensionService>();
        services.AddSingleton<IExtensionService, JsonRPCExtensionService>();

        // Register services
        services.AddSingleton<KeyboardService>();
        services.AddSingleton<TrayIconService>();

        // Register view models
        services.AddSingleton<ShellViewModel>();

        // Register views
        services.AddSingleton<ShellPage>();
        services.AddSingleton<MainWindow>();

        // Register services
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var activatedEventArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();

        var mainWindow = _services.GetRequiredService<MainWindow>();
        AppWindow = mainWindow;

        ((MainWindow)AppWindow).HandleLaunchNonUI(activatedEventArgs);
    }
}
