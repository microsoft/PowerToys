﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.CmdPal.UI.Events;
using Microsoft.PowerToys.Telemetry;
using Microsoft.Windows.AppLifecycle;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI;

// cribbed heavily from
//
// https://github.com/microsoft/WindowsAppSDK-Samples/tree/main/Samples/AppLifecycle/Instancing/cs2/cs-winui-packaged/CsWinUiDesktopInstancing
internal sealed class Program
{
    private static App? app;

    // LOAD BEARING
    //
    // Main cannot be async. If it is, then the clipboard won't work, and neither will narrator.
    // That means you, the person thinking about making this a MTA thread. Don't
    // do it. It won't work. That's not the solution.
    [STAThread]
    private static int Main(string[] args)
    {
        if (Helpers.GpoValueChecker.GetConfiguredCmdPalEnabledValue() == Helpers.GpoRuleConfiguredValue.Disabled)
        {
            // There's a GPO rule configured disabling CmdPal. Exit as soon as possible.
            return 0;
        }

        try
        {
            Logger.InitializeLogger("\\CmdPal\\Logs\\");
        }
        catch (COMException e)
        {
            // This is unexpected. For the sake of debugging:
            // pop a message box
            PInvoke.MessageBox(
                (HWND)IntPtr.Zero,
                $"Failed to initialize the logger. COMException: \r{e.Message}",
                "Command Palette",
                MESSAGEBOX_STYLE.MB_OK | MESSAGEBOX_STYLE.MB_ICONERROR);
            return 0;
        }
        catch (Exception e2)
        {
            // This is unexpected. For the sake of debugging:
            // pop a message box
            PInvoke.MessageBox(
                (HWND)IntPtr.Zero,
                $"Failed to initialize the logger. Unknown Exception: \r{e2.Message}",
                "Command Palette",
                MESSAGEBOX_STYLE.MB_OK | MESSAGEBOX_STYLE.MB_ICONERROR);
            return 0;
        }

        Logger.LogDebug($"Starting at {DateTime.UtcNow}");
        PowerToysTelemetry.Log.WriteEvent(new CmdPalProcessStarted());

        WinRT.ComWrappersSupport.InitializeComWrappers();
        var isRedirect = DecideRedirection();
        if (!isRedirect)
        {
            Microsoft.UI.Xaml.Application.Start((p) =>
            {
                Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext context = new(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                app = new App();
            });
        }

        return 0;
    }

    private static bool DecideRedirection()
    {
        var isRedirect = false;
        var args = AppInstance.GetCurrent().GetActivatedEventArgs();
        var keyInstance = AppInstance.FindOrRegisterForKey("randomKey");

        if (keyInstance.IsCurrent)
        {
            PowerToysTelemetry.Log.WriteEvent(new ColdLaunch());
            keyInstance.Activated += OnActivated;
        }
        else
        {
            isRedirect = true;
            PowerToysTelemetry.Log.WriteEvent(new ReactivateInstance());
            keyInstance.RedirectActivationToAsync(args).AsTask().ConfigureAwait(false);
        }

        return isRedirect;
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        // If we already have a form, display the message now.
        // Otherwise, add it to the collection for displaying later.
        if (App.Current is App thisApp)
        {
            if (thisApp.AppWindow is not null and
                MainWindow mainWindow)
            {
                mainWindow.HandleLaunch(args);

                // mainWindow.Summon(string.Empty);
            }
        }
    }
}
