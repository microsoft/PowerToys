// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Ext.Shell;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Microsoft.CmdPal.Core.UI;

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

        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        AppWindow = new MainWindow(Services);

        // var activatedEventArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
        // ((MainWindow)AppWindow).HandleLaunch(activatedEventArgs);
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
        services.AddSingleton<ICommandProvider, ShellCommandsProvider>(); // TODO! test

        // Models

        // TODO!
        services.AddSingleton<IRootPageService, CoreRootPageService>();

        services.AddSingleton<IAppHostService, DummyAppHostService>();

        // services.AddSingleton(new TelemetryForwarder());

        // ViewModels
        services.AddSingleton<ShellViewModel>();

        // TODO!
        services.AddSingleton<IPageViewModelFactoryService, CoreViewModelFactory>();
        return services.BuildServiceProvider();
    }

    internal sealed class CoreRootPageService : IRootPageService, IDisposable
    {
        private readonly ShellCommandsProvider _shellCommandsProvider = new();

        public CoreRootPageService()
        {
        }

        public IPage GetRootPage()
        {
            var commands = _shellCommandsProvider.TopLevelCommands();
            var commandItem = commands[0];
            return (commandItem.Command as IPage)!;
        }

        public void GoHome()
        {
        }

        public void OnPerformCommand(object? context, bool topLevel, AppExtensionHost? currentHost)
        {
        }

        public Task PostLoadRootPageAsync()
        {
            return Task.CompletedTask;
        }

        public Task PreLoadAsync()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    internal sealed partial class DummyAppHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "This is test code fool";
    }

    internal sealed class DummyAppHostService : IAppHostService
    {
        private readonly DummyAppHost _host = new();

        public AppExtensionHost GetDefaultHost() => _host;

        public AppExtensionHost GetHostForCommand(object? context, AppExtensionHost? currentHost) => _host;
    }

    internal sealed class CoreViewModelFactory : IPageViewModelFactoryService
    {
        private readonly TaskScheduler _scheduler;

        public CoreViewModelFactory(TaskScheduler scheduler)
        {
            _scheduler = scheduler;
        }

        public PageViewModel? TryCreatePageViewModel(IPage page, bool nested, AppExtensionHost host)
        {
            return page switch
            {
                IListPage listPage => new ListViewModel(listPage, _scheduler, host) { IsNested = nested },
                IContentPage contentPage => new ContentPageViewModel(contentPage, _scheduler, host),
                _ => null,
            };
        }
    }
}
