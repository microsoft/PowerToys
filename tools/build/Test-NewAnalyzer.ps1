# Test script to run the updated CmdPal analysis with current working functionality
param(
    [string]$Configuration = "Release",
    [switch]$EnableAOT = $true
)

Write-Host "===== CmdPal AOT Analysis Test ====="

$rootDir = Split-Path -Parent $PSScriptRoot
$rootDir = Split-Path -Parent $rootDir

$cmdPalProject = Join-Path $rootDir "src\modules\cmdpal\Microsoft.CmdPal.UI\Microsoft.CmdPal.UI.csproj"
$analyzerProject = Join-Path $rootDir "tools\TrimmingAnalyzer\TrimmingAnalyzer.csproj"

# Build the current analyzer
Write-Host "Building TrimmingAnalyzer..."
dotnet build $analyzerProject -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build TrimmingAnalyzer"
    exit 1
}

$analyzerPath = Join-Path $rootDir "tools\TrimmingAnalyzer\bin\Release\net9.0\TrimmingAnalyzer.exe"

# Test the analyzer help
Write-Host "`nTesting analyzer functionality..."
& $analyzerPath

Write-Host "`nAnalyzer test completed."
