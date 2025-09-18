// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToys.WorkspacesMCP.Models;

// MCP Protocol Models
#pragma warning disable SA1649 // File name should match first type name
public record MCPRequest
#pragma warning restore SA1649 // File name should match first type name
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; init; }

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public object? Params { get; init; }
}

#pragma warning disable SA1402 // File may only contain a single type
public record MCPResponse
#pragma warning restore SA1402 // File may only contain a single type
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; init; }

    [JsonPropertyName("result")]
    public object? Result { get; init; }

    [JsonPropertyName("error")]
    public MCPError? Error { get; init; }
}

#pragma warning disable SA1402 // File may only contain a single type
public record MCPError
#pragma warning restore SA1402 // File may only contain a single type
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public object? Data { get; init; }
}

// Tool Models
#pragma warning disable SA1402 // File may only contain a single type
public record MCPTool
#pragma warning restore SA1402 // File may only contain a single type
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public ToolInputSchema InputSchema { get; init; } = new();
}

#pragma warning disable SA1402 // File may only contain a single type
public record ToolInputSchema
#pragma warning restore SA1402 // File may only contain a single type
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, ToolProperty> Properties { get; init; } = new();

    [JsonPropertyName("required")]
    public string[] Required { get; init; } = Array.Empty<string>();
}

#pragma warning disable SA1402 // File may only contain a single type
public record ToolProperty
#pragma warning restore SA1402 // File may only contain a single type
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("enum")]
    public string[]? Enum { get; init; }
}

// Resource Models
#pragma warning disable SA1402 // File may only contain a single type
public record MCPResource
#pragma warning restore SA1402 // File may only contain a single type
{
    [JsonPropertyName("uri")]
    public string Uri { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("mimeType")]
    public string MimeType { get; init; } = "application/json";
}

// Domain Models
#pragma warning disable SA1402 // File may only contain a single type
public record WindowInfo
#pragma warning restore SA1402 // File may only contain a single type
{
    [JsonPropertyName("hwnd")]
    public long Hwnd { get; init; }

    [JsonPropertyName("processId")]
    public uint ProcessId { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("className")]
    public string ClassName { get; init; } = string.Empty;

    [JsonPropertyName("bounds")]
    public WindowBounds Bounds { get; init; } = new();

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; init; }

    [JsonPropertyName("isMinimized")]
    public bool IsMinimized { get; init; }

    [JsonPropertyName("isMaximized")]
    public bool IsMaximized { get; init; }
}

#pragma warning disable SA1402 // File may only contain a single type
public record WindowBounds
#pragma warning restore SA1402 // File may only contain a single type
{
    [JsonPropertyName("x")]
    public int X { get; init; }

    [JsonPropertyName("y")]
    public int Y { get; init; }

    [JsonPropertyName("width")]
    public int Width { get; init; }

    [JsonPropertyName("height")]
    public int Height { get; init; }
}

#pragma warning disable SA1402 // File may only contain a single type
public record AppInfo
#pragma warning restore SA1402 // File may only contain a single type
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("processId")]
    public uint ProcessId { get; init; }

    [JsonPropertyName("executablePath")]
    public string ExecutablePath { get; init; } = string.Empty;

    [JsonPropertyName("packageFullName")]
    public string? PackageFullName { get; init; }

    [JsonPropertyName("isUwpApp")]
    public bool IsUwpApp { get; init; }

    [JsonPropertyName("isRunning")]
    public bool IsRunning { get; init; }

    [JsonPropertyName("windows")]
    public WindowInfo[] Windows { get; init; } = Array.Empty<WindowInfo>();
}

#pragma warning disable SA1402 // File may only contain a single type
public record WorkspaceInfo
#pragma warning restore SA1402 // File may only contain a single type
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("applications")]
    public AppInfo[] Applications { get; init; } = Array.Empty<AppInfo>();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; init; }
}
