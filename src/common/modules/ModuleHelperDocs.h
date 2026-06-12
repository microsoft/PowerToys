// ModuleHelperDocs.h - XML documentation for ModuleHelper
// Implements fix for issue #45364
#pragma once

namespace PowerToys::Modules
{
    /// <summary>
    /// Provides helper methods for PowerToys module management.
    /// </summary>
    /// <remarks>
    /// This class contains utility functions used across all PowerToys modules
    /// for common operations like initialization, configuration loading, and cleanup.
    /// </remarks>
    class ModuleHelper
    {
    public:
        /// <summary>
        /// Initializes the module with the specified configuration path.
        /// </summary>
        /// <param name="configPath">The path to the module's configuration file.</param>
        /// <returns>True if initialization succeeded, false otherwise.</returns>
        /// <exception cref="std::invalid_argument">Thrown when configPath is empty.</exception>
        static bool Initialize(const std::wstring& configPath);
        
        /// <summary>
        /// Loads module settings from the registry or settings file.
        /// </summary>
        /// <param name="moduleName">The name of the module to load settings for.</param>
        /// <param name="defaultSettings">Default settings to use if none are found.</param>
        /// <returns>A Settings object containing the loaded or default settings.</returns>
        static Settings LoadSettings(const std::wstring& moduleName, const Settings& defaultSettings);
        
        /// <summary>
        /// Validates the module's current state and configuration.
        /// </summary>
        /// <returns>True if the module is in a valid state, false otherwise.</returns>
        /// <remarks>
        /// This method should be called after initialization to ensure
        /// the module is properly configured before use.
        /// </remarks>
        static bool Validate();
        
        /// <summary>
        /// Cleans up module resources and saves current state.
        /// </summary>
        /// <remarks>
        /// Always call this method before unloading the module to prevent
        /// resource leaks and ensure settings are persisted.
        /// </remarks>
        static void Cleanup();
    };
}
