// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.UI.Helpers;
using Microsoft.CommandPalette.UI.Models;
using Microsoft.CommandPalette.UI.Pages;
using Microsoft.CommandPalette.UI.Services;
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

    /// <summary>
    /// Gets the current <see cref="App"/> instance in use.
    /// </summary>
    public static new App Current => (App)Application.Current;

    public Window? AppWindow { get; private set; }

    public ETWTrace EtwTrace { get; private set; } = new ETWTrace();

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
    /// </summary>
    public IServiceProvider Services { get; }

    public App(ILogger logger)
    {
        this.logger = logger;
        _globalErrorHandler = new((CmdPalLogger)logger);

#if !CMDPAL_DISABLE_GLOBAL_ERROR_HANDLER
        _globalErrorHandler.Register(this);
#endif

        Services = ConfigureServices();

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

        // Register settings & app state
        var settingsModel = SettingsModel.LoadSettings(logger);
        services.AddSingleton(settingsModel);

        var appStateModel = AppStateModel.LoadState(logger);
        services.AddSingleton(appStateModel);

        // Register services
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

        var mainWindow = Services.GetRequiredService<MainWindow>();
        AppWindow = mainWindow;

        ((MainWindow)AppWindow).HandleLaunchNonUI(activatedEventArgs);
    }
}
