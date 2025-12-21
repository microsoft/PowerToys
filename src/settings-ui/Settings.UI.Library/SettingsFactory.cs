// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// Factory service for getting PowerToys module Settings that implement IHotkeyConfig
    /// </summary>
    public class SettingsFactory
    {
        private readonly SettingsUtils _settingsUtils;
        private readonly Dictionary<string, Type> _settingsTypes;

        public SettingsFactory(SettingsUtils settingsUtils)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            _settingsTypes = DiscoverSettingsTypes();
        }

        /// <summary>
        /// Dynamically discovers all Settings types that implement IHotkeyConfig
        /// </summary>
        private Dictionary<string, Type> DiscoverSettingsTypes()
        {
            var settingsTypes = new Dictionary<string, Type>();

            // Get the Settings.UI.Library assembly
            var assembly = Assembly.GetAssembly(typeof(IHotkeyConfig));
            if (assembly == null)
            {
                return settingsTypes;
            }

            try
            {
                // Find all types that implement IHotkeyConfig and ISettingsConfig
                var hotkeyConfigTypes = assembly.GetTypes()
                    .Where(type =>
                        type.IsClass &&
                        !type.IsAbstract &&
                        typeof(IHotkeyConfig).IsAssignableFrom(type) &&
                        typeof(ISettingsConfig).IsAssignableFrom(type))
                    .ToList();

                foreach (var type in hotkeyConfigTypes)
                {
                    // Try to get the ModuleName using SettingsRepository
                    try
                    {
                        var repositoryType = typeof(SettingsRepository<>).MakeGenericType(type);
                        var getInstanceMethod = repositoryType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);
                        var repository = getInstanceMethod?.Invoke(null, new object[] { _settingsUtils });

                        if (repository != null)
                        {
                            var settingsConfigProperty = repository.GetType().GetProperty("SettingsConfig");
                            var settingsInstance = settingsConfigProperty?.GetValue(repository) as ISettingsConfig;

                            if (settingsInstance != null)
                            {
                                var moduleName = settingsInstance.GetModuleName();
                                if (!string.IsNullOrEmpty(moduleName))
                                {
                                    settingsTypes[moduleName] = type;
                                    System.Diagnostics.Debug.WriteLine($"Discovered settings type: {type.Name} for module: {moduleName}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting module name for {type.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning assembly {assembly.FullName}: {ex.Message}");
            }

            return settingsTypes;
        }

        public IHotkeyConfig GetFreshSettings(string moduleKey)
        {
            if (!_settingsTypes.TryGetValue(moduleKey, out var settingsType))
            {
                return null;
            }

            try
            {
                // Create a generic method call to _settingsUtils.GetSettingsOrDefault<T>(moduleKey)
                var getSettingsMethod = typeof(SettingsUtils).GetMethod("GetSettingsOrDefault", new[] { typeof(string), typeof(string) });
                var genericMethod = getSettingsMethod?.MakeGenericMethod(settingsType);

                // Call GetSettingsOrDefault<T>(moduleKey) to get fresh settings from file
                var freshSettings = genericMethod?.Invoke(_settingsUtils, new object[] { moduleKey, "settings.json" });

                return freshSettings as IHotkeyConfig;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting fresh settings for {moduleKey}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets a settings instance for the specified module using SettingsRepository
        /// </summary>
        /// <param name="moduleKey">The module key/name</param>
        /// <returns>The settings instance implementing IHotkeyConfig, or null if not found</returns>
        public IHotkeyConfig GetSettings(string moduleKey)
        {
            if (!_settingsTypes.TryGetValue(moduleKey, out var settingsType))
            {
                return null;
            }

            try
            {
                var repositoryType = typeof(SettingsRepository<>).MakeGenericType(settingsType);
                var getInstanceMethod = repositoryType.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static);
                var repository = getInstanceMethod?.Invoke(null, new object[] { _settingsUtils });

                if (repository != null)
                {
                    var settingsConfigProperty = repository.GetType().GetProperty("SettingsConfig");
                    return settingsConfigProperty?.GetValue(repository) as IHotkeyConfig;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting Settings for {moduleKey}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets all available module names that have settings implementing IHotkeyConfig
        /// </summary>
        /// <returns>List of module names</returns>
        public List<string> GetAvailableModuleNames()
        {
            return _settingsTypes.Keys.ToList();
        }

        /// <summary>
        /// Gets all available settings that implement IHotkeyConfig
        /// </summary>
        /// <returns>Dictionary of module name to settings instance</returns>
        public Dictionary<string, IHotkeyConfig> GetAllHotkeySettings()
        {
            var result = new Dictionary<string, IHotkeyConfig>();

            foreach (var moduleKey in _settingsTypes.Keys)
            {
                try
                {
                    var settings = GetSettings(moduleKey);
                    if (settings != null)
                    {
                        result[moduleKey] = settings;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting settings for {moduleKey}: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Gets a specific settings repository instance
        /// </summary>
        /// <typeparam name="T">The settings type</typeparam>
        /// <returns>The settings repository instance</returns>
        public ISettingsRepository<T> GetRepository<T>()
            where T : class, ISettingsConfig, new()
        {
            return SettingsRepository<T>.GetInstance(_settingsUtils);
        }
    }
}
