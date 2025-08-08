// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace PowerToys.DSC.Models;

internal sealed class SettingsFunctionData<TSettingsConfig> : BaseFunctionData, ISettingsFunctionData
    where TSettingsConfig : ISettingsConfig, new()
{
    private static readonly SettingsUtils _settingsUtils = new();
    private static readonly TSettingsConfig _settingsConfig = new();

    public SettingsResourceObject<TSettingsConfig> Input { get; }

    public SettingsResourceObject<TSettingsConfig> Output { get; }

    public SettingsFunctionData()
        : this(null)
    {
    }

    public SettingsFunctionData(string? input)
    {
        Output = new();
        Input = string.IsNullOrEmpty(input) ? new() : JsonSerializer.Deserialize<SettingsResourceObject<TSettingsConfig>>(input) ?? new();
    }

    public void Get()
    {
        Output.Settings = GetSettings();
    }

    public void Set()
    {
        Debug.Assert(Output.Settings != null, "Output settings should not be null");
        SaveSettings(Output.Settings);
    }

    public bool Test()
    {
        var input = JsonSerializer.SerializeToNode(Input.Settings);
        var output = JsonSerializer.SerializeToNode(Output.Settings);
        return JsonNode.DeepEquals(input, output);
    }

    public JsonArray DiffJson()
    {
        var diff = new JsonArray();
        if (!Test())
        {
            diff.Add(SettingsResourceObject<TSettingsConfig>.SettingsJsonPropertyName);
        }

        return diff;
    }

    public string Schema()
    {
        return GenerateSchema<SettingsResourceObject<TSettingsConfig>>();
    }

    private static TSettingsConfig GetSettings()
    {
        return _settingsUtils.GetSettingsOrDefault<TSettingsConfig>(_settingsConfig.GetModuleName());
    }

    private static void SaveSettings(TSettingsConfig settings)
    {
        var inputJson = JsonSerializer.Serialize(settings);
        _settingsUtils.SaveSettings(inputJson, _settingsConfig.GetModuleName());
    }

    public ISettingsResourceObject GetInput()
    {
        return Input;
    }

    public ISettingsResourceObject GetOutput()
    {
        return Output;
    }
}
