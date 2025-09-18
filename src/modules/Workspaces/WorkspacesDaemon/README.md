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

## 🧪 Testing the MCP Server

### Option 1: Manual Testing with JSON Messages

Send MCP protocol messages via stdin. Here are some examples:

#### Initialize the server:
```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{"tools":{}},"clientInfo":{"name":"test-client","version":"1.0.0"}}}
```

#### List available tools:
```json
{"jsonrpc":"2.0","id":2,"method":"tools/list"}
```

#### Call a tool (get all windows):
```json
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_windows","arguments":{"includeMinimized":false}}}
```

#### List available resources:
```json
{"jsonrpc":"2.0","id":4,"method":"resources/list"}
```

#### Read a resource:
```json
{"jsonrpc":"2.0","id":5,"method":"resources/read","params":{"uri":"workspace://current"}}
```

### Option 2: Use a MCP Client

You can use any MCP-compatible client to test this server. Popular options include:

1. **Claude Desktop** - Configure as an MCP server
2. **MCP Inspector** - A debugging tool for MCP servers
3. **Custom client** - Build your own using MCP SDK

### Option 3: PowerShell Testing Script

Here's a simple PowerShell script to test the server:

```powershell
# test-mcp-server.ps1
$serverPath = "x64\Debug\PowerToys.WorkspacesMCP.exe"

# Start the server process
$process = Start-Process -FilePath $serverPath -RedirectStandardInput -RedirectStandardOutput -PassThru

# Send initialize message
$initMessage = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{"tools":{}},"clientInfo":{"name":"test-client","version":"1.0.0"}}}'
$process.StandardInput.WriteLine($initMessage)

# Read response
$response = $process.StandardOutput.ReadLine()
Write-Output "Initialize Response: $response"

# Send tools/list message
$toolsMessage = '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
$process.StandardInput.WriteLine($toolsMessage)

# Read response
$response = $process.StandardOutput.ReadLine()
Write-Output "Tools List Response: $response"

# Clean up
$process.Kill()
```

## 🛠️ Available Capabilities

### Tools:
- **get_windows** - Get information about all visible windows
- **get_apps** - Get information about all running applications  
- **check_app_running** - Check if a specific application is running
- **find_windows** - Find windows by title or class name

### Resources:
- **workspace://current** - Current workspace state
- **workspace://apps** - Application catalog
- **workspace://hierarchy** - Window hierarchy

## 🐛 Development & Debugging

### Build for Development:
```bash
dotnet build src\modules\Workspaces\WorkspacesDaemon\PowerToys.WorkspacesMCP.csproj -c Debug --verbosity normal
```

### Run with Logging:
The server outputs errors to stderr, so you can redirect them for debugging:
```bash
PowerToys.WorkspacesMCP.exe 2>error.log
```

### Attach Debugger:
1. Build in Debug configuration
2. Run `PowerToys.WorkspacesMCP.exe` 
3. Attach Visual Studio debugger to the process
4. Set breakpoints in the service methods

## 📋 Configuration

The MCP server is configured to:
- Use stdin/stdout for communication (MCP standard)
- Support graceful shutdown with Ctrl+C
- Log errors to stderr
- Return JSON responses for all capabilities

## 🔗 MCP Protocol Documentation

For more information about the Model Context Protocol, visit:
- [MCP Specification](https://spec.modelcontextprotocol.org/)
- [MCP SDK Documentation](https://modelcontextprotocol.org/docs/)