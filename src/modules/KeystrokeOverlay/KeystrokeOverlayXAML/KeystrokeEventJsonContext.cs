// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using KeystrokeOverlayUI.Controls;

namespace KeystrokeOverlayUI;

/// <summary>
/// JSON source-generated metadata for KeystrokeEvent to support trimming-safe deserialization.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(KeystrokeEvent))]
internal sealed partial class KeystrokeEventJsonContext : JsonSerializerContext
{
}
