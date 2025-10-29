// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.CmdPal.UI.Events;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI;

// cribbed heavily from
//
// https://github.com/microsoft/WindowsAppSDK-Samples/tree/main/Samples/AppLifecycle/Instancing/cs2/cs-winui-packaged/CsWinUiDesktopInstancing
internal sealed class Program
{
    private static DispatcherQueueSynchronizationContext? uiContext;
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
                uiContext = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(uiContext);
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
            RedirectActivationTo(args, keyInstance);
        }

        return isRedirect;
    }

    private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
    {
        // Do the redirection on another thread, and use a non-blocking
        // wait method to wait for the redirection to complete.
        using var redirectSemaphore = new Semaphore(0, 1);
        var redirectTimeout = TimeSpan.FromSeconds(32);

        _ = Task.Run(() =>
        {
            using var cts = new CancellationTokenSource(redirectTimeout);
            try
            {
                keyInstance.RedirectActivationToAsync(args)
                    .AsTask(cts.Token)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (OperationCanceledException)
            {
                Logger.LogError($"Failed to activate existing instance; timed out after {redirectTimeout}.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to activate existing instance", ex);
            }
            finally
            {
                redirectSemaphore.Release();
            }
        });

        _ = PInvoke.CoWaitForMultipleObjects(
            (uint)CWMO_FLAGS.CWMO_DEFAULT,
            PInvoke.INFINITE,
            [new HANDLE(redirectSemaphore.SafeWaitHandle.DangerousGetHandle())],
            out _);
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        // If we already have a form, display the message now.
        // Otherwise, add it to the collection for displaying later.
        if (App.Current?.AppWindow is MainWindow mainWindow)
        {
            // LOAD BEARING
            // This must be synchronous to ensure the method does not return
            // before the activation is fully handled and the parameters are processed.
            // The sending instance remains blocked until this returns; afterward it may quit,
            // causing the activation arguments to be lost.
            mainWindow.HandleLaunchNonUI(args);
        }
    }
}
