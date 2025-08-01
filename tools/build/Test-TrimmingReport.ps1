# Quick test of the trimming analyzer with existing builds
param(
    [string]$BaselineDir = "C:\Users\yuleng\PowerToys\src\modules\cmdpal\Microsoft.CmdPal.UI\bin\Release\net9.0-windows10.0.26100.0\win-x64\publish",
    [string]$TrimmedDir = "C:\Users\yuleng\PowerToys\src\modules\cmdpal\Microsoft.CmdPal.UI\bin\Release\net9.0-windows10.0.26100.0\win-x64\publish"
)

Write-Host "===== Quick Trimming Analysis Test ====="

# Build the analyzer first
Write-Host "Building TrimmingAnalyzer..."
dotnet build "C:\Users\yuleng\PowerToys\tools\TrimmingAnalyzer\TrimmingAnalyzer.csproj" -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build TrimmingAnalyzer"
    exit 1
}

$analyzerPath = "C:\Users\yuleng\PowerToys\tools\TrimmingAnalyzer\bin\Release\net9.0\TrimmingAnalyzer.exe"

if (-not (Test-Path $analyzerPath)) {
    Write-Error "TrimmingAnalyzer not found at $analyzerPath"
    exit 1
}

# Check if any DLLs exist in the specified directories
$baselineDlls = Get-ChildItem -Path $BaselineDir -Filter "*.dll" -ErrorAction SilentlyContinue
$trimmedDlls = Get-ChildItem -Path $TrimmedDir -Filter "*.dll" -ErrorAction SilentlyContinue

Write-Host "Baseline directory: $BaselineDir"
Write-Host "Found $($baselineDlls.Count) DLLs in baseline"

Write-Host "Trimmed directory: $TrimmedDir"
Write-Host "Found $($trimmedDlls.Count) DLLs in trimmed"

if ($baselineDlls.Count -eq 0 -or $trimmedDlls.Count -eq 0) {
    Write-Warning "No DLLs found in one or both directories. The script may need actual AOT builds to compare."
    Write-Host "Current directories contain:"
    Write-Host "Baseline: $($baselineDlls.Name -join ', ')"
    Write-Host "Trimmed: $($trimmedDlls.Name -join ', ')"
}

# Test the analyzer with a simple case
if ($baselineDlls.Count -gt 0 -and $trimmedDlls.Count -gt 0) {
    $testDll = $baselineDlls[0].Name
    $baselinePath = Join-Path $BaselineDir $testDll
    $trimmedPath = Join-Path $TrimmedDir $testDll
    
    if (Test-Path $trimmedPath) {
        Write-Host "Testing analyzer with $testDll..."
        & $analyzerPath analyze "$baselinePath" "$trimmedPath" --output "test-report"
        
        if (Test-Path "test-report.md") {
            Write-Host "`nGenerated report preview:"
            Get-Content "test-report.md" | Select-Object -First 20
        }
    }
}

Write-Host "`nAnalyzer path: $analyzerPath"
Write-Host "Test completed."
