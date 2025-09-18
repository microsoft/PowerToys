// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using PowerToys.WorkspacesMCP.Models;

namespace PowerToys.WorkspacesMCP.Services;

public class MCPProtocolService
{
    private readonly JsonSerializerOptions _jsonOptions;

    public MCPProtocolService()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
    }

    [RequiresUnreferencedCode("JSON serialization may require types that cannot be statically analyzed")]
    [RequiresDynamicCode("JSON serialization may require runtime code generation for AOT")]
    public async Task ProcessMessagesAsync(Stream input, Stream output, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(input);
        using var writer = new StreamWriter(output) { AutoFlush = true };

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var request = JsonSerializer.Deserialize<MCPRequest>(line, _jsonOptions);
                    if (request != null)
                    {
                        var response = await ProcessRequestAsync(request);
                        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);

                        await writer.WriteLineAsync(responseJson);
                    }
                }
                catch (JsonException)
                {
                    var errorResponse = CreateErrorResponse(null, -32700, "Parse error", null);
                    var errorJson = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                    await writer.WriteLineAsync(errorJson);
                }
                catch (Exception ex)
                {
                    var errorResponse = CreateErrorResponse(null, -32603, "Internal error", ex.Message);
                    var errorJson = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                    await writer.WriteLineAsync(errorJson);
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors in message processing loop
        }
    }

    private async Task<MCPResponse> ProcessRequestAsync(MCPRequest request)
    {
        try
        {
            return request.Method switch
            {
                "initialize" => await HandleInitializeAsync(request),
                "tools/list" => await HandleToolsListAsync(request),
                "tools/call" => await HandleToolsCallAsync(request),
                "resources/list" => await HandleResourcesListAsync(request),
                "resources/read" => await HandleResourcesReadAsync(request),
                "notifications/initialized" => HandleInitialized(request),
                _ => CreateErrorResponse(request.Id, -32601, "Method not found", $"Unknown method: {request.Method}"),
            };
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(request.Id, -32603, "Internal error", ex.Message);
        }
    }

    private Task<MCPResponse> HandleInitializeAsync(MCPRequest request)
    {
        var capabilities = new
        {
            tools = new { },
            resources = new { },
        };

        var serverInfo = new
        {
            name = "PowerToys Workspaces MCP Server",
            version = "1.0.0",
        };

        var result = new
        {
            protocolVersion = "2024-11-05",
            capabilities,
            serverInfo,
        };

        return Task.FromResult(new MCPResponse
        {
            Id = request.Id,
            Result = result,
        });
    }

    private MCPResponse HandleInitialized(MCPRequest request)
    {
        // For notifications, we don't send a response (id should be null)
        return new MCPResponse
        {
            Id = null,
            Result = new { },
        };
    }

    private Task<MCPResponse> HandleToolsListAsync(MCPRequest request)
    {
        var tools = new[]
        {
            new MCPTool
            {
                Name = "get_windows",
                Description = "Get information about all visible windows",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["includeMinimized"] = new() { Type = "boolean", Description = "Include minimized windows" },
                    },
                },
            },
            new MCPTool
            {
                Name = "get_apps",
                Description = "Get information about all running applications",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>(),
                },
            },
            new MCPTool
            {
                Name = "check_app_running",
                Description = "Check if a specific application is running",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["appName"] = new() { Type = "string", Description = "Name of the application to check" },
                    },
                    Required = new[] { "appName" },
                },
            },
            new MCPTool
            {
                Name = "find_windows",
                Description = "Find windows by title or class name",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["titlePattern"] = new() { Type = "string", Description = "Pattern to match in window title" },
                        ["className"] = new() { Type = "string", Description = "Window class name to match" },
                    },
                },
            },
        };

        return Task.FromResult(new MCPResponse
        {
            Id = request.Id,
            Result = new { tools },
        });
    }

    private async Task<MCPResponse> HandleToolsCallAsync(MCPRequest request)
    {
        if (request.Params == null)
        {
            return CreateErrorResponse(request.Id, -32602, "Invalid params", "Missing params");
        }

        try
        {
            var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
            var toolCall = JsonSerializer.Deserialize<ToolCallParams>(paramsJson, _jsonOptions);

            if (toolCall == null || string.IsNullOrEmpty(toolCall.Name))
            {
                return CreateErrorResponse(request.Id, -32602, "Invalid params", "Missing tool name");
            }

            var result = await ExecuteToolAsync(toolCall.Name, toolCall.Arguments);

            return new MCPResponse
            {
                Id = request.Id,
                Result = new { content = new[] { new { type = "text", text = JsonSerializer.Serialize(result, _jsonOptions) } } },
            };
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(request.Id, -32603, "Tool execution error", ex.Message);
        }
    }

    private Task<MCPResponse> HandleResourcesListAsync(MCPRequest request)
    {
        var resources = new[]
        {
            new MCPResource
            {
                Uri = "workspace://current",
                Name = "Current Workspace State",
                Description = "Current state of all windows and applications",
                MimeType = "application/json",
            },
            new MCPResource
            {
                Uri = "workspace://apps",
                Name = "Application Catalog",
                Description = "List of all installed applications",
                MimeType = "application/json",
            },
            new MCPResource
            {
                Uri = "workspace://hierarchy",
                Name = "Window Hierarchy",
                Description = "Hierarchical view of all windows",
                MimeType = "application/json",
            },
        };

        return Task.FromResult(new MCPResponse
        {
            Id = request.Id,
            Result = new { resources },
        });
    }

    private async Task<MCPResponse> HandleResourcesReadAsync(MCPRequest request)
    {
        if (request.Params == null)
        {
            return CreateErrorResponse(request.Id, -32602, "Invalid params", "Missing params");
        }

        try
        {
            var paramsJson = JsonSerializer.Serialize(request.Params, _jsonOptions);
            var readParams = JsonSerializer.Deserialize<ResourceReadParams>(paramsJson, _jsonOptions);

            if (readParams == null || string.IsNullOrEmpty(readParams.Uri))
            {
                return CreateErrorResponse(request.Id, -32602, "Invalid params", "Missing resource URI");
            }

            var content = await ReadResourceAsync(readParams.Uri);

            return new MCPResponse
            {
                Id = request.Id,
                Result = new
                {
                    contents = new[]
                    {
                        new
                        {
                            uri = readParams.Uri,
                            mimeType = "application/json",
                            text = JsonSerializer.Serialize(content, _jsonOptions),
                        },
                    },
                },
            };
        }
        catch (Exception ex)
        {
            return CreateErrorResponse(request.Id, -32603, "Resource read error", ex.Message);
        }
    }

    private async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object>? arguments)
    {
        var windowsApiService = new WindowsApiService();

        return toolName switch
        {
            "get_windows" => await windowsApiService.GetAllWindowsAsync(),
            "get_apps" => await windowsApiService.GetRunningApplicationsAsync(),
            "check_app_running" => await Task.FromResult(windowsApiService.IsApplicationRunning(
                arguments?.GetValueOrDefault("appName")?.ToString() ?? string.Empty)),
            "find_windows" => await Task.FromResult(HandleFindWindows(windowsApiService, arguments)),
            _ => throw new InvalidOperationException($"Unknown tool: {toolName}"),
        };
    }

    private object HandleFindWindows(WindowsApiService windowsApiService, Dictionary<string, object>? arguments)
    {
        var titlePattern = arguments?.GetValueOrDefault("titlePattern")?.ToString();
        var className = arguments?.GetValueOrDefault("className")?.ToString();

        if (!string.IsNullOrEmpty(titlePattern))
        {
            return windowsApiService.FindWindowsByTitle(titlePattern);
        }
        else if (!string.IsNullOrEmpty(className))
        {
            return windowsApiService.FindWindowsByClassName(className);
        }
        else
        {
            throw new ArgumentException("Either titlePattern or className must be provided");
        }
    }

    private async Task<object> ReadResourceAsync(string uri)
    {
        var windowsApiService = new WindowsApiService();

        return uri switch
        {
            "workspace://current" => await GetCurrentWorkspaceState(windowsApiService),
            "workspace://apps" => await windowsApiService.GetRunningApplicationsAsync(),
            "workspace://hierarchy" => await GetWindowHierarchy(windowsApiService),
            _ => throw new InvalidOperationException($"Unknown resource: {uri}"),
        };
    }

    private async Task<object> GetCurrentWorkspaceState(WindowsApiService windowsApiService)
    {
        var apps = await windowsApiService.GetRunningApplicationsAsync();
        var windows = await windowsApiService.GetAllWindowsAsync();

        return new
        {
            timestamp = DateTime.UtcNow,
            applications = apps,
            totalWindows = windows.Count,
            visibleWindows = windows.Count(w => w.IsVisible),
        };
    }

    private async Task<object> GetWindowHierarchy(WindowsApiService windowsApiService)
    {
        var apps = await windowsApiService.GetRunningApplicationsAsync();

        return new
        {
            hierarchy = apps.Select(app => new
            {
                app.Name,
                app.ProcessId,
                app.ExecutablePath,
                windowCount = app.Windows.Length,
                windows = app.Windows.Select(w => new
                {
                    w.Title,
                    w.ClassName,
                    w.Bounds,
                    w.IsVisible,
                    w.IsMinimized,
                    w.IsMaximized,
                }),
            }),
        };
    }

    private MCPResponse CreateErrorResponse(object? id, int code, string message, object? data)
    {
        return new MCPResponse
        {
            Id = id,
            Error = new MCPError
            {
                Code = code,
                Message = message,
                Data = data,
            },
        };
    }
}
