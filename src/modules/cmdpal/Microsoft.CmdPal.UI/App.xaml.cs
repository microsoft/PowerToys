// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Bookmarks;
using Microsoft.CmdPal.Ext.Calc;
using Microsoft.CmdPal.Ext.Settings;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Microsoft.CmdPal.UI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Gets the current <see cref="App"/> instance in use.
    /// </summary>
    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        Services = ConfigureServices();

        this.InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    private Window? _window;

    /// <summary>
    /// Configures the services for the application
    /// </summary>
    private static ServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();

        // Built-in Commands
        services.AddSingleton<ICommandProvider, BookmarksCommandProvider>();
        services.AddSingleton<ICommandProvider, CalculatorCommandProvider>();
        services.AddSingleton<ICommandProvider, SettingsCommandProvider>();

        // ViewModels
        services.AddSingleton<ShellViewModel>((services) => new(services.GetServices<ICommandProvider>()));

        return services.BuildServiceProvider();
    }
}
