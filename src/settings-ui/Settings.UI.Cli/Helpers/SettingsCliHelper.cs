// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
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

    public static ModuleStatus GetModuleStatus(string moduleName, SettingsUtils? settingsUtils = null)
    {
        var moduleEntry = GetModuleEntry(moduleName, settingsUtils);
        var gpoRule = GetModuleGpoRule(moduleEntry.ModuleName);

        return new ModuleStatus(
            moduleEntry.ModuleName,
            moduleEntry.Enabled,
            gpoRule is GpoRuleConfigured.Enabled or GpoRuleConfigured.Disabled ? gpoRule.ToString() : null);
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

    public static ModuleStatus SetModuleEnabled(string moduleName, bool enabled, SettingsUtils? settingsUtils = null)
    {
        settingsUtils ??= SettingsUtils.Default;
        var moduleEntry = GetModuleEntry(moduleName, settingsUtils);

        CheckModuleGpoLock(moduleEntry.ModuleName);

        SetSettingCommandLineCommand.Execute($"GeneralSettings.Enabled.{moduleEntry.ModuleName}", enabled.ToString().ToLowerInvariant(), settingsUtils);
        return GetModuleStatus(moduleEntry.ModuleName, settingsUtils);
    }

    public static string SerializeToJson<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    private static ModuleEntry GetModuleEntry(string moduleName, SettingsUtils? settingsUtils)
    {
        var modules = GetModulesAndStatus(settingsUtils);
        var matchedKey = modules.Keys.FirstOrDefault(k => string.Equals(k, moduleName, StringComparison.OrdinalIgnoreCase));
        if (matchedKey == null)
        {
            throw new ArgumentException($"Module '{moduleName}' was not found.");
        }

        return new ModuleEntry(matchedKey, modules[matchedKey]);
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

    private sealed record ModuleEntry(string ModuleName, bool Enabled);

    public sealed record ModuleStatus(string ModuleName, bool Enabled, string? GroupPolicy);
}
