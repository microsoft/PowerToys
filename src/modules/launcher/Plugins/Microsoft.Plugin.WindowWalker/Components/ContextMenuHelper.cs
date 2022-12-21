// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Microsoft.Plugin.WindowWalker.Properties;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.WindowWalker.Components
{
    internal class ContextMenuHelper
    {
        /// <summary>
        /// Returns a list of all <see cref="ContextMenuResult"/>s for the selected <see cref="Result"/>.
        /// </summary>
        /// <param name="result">Selected result</param>
        /// <returns>List of context menu results</returns>
        internal static List<ContextMenuResult> GetContextMenuResults(in Result result)
        {
            if (!(result?.ContextData is Window windowData))
            {
                return new List<ContextMenuResult>(0);
            }

            var contextMenu = new List<ContextMenuResult>()
            {
                new ContextMenuResult
                {
                    AcceleratorKey = Key.F4,
                    AcceleratorModifiers = ModifierKeys.Control,
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE8BB",                       // E8B8 => Symbol: ChromeClose
                    Title = $"{Resources.wox_plugin_windowwalker_Close} (Ctrl+F4)",
                    Action = _ =>
                    {
                        if (!windowData.IsWindow)
                        {
                            Log.Debug($"Can not close the window '{windowData.Title}' ({windowData.Hwnd}), because it doesn't exist.", typeof(ContextMenuHelper));
                            return false;
                        }

                        windowData.CloseThisWindow();

                        return !WindowWalkerSettings.Instance.OpenAfterKillAndClose;
                    },
                },
            };

            // Hide menu if Explorer.exe is the shell process or the process name is ApplicationFrameHost.exe
            // In the first case we would crash the windows ui and in the second case we would kill the generic process for uwp apps.
            if (!windowData.Process.IsShellProcess && !(windowData.Process.IsUwpApp & windowData.Process.Name.ToLower(System.Globalization.CultureInfo.InvariantCulture) == "applicationframehost.exe")
                && !(windowData.Process.IsFullAccessDenied & WindowWalkerSettings.Instance.HideKillProcessOnElevatedProcesses))
            {
                contextMenu.Add(new ContextMenuResult
                {
                    AcceleratorKey = Key.Delete,
                    AcceleratorModifiers = ModifierKeys.Control,
                    FontFamily = "Segoe MDL2 Assets",
                    Glyph = "\xE74D",                       // E74D => Symbol: Delete
                    Title = $"{Resources.wox_plugin_windowwalker_Kill} (Ctrl+Delete)",
                    Action = _ => KillProcessCommand(windowData),
                });
            }

            return contextMenu;
        }

        /// <summary>
        /// Method to initiate killing the process of a window
        /// </summary>
        /// <param name="window">Window data</param>
        /// <returns>True if the PT Run window should close, otherwise false.</returns>
        private static bool KillProcessCommand(Window window)
        {
            // Validate process
            if (!window.IsWindow || !window.Process.DoesExist || !window.Process.Name.Equals(WindowProcess.GetProcessNameFromProcessID(window.Process.ProcessID), StringComparison.Ordinal))
            {
                Log.Debug($"Can not kill process '{window.Process.Name}' ({window.Process.ProcessID}) of the window '{window.Title}' ({window.Hwnd}), because it doesn't exist.", typeof(ContextMenuHelper));
                return false;
            }

            // Request user confirmation
            if (WindowWalkerSettings.Instance.ConfirmKillProcess)
            {
                string messageBody = $"{Resources.wox_plugin_windowwalker_KillMessage}\n"
                    + $"{window.Process.Name} ({window.Process.ProcessID})\n\n"
                    + $"{(window.Process.IsUwpApp ? Resources.wox_plugin_windowwalker_KillMessageUwp : Resources.wox_plugin_windowwalker_KillMessageQuestion)}";
                MessageBoxResult messageBoxResult = MessageBox.Show(
                    messageBody,
                    Resources.wox_plugin_windowwalker_plugin_name + " - " + Resources.wox_plugin_windowwalker_KillMessageTitle,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (messageBoxResult == MessageBoxResult.No)
                {
                    return false;
                }
            }

            // Kill process
            window.Process.KillThisProcess(WindowWalkerSettings.Instance.KillProcessTree);
            return !WindowWalkerSettings.Instance.OpenAfterKillAndClose;
        }
    }
}
