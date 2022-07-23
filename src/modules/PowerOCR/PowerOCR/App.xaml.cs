// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerOCR.Keyboard;
using PowerOCR.Settings;
using System;
using System.Windows;

namespace PowerOCR;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application, IDisposable
{
    KeyboardMonitor? keyboardMonitor;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        keyboardMonitor?.Dispose();
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // WindowUtilities.LaunchOCROverlayOnEveryScreen();
        var userSettings = new UserSettings();
        keyboardMonitor = new KeyboardMonitor(userSettings);
        keyboardMonitor?.Start();
    }

    private void Application_Exit(object sender, ExitEventArgs e)
    {
        Dispose();
    }
}
