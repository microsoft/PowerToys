// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using NJsonSchema.Annotations;

namespace PowerToys.DSC.Models;

internal sealed class SettingsResourceObject<TSettingsConfig> : BaseResourceObject, ISettingsResourceObject
    where TSettingsConfig : ISettingsConfig, new()
{
    public const string SettingsJsonPropertyName = "settings";

    [JsonPropertyName(SettingsJsonPropertyName)]
    [Required]
    [Description("The settings content for the module.")]
    [JsonSchemaType(typeof(object))]
    public TSettingsConfig Settings { get; set; } = new();

    public ISettingsConfig GetSettings()
    {
        return Settings;
    }

    public void SetSettings(ISettingsConfig settings)
    {
        Settings = (TSettingsConfig)settings;
    }

    public void SetInDesiredState(bool inDesiredState)
    {
        InDesiredState = inDesiredState;
    }
}
