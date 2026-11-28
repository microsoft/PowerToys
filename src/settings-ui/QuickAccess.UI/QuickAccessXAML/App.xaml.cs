// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.QuickAccess.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.QuickAccess;

public sealed partial class App : Application, IDisposable
{
    private QuickAccessFlyoutHost? _flyoutHost;
    private Window? _xamlWindow;
    private bool _isExiting;
    private bool _disposed;

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_xamlWindow is not null || _disposed)
        {
            return;
        }

        try
        {
            var content = new Grid();
            var contentLoaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            content.Loaded += OnContentLoaded;
            _xamlWindow = new Window { Content = content };

            // The runner can request visibility during startup. Wait for WinUI's default styles
            // to be ready before the flyout host begins handling requests.
            await contentLoaded.Task;

            var launchContext = QuickAccessLaunchContext.Parse(Environment.GetCommandLineArgs());
            _flyoutHost = new QuickAccessFlyoutHost(launchContext, ExitApplication);

            void OnContentLoaded(object sender, RoutedEventArgs e)
            {
                content.Loaded -= OnContentLoaded;
                contentLoaded.TrySetResult();
            }
        }
        catch (Exception ex)
        {
            // Failing here means the flyout host could not be constructed. Log and exit cleanly
            // rather than letting the throw bubble out into a stowed XAML failure that crashes
            // the runner-owned launcher.
            Logger.LogError("QuickAccess: failed to launch flyout host.", ex);
            Exit();
        }
    }

    private void ExitApplication()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        Dispose();
        Exit();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _flyoutHost?.Dispose();
        _flyoutHost = null;
        _xamlWindow?.Close();
        _xamlWindow = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // QuickAccess is a transient launcher flyout owned by the runner. An unhandled XAML
        // exception here would otherwise be stowed and FailFast the process; mark the event
        // handled so the next summon can recover. The error is still recorded for diagnostics.
        Logger.LogError("QuickAccess: unhandled XAML exception.", e.Exception);
        e.Handled = true;
    }
}
