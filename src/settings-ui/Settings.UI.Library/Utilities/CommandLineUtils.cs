// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library;

/// <summary>
/// AOT-compatible command line utilities.
/// Uses static type mapping instead of AppDomain reflection.
/// </summary>
public class CommandLineUtils
{
    private static readonly Dictionary<string, Type> _settingsTypes = new()
    {
        ["GeneralSettings"] = typeof(GeneralSettings),
        ["AdvancedPaste"] = typeof(AdvancedPasteSettings),
        ["AlwaysOnTop"] = typeof(AlwaysOnTopSettings),
        ["Awake"] = typeof(AwakeSettings),
        ["CmdNotFound"] = typeof(CmdNotFoundSettings),
        ["ColorPicker"] = typeof(ColorPickerSettings),
        ["CropAndLock"] = typeof(CropAndLockSettings),
        ["CursorWrap"] = typeof(CursorWrapSettings),
        ["EnvironmentVariables"] = typeof(EnvironmentVariablesSettings),
        ["FancyZones"] = typeof(FancyZonesSettings),
        ["FileLocksmith"] = typeof(FileLocksmithSettings),
        ["FindMyMouse"] = typeof(FindMyMouseSettings),
        ["Hosts"] = typeof(HostsSettings),
        ["ImageResizer"] = typeof(ImageResizerSettings),
        ["KeyboardManager"] = typeof(KeyboardManagerSettings),
        ["LightSwitch"] = typeof(LightSwitchSettings),
        ["MeasureTool"] = typeof(MeasureToolSettings),
        ["MouseHighlighter"] = typeof(MouseHighlighterSettings),
        ["MouseJump"] = typeof(MouseJumpSettings),
        ["MousePointerCrosshairs"] = typeof(MousePointerCrosshairsSettings),
        ["MouseWithoutBorders"] = typeof(MouseWithoutBordersSettings),
        ["NewPlus"] = typeof(NewPlusSettings),
        ["Peek"] = typeof(PeekSettings),
        ["PowerAccent"] = typeof(PowerAccentSettings),
        ["PowerLauncher"] = typeof(PowerLauncherSettings),
        ["PowerOCR"] = typeof(PowerOcrSettings),
        ["PowerRename"] = typeof(PowerRenameSettings),
        ["PowerPreview"] = typeof(PowerPreviewSettings),
        ["RegistryPreview"] = typeof(RegistryPreviewSettings),
        ["ShortcutGuide"] = typeof(ShortcutGuideSettings),
        ["Workspaces"] = typeof(WorkspacesSettings),
        ["ZoomIt"] = typeof(ZoomItSettings),
    };

    public static ISettingsConfig GetSettingsConfigFor(string moduleName, SettingsUtils settingsUtils, Assembly settingsLibraryAssembly = null)
    {
        if (!_settingsTypes.TryGetValue(moduleName, out var settingsType))
        {
            return null;
        }

        return GetSettingsConfigFor(settingsType, settingsUtils);
    }

    /// <summary>
    /// Gets settings config for a given type using static dispatch.
    /// AOT-compatible: replaces MakeGenericType/GetMethod/Invoke pattern.
    /// </summary>
    public static ISettingsConfig GetSettingsConfigFor(Type moduleSettingsType, SettingsUtils settingsUtils)
    {
        return moduleSettingsType.Name switch
        {
            nameof(GeneralSettings) => SettingsRepository<GeneralSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(AdvancedPasteSettings) => SettingsRepository<AdvancedPasteSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(AlwaysOnTopSettings) => SettingsRepository<AlwaysOnTopSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(AwakeSettings) => SettingsRepository<AwakeSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(CmdNotFoundSettings) => SettingsRepository<CmdNotFoundSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(ColorPickerSettings) => SettingsRepository<ColorPickerSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(CropAndLockSettings) => SettingsRepository<CropAndLockSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(CursorWrapSettings) => SettingsRepository<CursorWrapSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(EnvironmentVariablesSettings) => SettingsRepository<EnvironmentVariablesSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(FancyZonesSettings) => SettingsRepository<FancyZonesSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(FileLocksmithSettings) => SettingsRepository<FileLocksmithSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(FindMyMouseSettings) => SettingsRepository<FindMyMouseSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(HostsSettings) => SettingsRepository<HostsSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(ImageResizerSettings) => SettingsRepository<ImageResizerSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(KeyboardManagerSettings) => SettingsRepository<KeyboardManagerSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(LightSwitchSettings) => SettingsRepository<LightSwitchSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(MeasureToolSettings) => SettingsRepository<MeasureToolSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(MouseHighlighterSettings) => SettingsRepository<MouseHighlighterSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(MouseJumpSettings) => SettingsRepository<MouseJumpSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(MousePointerCrosshairsSettings) => SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(MouseWithoutBordersSettings) => SettingsRepository<MouseWithoutBordersSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(NewPlusSettings) => SettingsRepository<NewPlusSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(PeekSettings) => SettingsRepository<PeekSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(PowerAccentSettings) => SettingsRepository<PowerAccentSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(PowerLauncherSettings) => SettingsRepository<PowerLauncherSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(PowerOcrSettings) => SettingsRepository<PowerOcrSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(PowerRenameSettings) => SettingsRepository<PowerRenameSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(PowerPreviewSettings) => SettingsRepository<PowerPreviewSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(RegistryPreviewSettings) => SettingsRepository<RegistryPreviewSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(ShortcutGuideSettings) => SettingsRepository<ShortcutGuideSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(WorkspacesSettings) => SettingsRepository<WorkspacesSettings>.GetInstance(settingsUtils).SettingsConfig,
            nameof(ZoomItSettings) => SettingsRepository<ZoomItSettings>.GetInstance(settingsUtils).SettingsConfig,
            _ => null,
        };
    }

    /// <summary>
    /// Gets the Properties object from a settings config.
    /// For GeneralSettings, returns the settings itself. For others, returns the Properties property.
    /// </summary>
    public static object GetProperties(ISettingsConfig settingsConfig)
    {
        // Use reflection fallback for all settings types to preserve compatibility
        // This is needed because not all settings have static patterns
        var settingsType = settingsConfig.GetType();
        if (settingsType == typeof(GeneralSettings))
        {
            return settingsConfig;
        }

        var propertiesProperty = settingsType.GetProperty("Properties");
        return propertiesProperty?.GetValue(settingsConfig);
    }

    /// <summary>
    /// Gets enabled state for a specific module.
    /// AOT-compatible: static dispatch instead of reflection.
    /// </summary>
    public static bool GetEnabledModuleValue(string moduleName, EnabledModules enabled)
    {
        return moduleName switch
        {
            "AdvancedPaste" => enabled.AdvancedPaste,
            "AlwaysOnTop" => enabled.AlwaysOnTop,
            "Awake" => enabled.Awake,
            "CmdNotFound" => enabled.CmdNotFound,
            "ColorPicker" => enabled.ColorPicker,
            "CropAndLock" => enabled.CropAndLock,
            "CursorWrap" => enabled.CursorWrap,
            "EnvironmentVariables" => enabled.EnvironmentVariables,
            "FancyZones" => enabled.FancyZones,
            "FileLocksmith" => enabled.FileLocksmith,
            "FindMyMouse" => enabled.FindMyMouse,
            "Hosts" => enabled.Hosts,
            "ImageResizer" => enabled.ImageResizer,
            "KeyboardManager" => enabled.KeyboardManager,
            "LightSwitch" => enabled.LightSwitch,
            "MeasureTool" => enabled.MeasureTool,
            "MouseHighlighter" => enabled.MouseHighlighter,
            "MouseJump" => enabled.MouseJump,
            "MousePointerCrosshairs" => enabled.MousePointerCrosshairs,
            "MouseWithoutBorders" => enabled.MouseWithoutBorders,
            "NewPlus" => enabled.NewPlus,
            "Peek" => enabled.Peek,
            "PowerAccent" => enabled.PowerAccent,
            "PowerLauncher" => enabled.PowerLauncher,
            "PowerOcr" => enabled.PowerOcr,
            "PowerRename" => enabled.PowerRename,
            "RegistryPreview" => enabled.RegistryPreview,
            "ShortcutGuide" => enabled.ShortcutGuide,
            "Workspaces" => enabled.Workspaces,
            "ZoomIt" => enabled.ZoomIt,
            _ => false,
        };
    }

    /// <summary>
    /// Locates a setting property and returns both the PropertyInfo and the properties object.
    /// Uses reflection on properties which is preserved via DynamicallyAccessedMembers on property types.
    /// </summary>
    public static (PropertyInfo SettingInfo, object Properties) LocateSetting(string propertyName, ISettingsConfig settingsConfig)
    {
        var properties = GetProperties(settingsConfig);
        var propertiesType = properties.GetType();

        // Special handling for GeneralSettings.Enabled.*
        if (propertiesType == typeof(GeneralSettings) && propertyName.StartsWith("Enabled.", StringComparison.InvariantCulture))
        {
            var moduleNameToToggle = propertyName.Replace("Enabled.", string.Empty);
            properties = propertiesType.GetProperty("Enabled").GetValue(properties);
            propertiesType = properties.GetType();
            propertyName = moduleNameToToggle;
        }

        return (propertiesType.GetProperty(propertyName), properties);
    }

    /// <summary>
    /// Gets the value of a property from a settings config.
    /// </summary>
    public static object GetPropertyValue(string propertyName, ISettingsConfig settingsConfig)
    {
        var (settingInfo, properties) = LocateSetting(propertyName, settingsConfig);
        return settingInfo?.GetValue(properties);
    }

    /// <summary>
    /// Gets the PropertyInfo for a setting property.
    /// </summary>
    public static PropertyInfo GetSettingPropertyInfo(string propertyName, ISettingsConfig settingsConfig)
    {
        return LocateSetting(propertyName, settingsConfig).SettingInfo;
    }
}
