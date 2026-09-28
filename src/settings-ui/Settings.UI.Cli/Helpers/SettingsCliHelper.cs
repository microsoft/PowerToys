// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
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

    public static Dictionary<string, bool> GetModulesAndStatus(
        SettingsUtils? settingsUtils = null,
        Func<string, bool?>? gpoEnabledStateProvider = null)
    {
        settingsUtils ??= SettingsUtils.Default;
        gpoEnabledStateProvider ??= GetModuleGpoEnabledState;
        var generalSettings = settingsUtils.GetSettingsOrDefaultReadOnly<GeneralSettings>();
        var enabledModules = generalSettings.Enabled;

        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var properties = typeof(EnabledModules).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        foreach (var prop in properties)
        {
            if (prop.PropertyType == typeof(bool))
            {
                var val = (bool)(prop.GetValue(enabledModules) ?? false);
                var gpoEnabledState = gpoEnabledStateProvider(prop.Name);
                if (gpoEnabledState.HasValue)
                {
                    val = gpoEnabledState.Value;
                }

                result[prop.Name] = val;
            }
        }

        return result;
    }

    public static ModuleStatus GetModuleStatus(
        string moduleName,
        SettingsUtils? settingsUtils = null,
        Func<string, bool?>? gpoEnabledStateProvider = null)
    {
        gpoEnabledStateProvider ??= GetModuleGpoEnabledState;
        var moduleEntry = GetModuleEntry(moduleName, settingsUtils, gpoEnabledStateProvider);
        var gpoEnabledState = gpoEnabledStateProvider(moduleEntry.ModuleName);
        var groupPolicy = gpoEnabledState switch
        {
            true => "Enabled",
            false => "Disabled",
            _ => null,
        };

        return new ModuleStatus(
            moduleEntry.ModuleName,
            moduleEntry.Enabled,
            groupPolicy);
    }

    public static GpoRuleConfigured GetModuleGpoRule(string moduleName)
    {
        return moduleName.ToLowerInvariant() switch
        {
            "advancedpaste" => GpoApi.GetConfiguredAdvancedPasteEnabledValue(),
            "alwaysontop" => GpoApi.GetConfiguredAlwaysOnTopEnabledValue(),
            "autohidecursor" => GpoApi.GetConfiguredAutoHideCursorEnabledValue(),
            "awake" => GpoApi.GetConfiguredAwakeEnabledValue(),
            "cmdpal" => GpoApi.GetConfiguredCmdPalEnabledValue(),
            "cmdnotfound" => GpoApi.GetConfiguredCmdNotFoundEnabledValue(),
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

    public static ModuleStatus SetModuleEnabled(
        string moduleName,
        bool enabled,
        SettingsUtils? settingsUtils = null,
        Func<string, bool?>? gpoEnabledStateProvider = null,
        Func<IDisposable>? settingsLockProvider = null)
    {
        settingsUtils ??= SettingsUtils.Default;
        gpoEnabledStateProvider ??= GetModuleGpoEnabledState;
        settingsLockProvider ??= () => AcquireSettingsFileLock(settingsUtils);
        using var settingsLock = settingsLockProvider();

        var moduleEntry = GetModuleEntry(moduleName, settingsUtils, gpoEnabledStateProvider);
        CheckModuleGpoLock(moduleEntry.ModuleName, gpoEnabledStateProvider);

        SetSettingCommandLineCommand.ExecuteAndThrowOnSaveFailure(
            $"GeneralSettings.Enabled.{moduleEntry.ModuleName}",
            enabled.ToString().ToLowerInvariant(),
            settingsUtils);
        return GetModuleStatus(moduleEntry.ModuleName, settingsUtils, gpoEnabledStateProvider);
    }

    public static string SerializeToJson<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    private static ModuleEntry GetModuleEntry(
        string moduleName,
        SettingsUtils? settingsUtils,
        Func<string, bool?> gpoEnabledStateProvider)
    {
        var modules = GetModulesAndStatus(settingsUtils, gpoEnabledStateProvider);
        var matchedKey = modules.Keys.FirstOrDefault(k => string.Equals(k, moduleName, StringComparison.OrdinalIgnoreCase));
        if (matchedKey == null)
        {
            throw new ArgumentException($"Module '{moduleName}' was not found.");
        }

        return new ModuleEntry(matchedKey, modules[matchedKey]);
    }

    private static void CheckModuleGpoLock(string moduleName, Func<string, bool?> gpoEnabledStateProvider)
    {
        var gpoEnabledState = gpoEnabledStateProvider(moduleName);
        if (gpoEnabledState == false)
        {
            throw new InvalidOperationException($"Module '{moduleName}' is disabled by Group Policy and cannot be modified.");
        }

        if (gpoEnabledState == true)
        {
            throw new InvalidOperationException($"Module '{moduleName}' is force-enabled by Group Policy and cannot be modified.");
        }
    }

    private static bool? GetModuleGpoEnabledState(string moduleName)
    {
        return GetModuleGpoRule(moduleName) switch
        {
            GpoRuleConfigured.Enabled => true,
            GpoRuleConfigured.Disabled => false,
            _ => null,
        };
    }

    private static IDisposable AcquireSettingsFileLock(SettingsUtils settingsUtils)
    {
        try
        {
            return new FileStream(
                settingsUtils.GetSettingsFilePath() + ".cli-lock",
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException("PowerToys is starting or running. Retry the command after PowerToys exits.", ex);
        }
    }

    private sealed record ModuleEntry(string ModuleName, bool Enabled);

    public sealed record ModuleStatus(string ModuleName, bool Enabled, string? GroupPolicy);
}
