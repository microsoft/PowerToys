// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace PowerToys.DSC.Models;

internal sealed class SettingsResourceObject<T> : BaseResourceObject
    where T : ISettingsConfig
{
    [JsonPropertyName("settings")]
    public T? Settings { get; set; }
}
