// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using GpoApi = global::PowerToys.GPOWrapper.GPOWrapper;
using GpoRuleConfigured = global::PowerToys.GPOWrapper.GpoRuleConfigured;

namespace PowerToys.Settings.Cli.Helpers;

internal static class SettingsCliHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static Dictionary<string, bool> GetModulesAndStatus(SettingsUtils? settingsUtils = null)
    {
        settingsUtils ??= SettingsUtils.Default;
        var generalSettings = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils).SettingsConfig;
        var enabledModules = generalSettings.Enabled;

        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var properties = typeof(EnabledModules).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        foreach (var prop in properties)
        {
            if (prop.PropertyType == typeof(bool))
            {
                var val = (bool)(prop.GetValue(enabledModules) ?? false);
                var gpoRule = GetModuleGpoRule(prop.Name);
                if (gpoRule == GpoRuleConfigured.Enabled)
                {
                    val = true;
                }
                else if (gpoRule == GpoRuleConfigured.Disabled)
                {
                    val = false;
                }

                result[prop.Name] = val;
            }
        }

        return result;
    }

    public static Dictionary<string, object?> GetModuleSettings(string moduleName, SettingsUtils? settingsUtils = null)
    {
        settingsUtils ??= SettingsUtils.Default;
        var assembly = CommandLineUtils.GetSettingsAssembly();
        var config = CommandLineUtils.GetSettingsConfigFor(moduleName, settingsUtils, assembly);

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (config == null)
        {
            return result;
        }

        var propertiesObject = CommandLineUtils.GetProperties(config);
        if (propertiesObject == null)
        {
            return result;
        }

        var props = propertiesObject.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (var prop in props)
        {
            try
            {
                var val = prop.GetValue(propertiesObject);
                result[prop.Name] = UnwrapPropertyValue(val);
            }
            catch
            {
                // Skip write-only or non-accessible properties
            }
        }

        return result;
    }

    public static object? GetSettingValue(string qualifiedName, SettingsUtils? settingsUtils = null)
    {
        settingsUtils ??= SettingsUtils.Default;
        var parts = qualifiedName.Split('.', 2);

        if (parts.Length < 2)
        {
            throw new ArgumentException($"Invalid setting name format: '{qualifiedName}'. Expected format: 'Module.SettingName' or 'Enabled.ModuleName'.");
        }

        var moduleName = parts[0];
        var propertyName = parts[1];

        if (string.Equals(moduleName, "Enabled", StringComparison.OrdinalIgnoreCase))
        {
            var modules = GetModulesAndStatus(settingsUtils);
            if (modules.TryGetValue(propertyName, out var enabled))
            {
                return enabled;
            }

            throw new ArgumentException($"Module '{propertyName}' not found in EnabledModules.");
        }

        var assembly = CommandLineUtils.GetSettingsAssembly();
        var config = CommandLineUtils.GetSettingsConfigFor(moduleName, settingsUtils, assembly);
        if (config == null)
        {
            throw new ArgumentException($"Module settings for '{moduleName}' were not found.");
        }

        var rawValue = CommandLineUtils.GetPropertyValue(propertyName, config);
        return UnwrapPropertyValue(rawValue);
    }

    public static GpoRuleConfigured GetModuleGpoRule(string moduleName)
    {
        return moduleName.ToLowerInvariant() switch
        {
            "advancedpaste" => GpoApi.GetConfiguredAdvancedPasteEnabledValue(),
            "alwaysontop" => GpoApi.GetConfiguredAlwaysOnTopEnabledValue(),
            "awake" => GpoApi.GetConfiguredAwakeEnabledValue(),
            "cmdpal" => GpoApi.GetConfiguredCmdPalEnabledValue(),
            "colorpicker" => GpoApi.GetConfiguredColorPickerEnabledValue(),
            "cropandlock" => GpoApi.GetConfiguredCropAndLockEnabledValue(),
            "cursorwrap" => GpoApi.GetConfiguredCursorWrapEnabledValue(),
            "environmentvariables" => GpoApi.GetConfiguredEnvironmentVariablesEnabledValue(),
            "fancyzones" => GpoApi.GetConfiguredFancyZonesEnabledValue(),
            "filelocksmith" => GpoApi.GetConfiguredFileLocksmithEnabledValue(),
            "findmymouse" => GpoApi.GetConfiguredFindMyMouseEnabledValue(),
            "altwindowcycle" => GpoApi.GetConfiguredAltWindowCycleEnabledValue(),
            "hosts" => GpoApi.GetConfiguredHostsFileEditorEnabledValue(),
            "imageresizer" => GpoApi.GetConfiguredImageResizerEnabledValue(),
            "keyboardmanager" => GpoApi.GetConfiguredKeyboardManagerEnabledValue(),
            "lightswitch" => GpoApi.GetConfiguredLightSwitchEnabledValue(),
            "mousehighlighter" => GpoApi.GetConfiguredMouseHighlighterEnabledValue(),
            "mousejump" => GpoApi.GetConfiguredMouseJumpEnabledValue(),
            "mousepointercrosshairs" => GpoApi.GetConfiguredMousePointerCrosshairsEnabledValue(),
            "mousewithoutborders" => GpoApi.GetConfiguredMouseWithoutBordersEnabledValue(),
            "newplus" => GpoApi.GetConfiguredNewPlusEnabledValue(),
            "peek" => GpoApi.GetConfiguredPeekEnabledValue(),
            "powerrename" => GpoApi.GetConfiguredPowerRenameEnabledValue(),
            "powerlauncher" => GpoApi.GetConfiguredPowerLauncherEnabledValue(),
            "poweraccent" => GpoApi.GetConfiguredQuickAccentEnabledValue(),
            "workspaces" => GpoApi.GetConfiguredWorkspacesEnabledValue(),
            "registrypreview" => GpoApi.GetConfiguredRegistryPreviewEnabledValue(),
            "measuretool" => GpoApi.GetConfiguredScreenRulerEnabledValue(),
            "shortcutguide" => GpoApi.GetConfiguredShortcutGuideEnabledValue(),
            "powerocr" => GpoApi.GetConfiguredTextExtractorEnabledValue(),
            "powerdisplay" => GpoApi.GetConfiguredPowerDisplayEnabledValue(),
            "zoomit" => GpoApi.GetConfiguredZoomItEnabledValue(),
            "grabandmove" => GpoApi.GetConfiguredGrabAndMoveEnabledValue(),
            _ => GpoRuleConfigured.Unavailable,
        };
    }

    public static void SetSettingValue(string qualifiedName, string newValueStr, SettingsUtils? settingsUtils = null)
    {
        settingsUtils ??= SettingsUtils.Default;
        const string enabledPrefix = "Enabled.";
        const string generalEnabledPrefix = "GeneralSettings.Enabled.";

        if (qualifiedName.StartsWith(enabledPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var moduleName = qualifiedName.Substring(enabledPrefix.Length);
            CheckModuleGpoLock(moduleName);
            qualifiedName = "GeneralSettings." + qualifiedName;
        }
        else if (qualifiedName.StartsWith(generalEnabledPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var moduleName = qualifiedName.Substring(generalEnabledPrefix.Length);
            CheckModuleGpoLock(moduleName);
        }

        SetSettingCommandLineCommand.Execute(qualifiedName, newValueStr, settingsUtils);
    }

    public static bool ToggleModule(string moduleName, bool? targetState, SettingsUtils? settingsUtils = null)
    {
        settingsUtils ??= SettingsUtils.Default;
        var modules = GetModulesAndStatus(settingsUtils);

        // Find exact or case-insensitive match
        var matchedKey = modules.Keys.FirstOrDefault(k => string.Equals(k, moduleName, StringComparison.OrdinalIgnoreCase));
        if (matchedKey == null)
        {
            throw new ArgumentException($"Module '{moduleName}' was not found.");
        }

        CheckModuleGpoLock(matchedKey);

        var currentState = modules[matchedKey];
        var newState = targetState ?? !currentState;

        SetSettingCommandLineCommand.Execute($"GeneralSettings.Enabled.{matchedKey}", newState.ToString().ToLowerInvariant(), settingsUtils);
        return newState;
    }

    private static void CheckModuleGpoLock(string moduleName)
    {
        var gpoRule = GetModuleGpoRule(moduleName);
        if (gpoRule == GpoRuleConfigured.Disabled)
        {
            throw new InvalidOperationException($"Module '{moduleName}' is disabled by Group Policy and cannot be modified.");
        }

        if (gpoRule == GpoRuleConfigured.Enabled)
        {
            throw new InvalidOperationException($"Module '{moduleName}' is force-enabled by Group Policy and cannot be modified.");
        }
    }

    public static string SerializeToJson<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    private static object? UnwrapPropertyValue(object? val)
    {
        if (val == null)
        {
            return null;
        }

        if (val is BoolProperty bp)
        {
            return bp.Value;
        }

        if (val is IntProperty ip)
        {
            return ip.Value;
        }

        if (val is StringProperty sp)
        {
            return sp.Value;
        }

        if (val is ICmdLineRepresentable representable && representable.TryToCmdRepresentable(out var result))
        {
            return result;
        }

        return val;
    }
}
