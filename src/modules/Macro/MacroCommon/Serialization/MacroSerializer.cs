// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroCommon.Serialization;

public static class MacroSerializer
{
    public static MacroDefinition Deserialize(string json) =>
        JsonSerializer.Deserialize(json, MacroJsonContext.Default.MacroDefinition)
        ?? throw new InvalidOperationException("Deserialized macro was null.");

    public static string Serialize(MacroDefinition macro) =>
        JsonSerializer.Serialize(macro, MacroJsonContext.Default.MacroDefinition);

    public static async Task<MacroDefinition> DeserializeFileAsync(string path, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(path, ct);
        return Deserialize(json);
    }

    public static async Task SerializeFileAsync(MacroDefinition macro, string path, CancellationToken ct = default)
    {
        var json = Serialize(macro);
        await File.WriteAllTextAsync(path, json, ct);
    }
}
