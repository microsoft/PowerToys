// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Plugin.WindowWalker.Properties;
using Microsoft.PowerToys.Settings.UI.Library;

[assembly: InternalsVisibleTo("Microsoft.Plugin.WindowWalker.UnitTests")]

namespace Microsoft.Plugin.WindowWalker.Components
{
    /// <summary>
    /// Additional settings for the WindowWalker plugin.
    /// </summary>
    /// <remarks>Some code parts reused from TimeZone plugin.</remarks>
    internal sealed class WindowWalkerSettings
    {
        /// <summary>
        /// Are the class properties initialized with default values
        /// </summary>
        private readonly bool _initialized;

        /// <summary>
        /// An instance of the class <see cref="WindowWalkerSettings"></see>
        /// </summary>
        private static WindowWalkerSettings instance;

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
        /// Gets a value indicating whether to kill the entire process tree or the selected process only.
        /// </summary>
        internal bool KillProcessTree { get; private set; }

        /// <summary>
        /// Gets a value indicating whether PowerToys run should stay open after executing killing process and closing window.
        /// </summary>
        internal bool OpenAfterKillAndClose { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the "kill process" command is hidden on processes that require additional permissions (UAC).
        /// </summary>
        internal bool HideKillProcessOnElevatedProcesses { get; private set; }

        /// <summary>
        /// Gets a value indicating whether we show the explorer settings info or not.
        /// </summary>
        internal bool HideExplorerSettingInfo { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowWalkerSettings"/> class.
        /// Private constructor to make sure there is never more than one instance of this class
        /// </summary>
        private WindowWalkerSettings()
        {
            // Init class properties with default values
            UpdateSettings(null);
            _initialized = true;
        }

        /// <summary>
        /// Gets an instance property of this class that makes sure that the first instance gets created
        /// and that all the requests end up at that one instance.
        /// The benefit of this is that we don't need additional variables/parameters
        /// to communicate the settings between plugin's classes/methods.
        /// We can simply access this one instance, whenever we need the actual settings.
        /// </summary>
        internal static WindowWalkerSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new WindowWalkerSettings();
                }

                return instance;
            }
        }

        /// <summary>
        /// Return a list with all additional plugin options.
        /// </summary>
        /// <returns>A list with all additional plugin options.</returns>
        internal static List<PluginAdditionalOption> GetAdditionalOptions()
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
                    DisplayDescription = Resources.wox_plugin_windowwalker_SettingSubtitleDesktopName_Description,
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
                    Key = nameof(KillProcessTree),
                    DisplayLabel = Resources.wox_plugin_windowwalker_SettingKillProcessTree,
                    DisplayDescription = Resources.wox_plugin_windowwalker_SettingKillProcessTree_Description,
                    Value = false,
                },
                new PluginAdditionalOption
                {
                    Key = nameof(OpenAfterKillAndClose),
                    DisplayLabel = Resources.wox_plugin_windowwalker_SettingOpenAfterKillAndClose,
                    DisplayDescription = Resources.wox_plugin_windowwalker_SettingOpenAfterKillAndClose_Description,
                    Value = false,
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
                    DisplayDescription = Resources.wox_plugin_windowwalker_SettingExplorerSettingInfo_Description,
                    Value = false,
                },
            };

            return optionList;
        }

        /// <summary>
        /// Update this settings.
        /// </summary>
        /// <param name="settings">The settings for all power launcher plugins.</param>
        internal void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if ((settings is null || settings.AdditionalOptions is null) & _initialized)
            {
                return;
            }

            ResultsFromVisibleDesktopOnly = GetSettingOrDefault(settings, nameof(ResultsFromVisibleDesktopOnly));
            SubtitleShowPid = GetSettingOrDefault(settings, nameof(SubtitleShowPid));
            SubtitleShowDesktopName = GetSettingOrDefault(settings, nameof(SubtitleShowDesktopName));
            ConfirmKillProcess = GetSettingOrDefault(settings, nameof(ConfirmKillProcess));
            KillProcessTree = GetSettingOrDefault(settings, nameof(KillProcessTree));
            OpenAfterKillAndClose = GetSettingOrDefault(settings, nameof(OpenAfterKillAndClose));
            HideKillProcessOnElevatedProcesses = GetSettingOrDefault(settings, nameof(HideKillProcessOnElevatedProcesses));
            HideExplorerSettingInfo = GetSettingOrDefault(settings, nameof(HideExplorerSettingInfo));
        }

        /// <summary>
        /// Return one <see cref="bool"/> setting of the given settings list with the given name.
        /// </summary>
        /// <param name="settings">The object that contain all settings.</param>
        /// <param name="name">The name of the setting.</param>
        /// <returns>A settings value.</returns>
        private static bool GetSettingOrDefault(PowerLauncherPluginSettings settings, string name)
        {
            var option = settings?.AdditionalOptions?.FirstOrDefault(x => x.Key == name);

            // If a setting isn't available, we use the value defined in the method GetAdditionalOptions() as fallback.
            // We can use First() instead of FirstOrDefault() because the values must exist. Otherwise, we made a mistake when defining the settings.
            return option?.Value ?? GetAdditionalOptions().First(x => x.Key == name).Value;
        }
    }
}
