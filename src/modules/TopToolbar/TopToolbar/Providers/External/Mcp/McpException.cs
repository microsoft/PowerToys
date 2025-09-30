// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;

namespace TopToolbar.Providers.External.Mcp;

internal sealed class McpException : Exception
{
    public McpException(int code, string message, JsonElement details)
        : base(string.IsNullOrWhiteSpace(message) ? $"MCP error ({code})." : message)
    {
        ErrorCode = code;
        ErrorDetails = details;
    }

    public int ErrorCode { get; }

    public JsonElement ErrorDetails { get; }
}
