// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.Windows.AppLifecycle;

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

        Logger.InitializeLogger("\\CmdPal\\Logs\\");
        Logger.LogDebug($"Starting at {DateTime.UtcNow}");

        WinRT.ComWrappersSupport.InitializeComWrappers();
        bool isRedirect = DecideRedirection();
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
        bool isRedirect = false;
        AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
        AppInstance keyInstance = AppInstance.FindOrRegisterForKey("randomKey");

        if (keyInstance.IsCurrent)
        {
            keyInstance.Activated += OnActivated;
        }
        else
        {
            isRedirect = true;
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
                mainWindow.Summon(string.Empty);
            }
        }
    }
}
