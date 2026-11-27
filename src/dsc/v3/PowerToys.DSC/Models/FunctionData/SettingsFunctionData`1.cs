// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using PowerToys.DSC.Models.KeyboardManager;
using PowerToys.DSC.Models.ResourceObjects;

namespace PowerToys.DSC.Models.FunctionData;

/// <summary>
/// Represents function data for the settings DSC resource.
/// </summary>
/// <typeparam name="TSettingsConfig">The module settings configuration type.</typeparam>
public sealed class SettingsFunctionData<TSettingsConfig> : BaseFunctionData, ISettingsFunctionData
    where TSettingsConfig : ISettingsConfig, new()
{
    private static readonly SettingsUtils _settingsUtils = SettingsUtils.Default;
    private static readonly TSettingsConfig _settingsConfig = new();

    private readonly SettingsResourceObject<TSettingsConfig> _input;
    private readonly SettingsResourceObject<TSettingsConfig> _output;

    // The input as given, to tell a member that was omitted from one that
    // was set to its default value (the typed model cannot).
    private readonly JsonNode? _inputNode;

    /// <inheritdoc/>
    public ISettingsResourceObject Input => _input;

    /// <inheritdoc/>
    public ISettingsResourceObject Output => _output;

    public SettingsFunctionData(string? input = null)
    {
        _output = new();
        _input = string.IsNullOrEmpty(input) ? new() : JsonSerializer.Deserialize<SettingsResourceObject<TSettingsConfig>>(input) ?? new();
        _inputNode = string.IsNullOrEmpty(input) ? null : JsonNode.Parse(input);
    }

    /// <inheritdoc/>
    public void GetState()
    {
        _output.Settings = GetSettings();

        // Keyboard Manager keeps which profile is active, and the list of
        // profiles, in its settings. An input that leaves them out (e.g. only
        // "useNewEditor") must not reset them to "default": complete the
        // desired state from the current one, before Test and Set use it.
        if (_inputNode != null &&
            (object?)_input.Settings is KeyboardManagerSettings desired &&
            (object?)_output.Settings is KeyboardManagerSettings current)
        {
            KeyboardManagerSettingsInput.KeepProfileFieldsWhenOmitted(_inputNode, desired, current);
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
