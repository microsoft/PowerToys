// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using KeystrokeOverlayUI.Controls;
using KeystrokeOverlayUI.Models;

namespace KeystrokeOverlayUI
{
    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(KeystrokeEvent))]
    [JsonSerializable(typeof(KeystrokeBatchRoot))]
    [JsonSerializable(typeof(KeystrokeBatchEvent))]
    internal sealed partial class KeystrokeEventJsonContext : JsonSerializerContext
    {
    }
}
