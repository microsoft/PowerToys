// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Tests;

internal static class TestTool
{
    internal static JsonObject Descriptor(string input = "none", string output = "text", string name = "demo") => new()
    {
        ["name"] = name,
        ["inputSchema"] = Schema(input),
        ["outputSchema"] = Schema(output),
    };

    internal static PowerScriptManifest Parse(JsonObject descriptor, string entry = "script.ps1") =>
        ToolDescriptorParser.Parse(descriptor.Deserialize<ToolDescriptor>(), entry)!;

    private static JsonObject Schema(string shape)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        if (shape != "none")
        {
            properties[shape] = shape == "text" ? new JsonObject { ["type"] = "string" }
                : new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } };
            required.Add(shape);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
    }
}
