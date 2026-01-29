// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Services
{
    /// <summary>
    /// AOT-compatible factory service for PowerToys module Settings.
    /// Uses static type registration instead of reflection-based discovery.
    /// </summary>
    /// <remarks>
    /// When adding a new PowerToys module, add it to both InitializeFactories() and InitializeTypes() methods.
    /// </remarks>
    public class SettingsFactory
    {
        private readonly SettingsUtils _settingsUtils;
        private readonly Dictionary<string, Func<IHotkeyConfig>> _settingsFactories;
        private readonly Dictionary<string, Type> _settingsTypes;

        public SettingsFactory(SettingsUtils settingsUtils)
        {
            _settingsUtils = settingsUtils ?? throw new ArgumentNullException(nameof(settingsUtils));
            _settingsFactories = InitializeFactories();
            _settingsTypes = InitializeTypes();
        }

        /// <summary>
        /// Static registry of all module settings factories.
        /// IMPORTANT: When adding a new module, add it here.
        /// </summary>
        private Dictionary<string, Func<IHotkeyConfig>> InitializeFactories()
        {
            return new Dictionary<string, Func<IHotkeyConfig>>
            {
                ["GeneralSettings"] = () => SettingsRepository<GeneralSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["AdvancedPaste"] = () => SettingsRepository<AdvancedPasteSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["AlwaysOnTop"] = () => SettingsRepository<AlwaysOnTopSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["ColorPicker"] = () => SettingsRepository<ColorPickerSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["CropAndLock"] = () => SettingsRepository<CropAndLockSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["CursorWrap"] = () => SettingsRepository<CursorWrapSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["FindMyMouse"] = () => SettingsRepository<FindMyMouseSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["LightSwitch"] = () => SettingsRepository<LightSwitchSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["MeasureTool"] = () => SettingsRepository<MeasureToolSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["MouseHighlighter"] = () => SettingsRepository<MouseHighlighterSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["MouseJump"] = () => SettingsRepository<MouseJumpSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["MousePointerCrosshairs"] = () => SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["MouseWithoutBorders"] = () => SettingsRepository<MouseWithoutBordersSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["Peek"] = () => SettingsRepository<PeekSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["PowerLauncher"] = () => SettingsRepository<PowerLauncherSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["PowerOCR"] = () => SettingsRepository<PowerOcrSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["ShortcutGuide"] = () => SettingsRepository<ShortcutGuideSettings>.GetInstance(_settingsUtils).SettingsConfig,
                ["Workspaces"] = () => SettingsRepository<WorkspacesSettings>.GetInstance(_settingsUtils).SettingsConfig,
            };
        }

        /// <summary>
        /// Static registry of module name to settings type mapping.
        /// IMPORTANT: When adding a new module, add it here.
        /// </summary>
        private Dictionary<string, Type> InitializeTypes()
        {
            return new Dictionary<string, Type>
            {
                ["GeneralSettings"] = typeof(GeneralSettings),
                ["AdvancedPaste"] = typeof(AdvancedPasteSettings),
                ["AlwaysOnTop"] = typeof(AlwaysOnTopSettings),
                ["ColorPicker"] = typeof(ColorPickerSettings),
                ["CropAndLock"] = typeof(CropAndLockSettings),
                ["CursorWrap"] = typeof(CursorWrapSettings),
                ["FindMyMouse"] = typeof(FindMyMouseSettings),
                ["LightSwitch"] = typeof(LightSwitchSettings),
                ["MeasureTool"] = typeof(MeasureToolSettings),
                ["MouseHighlighter"] = typeof(MouseHighlighterSettings),
                ["MouseJump"] = typeof(MouseJumpSettings),
                ["MousePointerCrosshairs"] = typeof(MousePointerCrosshairsSettings),
                ["MouseWithoutBorders"] = typeof(MouseWithoutBordersSettings),
                ["Peek"] = typeof(PeekSettings),
                ["PowerLauncher"] = typeof(PowerLauncherSettings),
                ["PowerOCR"] = typeof(PowerOcrSettings),
                ["ShortcutGuide"] = typeof(ShortcutGuideSettings),
                ["Workspaces"] = typeof(WorkspacesSettings),
            };
        }

        /// <summary>
        /// Gets a settings instance for the specified module using SettingsRepository.
        /// AOT-compatible: uses static factory lookup instead of reflection.
        /// </summary>
        /// <param name="moduleKey">The module key/name</param>
        /// <returns>The settings instance implementing IHotkeyConfig, or null if not found</returns>
        public IHotkeyConfig GetSettings(string moduleKey)
        {
            if (!_settingsFactories.TryGetValue(moduleKey, out var factory))
            {
                return null;
            }

            try
            {
                return factory();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting Settings for {moduleKey}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets fresh settings from disk for the specified module.
        /// AOT-compatible: uses static type dispatch instead of MakeGenericMethod.
        /// </summary>
        public IHotkeyConfig GetFreshSettings(string moduleKey)
        {
            if (!_settingsTypes.TryGetValue(moduleKey, out var settingsType))
            {
                return null;
            }

            try
            {
                string actualModuleKey = moduleKey == "GeneralSettings" ? string.Empty : moduleKey;
                return GetFreshSettingsForType(settingsType, actualModuleKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting fresh settings for {moduleKey}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Static dispatch for GetSettingsOrDefault using pattern matching.
        /// Replaces reflection-based MakeGenericMethod/Invoke pattern.
        /// </summary>
        private IHotkeyConfig GetFreshSettingsForType(Type settingsType, string moduleKey)
        {
            return settingsType.Name switch
            {
                nameof(GeneralSettings) => _settingsUtils.GetSettingsOrDefault<GeneralSettings>(moduleKey, "settings.json"),
                nameof(AdvancedPasteSettings) => _settingsUtils.GetSettingsOrDefault<AdvancedPasteSettings>(moduleKey, "settings.json"),
                nameof(AlwaysOnTopSettings) => _settingsUtils.GetSettingsOrDefault<AlwaysOnTopSettings>(moduleKey, "settings.json"),
                nameof(ColorPickerSettings) => _settingsUtils.GetSettingsOrDefault<ColorPickerSettings>(moduleKey, "settings.json"),
                nameof(CropAndLockSettings) => _settingsUtils.GetSettingsOrDefault<CropAndLockSettings>(moduleKey, "settings.json"),
                nameof(CursorWrapSettings) => _settingsUtils.GetSettingsOrDefault<CursorWrapSettings>(moduleKey, "settings.json"),
                nameof(FindMyMouseSettings) => _settingsUtils.GetSettingsOrDefault<FindMyMouseSettings>(moduleKey, "settings.json"),
                nameof(LightSwitchSettings) => _settingsUtils.GetSettingsOrDefault<LightSwitchSettings>(moduleKey, "settings.json"),
                nameof(MeasureToolSettings) => _settingsUtils.GetSettingsOrDefault<MeasureToolSettings>(moduleKey, "settings.json"),
                nameof(MouseHighlighterSettings) => _settingsUtils.GetSettingsOrDefault<MouseHighlighterSettings>(moduleKey, "settings.json"),
                nameof(MouseJumpSettings) => _settingsUtils.GetSettingsOrDefault<MouseJumpSettings>(moduleKey, "settings.json"),
                nameof(MousePointerCrosshairsSettings) => _settingsUtils.GetSettingsOrDefault<MousePointerCrosshairsSettings>(moduleKey, "settings.json"),
                nameof(MouseWithoutBordersSettings) => _settingsUtils.GetSettingsOrDefault<MouseWithoutBordersSettings>(moduleKey, "settings.json"),
                nameof(PeekSettings) => _settingsUtils.GetSettingsOrDefault<PeekSettings>(moduleKey, "settings.json"),
                nameof(PowerLauncherSettings) => _settingsUtils.GetSettingsOrDefault<PowerLauncherSettings>(moduleKey, "settings.json"),
                nameof(PowerOcrSettings) => _settingsUtils.GetSettingsOrDefault<PowerOcrSettings>(moduleKey, "settings.json"),
                nameof(ShortcutGuideSettings) => _settingsUtils.GetSettingsOrDefault<ShortcutGuideSettings>(moduleKey, "settings.json"),
                nameof(WorkspacesSettings) => _settingsUtils.GetSettingsOrDefault<WorkspacesSettings>(moduleKey, "settings.json"),
                _ => null,
            };
        }

        /// <summary>
        /// Gets all available module names that have settings implementing IHotkeyConfig
        /// </summary>
        /// <returns>List of module names</returns>
        public List<string> GetAvailableModuleNames()
        {
            return new List<string>(_settingsTypes.Keys);
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
