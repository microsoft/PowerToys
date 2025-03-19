// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Bookmarks;
using Microsoft.CmdPal.Ext.Calc;
using Microsoft.CmdPal.Ext.Indexer;
using Microsoft.CmdPal.Ext.Registry;
using Microsoft.CmdPal.Ext.Shell;
using Microsoft.CmdPal.Ext.System;
using Microsoft.CmdPal.Ext.TimeDate;
using Microsoft.CmdPal.Ext.WebSearch;
using Microsoft.CmdPal.Ext.WindowsServices;
using Microsoft.CmdPal.Ext.WindowsSettings;
using Microsoft.CmdPal.Ext.WindowsTerminal;
using Microsoft.CmdPal.Ext.WindowWalker;
using Microsoft.CmdPal.Ext.WinGet;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
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

    public Window? AppWindow { get; private set; }

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
        AppWindow = new MainWindow();
        AppWindow.Activate();
    }

    /// <summary>
    /// Configures the services for the application
    /// </summary>
    private static ServiceProvider ConfigureServices()
    {
        // TODO: It's in the Labs feed, but we can use Sergio's AOT-friendly source generator for this: https://github.com/CommunityToolkit/Labs-Windows/discussions/463
        ServiceCollection services = new();

        // Root services
        services.AddSingleton(TaskScheduler.FromCurrentSynchronizationContext());

        // Built-in Commands. Order matters - this is the order they'll be presented by default.
        var allApps = new AllAppsCommandProvider();
        var winget = new WinGetExtensionCommandsProvider();
        var callback = allApps.LookupApp;
        winget.SetAllLookup(callback);
        services.AddSingleton<ICommandProvider>(allApps);
        services.AddSingleton<ICommandProvider, ShellCommandsProvider>();
        services.AddSingleton<ICommandProvider, CalculatorCommandProvider>();
        services.AddSingleton<ICommandProvider, IndexerCommandsProvider>();
        services.AddSingleton<ICommandProvider, BookmarksCommandProvider>();

        // TODO GH #527 re-enable the clipboard commands
        // services.AddSingleton<ICommandProvider, ClipboardHistoryCommandsProvider>();
        services.AddSingleton<ICommandProvider, WindowWalkerCommandsProvider>();
        services.AddSingleton<ICommandProvider, WebSearchCommandsProvider>();
        services.AddSingleton<ICommandProvider>(winget);
        services.AddSingleton<ICommandProvider, WindowsTerminalCommandsProvider>();
        services.AddSingleton<ICommandProvider, WindowsSettingsCommandsProvider>();
        services.AddSingleton<ICommandProvider, RegistryCommandsProvider>();
        services.AddSingleton<ICommandProvider, WindowsServicesCommandsProvider>();
        services.AddSingleton<ICommandProvider, BuiltInsCommandProvider>();
        services.AddSingleton<ICommandProvider, TimeDateCommandsProvider>();
        services.AddSingleton<ICommandProvider, SystemCommandExtensionProvider>();

        // Models
        services.AddSingleton<TopLevelCommandManager>();
        services.AddSingleton<AliasManager>();
        services.AddSingleton<HotkeyManager>();
        var sm = SettingsModel.LoadSettings();
        services.AddSingleton(sm);
        var state = AppStateModel.LoadState();
        services.AddSingleton(state);
        services.AddSingleton<IExtensionService, ExtensionService>();

        // ViewModels
        services.AddSingleton<ShellViewModel>();

        return services.BuildServiceProvider();
    }
}
