// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using global::PowerToys.GPOWrapper;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

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
        try
        {
            return moduleName.ToLowerInvariant() switch
            {
                "advancedpaste" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredAdvancedPasteEnabledValue(),
                "alwaysontop" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue(),
                "awake" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredAwakeEnabledValue(),
                "cmdpal" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredCmdPalEnabledValue(),
                "colorpicker" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredColorPickerEnabledValue(),
                "cropandlock" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredCropAndLockEnabledValue(),
                "cursorwrap" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredCursorWrapEnabledValue(),
                "environmentvariables" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredEnvironmentVariablesEnabledValue(),
                "fancyzones" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredFancyZonesEnabledValue(),
                "filelocksmith" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredFileLocksmithEnabledValue(),
                "findmymouse" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredFindMyMouseEnabledValue(),
                "altwindowcycle" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredAltWindowCycleEnabledValue(),
                "hosts" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredHostsFileEditorEnabledValue(),
                "imageresizer" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredImageResizerEnabledValue(),
                "keyboardmanager" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredKeyboardManagerEnabledValue(),
                "lightswitch" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredLightSwitchEnabledValue(),
                "mousehighlighter" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMouseHighlighterEnabledValue(),
                "mousejump" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMouseJumpEnabledValue(),
                "mousepointercrosshairs" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMousePointerCrosshairsEnabledValue(),
                "mousewithoutborders" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue(),
                "newplus" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredNewPlusEnabledValue(),
                "peek" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredPeekEnabledValue(),
                "powerrename" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredPowerRenameEnabledValue(),
                "powerlauncher" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredPowerLauncherEnabledValue(),
                "poweraccent" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredQuickAccentEnabledValue(),
                "workspaces" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredWorkspacesEnabledValue(),
                "registrypreview" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredRegistryPreviewEnabledValue(),
                "measuretool" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredScreenRulerEnabledValue(),
                "shortcutguide" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredShortcutGuideEnabledValue(),
                "powerocr" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredTextExtractorEnabledValue(),
                "powerdisplay" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredPowerDisplayEnabledValue(),
                "zoomit" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredZoomItEnabledValue(),
                "grabandmove" => (GpoRuleConfigured)global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredGrabAndMoveEnabledValue(),
                _ => GpoRuleConfigured.NotConfigured,
            };
        }
        catch
        {
            return GpoRuleConfigured.NotConfigured;
        }
    }

    public static void SetSettingValue(string qualifiedName, string newValueStr, SettingsUtils? settingsUtils = null)
    {
        settingsUtils ??= SettingsUtils.Default;
        if (qualifiedName.StartsWith("Enabled.", StringComparison.OrdinalIgnoreCase))
        {
            var moduleName = qualifiedName.Substring("Enabled.".Length);
            CheckModuleGpoLock(moduleName);
            qualifiedName = "GeneralSettings." + qualifiedName;
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

    public static void BackupSettings(string outputPath, SettingsUtils? settingsUtils = null)
    {
        settingsUtils ??= SettingsUtils.Default;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var ptPath = Path.Combine(localAppData, "Microsoft", "PowerToys");

        if (!Directory.Exists(ptPath))
        {
            throw new DirectoryNotFoundException($"PowerToys settings path not found: {ptPath}");
        }

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDir = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        if (fullOutputPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(fullOutputPath))
            {
                File.Delete(fullOutputPath);
            }

            ZipFile.CreateFromDirectory(ptPath, fullOutputPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }
        else
        {
            if (!Directory.Exists(fullOutputPath))
            {
                Directory.CreateDirectory(fullOutputPath);
            }

            CopyDirectory(ptPath, fullOutputPath);
        }
    }

    public static void RestoreSettings(string inputPath, SettingsUtils? settingsUtils = null)
    {
        settingsUtils ??= SettingsUtils.Default;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var ptPath = Path.Combine(localAppData, "Microsoft", "PowerToys");

        var fullInputPath = Path.GetFullPath(inputPath);

        if (File.Exists(fullInputPath) && fullInputPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var tempExtractDir = Path.Combine(Path.GetTempPath(), "PowerToys_Restore_" + Guid.NewGuid().ToString("N"));
            try
            {
                ZipFile.ExtractToDirectory(fullInputPath, tempExtractDir);
                SettingsBackupAndRestoreUtils.Instance.RestoreSettings(ptPath, tempExtractDir);
            }
            finally
            {
                if (Directory.Exists(tempExtractDir))
                {
                    Directory.Delete(tempExtractDir, recursive: true);
                }
            }
        }
        else if (Directory.Exists(fullInputPath))
        {
            SettingsBackupAndRestoreUtils.Instance.RestoreSettings(ptPath, fullInputPath);
        }
        else
        {
            throw new FileNotFoundException($"Backup input path not found: {inputPath}");
        }
    }

    public static void ResetModuleSettings(string moduleName, SettingsUtils? settingsUtils = null)
    {
        settingsUtils ??= SettingsUtils.Default;

        if (string.Equals(moduleName, "all", StringComparison.OrdinalIgnoreCase))
        {
            var modules = GetModulesAndStatus(settingsUtils);
            foreach (var mod in modules.Keys)
            {
                settingsUtils.DeleteSettings(mod);
            }

            settingsUtils.DeleteSettings("GeneralSettings");
        }
        else
        {
            settingsUtils.DeleteSettings(moduleName);
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

        return val;
    }

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(dir));
            CopyDirectory(dir, dest);
        }
    }
}
