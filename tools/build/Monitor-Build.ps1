# Monitor build progress and report generation
Write-Host "===== Monitoring CmdPal Build Progress ====="

$reportPath = "C:\Users\yuleng\PowerToys\src\modules\cmdpal\Microsoft.CmdPal.UI\TrimmedTypes.md"
$lastSize = 0
$lastModified = Get-Date "1/1/1900"

if (Test-Path $reportPath) {
    $lastModified = (Get-Item $reportPath).LastWriteTime
    $lastSize = (Get-Item $reportPath).Length
    Write-Host "Current report file: $reportPath"
    Write-Host "Last modified: $lastModified"
    Write-Host "File size: $lastSize bytes"
} else {
    Write-Host "No report file found yet at: $reportPath"
}

Write-Host "`nChecking for active build processes..."
$buildProcesses = Get-Process | Where-Object { 
    $_.ProcessName -like "*msbuild*" -or 
    $_.ProcessName -like "*dotnet*" -or 
    $_.ProcessName -like "*cl*" -or
    $_.ProcessName -like "*link*"
}

Write-Host "Found $($buildProcesses.Count) build-related processes running"

if ($buildProcesses.Count -gt 0) {
    Write-Host "Build processes still active:"
    $buildProcesses | Select-Object ProcessName, Id, CPU | Format-Table -AutoSize
} else {
    Write-Host "No build processes detected. Build may be complete."
}

# Check if directories exist for both builds
$debugPath = "C:\Users\yuleng\PowerToys\src\modules\cmdpal\Microsoft.CmdPal.UI\bin\Debug"
$releasePath = "C:\Users\yuleng\PowerToys\src\modules\cmdpal\Microsoft.CmdPal.UI\bin\Release"

Write-Host "`nBuild directory status:"
Write-Host "Debug path exists: $(Test-Path $debugPath)"
Write-Host "Release path exists: $(Test-Path $releasePath)"

if (Test-Path $debugPath) {
    $debugDlls = Get-ChildItem -Path $debugPath -Recurse -Filter "Microsoft.CmdPal.UI.dll" -ErrorAction SilentlyContinue
    Write-Host "Debug DLLs found: $($debugDlls.Count)"
}

if (Test-Path $releasePath) {
    $releaseDlls = Get-ChildItem -Path $releasePath -Recurse -Filter "Microsoft.CmdPal.UI.dll" -ErrorAction SilentlyContinue
    Write-Host "Release DLLs found: $($releaseDlls.Count)"
}

Write-Host "`nMonitoring complete."
