// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    private static readonly string _namespace = "calculator";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private static readonly List<ChoiceSetSetting.Choice> _trigUnitChoices = new()
    {
        new ChoiceSetSetting.Choice(Properties.Resources.calculator_settings_trig_unit_radians, "0"),
        new ChoiceSetSetting.Choice(Properties.Resources.calculator_settings_trig_unit_degrees, "1"),
        new ChoiceSetSetting.Choice(Properties.Resources.calculator_settings_trig_unit_gradians, "2"),
    };

    private readonly ChoiceSetSetting _trigUnit = new(
        Namespaced(nameof(TrigUnit)),
        Properties.Resources.calculator_settings_trig_unit_mode,
        Properties.Resources.calculator_settings_trig_unit_mode_description,
        _trigUnitChoices);

    private readonly ToggleSetting _inputUseEnNumberFormat = new(
        Namespaced(nameof(InputUseEnglishFormat)),
        Properties.Resources.calculator_settings_in_en_format,
        Properties.Resources.calculator_settings_in_en_format_description,
        false);

    private readonly ToggleSetting _outputUseEnNumberFormat = new(
        Namespaced(nameof(OutputUseEnglishFormat)),
        Properties.Resources.calculator_settings_out_en_format,
        Properties.Resources.calculator_settings_out_en_format_description,
        false);

    private readonly ToggleSetting _closeOnEnter = new(
        Namespaced(nameof(CloseOnEnter)),
        Properties.Resources.calculator_settings_close_on_enter,
        Properties.Resources.calculator_settings_close_on_enter_description,
        true);

    private readonly ToggleSetting _copyResultToSearchBarIfQueryEndsWithEqualSign = new(
        Namespaced(nameof(CopyResultToSearchBarIfQueryEndsWithEqualSign)),
        Properties.Resources.calculator_settings_copy_result_to_search_bar,
        Properties.Resources.calculator_settings_copy_result_to_search_bar_description,
        false);

    private readonly ToggleSetting _autoFixQuery = new(
        Namespaced(nameof(AutoFixQuery)),
        Properties.Resources.calculator_settings_auto_fix_query,
        Properties.Resources.calculator_settings_auto_fix_query_description,
        true);

    public CalculateEngine.TrigMode TrigUnit
    {
        get
        {
            if (_trigUnit.Value == null || string.IsNullOrEmpty(_trigUnit.Value))
            {
                return CalculateEngine.TrigMode.Radians;
            }

            var success = int.TryParse(_trigUnit.Value, out var result);

            if (!success)
            {
                return CalculateEngine.TrigMode.Radians;
            }

            switch (result)
            {
                case 0:
                    return CalculateEngine.TrigMode.Radians;
                case 1:
                    return CalculateEngine.TrigMode.Degrees;
                case 2:
                    return CalculateEngine.TrigMode.Gradians;
                default:
                    return CalculateEngine.TrigMode.Radians;
            }
        }
    }

    public bool InputUseEnglishFormat => _inputUseEnNumberFormat.Value;

    public bool OutputUseEnglishFormat => _outputUseEnNumberFormat.Value;

    public bool CloseOnEnter => _closeOnEnter.Value;

    public bool CopyResultToSearchBarIfQueryEndsWithEqualSign => _copyResultToSearchBarIfQueryEndsWithEqualSign.Value;

    public bool AutoFixQuery => _autoFixQuery.Value;

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"{_namespace}.settings.json");
    }

    private static string LegacySettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        return Path.Combine(directory, "settings.json");
    }

    /// <summary>
    /// Migrates settings from a shared legacy file to this extension's own settings file.
    /// Call after registering all settings with <see cref="Settings"/> and before <see cref="LoadSettings"/>.
    /// Skips if <see cref="FilePath"/> already exists or <paramref name="legacyFilePath"/> is missing.
    /// </summary>
    private void MigrateFromLegacyFile(string legacyFilePath)
    {
        if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(legacyFilePath))
        {
            return;
        }

        // Already migrated — per-extension file exists.
        if (File.Exists(FilePath))
        {
            return;
        }

        if (!File.Exists(legacyFilePath))
        {
            return;
        }

        try
        {
            var legacyContent = File.ReadAllText(legacyFilePath);
            if (JsonNode.Parse(legacyContent) is not JsonObject)
            {
                return;
            }

            // Extract only the keys this extension owns.
            Settings.Update(legacyContent);
            var settingsJson = Settings.ToJson();

            if (JsonNode.Parse(settingsJson) is JsonObject extracted && extracted.Count > 0)
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(FilePath, extracted.ToJsonString(_serializerOptions));
            }
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Settings migration failed from '{legacyFilePath}' to '{FilePath}': {ex}" });
        }
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_trigUnit);
        Settings.Add(_inputUseEnNumberFormat);
        Settings.Add(_outputUseEnNumberFormat);
        Settings.Add(_closeOnEnter);
        Settings.Add(_copyResultToSearchBarIfQueryEndsWithEqualSign);
        Settings.Add(_autoFixQuery);

        MigrateFromLegacyFile(LegacySettingsJsonPath());
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
