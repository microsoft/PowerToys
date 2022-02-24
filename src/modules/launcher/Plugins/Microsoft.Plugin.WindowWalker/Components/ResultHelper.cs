// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Microsoft.Plugin.WindowWalker.Properties;
using Wox.Plugin;

namespace Microsoft.Plugin.WindowWalker.Components
{
    /// <summary>
    /// Helper class to work with results
    /// </summary>
    internal static class ResultHelper
    {
        /// <summary>
        /// Returns the subtitle for a result
        /// </summary>
        /// <param name="window">The window properties of the result</param>
        /// <returns>String with the subtitle</returns>
        internal static string GetSubtitle(Window window)
        {
            if (window == null || !(window is Window))
            {
                return string.Empty;
            }

            string subtitleText = Resources.wox_plugin_windowwalker_running + ": " + window.Process.Name;

            if (WindowWalkerSettings.Instance.SubtitleShowPid)
            {
                subtitleText += $" ({window.Process.ProcessID})";
            }

            if (WindowWalkerSettings.Instance.SubtitleShowDesktopName && Main.VirtualDesktopHelperInstance.GetDesktopCount() > 1)
            {
                subtitleText += $" - {Resources.wox_plugin_windowwalker_Desktop}: {window.Desktop.Name}";
            }

            return subtitleText;
        }

        /// <summary>
        /// Returns the tootlip for a result
        /// </summary>
        /// <param name="window">The window properties of the result</param>
        /// <param name="debugToolTip">Value indicating if a detailed debug tooltip should be returned</param>
        /// <returns>Tooltip for the result or null of failure</returns>
        internal static ToolTipData GetToolTip(Window window, bool debugToolTip = false)
        {
            if (window == null || !(window is Window))
            {
                return null;
            }

            if (!debugToolTip)
            {
                string text = $"{Resources.wox_plugin_windowwalker_Process}: {window.Process.Name}\n" +
                    $"{Resources.wox_plugin_windowwalker_ProcessId}: {window.Process.ProcessID}\n" +
                    $"{Resources.wox_plugin_windowwalker_Desktop}: {window.Desktop.Name}";

                text += window.Desktop.IsAllDesktopsView ? string.Empty : $" ({Resources.wox_plugin_windowwalker_Number} {window.Desktop.Number})";

                return new ToolTipData(window.Title, text);
            }
           else
            {
                string text = $"hWnd: {window.Hwnd}\n" +
                    $"Window class: {window.ClassName}\n" +
                    $"Process ID: {window.Process.ProcessID}\n" +
                    $"Thread ID: {window.Process.ThreadID}\n" +
                    $"Process: {window.Process.Name}\n" +
                    $"Process exists: {window.Process.DoesExist}\n" +
                    $"Is full access denied: {window.Process.IsFullAccessDenied}\n" +
                    $"Is uwp app: {window.Process.IsUwpApp}\n" +
                    $"Is ShellProcess: {window.Process.IsShellProcess}\n" +
                    $"Is window cloaked: {window.IsCloaked}\n" +
                    $"Window cloak state: {window.GetWindowCloakState()}\n" +
                    $"Desktop name: {window.Desktop.Name}" +
                    $"Desktop number: {window.Desktop.Number}\n" +
                    $"Desktop is visible: {window.Desktop.IsVisible}\n" +
                    $"Desktop position: {window.Desktop.Position}\n" +
                    $"Is AllDesktops view: {window.Desktop.IsAllDesktopsView}";

                return new ToolTipData(window.Title, text);
           }
        }
    }
}
