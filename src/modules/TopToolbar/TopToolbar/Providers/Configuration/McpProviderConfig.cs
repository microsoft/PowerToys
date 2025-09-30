// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable SA1402 // File may only contain a single type

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TopToolbar.Providers.Configuration;

/// <summary>
/// Modern MCP (Model Context Protocol) provider configuration schema v2.0
/// This replaces the legacy mixed static/dynamic action approach with pure MCP discovery
/// </summary>
public sealed class McpProviderConfig
{
    public string Version { get; set; } = "2.0";

    public string Type { get; set; } = "mcp";

    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public McpGroupLayout GroupLayout { get; set; } = new();

    public McpConnection Connection { get; set; } = new();

    public McpDiscovery Discovery { get; set; } = new();

    public McpFallback Fallback { get; set; } = new();
}

/// <summary>
/// Layout configuration for the MCP provider group
/// </summary>
public sealed class McpGroupLayout
{
    public string Style { get; set; } = "capsule";

    public string Overflow { get; set; } = "menu";

    public int MaxInline { get; set; } = 8;

    public bool ShowLabels { get; set; } = true;
}

/// <summary>
/// MCP server connection configuration
/// </summary>
public sealed class McpConnection
{
    public string Type { get; set; } = "stdio"; // "stdio" or "http"

    public McpExecutable Executable { get; set; } = new();

    public McpHttpEndpoint Http { get; set; }

    public McpTimeouts Timeouts { get; set; } = new();
}

/// <summary>
/// Stdio-based MCP server executable configuration
/// </summary>
public sealed class McpExecutable
{
    public string Path { get; set; } = string.Empty;

    public List<string> Args { get; set; } = new();

    public string WorkingDirectory { get; set; } = string.Empty;

    public Dictionary<string, string> Environment { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// HTTP-based MCP server endpoint configuration (for future use)
/// </summary>
public sealed class McpHttpEndpoint
{
    public string BaseUrl { get; set; } = string.Empty;

    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public McpAuthentication Authentication { get; set; }
}

/// <summary>
/// HTTP authentication configuration (for future use)
/// </summary>
public sealed class McpAuthentication
{
    public string Type { get; set; } = "none"; // "none", "bearer", "basic", "apikey"

    public string Token { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiKeyHeader { get; set; } = "X-API-Key";
}

/// <summary>
/// Timeout configuration for MCP operations
/// </summary>
public sealed class McpTimeouts
{
    public int StartupSeconds { get; set; } = 10;

    public int RequestSeconds { get; set; } = 30;

    public int ShutdownSeconds { get; set; } = 5;
}

/// <summary>
/// MCP tool discovery configuration
/// </summary>
public sealed class McpDiscovery
{
    public string Mode { get; set; } = "dynamic"; // "dynamic", "static", "hybrid"

    public int RefreshIntervalMinutes { get; set; } = 5;

    public int CacheToolsForSeconds { get; set; } = 15;
}

/// <summary>
/// Fallback actions when MCP server is unavailable
/// </summary>
public sealed class McpFallback
{
    public bool Enabled { get; set; } = true;

    public List<McpFallbackAction> Actions { get; set; } = new();
}

/// <summary>
/// Individual fallback action definition
/// </summary>
public sealed class McpFallbackAction
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string IconGlyph { get; set; } = "\\uECAA";

    public int SortOrder { get; set; }

    public Dictionary<string, McpParameter> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Parameter definition for MCP actions
/// </summary>
public sealed class McpParameter
{
    public string Type { get; set; } = "string"; // "string", "number", "boolean", "object", "array"

    public bool Required { get; set; }

    public string Description { get; set; } = string.Empty;

    public object DefaultValue { get; set; }
}
