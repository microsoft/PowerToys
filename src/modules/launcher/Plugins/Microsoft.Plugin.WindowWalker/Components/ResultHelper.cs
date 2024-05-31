// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Linq;
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
            if (searchControllerResults == null || searchControllerResults.Count == 0)
            {
                return new List<Result>();
            }

            List<Result> resultsList = new List<Result>(searchControllerResults.Count);
            bool addExplorerInfo = searchControllerResults.Any(x =>
                string.Equals(x.Result.Process.Name, "explorer.exe", StringComparison.OrdinalIgnoreCase) &&
                x.Result.Process.IsShellProcess);

            // Process each SearchResult to convert it into a Result.
            // Using parallel processing if the operation is CPU-bound and the list is large.
            resultsList = searchControllerResults
                .AsParallel()
                .Select(x => CreateResultFromSearchResult(x, icon))
                .ToList();

            if (addExplorerInfo && isKeywordSearch && !WindowWalkerSettings.Instance.HideExplorerSettingInfo)
            {
                resultsList.Add(GetExplorerInfoResult(infoIcon));
            }

            return resultsList;
        }

        /// <summary>
        /// Creates a Result object from a given SearchResult.
        /// </summary>
        /// <param name="searchResult">The SearchResult object to convert.</param>
        /// <param name="icon">The path to the icon that should be used for the Result.</param>
        /// <returns>A Result object populated with data from the SearchResult.</returns>
        private static Result CreateResultFromSearchResult(SearchResult searchResult, string icon)
        {
            return new Result
            {
                Title = searchResult.Result.Title,
                IcoPath = icon,
                SubTitle = GetSubtitle(searchResult.Result),
                ContextData = searchResult.Result,
                Action = c =>
                {
                    searchResult.Result.SwitchToWindow();
                    return true;
                },

                // For debugging you can set the second parameter to true to see more information.
                ToolTipData = GetToolTip(searchResult.Result, false),
            };
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

            if (!window.Process.IsResponding)
            {
                subtitleText += $" [{Resources.wox_plugin_windowwalker_NotResponding}]";
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
                    $"Is AllDesktops view: {window.Desktop.IsAllDesktopsView}\n" +
                    $"Responding: {window.Process.IsResponding}";

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
