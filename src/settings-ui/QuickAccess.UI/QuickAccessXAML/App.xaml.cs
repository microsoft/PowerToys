// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.QuickAccess;

public partial class App : Application
{
    private static MainWindow? _window;

    public App()
    {
        InitializeComponent();

        // Catch unhandled exceptions so WinUI does not FailFast the process.
        // This can happen at boot when explorer.exe is not yet ready.
        UnhandledException += OnUnhandledException;
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Logger.LogError("QuickAccess: unhandled exception – exiting so the runner can restart when explorer is ready.", e.Exception);

        // Mark as handled to suppress WinUI FailFast, then exit gracefully.
        e.Handled = true;
        Environment.Exit(1);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            var launchContext = QuickAccessLaunchContext.Parse(Environment.GetCommandLineArgs());
            _window = new MainWindow(launchContext);
            _window.Closed += OnWindowClosed;
            _window.Activate();
        }
        catch (Exception ex)
        {
            Logger.LogError("QuickAccess: failed to launch – exiting so the runner can restart when explorer is ready.", ex);
            Environment.Exit(1);
        }
    }

    private static void OnWindowClosed(object sender, WindowEventArgs args)
    {
        if (sender is MainWindow window)
        {
            window.Closed -= OnWindowClosed;
        }

        _window = null;
    }
}
