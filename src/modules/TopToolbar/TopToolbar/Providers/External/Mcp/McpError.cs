// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;

namespace TopToolbar.Providers.External.Mcp;

internal sealed class McpError
{
    private McpError(int code, string message, JsonElement data)
    {
        Code = code;
        Message = message;
        Data = data;
    }

    public int Code { get; }

    public string Message { get; }

    public JsonElement Data { get; }

    public static McpError FromJson(JsonElement element)
    {
        var code = element.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var parsedCode)
            ? parsedCode
            : 0;

        string message = "MCP error.";
        if (element.TryGetProperty("message", out var messageElement))
        {
            message = messageElement.GetString() ?? message;
        }

        JsonElement data = default;
        if (element.TryGetProperty("data", out var dataElement))
        {
            data = dataElement.Clone();
        }

        return new McpError(code, message, data);
    }
}
