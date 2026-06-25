// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
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
public partial class App : Application, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private readonly DispatcherQueue _dispatcherQueue;
    private bool disposedValue;

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
            Logger.LogInfo("entering App.OnLaunched");

            var etwTrace = new ETWTrace();

            var settingsHelper = new SettingsHelper();
            var previewWindow = new PreviewWindow(settingsHelper);

            // start the handler that listens for the "show preview" event
            Logger.LogInfo("starting 'show preview' event handler");
            MouseJumpEventLoop.RunAsyncEventHandler(
                Constants.MouseJumpShowPreviewEvent(),
                previewWindow.ShowPreviewAsync,
                _dispatcherQueue,
                _cancellationTokenSource.Token);

            // start the handler that listens for the "terminate app" event
            Logger.LogInfo("starting 'terminate' event loop");
            MouseJumpEventLoop.RunEventHandler(
                Constants.TerminateMouseJumpSharedEvent(),
                this.TerminateApp,
                _dispatcherQueue,
                _cancellationTokenSource.Token);

            Logger.LogInfo("leaving App.OnLaunched");

            etwTrace?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
            throw;
        }
    }

    internal void TerminateApp()
    {
        Logger.LogInfo("exiting Mouse Jump.");
        _cancellationTokenSource.Cancel();
        _dispatcherQueue.TryEnqueue(this.Exit);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposedValue)
        {
            return;
        }

        if (disposing)
        {
            this._cancellationTokenSource.Cancel();
        }

        disposedValue = true;
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
