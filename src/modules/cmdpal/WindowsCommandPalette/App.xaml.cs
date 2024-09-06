// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Contracts;
using Microsoft.CmdPal.Common.Extensions;
using Microsoft.CmdPal.Common.Models;
using Microsoft.CmdPal.Common.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Windows.CommandPalette.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace DeveloperCommandPalette;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application, IApp
{
    private Window? _window;

    public Window? AppWindow
    {
        get => _window;
        private set { }
    }

    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get; private set;
    }

    public T GetService<T>()
        where T : class
        => Host.GetService<T>();

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.
            CreateDefaultBuilder()
            .UseContentRoot(AppContext.BaseDirectory)
            .UseDefaultServiceProvider((context, options) =>
            {
                options.ValidateOnBuild = true;
            })
            .ConfigureServices((context, services) =>
            {
                // Services
                services.AddSingleton<ILocalSettingsService, LocalSettingsService>();

                // Core Services
                services.AddSingleton<IFileService, FileService>();
                services.AddSingleton<IExtensionService, ExtensionService>();

                //// Main window: Allow access to the main window
                //// from anywhere in the application.
                // services.AddSingleton(_ => MainWindow);

                //// DispatcherQueue: Allow access to the DispatcherQueue for
                //// the main window for general purpose UI thread access.
                // services.AddSingleton(_ => MainWindow.DispatcherQueue);

                // Configuration
                services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
            })
            .Build();

        UnhandledException += App_UnhandledException;
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    private async void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.
        // Log.Fatal(e.Exception, $"Unhandled exception: {e.Message}");

        // We are about to crash, so signal the extensions to stop.
        await GetService<IExtensionService>().SignalStopExtensionsAsync();

        // Log.CloseAndFlush();

        // We are very likely in a bad and unrecoverable state, so ensure Dev Home crashes w/ the exception info.
        Environment.FailFast(e.Message, e.Exception);
    }
}
