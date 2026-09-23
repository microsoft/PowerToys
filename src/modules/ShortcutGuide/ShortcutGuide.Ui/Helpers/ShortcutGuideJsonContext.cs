// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using ShortcutGuide.Models;

namespace ShortcutGuide.Helpers
{
    // Enable source generation for the pinned-shortcuts type
    [JsonSourceGenerationOptions(WriteIndented = false)]
    [JsonSerializable(typeof(Dictionary<string, List<ShortcutEntry>>))]
    [JsonSerializable(typeof(ShortcutEntry))]
    [JsonSerializable(typeof(ShortcutDescription))]

    public partial class ShortcutGuideJsonContext : JsonSerializerContext
    {
    }
}
