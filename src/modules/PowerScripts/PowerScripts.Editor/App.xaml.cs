// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace PowerScripts.Editor;

public partial class App : Application
{
    public App()
    {
        Logger.InitializeLogger("\\PowerScripts\\Editor\\Logs");
        UnhandledException += (_, e) => Logger.LogError("Unhandled PowerScripts editor exception.", e.Exception);
        InitializeComponent();
    }

    public static MainWindow MainWindow { get; private set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var instance = AppInstance.FindOrRegisterForKey("PowerToys.PowerScriptsEditor");
        if (!instance.IsCurrent)
        {
            await instance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Exit();
            return;
        }

        MainWindow = new MainWindow();
        instance.Activated += (_, _) => MainWindow.DispatcherQueue.TryEnqueue(MainWindow.Activate);
        MainWindow.Activate();
    }
}
