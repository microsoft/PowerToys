# PowerToys Workspaces MCP Server Test Script
# Usage: .\test-mcp-server.ps1

Write-Host "🚀 Testing PowerToys Workspaces MCP Server" -ForegroundColor Green

# Build the project first
Write-Host "`n📦 Building the project..." -ForegroundColor Yellow
$buildResult = dotnet build "PowerToys.WorkspacesMCP.csproj" -c Debug --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Build successful!" -ForegroundColor Green

# Find the executable
$exePath = "..\..\..\..\x64\Debug\PowerToys.WorkspacesMCP.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "❌ Executable not found at: $exePath" -ForegroundColor Red
    Write-Host "   Please check the build output path." -ForegroundColor Red
    exit 1
}

Write-Host "📍 Found executable at: $exePath" -ForegroundColor Cyan

# Test messages
$messages = @(
    @{
        name = "Initialize Server"
        message = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{"tools":{}},"clientInfo":{"name":"test-client","version":"1.0.0"}}}'
    },
    @{
        name = "List Tools" 
        message = '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
    },
    @{
        name = "Get Windows"
        message = '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_windows","arguments":{"includeMinimized":false}}}'
    },
    @{
        name = "Get Apps"
        message = '{"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"get_apps","arguments":{}}}'
    },
    @{
        name = "Check App Running"
        message = '{"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"check_app_running","arguments":{"appName":"notepad"}}}'
    },
    @{
        name = "List Resources"
        message = '{"jsonrpc":"2.0","id":6,"method":"resources/list"}'
    },
    @{
        name = "Read Current Workspace"
        message = '{"jsonrpc":"2.0","id":7,"method":"resources/read","params":{"uri":"workspace://current"}}'
    },
    @{
        name = "List Workspaces"
        message = '{"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"list_workspaces","arguments":{}}}'
    },
    @{
        name = "Create Workspace Snapshot (Basic)"
        message = '{"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"create_workspace_snapshot","arguments":{}}}'
    },
    @{
        name = "Create Workspace Snapshot (With ID)"
        message = '{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"create_workspace_snapshot","arguments":{"workspaceId":"test-workspace-123"}}}'
    },
    @{
        name = "Create Workspace Snapshot (Force Save)"
        message = '{"jsonrpc":"2.0","id":11,"method":"tools/call","params":{"name":"create_workspace_snapshot","arguments":{"force":true}}}'
    }
)

Write-Host "`n🧪 Starting MCP Server Tests..." -ForegroundColor Yellow

try {
    # Start the server process
    $process = Start-Process -FilePath $exePath -RedirectStandardInput -RedirectStandardOutput -RedirectStandardError -NoNewWindow -PassThru
    
    # Wait a moment for the server to start
    Start-Sleep -Milliseconds 500
    
    foreach ($test in $messages) {
        Write-Host "`n🔹 Testing: $($test.name)" -ForegroundColor Cyan
        Write-Host "   Sending: $($test.message)" -ForegroundColor Gray
        
        try {
            # Send the message
            $process.StandardInput.WriteLine($test.message)
            $process.StandardInput.Flush()
            
            # Wait for response with timeout
            $timeout = 5000  # 5 seconds
            $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
            
            while ($stopwatch.ElapsedMilliseconds -lt $timeout) {
                if ($process.StandardOutput.Peek() -ge 0) {
                    $response = $process.StandardOutput.ReadLine()
                    Write-Host "   ✅ Response: $response" -ForegroundColor Green
                    break
                }
                Start-Sleep -Milliseconds 100
            }
            
            if ($stopwatch.ElapsedMilliseconds -ge $timeout) {
                Write-Host "   ⏱️ Timeout waiting for response" -ForegroundColor Yellow
            }
            
            $stopwatch.Stop()
            
        } catch {
            Write-Host "   ❌ Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    
} catch {
    Write-Host "❌ Failed to start server: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    # Clean up
    if ($process -and !$process.HasExited) {
        Write-Host "`n🛑 Stopping server..." -ForegroundColor Yellow
        $process.Kill()
        $process.WaitForExit(2000)
    }
}

Write-Host "`n🏁 Testing completed!" -ForegroundColor Green
Write-Host "`n💡 Tips:" -ForegroundColor Yellow
Write-Host "   - Check error.log for detailed error messages" -ForegroundColor Gray
Write-Host "   - Use Visual Studio debugger for step-by-step debugging" -ForegroundColor Gray
Write-Host "   - Ensure Windows API permissions are available" -ForegroundColor Gray