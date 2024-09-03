// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.Storage;

namespace DeveloperCommandPalette;

// cribbed heavily from
//
// https://github.com/microsoft/WindowsAppSDK-Samples/tree/main/Samples/AppLifecycle/Instancing/cs2/cs-winui-packaged/CsWinUiDesktopInstancing

sealed class  Program
{

    private static App? app;

    // LOAD BEARING
    // 
    // Main cannot be async. If it is, then the clipboard won't work, and neither will narrator. 
    [STAThread]
    static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        var isRedirect = DecideRedirection();
        if (!isRedirect)
        {
            global::Microsoft.UI.Xaml.Application.Start((p) => {
                var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                app = new App();
            });
        }
        return 0;
    }

    private static bool DecideRedirection()
    {
        var isRedirect = false;
        AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
        AppInstance keyInstance = AppInstance.FindOrRegisterForKey("randomKey");

        if (keyInstance.IsCurrent)
        {
            keyInstance.Activated += OnActivated;
        }
        else
        {
            isRedirect = true;
            _ = keyInstance.RedirectActivationToAsync(args);
        }
        return isRedirect;
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        ExtendedActivationKind kind = args.Kind;
        _ = kind;
        //app?.
        // If we already have a form, display the message now.
        // Otherwise, add it to the collection for displaying later.
        if (App.Current is App thisApp)
        {
            if (thisApp.AppWindow != null &&
                thisApp.AppWindow is MainWindow mainWindow)
            {
                mainWindow.Summon();
            }
        }
    }
}
