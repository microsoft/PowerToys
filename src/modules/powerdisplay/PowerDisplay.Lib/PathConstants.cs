// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace PowerDisplay.Common
{
    /// <summary>
    /// Centralized path constants for PowerDisplay module.
    /// Provides unified access to all file and folder paths used by PowerDisplay and related integrations.
    /// </summary>
    public static class PathConstants
    {
        private static readonly Lazy<string> _localAppDataPath = new Lazy<string>(
            () => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        private static readonly Lazy<string> _powerToysBasePath = new Lazy<string>(
            () => Path.Combine(_localAppDataPath.Value, "Microsoft", "PowerToys"));

        /// <summary>
        /// Gets the base PowerToys settings folder path.
        /// Example: C:\Users\{User}\AppData\Local\Microsoft\PowerToys
        /// </summary>
        public static string PowerToysBasePath => _powerToysBasePath.Value;

        /// <summary>
        /// Gets the PowerDisplay module folder path.
        /// Example: C:\Users\{User}\AppData\Local\Microsoft\PowerToys\PowerDisplay
        /// </summary>
        public static string PowerDisplayFolderPath => Path.Combine(PowerToysBasePath, "PowerDisplay");

        /// <summary>
        /// Gets the PowerDisplay profiles file path.
        /// Example: C:\Users\{User}\AppData\Local\Microsoft\PowerToys\PowerDisplay\profiles.json
        /// </summary>
        public static string ProfilesFilePath => Path.Combine(PowerDisplayFolderPath, ProfilesFileName);

        /// <summary>
        /// Gets the PowerDisplay settings file path.
        /// Example: C:\Users\{User}\AppData\Local\Microsoft\PowerToys\PowerDisplay\settings.json
        /// </summary>
        public static string SettingsFilePath => Path.Combine(PowerDisplayFolderPath, SettingsFileName);

        /// <summary>
        /// Gets the LightSwitch module folder path.
        /// Example: C:\Users\{User}\AppData\Local\Microsoft\PowerToys\LightSwitch
        /// </summary>
        public static string LightSwitchFolderPath => Path.Combine(PowerToysBasePath, "LightSwitch");

        /// <summary>
        /// Gets the LightSwitch settings file path.
        /// Example: C:\Users\{User}\AppData\Local\Microsoft\PowerToys\LightSwitch\settings.json
        /// </summary>
        public static string LightSwitchSettingsFilePath => Path.Combine(LightSwitchFolderPath, SettingsFileName);

        /// <summary>
        /// The name of the profiles file.
        /// </summary>
        public const string ProfilesFileName = "profiles.json";

        /// <summary>
        /// The name of the settings file.
        /// </summary>
        public const string SettingsFileName = "settings.json";

        /// <summary>
        /// The name of the monitor state file.
        /// </summary>
        public const string MonitorStateFileName = "monitor_state.json";

        /// <summary>
        /// Gets the monitor state file path.
        /// Example: C:\Users\{User}\AppData\Local\Microsoft\PowerToys\PowerDisplay\monitor_state.json
        /// </summary>
        public static string MonitorStateFilePath => Path.Combine(PowerDisplayFolderPath, MonitorStateFileName);

        /// <summary>
        /// Event name for LightSwitch light theme change notifications.
        /// Signaled when LightSwitch switches to light mode.
        /// </summary>
        public const string LightSwitchLightThemeEventName = "Local\\PowerToys_LightSwitch_LightTheme";

        /// <summary>
        /// Event name for LightSwitch dark theme change notifications.
        /// Signaled when LightSwitch switches to dark mode.
        /// </summary>
        public const string LightSwitchDarkThemeEventName = "Local\\PowerToys_LightSwitch_DarkTheme";

        /// <summary>
        /// Ensures the PowerDisplay folder exists. Creates it if necessary.
        /// </summary>
        /// <returns>The PowerDisplay folder path</returns>
        public static string EnsurePowerDisplayFolderExists()
            => EnsureFolderExists(PowerDisplayFolderPath);

        /// <summary>
        /// Ensures the LightSwitch folder exists. Creates it if necessary.
        /// </summary>
        /// <returns>The LightSwitch folder path</returns>
        public static string EnsureLightSwitchFolderExists()
            => EnsureFolderExists(LightSwitchFolderPath);

        /// <summary>
        /// Ensures the specified folder exists. Creates it if necessary.
        /// </summary>
        /// <param name="folderPath">The folder path to ensure exists</param>
        /// <returns>The folder path</returns>
        private static string EnsureFolderExists(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            return folderPath;
        }
    }
}
