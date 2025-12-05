// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CommandPalette.UI.Pages;
using Microsoft.Extensions.Logging;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using WinUIEx;

namespace Microsoft.CommandPalette.UI;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : WindowEx
{
    private readonly ILogger logger;
    private readonly ShellPage shellPage;

    public MainWindow(ShellPage shellPage, ILogger logger)
    {
        InitializeComponent();
        this.logger = logger;
        this.shellPage = shellPage;

        RootElement.Content = this.shellPage;
    }

    public void HandleLaunchNonUI(AppActivationArguments? activatedEventArgs)
    {
        // LOAD BEARING
        // Any reading and processing of the activation arguments must be done
        // synchronously in this method, before it returns. The sending instance
        // remains blocked until this returns; afterward it may quit, causing
        // the activation arguments to be lost.
        if (activatedEventArgs is null)
        {
            // Summon(string.Empty);
            return;
        }

        try
        {
            if (activatedEventArgs.Kind == ExtendedActivationKind.StartupTask)
            {
                return;
            }

            if (activatedEventArgs.Kind == ExtendedActivationKind.Protocol)
            {
                if (activatedEventArgs.Data is IProtocolActivatedEventArgs protocolArgs)
                {
                    if (protocolArgs.Uri.ToString() is string uri)
                    {
                        // was the URI "x-cmdpal://background" ?
                        if (uri.StartsWith("x-cmdpal://background", StringComparison.OrdinalIgnoreCase))
                        {
                            // we're running, we don't want to activate our window. bail
                            return;
                        }
                        else if (uri.StartsWith("x-cmdpal://settings", StringComparison.OrdinalIgnoreCase))
                        {
                            // WeakReferenceMessenger.Default.Send<OpenSettingsMessage>(new());
                            return;
                        }
                        else if (uri.StartsWith("x-cmdpal://reload", StringComparison.OrdinalIgnoreCase))
                        {
                            // var settings = App.Current.Services.GetService<SettingsModel>();
                            // if (settings?.AllowExternalReload == true)
                            // {
                            //     Logger.LogInfo("External Reload triggered");
                            //     WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
                            // }
                            // else
                            // {
                            //     Logger.LogInfo("External Reload is disabled");
                            // }
                            return;
                        }
                    }
                }
            }
        }
        catch (COMException ex)
        {
            // https://learn.microsoft.com/en-us/windows/win32/rpc/rpc-return-values
            const int RPC_S_SERVER_UNAVAILABLE = -2147023174;
            const int RPC_S_CALL_FAILED = 2147023170;

            // Accessing properties activatedEventArgs.Kind and activatedEventArgs.Data might cause COMException
            // if the args are not valid or not passed correctly.
            if (ex.HResult is RPC_S_SERVER_UNAVAILABLE or RPC_S_CALL_FAILED)
            {
                // Logger.LogWarning(
                //    $"COM exception (HRESULT {ex.HResult}) when accessing activation arguments. " +
                //    $"This might be due to the calling application not passing them correctly or exiting before we could read them. " +
                //    $"The application will continue running and fall back to showing the Command Palette window.");
            }
            else
            {
                // Logger.LogError(
                //    $"COM exception (HRESULT {ex.HResult}) when activating the application. " +
                //    $"The application will continue running and fall back to showing the Command Palette window.",
                //    ex);
            }
        }

        // Summon(string.Empty);
    }

    // public void Summon(string commandId) =>
    // The actual showing and hiding of the window will be done by the
    // ShellPage. This is because we don't want to show the window if the
    // user bound a hotkey to just an invokable command, which we can't
    // know till the message is being handled.
    // WeakReferenceManager.Default.Send<HotkeySummonMessage>(new(commandId, _hwnd));
}
