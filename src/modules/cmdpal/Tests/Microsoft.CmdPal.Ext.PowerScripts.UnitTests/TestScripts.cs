// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts.UnitTests;

internal static class TestScripts
{
    internal static PowerScriptInfo Create(params PowerScriptParameter[] parameters) => new()
    {
        Id = "test.script",
        Name = "Test script",
        Description = "A script supplied by the test service.",
        Runtime = "PowerShell",
        Input = PowerScriptIO.None,
        Output = PowerScriptIO.Text,
        Parameters = parameters,
        Trusted = true,
    };

    internal static PowerScriptParameter Parameter(
        string name,
        string type = "string",
        bool required = false,
        string? defaultValue = null,
        IReadOnlyList<string>? options = null,
        int? min = null,
        int? max = null) => new(name, type, required, null, null, defaultValue, options ?? [], min, max);

    internal static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
