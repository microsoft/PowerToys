# PowerToys Workspaces MCP Server

This is a Model Context Protocol (MCP) server that provides Windows desktop management capabilities for PowerToys Workspaces.

## 🚀 How to Run

### 1. Build the Project
```bash
# From the PowerToys root directory
dotnet build src\modules\Workspaces\WorkspacesDaemon\PowerToys.WorkspacesMCP.csproj -c Debug
```

### 2. Run the Server
```bash
# Navigate to the output directory
cd x64\Debug

# Run the MCP server
PowerToys.WorkspacesMCP.exe
```

The server will start and listen on stdin/stdout for MCP protocol messages.

## 🔧 Available Tools

### `create_workspace_snapshot`

Creates a workspace snapshot using PowerToys.WorkspacesSnapshotTool.exe. This captures the current state of all open windows and applications.

**Parameters:**
- `workspaceId` (string, optional): Custom workspace identifier to assign to the snapshot
- `force` (boolean, optional): When true, saves directly to workspaces.json; when false, creates a temporary file
- `skipMinimized` (boolean, optional): When true, excludes minimized windows from the snapshot

**Examples:**
- Basic snapshot: `{"arguments": {}}`
- Force save to workspaces.json: `{"arguments": {"force": true}}`
- Skip minimized windows: `{"arguments": {"skipMinimized": true}}`
- Custom ID with all options: `{"arguments": {"workspaceId": "my-workspace-123", "force": true, "skipMinimized": true}}`

### `launch_workspace`

Launches an existing workspace using PowerToys.WorkspacesLauncher.exe. This restores all the windows and applications defined in the specified workspace.

**Parameters:**
- `workspaceId` (string, required): ID of the workspace to launch

**Examples:**
- Launch workspace: `{"arguments": {"workspaceId": "25-09-18-18-09"}}`

## 🧪 Testing the MCP Server

### JSON-RPC — Detailed test requests and parsing (single-line JSON)

This section provides end-to-end JSON-RPC request examples and robust client parsing patterns. The MCP server communicates over stdio (one JSON object per line).

Note: For backward compatibility some responses include both a `text` field (serialized JSON string) and a `data` field (structured object). Clients should prefer `data` and fall back to parsing `text` if `data` is not present.

#### Basic requests (one JSON object per line)

Initialize:
```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{"tools":{}},"clientInfo":{"name":"test-client","version":"1.0.0"}}}
```

List tools (`tools/list`):
```json
{"jsonrpc":"2.0","id":2,"method":"tools/list","params":null}
```

Call `list_workspaces` tool:
```json
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"list_workspaces","arguments":{}}}
```

Read `workspace://workspaces` resource:
```json
{"jsonrpc":"2.0","id":4,"method":"resources/read","params":{"uri":"workspace://workspaces"}}
```

Find windows by title (`find_windows`):
```json
{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"find_windows","arguments":{"titlePattern":"Notepad"}}}
```

Create workspace snapshot with automatic naming (`create_workspace_snapshot`):
```json
{"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"create_workspace_snapshot","arguments":{}}}
```
This tool automatically:
- Creates a workspace snapshot with name format `yy-MM-dd-HH-mm` (current date/time)
- Enables force save to write directly to workspaces.json
- Skips all minimized windows
- No parameters needed!

Launch an existing workspace (`launch_workspace`):
```json
{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"launch_workspace","arguments":{"workspaceId":"25-09-18-21-08"}}}
```

## 📋 Available Tools

### `create_workspace_snapshot`
**Description:** Creates a workspace snapshot with automatic configuration  
**Parameters:** None required  
**Features:**
- **Auto-naming:** Uses current timestamp in `yy-MM-dd-HH-mm` format
- **Force save:** Always saves to workspaces.json (not temp file)
- **Skip minimized:** Ignores minimized windows for cleaner snapshots

### `launch_workspace`
**Description:** Launches an existing workspace by restoring all its windows and applications  
**Parameters:**
- `workspaceId` (required): ID of the workspace to launch
**Features:**
- **Async launch:** Initiates workspace restoration without waiting for completion
- **Window arrangement:** Automatically positions windows to match snapshot
- **App launching:** Starts any applications that aren't currently running

### `list_workspaces`
**Description:** Lists all available workspace definitions  
**Parameters:** None

### `find_windows`
**Description:** Find windows by title pattern or class name  
**Parameters:**
- `titlePattern` (optional): Pattern to match in window titles
- `className` (optional): Window class name to match

## 🛠️ Technical Details

The MCP server provides a simplified interface for workspace management. The `create_workspace_snapshot` tool is designed to be fire-and-forget - simply call it without any parameters and it will handle all the complexity automatically.