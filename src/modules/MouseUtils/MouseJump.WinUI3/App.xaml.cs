// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Threading;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;

using MouseJump.Common.Helpers;
using MouseJump.WinUI3.Helpers;
using MouseJump.WinUI3.UI;

using PowerToys.Interop;

using Application = Microsoft.UI.Xaml.Application;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace MouseJump.WinUI3;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

    /// <summary>
    /// Initializes a new instance of the <see cref="App"/> class.
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
    }

    private TrayIcon? TrayIcon
    {
        get;
        set;
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Logger.InitializeLogger("\\MouseJump\\Logs");
            Logger.LogInfo("app launched");

            // make sure we're in the right high dpi mode otherwise pixel positions and sizes for
            // screen captures get distorted and various coordinates aren't calculated correctly.
            Logger.LogInfo("checking high dpi mode");
            DpiModeHelper.EnsurePerMonitorV2Enabled();
            Logger.LogInfo("high dpi mode is ok");

            var etwTrace = new ETWTrace();

            var settingsHelper = new SettingsHelper();
            var previewWindow = new PreviewWindow(settingsHelper);

            Logger.LogInfo("Starting 'show preview' event handler");
            MouseJumpEventLoop.RunEventHandler(
                Constants.MouseJumpShowPreviewEvent(),
                previewWindow.ShowPreviewAsync,
                Dispatcher.CurrentDispatcher,
                App.CancellationTokenSource.Token);

            Logger.LogInfo("Starting 'terminate' event loop");
            MouseJumpEventLoop.RunEventHandler(
                Constants.TerminateMouseJumpSharedEvent(),
                App.TerminateAppAsync,
                Dispatcher.CurrentDispatcher,
                App.CancellationTokenSource.Token);

            Logger.LogInfo("Starting application loop");

            etwTrace?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
            throw;
        }
    }

    private static async Task TerminateAppAsync()
    {
        Logger.LogInfo("Exiting Mouse Jump.");
        await App.CancellationTokenSource.CancelAsync();
    }
}
