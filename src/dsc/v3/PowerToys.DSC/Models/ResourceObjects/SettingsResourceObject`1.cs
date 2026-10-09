// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using NJsonSchema.Annotations;

namespace PowerToys.DSC.Models.ResourceObjects;

/// <summary>
/// Represents a settings resource object for a module's settings configuration.
/// </summary>
/// <typeparam name="TSettingsConfig">The type of the settings configuration.</typeparam>
public sealed class SettingsResourceObject<TSettingsConfig> : BaseResourceObject, ISettingsResourceObject
    where TSettingsConfig : ISettingsConfig, new()
{
    public const string SettingsJsonPropertyName = "settings";

    /// <summary>
    /// Gets or sets the settings content for the module.
    /// </summary>
    [JsonPropertyName(SettingsJsonPropertyName)]
    [Required]
    [Description("The settings content for the module.")]
    [JsonSchemaType(typeof(object))]
    public TSettingsConfig Settings { get; set; } = new();

    /// <inheritdoc/>
    [JsonIgnore]
    public ISettingsConfig SettingsInternal { get => Settings; set => Settings = (TSettingsConfig)value; }
}
