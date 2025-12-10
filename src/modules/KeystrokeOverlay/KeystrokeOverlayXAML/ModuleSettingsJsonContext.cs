// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using KeystrokeOverlayUI.Models;

namespace KeystrokeOverlayUI;

/// Trimming-safe JSON metadata for loading module settings.
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(ModuleSettingsRoot))]
internal sealed partial class ModuleSettingsJsonContext : JsonSerializerContext
{
}
