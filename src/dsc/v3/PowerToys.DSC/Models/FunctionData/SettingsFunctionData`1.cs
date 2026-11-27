// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using PowerToys.DSC.Models.KeyboardManager;
using PowerToys.DSC.Models.ResourceObjects;
using PowerToys.DSC.Properties;

namespace PowerToys.DSC.Models.FunctionData;

/// <summary>
/// Represents function data for the settings DSC resource.
/// </summary>
/// <typeparam name="TSettingsConfig">The module settings configuration type.</typeparam>
public sealed class SettingsFunctionData<TSettingsConfig> : BaseFunctionData, ISettingsFunctionData
    where TSettingsConfig : ISettingsConfig, new()
{
    private static readonly CompositeFormat KeyboardManagerProfileFieldNotApplied = CompositeFormat.Parse(Resources.KeyboardManagerProfileFieldNotApplied);

    private static readonly SettingsUtils _settingsUtils = SettingsUtils.Default;
    private static readonly TSettingsConfig _settingsConfig = new();

    private readonly SettingsResourceObject<TSettingsConfig> _input;
    private readonly SettingsResourceObject<TSettingsConfig> _output;

    // Whether a desired state was given (Set, Test) or not (Get, Export).
    private readonly bool _hasInput;

    private readonly string? _inputJson;
    private readonly List<string> _warnings = [];

    /// <inheritdoc/>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <inheritdoc/>
    public ISettingsResourceObject Input => _input;

    /// <inheritdoc/>
    public ISettingsResourceObject Output => _output;

    public SettingsFunctionData(string? input = null)
    {
        _output = new();
        _input = string.IsNullOrEmpty(input) ? new() : JsonSerializer.Deserialize<SettingsResourceObject<TSettingsConfig>>(input) ?? new();
        _hasInput = !string.IsNullOrEmpty(input);
        _inputJson = input;
    }

    /// <inheritdoc/>
    public void GetState()
    {
        _output.Settings = GetSettings();

        // Keyboard Manager keeps which profile is active, and the list of
        // profiles, in its settings; the engine and the editor own them. The
        // desired state takes both from the current state before Test and Set
        // use it, so applying e.g. only "useNewEditor" does not reset them.
        if (_hasInput &&
            (object?)_input.Settings is KeyboardManagerSettings desired &&
            (object?)_output.Settings is KeyboardManagerSettings current)
        {
            // An input that names another profile or list is not applied in
            // that part; say so instead of reporting it as the desired state.
            _warnings.Clear();
            foreach (var field in KeyboardManagerSettingsInput.GetProfileFieldsNotApplied(_inputJson, current))
            {
                _warnings.Add(string.Format(CultureInfo.InvariantCulture, KeyboardManagerProfileFieldNotApplied, field));
            }

            KeyboardManagerSettingsInput.UseCurrentProfileFields(desired, current);
        }
    }

    /// <inheritdoc/>
    public void SetState()
    {
        Debug.Assert(_output.Settings != null, "Output settings should not be null");
        SaveSettings(_output.Settings);
    }

    /// <inheritdoc/>
    public bool TestState()
    {
        var input = JsonSerializer.SerializeToNode(_input.Settings);
        var output = JsonSerializer.SerializeToNode(_output.Settings);
        return JsonNode.DeepEquals(input, output);
    }

    /// <inheritdoc/>
    public JsonArray GetDiffJson()
    {
        var diff = new JsonArray();
        if (!TestState())
        {
            diff.Add(SettingsResourceObject<TSettingsConfig>.SettingsJsonPropertyName);
        }

        return diff;
    }

    /// <inheritdoc/>
    public string Schema()
    {
        return GenerateSchema<SettingsResourceObject<TSettingsConfig>>();
    }

    /// <summary>
    /// Gets the settings configuration from the settings utils for a specific module.
    /// </summary>
    /// <returns>The settings configuration for the module.</returns>
    private static TSettingsConfig GetSettings()
    {
        // GetSettingsOrDefault writes a default settings file over one it
        // cannot parse. For Keyboard Manager that would reset the active
        // profile and the profile list when the read meets a file that is
        // being written, so read it without that side effect (it retries, and
        // fails the operation if the file stays unreadable).
        if (typeof(TSettingsConfig) == typeof(KeyboardManagerSettings))
        {
            return _settingsUtils.GetSettingsOrDefaultReadOnly<TSettingsConfig>(_settingsConfig.GetModuleName());
        }

        return _settingsUtils.GetSettingsOrDefault<TSettingsConfig>(_settingsConfig.GetModuleName());
    }

    /// <summary>
    /// Saves the settings configuration to the settings utils for a specific module.
    /// </summary>
    /// <param name="settings">Settings of a specific module</param>
    private static void SaveSettings(TSettingsConfig settings)
    {
        var inputJson = JsonSerializer.Serialize(settings);
        _settingsUtils.SaveSettings(inputJson, _settingsConfig.GetModuleName());
    }
}
