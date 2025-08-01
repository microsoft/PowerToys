# Simple test to verify TrimmingAnalyzer functionality
Write-Host "===== TrimmingAnalyzer Simple Test ====="

$analyzerPath = "C:\Users\yuleng\PowerToys\tools\TrimmingAnalyzer\bin\Release\net9.0\TrimmingAnalyzer.exe"

if (-not (Test-Path $analyzerPath)) {
    Write-Error "TrimmingAnalyzer not found at $analyzerPath"
    exit 1
}

Write-Host "TrimmingAnalyzer found at: $analyzerPath"

# Test the help command
Write-Host "`nTesting help command..."
try {
    $helpOutput = & $analyzerPath --help 2>&1
    Write-Host "Help command executed successfully"
    Write-Host "Output length: $($helpOutput.Length) characters"
    
    # Show first few lines of help
    $helpLines = $helpOutput -split "`n"
    $helpLines | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" }
    
    if ($helpLines.Length -gt 10) {
        Write-Host "  ... (truncated)"
    }
} catch {
    Write-Error "Failed to run help command: $_"
}

Write-Host "`nTesting version command..."
try {
    $versionOutput = & $analyzerPath --version 2>&1
    Write-Host "Version: $versionOutput"
} catch {
    Write-Warning "Version command failed: $_"
}

Write-Host "`nTrimmingAnalyzer test completed."
