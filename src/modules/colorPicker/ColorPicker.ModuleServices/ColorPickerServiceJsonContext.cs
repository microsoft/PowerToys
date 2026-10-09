// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library;

namespace ColorPicker.ModuleServices;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<SavedColor>))]
[JsonSerializable(typeof(SavedColor))]
[JsonSerializable(typeof(ColorFormatValue))]
[JsonSerializable(typeof(ColorPickerSettings))]
internal sealed partial class ColorPickerServiceJsonContext : JsonSerializerContext
{
}
