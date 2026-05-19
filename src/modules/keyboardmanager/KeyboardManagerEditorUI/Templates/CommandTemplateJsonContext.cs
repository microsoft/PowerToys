// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyboardManagerEditorUI.Templates
{
    [JsonSerializable(typeof(PowerToysCliCatalog))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = false,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    internal sealed partial class CommandTemplateJsonContext : JsonSerializerContext
    {
    }
}
