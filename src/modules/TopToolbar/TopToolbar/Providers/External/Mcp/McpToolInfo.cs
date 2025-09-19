// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;

namespace TopToolbar.Providers.External.Mcp;

internal sealed class McpToolInfo
{
    public McpToolInfo(string name, string description, JsonElement inputSchema)
    {
        Name = name ?? string.Empty;
        Description = description ?? string.Empty;
        InputSchema = inputSchema;
    }

    public string Name { get; }

    public string Description { get; }

    public JsonElement InputSchema { get; }
}
