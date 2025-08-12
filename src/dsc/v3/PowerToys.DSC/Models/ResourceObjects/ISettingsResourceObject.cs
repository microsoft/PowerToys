// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace PowerToys.DSC.Models.ResourceObjects;

/// <summary>
/// Interface for settings resource objects.
/// </summary>
public interface ISettingsResourceObject
{
    /// <summary>
    /// Gets or sets the settings configuration.
    /// </summary>
    public ISettingsConfig SettingsInternal { get; set; }

    /// <summary>
    /// Gets or sets whether an instance is in the desired state.
    /// </summary>
    public bool? InDesiredState { get; set; }

    /// <summary>
    /// Generates a JSON representation of the resource object.
    /// </summary>
    /// <returns>String representation of the resource object in JSON format.</returns>
    public JsonNode ToJson();
}
