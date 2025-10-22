# PowerToys Model Context Protocol Server

This module hosts a standalone Model Context Protocol (MCP) server that exposes PowerToys functionality to MCP-compliant AI agents. The server is built as a .NET 9 console application that implements the MCP specification using the official ModelContextProtocol SDK.

## Project Structure

- **Program.cs**: Main entry point that configures the MCP server with stdio transport
- **Tools/AwakeTools.cs**: Implementation of Awake-related MCP tools
- **PowerToys.McpServer.csproj**: .NET 9 project configuration with MCP dependencies

## Dependencies

- **Microsoft.Extensions.Hosting**: For hosting infrastructure and dependency injection
- **ModelContextProtocol**: Official MCP SDK for .NET
- **PowerToys Settings Library**: Integration with PowerToys settings system
- **ManagedCommon**: PowerToys logging and utilities

## Available Tools

| Tool Name | Description | Parameters | Module |
|-----------|-------------|------------|--------|
| `GetAwakeStatus` | Returns the current Awake configuration (mode, timers, display policy) | None | Awake |
| `SetAwakePassive` | Set Awake to passive mode (allow system to sleep normally) | None | Awake |
| `SetAwakeIndefinite` | Set Awake to indefinite mode (keep system awake until manually changed) | `keepDisplayOn` (bool), `force` (bool) | Awake |
| `SetAwakeTimed` | Set Awake to timed mode (keep system awake for a specific duration) | `durationSeconds` (int), `keepDisplayOn` (bool), `force` (bool) | Awake |

## Building and Running

### Prerequisites
- .NET 9 SDK
- Visual Studio 2022 (recommended) or VS Code with C# extension

### Build
```bash
# From PowerToys root directory
msbuild src/McpServer/PowerToys.McpServer.csproj /p:Platform=x64 /p:Configuration=Debug

# Or using dotnet CLI
cd src/McpServer
dotnet build -c Debug
```

### Run the Server
The executable is built to `x64\Debug\PowerToys.McpServer.exe`. The server communicates over standard input/output using MCP framing (`Content-Length` header followed by JSON).

**Example MCP Client Session:**
1. Client sends `initialize` request with MCP version and capabilities
2. Client calls `tools/list` to discover available PowerToys tools
3. Client invokes `tools/call` with the desired tool name and arguments
4. Server responds with tool execution results or errors

The server will remain active until the process is terminated or a `shutdown` request is received.

### Logging
- Application logs are written to `%LOCALAPPDATA%\Microsoft\PowerToys\McpServer\Logs\`
- MCP protocol logs are sent to stderr (required by MCP specification)

## Architecture

The server uses the official ModelContextProtocol .NET SDK and follows these patterns:

- **Tool Discovery**: Tools are automatically discovered using `WithToolsFromAssembly()` 
- **Tool Attributes**: Methods marked with `[McpServerTool]` and `[Description]` are exposed as MCP tools
- **Parameter Binding**: Method parameters are automatically bound from MCP tool call arguments
- **Error Handling**: Exceptions are caught and returned as MCP error responses
- **Settings Integration**: Uses PowerToys settings system for configuration persistence

## Adding New Module Tools

1. Create a new static class in the `Tools/` directory (e.g., `FancyZonesTools.cs`)
2. Mark the class with `[McpServerToolType]` attribute
3. Implement static methods with `[McpServerTool]` and `[Description]` attributes:
   ```csharp
   [McpServerToolType]
   public static class MyModuleTools
   {
       [McpServerTool]
       [Description("Description of what this tool does")]
       public static JsonObject MyTool(
           [Description("Parameter description")] string parameter)
       {
           // Implementation here
           return new JsonObject();
       }
   }
   ```
4. Follow existing patterns in `AwakeTools.cs` for:
   - Settings integration using `SettingsUtils`
   - Logging using `Logger.LogInfo/LogError`
   - Error handling and response formatting
   - PowerToys process detection and module status checks

## Integration with PowerToys

The MCP server integrates with PowerToys through:

- **Settings System**: Uses the same settings files as the main PowerToys application
- **Process Management**: Detects and interacts with running PowerToys processes
- **Module Status**: Checks if specific PowerToys modules are enabled
- **Logging**: Uses PowerToys logging infrastructure for troubleshooting

Refer to the PowerToys developer documentation for build and packaging instructions.
