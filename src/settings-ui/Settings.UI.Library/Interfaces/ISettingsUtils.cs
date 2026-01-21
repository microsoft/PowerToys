// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.Library.Interfaces
{
    /// <summary>
    /// Interface for settings utility operations to enable dependency injection and testability.
    /// </summary>
    public interface ISettingsUtils
    {
        /// <summary>
        /// Checks if a settings file exists.
        /// </summary>
        /// <param name="powertoy">The module name.</param>
        /// <param name="fileName">The settings file name.</param>
        /// <returns>True if the settings file exists.</returns>
        bool SettingsExists(string powertoy = "", string fileName = "settings.json");

        /// <summary>
        /// Deletes settings for the specified module.
        /// </summary>
        /// <param name="powertoy">The module name.</param>
        void DeleteSettings(string powertoy = "");

        /// <summary>
        /// Gets settings for the specified module.
        /// </summary>
        /// <typeparam name="T">The settings type.</typeparam>
        /// <param name="powertoy">The module name.</param>
        /// <param name="fileName">The settings file name.</param>
        /// <returns>The deserialized settings object.</returns>
        T GetSettings<T>(string powertoy = "", string fileName = "settings.json")
            where T : ISettingsConfig, new();

        /// <summary>
        /// Gets settings for the specified module, or returns default if not found.
        /// </summary>
        /// <typeparam name="T">The settings type.</typeparam>
        /// <param name="powertoy">The module name.</param>
        /// <param name="fileName">The settings file name.</param>
        /// <returns>The deserialized settings object or default.</returns>
        T GetSettingsOrDefault<T>(string powertoy = "", string fileName = "settings.json")
            where T : ISettingsConfig, new();

        /// <summary>
        /// Saves settings to a JSON file.
        /// </summary>
        /// <param name="jsonSettings">The JSON settings string.</param>
        /// <param name="powertoy">The module name.</param>
        /// <param name="fileName">The settings file name.</param>
        void SaveSettings(string jsonSettings, string powertoy = "", string fileName = "settings.json");

        /// <summary>
        /// Gets the file path to the settings file.
        /// </summary>
        /// <param name="powertoy">The module name.</param>
        /// <param name="fileName">The settings file name.</param>
        /// <returns>The full path to the settings file.</returns>
        string GetSettingsFilePath(string powertoy = "", string fileName = "settings.json");
    }
}
