// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Collections.Generic;
using Microsoft.Plugin.WindowWalker.Properties;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.Plugin.WindowWalker.Components
{
    /// <summary>
    /// Helper class to work with results
    /// </summary>
    internal static class ResultHelper
    {
         /// <summary>
         /// Returns a list of all results for the query.
         /// </summary>
         /// <param name="searchControllerResults">List with all search controller matches</param>
         /// <param name="icon">The path to the result icon</param>
         /// <returns>List of results</returns>
        internal static List<Result> GetResultList(List<SearchResult> searchControllerResults, bool isKeywordSearch, string icon, string infoIcon)
        {
            bool addExplorerInfo = false;
            List<Result> resultsList = new List<Result>();

            foreach (SearchResult x in searchControllerResults)
            {
                if (x.Result.Process.Name.ToLower(System.Globalization.CultureInfo.InvariantCulture) == "explorer.exe" && x.Result.Process.IsShellProcess)
                {
                    addExplorerInfo = true;
                }

                resultsList.Add(new Result()
                {
                    Title = x.Result.Title,
                    IcoPath = icon,
                    SubTitle = GetSubtitle(x.Result),
                    ContextData = x.Result,
                    Action = c =>
                    {
                        x.Result.SwitchToWindow();
                        return true;
                    },

                    // For debugging you can set the second parameter to true to see more informations.
                    ToolTipData = GetToolTip(x.Result, false),
                });
            }

            if (addExplorerInfo && isKeywordSearch && !WindowWalkerSettings.Instance.HideExplorerSettingInfo)
            {
                resultsList.Add(GetExplorerInfoResult(infoIcon));
            }

            return resultsList;
        }

        /// <summary>
        /// Returns the subtitle for a result
        /// </summary>
        /// <param name="window">The window properties of the result</param>
        /// <returns>String with the subtitle</returns>
        private static string GetSubtitle(Window window)
        {
            if (window == null || !(window is Window))
            {
                return string.Empty;
            }

            string subtitleText = Resources.wox_plugin_windowwalker_Running + ": " + window.Process.Name;

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
        /// Returns the tool tip for a result
        /// </summary>
        /// <param name="window">The window properties of the result</param>
        /// <param name="debugToolTip">Value indicating if a detailed debug tooltip should be returned</param>
        /// <returns>Tooltip for the result or null of failure</returns>
        private static ToolTipData GetToolTip(Window window, bool debugToolTip)
        {
            if (window == null || !(window is Window))
            {
                return null;
            }

            if (!debugToolTip)
            {
                string text = $"{Resources.wox_plugin_windowwalker_Process}: {window.Process.Name}";
                text += $"\n{Resources.wox_plugin_windowwalker_ProcessId}: {window.Process.ProcessID}";

                if (Main.VirtualDesktopHelperInstance.GetDesktopCount() > 1)
                {
                    text += $"\n{Resources.wox_plugin_windowwalker_Desktop}: {window.Desktop.Name}";

                    if (!window.Desktop.IsAllDesktopsView)
                    {
                        text += $" ({Resources.wox_plugin_windowwalker_Number} {window.Desktop.Number})";
                    }
                }

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
                    $"Desktop id: {window.Desktop.Id}\n" +
                    $"Desktop name: {window.Desktop.Name}\n" +
                    $"Desktop number: {window.Desktop.Number}\n" +
                    $"Desktop is visible: {window.Desktop.IsVisible}\n" +
                    $"Desktop position: {window.Desktop.Position}\n" +
                    $"Is AllDesktops view: {window.Desktop.IsAllDesktopsView}";

                return new ToolTipData(window.Title, text);
           }
        }

        /// <summary>
        /// Returns an information result about the explorer setting
        /// </summary>
        /// <param name="iIcon">The path to the info icon.</param>
        /// <returns>An object of the type <see cref="Result"/> with the information.</returns>
        private static Result GetExplorerInfoResult(string iIcon)
        {
            return new Result()
            {
                Title = Resources.wox_plugin_windowwalker_ExplorerInfoTitle,
                IcoPath = iIcon,
                SubTitle = Resources.wox_plugin_windowwalker_ExplorerInfoSubTitle,
                Action = c =>
                {
                    Helper.OpenInShell("rundll32.exe", "shell32.dll,Options_RunDLL 7"); // "shell32.dll,Options_RunDLL 7" opens the view tab in folder options of explorer.
                    return true;
                },
                Score = 100_000,
            };
        }
    }
}
