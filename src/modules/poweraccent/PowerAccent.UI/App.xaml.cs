// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PowerAccent.UI;

public partial class App : Application, IDisposable
{
    private readonly ETWTrace _etwTrace = new ETWTrace();
    private bool _disposed;

    public static new App Current => (App)Application.Current;

    public DispatcherQueue DispatcherQueueForApp { get; private set; }

    public static MainWindow Window { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (s, e) => Logger.LogError("Unhandled exception", e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueueForApp = DispatcherQueue.GetForCurrentThread();
        Window = new MainWindow();

        // Quick Accent has no visible main window until summoned by the keyboard hook;
        // the accent selector keeps itself hidden (TransparentWindow hides its AppWindow on init).
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _etwTrace?.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
