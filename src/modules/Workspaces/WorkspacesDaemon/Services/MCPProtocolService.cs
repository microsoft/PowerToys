// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using PowerToys.WorkspacesMCP.Models;

namespace PowerToys.WorkspacesMCP.Services;

public class MCPProtocolService
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IWorkspaceStateProvider _stateProvider;
    private readonly WindowsApiService _fallbackWindowsApi;
    private readonly Workspaces.IWorkspaceCatalog _workspaceCatalog;

    // New DI constructor
    public MCPProtocolService(IWorkspaceStateProvider stateProvider, Workspaces.IWorkspaceCatalog workspaceCatalog, WindowsApiService? fallback = null)
    {
        _stateProvider = stateProvider;
        _workspaceCatalog = workspaceCatalog;
        _fallbackWindowsApi = fallback ?? new WindowsApiService();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
    }

    // Legacy parameterless constructor (can be removed once DI wiring is universal)
    [Obsolete("Use DI constructor with IWorkspaceStateProvider")]
    public MCPProtocolService()
        : this(new WorkspaceStateProvider(), new Workspaces.WorkspaceCatalogService())
    {
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
        var snapshot = _stateProvider.Current;
        var warmup = snapshot == null;

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
            workspaceVersion = snapshot?.Version ?? 0,
            warmup,
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
                Description = "Get cached information about all visible windows",
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
                Description = "Get cached information about all running applications",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>(),
                },
            },
            new MCPTool
            {
                Name = "check_app_running",
                Description = "Check if a specific application is running (cached lookup)",
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
                Description = "Find windows by title or class name (cached)",
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
            new MCPTool
            {
                Name = "list_workspaces",
                Description = "List cached workspace definitions",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>(),
                },
            },
            new MCPTool
            {
                Name = "create_workspace_snapshot",
                Description = "Create a workspace snapshot with automatic naming (yy-mm-dd-hh-mm), force save enabled, and skip minimized windows",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>(),
                },
            },
            new MCPTool
            {
                Name = "launch_workspace",
                Description = "Launch a workspace by its ID using PowerToys.WorkspacesLauncher.exe",
                InputSchema = new ToolInputSchema
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolProperty>
                    {
                        ["workspaceId"] = new() { Type = "string", Description = "ID of the workspace to launch" },
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
            var snapshot = _stateProvider.Current;

            return new MCPResponse
            {
                Id = request.Id,
                Result = new
                {
                    workspaceVersion = snapshot?.Version ?? 0,
                    content = new[]
                    {
                        new { type = "application/json", data = result },
                    },
                },
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
                Description = "Current cached state of windows and applications",
                MimeType = "application/json",
            },
            new MCPResource
            {
                Uri = "workspace://apps",
                Name = "Application Catalog",
                Description = "Cached list of running applications",
                MimeType = "application/json",
            },
            new MCPResource
            {
                Uri = "workspace://hierarchy",
                Name = "Window Hierarchy",
                Description = "Cached hierarchical view of all windows",
                MimeType = "application/json",
            },
            new MCPResource
            {
                Uri = "workspace://workspaces",
                Name = "Workspace Catalog",
                Description = "Cached list of workspace definitions",
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
            var snapshot = _stateProvider.Current;

            return new MCPResponse
            {
                Id = request.Id,
                Result = new
                {
                    workspaceVersion = snapshot?.Version ?? 0,
                    contents = new[]
                    {
                        new
                        {
                            uri = readParams.Uri,
                            mimeType = "application/json",
                            data = content,
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
        var snapshot = _stateProvider.Current;

        // Warmup fallback if snapshot not yet produced.
        if (snapshot == null)
        {
            var apps = await _fallbackWindowsApi.GetRunningApplicationsAsync();
            var windows = await _fallbackWindowsApi.GetAllWindowsAsync();
            snapshot = new ImmutableWorkspaceSnapshot(
                TimestampUtc: DateTime.UtcNow,
                Apps: apps,
                Windows: windows,
                VisibleWindows: windows.Count(w => w.IsVisible),
                Version: 0);
        }

        return toolName switch
        {
            "get_windows" => snapshot.Windows,
            "get_apps" => snapshot.Apps,
            "check_app_running" => snapshot.Apps.Any(a => string.Equals(a.Name, arguments?.GetValueOrDefault("appName")?.ToString(), StringComparison.OrdinalIgnoreCase)),
            "find_windows" => HandleFindWindows(snapshot, arguments),
            "list_workspaces" => _workspaceCatalog.Workspaces.Select(w => new { w.Id, w.Name, w.DisplayName }),
            "create_workspace_snapshot" => await HandleCreateWorkspaceSnapshotAsync(arguments),
            "launch_workspace" => await HandleLaunchWorkspaceAsync(arguments),
            _ => throw new InvalidOperationException($"Unknown tool: {toolName}"),
        };
    }

    private object HandleFindWindows(ImmutableWorkspaceSnapshot snapshot, Dictionary<string, object>? arguments)
    {
        var titlePattern = arguments?.GetValueOrDefault("titlePattern")?.ToString();
        var className = arguments?.GetValueOrDefault("className")?.ToString();

        IEnumerable<WindowInfo> query = snapshot.Windows;

        if (!string.IsNullOrEmpty(titlePattern))
        {
            query = query.Where(w => w.Title?.Contains(titlePattern, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrEmpty(className))
        {
            query = query.Where(w => string.Equals(w.ClassName, className, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    private Task<object> ReadResourceAsync(string uri)
    {
        var snapshot = _stateProvider.GetOrWait();

        object result = uri switch
        {
            "workspace://current" => new
            {
                snapshot.TimestampUtc,
                snapshot.Version,
                applications = snapshot.Apps,
                totalWindows = snapshot.Windows.Count,
                snapshot.VisibleWindows,
            },
            "workspace://apps" => new
            {
                snapshot.Version,
                applications = snapshot.Apps,
            },
            "workspace://hierarchy" => new
            {
                snapshot.Version,
                hierarchy = snapshot.Apps.Select(app => new
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
            },
            "workspace://workspaces" => new
            {
                _workspaceCatalog.LoadedAtUtc,
                workspaces = _workspaceCatalog.Workspaces.Select(w => new { w.Id, w.Name, w.DisplayName }),
            },
            _ => throw new InvalidOperationException($"Unknown resource: {uri}"),
        };

        return Task.FromResult(result);
    }

    // Removed GetCurrentWorkspaceState / GetWindowHierarchy (handled via snapshot)
    private async Task<object> HandleCreateWorkspaceSnapshotAsync(Dictionary<string, object>? arguments)
    {
        // Generate automatic workspace name using yy-MM-dd-HH-mm format
        var now = DateTime.Now;
        var workspaceId = now.ToString("yy-MM-dd-HH-mm", System.Globalization.CultureInfo.InvariantCulture);

        Console.Error.WriteLine($"[DEBUG] Auto-generated workspace name: '{workspaceId}'");

        // Build arguments with fixed force and skipMinimized flags
        var parts = new List<string>
        {
            workspaceId,
            "-force",
            "-skipMinimized",
        };

        var argumentsString = string.Join(' ', parts);
        Console.Error.WriteLine($"[DEBUG] Final command line arguments: '{argumentsString}'");

        try
        {
            var exitCode = await RunProcessAsync("PowerToys.WorkspacesSnapshotTool.exe", argumentsString, waitForExit: true);

            return new
            {
                success = exitCode == 0,
                exitCode,
                workspaceId,
                arguments = argumentsString,
                message = exitCode == 0
                    ? "Workspace snapshot created and saved to workspaces.json with automatic naming, force save, and minimized windows skipped"
                    : $"Snapshot tool exited with code {exitCode}",
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                exitCode = -1,
                workspaceId,
                arguments = argumentsString,
                error = ex.Message,
            };
        }
    }

    private async Task<object> HandleLaunchWorkspaceAsync(Dictionary<string, object>? arguments)
    {
        var workspaceId = arguments?.GetValueOrDefault("workspaceId")?.ToString();

        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return new
            {
                success = false,
                error = "workspaceId parameter is required",
            };
        }

        Console.Error.WriteLine($"[DEBUG] Launching workspace with ID: '{workspaceId}'");

        try
        {
            var exitCode = await RunProcessAsync("PowerToys.WorkspacesLauncher.exe", workspaceId, waitForExit: false);

            return new
            {
                success = true,
                exitCode,
                workspaceId,
                message = "Workspace launch initiated successfully",
            };
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = ex.Message,
                workspaceId,
            };
        }
    }

    private async Task<int> RunProcessAsync(string toolName, string arguments, bool waitForExit)
    {
        if (!TryResolveTool(toolName, out var resolvedPath))
        {
            throw new InvalidOperationException($"Unable to locate '{toolName}'. Expected alongside this application at '{AppContext.BaseDirectory}'.");
        }

        var startInfo = new ProcessStartInfo(resolvedPath)
        {
            Arguments = arguments,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(resolvedPath) ?? AppContext.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                throw new InvalidOperationException($"Failed to start '{toolName}'.");
            }

            if (!waitForExit)
            {
                return 0;
            }

            await process.WaitForExitAsync().ConfigureAwait(false);

            // Read any output for debugging
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();

            // Log output for debugging (you might want to remove this in production)
            if (!string.IsNullOrEmpty(stdout))
            {
                Console.Error.WriteLine($"[DEBUG] {toolName} stdout: {stdout}");
            }

            if (!string.IsNullOrEmpty(stderr))
            {
                Console.Error.WriteLine($"[DEBUG] {toolName} stderr: {stderr}");
            }

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to launch '{toolName}': {ex.Message}", ex);
        }
    }

    private bool TryResolveTool(string toolName, out string path)
    {
        path = Path.Combine(AppContext.BaseDirectory, toolName);
        Console.Error.WriteLine($"[DEBUG] TryResolveTool: Checking primary path: {path}");
        if (File.Exists(path))
        {
            Console.Error.WriteLine($"[DEBUG] TryResolveTool: Found at primary path: {path}");
            return true;
        }

        // Fallback to repository root for developer scenarios.
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
        var fallback = Path.Combine(repoRoot, toolName);
        Console.Error.WriteLine($"[DEBUG] TryResolveTool: Checking fallback path: {fallback}");
        if (File.Exists(fallback))
        {
            Console.Error.WriteLine($"[DEBUG] TryResolveTool: Found at fallback path: {fallback}");
            path = fallback;
            return true;
        }

        Console.Error.WriteLine($"[DEBUG] TryResolveTool: Tool not found. AppContext.BaseDirectory: {AppContext.BaseDirectory}");
        return false;
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
