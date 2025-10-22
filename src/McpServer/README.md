# PowerToys Model Context Protocol Server

This module hosts a standalone Model Context Protocol (MCP) server that exposes PowerToys functionality to MCP-compliant AI agents. The first release focuses on Awake, enabling agents to query status and toggle keep-awake modes without going through the PowerToys Runner process. The server is designed to grow over time with additional modules.

## Capabilities

| Tool name | Description | Module |
|-----------|-------------|--------|
| `awake_status` | Returns the current Awake configuration (mode, timers, display policy). | Awake |
| `awake_set_mode` | Switches Awake between passive, indefinite, timed, or expirable modes using the PowerToys settings pipeline. | Awake |

## Running the server

The executable lives next to other module binaries (for example, `PowerToys.McpServer.exe` under the architecture-specific output folder). The server communicates over standard input/output using MCP framing (`Content-Length` header followed by JSON). A minimal session looks like:

1. Client sends `initialize` request.
2. Client calls `tools/list` to discover available tools.
3. Client invokes `tools/call` with the desired tool name and arguments.

The server will remain active until the process is terminated or a `shutdown` request is received.

## Adding new module tools

1. Implement an `IMcpModule` that exposes one or more `IMcpTool` instances.
2. Register the module in `Program.cs` via `ModuleCatalog.RegisterModule`.
3. Use `Awake` implementations as reference for schema design, telemetry, and settings integration.
4. Update documentation and packaging (signing lists, installer termination lists, verification scripts) if new executables are introduced.

Refer to the PowerToys developer documentation for build and packaging instructions.
