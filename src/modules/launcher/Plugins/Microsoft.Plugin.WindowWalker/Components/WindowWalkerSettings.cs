// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Plugin.WindowWalker.Properties;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.Plugin.WindowWalker.Components
{
    /// <summary>
    /// Additional settings for the time zone plugin.
    /// </summary>
    /// <remarks>Code reused from "Microsoft.PowerToys.Run.Plugin.TimeZone" plugin.</remarks>
    internal sealed class WindowWalkerSettings
    {
        /// <summary>
        /// Gets a value indicating whether we only search for windows on the currently visible desktop or on all desktops.
        /// </summary>
        internal bool ResultsFromVisibleDesktopOnly { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the process id is shown in the subtitle.
        /// </summary>
        internal bool SubtitleShowPid { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the desktop name is shown in the subtitle.
        /// We don't show the desktop name if there is only one desktop.
        /// </summary>
        internal bool SubtitleShowDesktopName { get; private set; }

        /// <summary>
        /// Gets a value indicating whether we request a confirmation when the user kills a process.
        /// </summary>
        internal bool ConfirmKillProcess { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the "kill process" command is hidden on processes that require additional permissions (UAC).
        /// </summary>
        internal bool HideKillProcessOnElevatedProcesses { get; private set; }

        /// <summary>
        /// Gets a value indicating whether we show the explorer settings info or not.
        /// </summary>
        internal bool HideExplorerSettingInfo { get; private set; }

        /// <summary>
        /// Return a list with all settings. Additional
        /// </summary>
        /// <returns>A list with all settings.</returns>
        internal List<PluginAdditionalOption> GetAdditionalOptions()
        {
            var optionList = new List<PluginAdditionalOption>
            {
                new PluginAdditionalOption
                {
                    Key = nameof(ResultsFromVisibleDesktopOnly),
                    DisplayLabel = Resources.wox_plugin_windowwalker_SettingResultsVisibleDesktop,
                    Value = false,
                },
                new PluginAdditionalOption
                {
                    Key = nameof(SubtitleShowPid),
                    DisplayLabel = Resources.wox_plugin_windowwalker_SettingSubtitlePid,
                    Value = false,
                },
                new PluginAdditionalOption
                {
                    Key = nameof(SubtitleShowDesktopName),
                    DisplayLabel = Resources.wox_plugin_windowwalker_SettingSubtitleDesktopName,
                    Value = true,
                },
                new PluginAdditionalOption
                {
                    Key = nameof(ConfirmKillProcess),
                    DisplayLabel = Resources.wox_plugin_windowwalker_SettingConfirmKillProcess,
                    Value = true,
                },
                new PluginAdditionalOption
                {
                    Key = nameof(HideKillProcessOnElevatedProcesses),
                    DisplayLabel = Resources.wox_plugin_windowwalker_SettingHideKillProcess,
                    Value = false,
                },
                new PluginAdditionalOption
                {
                    Key = nameof(HideExplorerSettingInfo),
                    DisplayLabel = Resources.wox_plugin_windowwalker_SettingExplorerSettingInfo,
                    Value = false,
                },
            };

            return optionList;
        }

        /// <summary>
        /// Update this settings.
        /// </summary>
        /// <param name="settings">The settings for all power launcher plugin.</param>
        internal void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings is null || settings.AdditionalOptions is null)
            {
                return;
            }

            ResultsFromVisibleDesktopOnly = GetSettingOrFallback(settings, nameof(ResultsFromVisibleDesktopOnly), false);
            SubtitleShowPid = GetSettingOrFallback(settings, nameof(SubtitleShowPid), false);
            SubtitleShowDesktopName = GetSettingOrFallback(settings, nameof(SubtitleShowDesktopName), true);
            ConfirmKillProcess = GetSettingOrFallback(settings, nameof(ConfirmKillProcess), true);
            HideKillProcessOnElevatedProcesses = GetSettingOrFallback(settings, nameof(HideKillProcessOnElevatedProcesses), false);
            HideExplorerSettingInfo = GetSettingOrFallback(settings, nameof(HideExplorerSettingInfo), false);
        }

        /// <summary>
        /// Return one <see cref="bool"/> setting of the given settings list with the given name.
        /// </summary>
        /// <param name="settings">The object that contain all settings.</param>
        /// <param name="name">The name of the setting.</param>
        /// <param name="fallbackValue">The fall-back value that is used when the setting is not found.</param>
        /// <returns>A settings value.</returns>
        private static bool GetSettingOrFallback(PowerLauncherPluginSettings settings, string name, bool fallbackValue)
        {
            var option = settings.AdditionalOptions.FirstOrDefault(x => x.Key == name);
            var settingsValue = option?.Value ?? fallbackValue;
            return settingsValue;
        }
    }
}
