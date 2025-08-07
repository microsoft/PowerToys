// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library;

namespace PowerToys.DSC.Models;

internal class BaseResourceObject
{
    [JsonPropertyName("_inDesiredState")]
    public bool? InDesiredState { get; set; }

    [JsonPropertyName("settings")]
    public AwakeSettings? AwakeSettings { get; set; }
}
