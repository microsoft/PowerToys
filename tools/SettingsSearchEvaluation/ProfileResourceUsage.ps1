# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [string]$CasesJson = ".\tools\SettingsSearchEvaluation\cases\settings-search-cases.grouped.800.json",
    [string]$NormalizedCorpus = ".\tools\SettingsSearchEvaluation\artifacts\normalized-settings-corpus.tsv",
    [string[]]$Engines = @("basic", "semantic"),
    [int]$TopK = 5,
    [int]$MaxResults = 10,
    [int]$Iterations = 5,
    [int]$Warmup = 1,
    [int]$SemanticTimeoutMs = 15000,
    [int]$SampleIntervalMs = 500,
    [int]$TimeoutMinutes = 30,
    [string]$OutputJson = "",
    [switch]$NoDashboard
)

$ErrorActionPreference = "Stop"

function Write-ArgsFile {
    param(
        [string]$Path,
        [string[]]$Lines
    )

    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }

    [System.IO.File]::WriteAllLines($Path, $Lines)
}

function Get-EngineCode {
    param([string]$Engine)

    switch ($Engine.ToLowerInvariant()) {
        "basic" { return 0 }
        "semantic" { return 1 }
        default { throw "Unsupported engine '$Engine'. Use basic or semantic." }
    }
}

function Start-ProfiledRun {
    param(
        [string]$Engine,
        [string]$PackageFamilyName,
        [string]$RepoRoot,
        [string]$CasesPath,
        [string]$CorpusPath,
        [string]$ArtifactsDir,
        [int]$TopKValue,
        [int]$MaxResultsValue,
        [int]$IterationsValue,
        [int]$WarmupValue,
        [int]$SemanticTimeoutValue,
        [int]$SampleIntervalValue,
        [int]$TimeoutMinutesValue
    )

    $ts = Get-Date -Format "yyyyMMdd-HHmmss"
    $engineName = $Engine.ToLowerInvariant()
    $reportPath = Join-Path $ArtifactsDir ("report.profile.{0}.{1}.json" -f $engineName, $ts)
    $samplesPath = Join-Path $ArtifactsDir ("samples.profile.{0}.{1}.json" -f $engineName, $ts)

    $argsLines = @(
        "--normalized-corpus",
        $CorpusPath,
        "--cases-json",
        $CasesPath,
        "--engine",
        $engineName,
        "--top-k",
        "$TopKValue",
        "--max-results",
        "$MaxResultsValue",
        "--iterations",
        "$IterationsValue",
        "--warmup",
        "$WarmupValue",
        "--semantic-timeout-ms",
        "$SemanticTimeoutValue",
        "--output-json",
        $reportPath
    )

    $userArgs = Join-Path $env:LOCALAPPDATA "PowerToys.SettingsSearchEvaluation\launch.args.txt"
    Write-ArgsFile -Path $userArgs -Lines $argsLines

    $pkgArgsRoot = Join-Path $env:LOCALAPPDATA ("Packages\{0}\LocalCache\Local\PowerToys.SettingsSearchEvaluation" -f $PackageFamilyName)
    [System.IO.Directory]::CreateDirectory($pkgArgsRoot) | Out-Null
    Write-ArgsFile -Path (Join-Path $pkgArgsRoot "launch.args.txt") -Lines $argsLines

    $aumid = "shell:AppsFolder\{0}!SettingsSearchEvaluation" -f $PackageFamilyName
    $deadline = (Get-Date).AddMinutes($TimeoutMinutesValue)
    $startWall = Get-Date
    $logicalProcessors = [Environment]::ProcessorCount
    $samples = [System.Collections.Generic.List[object]]::new()

    Start-Process explorer.exe -ArgumentList $aumid | Out-Null

    $processStarted = $false
    $stableExitCount = 0
    while ((Get-Date) -lt $deadline) {
        $now = Get-Date
        $procs = Get-Process -Name "SettingsSearchEvaluation" -ErrorAction SilentlyContinue
        $procCount = @($procs).Count

        if ($procCount -gt 0) {
            $processStarted = $true
            $stableExitCount = 0
            $cpuSec = (@($procs | Measure-Object -Property CPU -Sum).Sum)
            $wsBytes = (@($procs | Measure-Object -Property WorkingSet64 -Sum).Sum)
            $privateBytes = (@($procs | Measure-Object -Property PrivateMemorySize64 -Sum).Sum)

            if ($null -eq $cpuSec) { $cpuSec = 0.0 }
            if ($null -eq $wsBytes) { $wsBytes = 0.0 }
            if ($null -eq $privateBytes) { $privateBytes = 0.0 }

            $samples.Add([pscustomobject]@{
                TimestampUtc = $now.ToUniversalTime().ToString("o")
                CpuSeconds = [double]$cpuSec
                WorkingSetMB = [math]::Round(([double]$wsBytes / 1MB), 3)
                PrivateMB = [math]::Round(([double]$privateBytes / 1MB), 3)
                ProcessCount = $procCount
            })
        }
        elseif (Test-Path $reportPath) {
            $stableExitCount++
            if ($stableExitCount -ge 3) {
                break
            }
        }

        Start-Sleep -Milliseconds $SampleIntervalValue
    }

    if (-not (Test-Path $reportPath)) {
        throw "Timed out waiting for output report for engine '$engineName': $reportPath"
    }

    $endWall = Get-Date
    $report = Get-Content -Raw $reportPath | ConvertFrom-Json
    $engineCode = Get-EngineCode -Engine $engineName
    $engineReport = @($report.Engines | Where-Object { $_.Engine -eq $engineCode })[0]
    if ($null -eq $engineReport) {
        throw "Engine report not found for '$engineName' in $reportPath"
    }

    $firstSample = $null
    $lastSample = $null
    if ($samples.Count -gt 0) {
        $firstSample = $samples[0]
        $lastSample = $samples[$samples.Count - 1]
    }

    $elapsedSeconds = ($endWall - $startWall).TotalSeconds
    $cpuSecondsDelta = 0.0
    if ($null -ne $firstSample -and $null -ne $lastSample) {
        $cpuSecondsDelta = [double]$lastSample.CpuSeconds - [double]$firstSample.CpuSeconds
        if ($cpuSecondsDelta -lt 0) {
            $cpuSecondsDelta = 0.0
        }
    }

    $avgCpuPercent = 0.0
    if ($elapsedSeconds -gt 0 -and $logicalProcessors -gt 0) {
        $avgCpuPercent = ($cpuSecondsDelta / $elapsedSeconds / $logicalProcessors) * 100.0
    }

    $peakWorkingSet = 0.0
    $peakPrivate = 0.0
    $avgWorkingSet = 0.0
    $avgPrivate = 0.0
    if ($samples.Count -gt 0) {
        $peakWorkingSet = [double](@($samples | Measure-Object -Property WorkingSetMB -Maximum).Maximum)
        $peakPrivate = [double](@($samples | Measure-Object -Property PrivateMB -Maximum).Maximum)
        $avgWorkingSet = [double](@($samples | Measure-Object -Property WorkingSetMB -Average).Average)
        $avgPrivate = [double](@($samples | Measure-Object -Property PrivateMB -Average).Average)
    }

    ($samples | ConvertTo-Json -Depth 5) | Set-Content -Path $samplesPath -Encoding UTF8

    return [pscustomobject]@{
        Engine = $engineName
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        ReportPath = $reportPath
        SamplesPath = $samplesPath
        IsAvailable = [bool]$engineReport.IsAvailable
        CapabilitiesSummary = [string]$engineReport.CapabilitiesSummary
        AvailabilityError = [string]$engineReport.AvailabilityError
        IndexingTimeMs = [double]$engineReport.IndexingTimeMs
        RecallAtK = [double]$engineReport.RecallAtK
        Mrr = [double]$engineReport.Mrr
        QueryCount = [int]$engineReport.QueryCount
        WallClockSeconds = [math]::Round($elapsedSeconds, 3)
        CpuSeconds = [math]::Round($cpuSecondsDelta, 3)
        AvgCpuPercent = [math]::Round($avgCpuPercent, 3)
        PeakWorkingSetMB = [math]::Round($peakWorkingSet, 3)
        PeakPrivateMB = [math]::Round($peakPrivate, 3)
        AvgWorkingSetMB = [math]::Round($avgWorkingSet, 3)
        AvgPrivateMB = [math]::Round($avgPrivate, 3)
        SampleCount = $samples.Count
    }
}

$repoRoot = (Resolve-Path ".").Path
$artifacts = Join-Path $repoRoot "tools\SettingsSearchEvaluation\artifacts"
[System.IO.Directory]::CreateDirectory($artifacts) | Out-Null

$resolvedCases = (Resolve-Path $CasesJson).Path
$resolvedCorpus = (Resolve-Path $NormalizedCorpus).Path

$pkg = Get-AppxPackage Microsoft.PowerToys.SettingsSearchEvaluation
if (-not $pkg) {
    throw "Package 'Microsoft.PowerToys.SettingsSearchEvaluation' is not installed."
}

$runResults = [System.Collections.Generic.List[object]]::new()
foreach ($engine in $Engines) {
    $runResults.Add((Start-ProfiledRun `
        -Engine $engine `
        -PackageFamilyName $pkg.PackageFamilyName `
        -RepoRoot $repoRoot `
        -CasesPath $resolvedCases `
        -CorpusPath $resolvedCorpus `
        -ArtifactsDir $artifacts `
        -TopKValue $TopK `
        -MaxResultsValue $MaxResults `
        -IterationsValue $Iterations `
        -WarmupValue $Warmup `
        -SemanticTimeoutValue $SemanticTimeoutMs `
        -SampleIntervalValue $SampleIntervalMs `
        -TimeoutMinutesValue $TimeoutMinutes))
}

$comparison = [ordered]@{
    GeneratedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    CasesJson = $resolvedCases
    NormalizedCorpus = $resolvedCorpus
    Iterations = $Iterations
    Warmup = $Warmup
    MaxResults = $MaxResults
    TopK = $TopK
    SampleIntervalMs = $SampleIntervalMs
    LogicalProcessors = [Environment]::ProcessorCount
    Runs = $runResults
}

if ([string]::IsNullOrWhiteSpace($OutputJson)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputJson = Join-Path $artifacts ("resource-profile.comparison.{0}.json" -f $stamp)
}

($comparison | ConvertTo-Json -Depth 8) | Set-Content -Path $OutputJson -Encoding UTF8
Write-Host "Resource profile written to: $OutputJson"

if (-not $NoDashboard) {
    $dashboardPath = [System.IO.Path]::ChangeExtension($OutputJson, ".html")
    $rowsJson = ($runResults | ConvertTo-Json -Depth 6 -Compress)
    $html = @"
<!doctype html>
<html>
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Settings Search Resource Profile</title>
  <style>
    body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; background: #f6f8fb; color: #1f2937; }
    table { border-collapse: collapse; width: 100%; background: #fff; }
    th, td { border: 1px solid #dbe2ea; padding: 8px; font-size: 13px; text-align: left; }
    th { background: #f1f5f9; }
    h1 { margin: 0 0 12px 0; }
    .muted { color: #64748b; margin-bottom: 14px; }
  </style>
</head>
<body>
  <h1>Settings Search Resource Profile</h1>
  <div class="muted">CPU and memory comparison for evaluator runs.</div>
  <table id="t">
    <thead>
      <tr>
        <th>Engine</th>
        <th>Available</th>
        <th>Indexing ms</th>
        <th>Wall sec</th>
        <th>CPU sec</th>
        <th>Avg CPU %</th>
        <th>Peak WS MB</th>
        <th>Peak Private MB</th>
        <th>Recall@K</th>
        <th>MRR</th>
      </tr>
    </thead>
    <tbody></tbody>
  </table>
  <script>
    const rows = $rowsJson;
    const tbody = document.querySelector("#t tbody");
    for (const r of rows) {
      const tr = document.createElement("tr");
      const values = [
        r.Engine,
        r.IsAvailable,
        r.IndexingTimeMs?.toFixed(2),
        r.WallClockSeconds?.toFixed(2),
        r.CpuSeconds?.toFixed(2),
        r.AvgCpuPercent?.toFixed(2),
        r.PeakWorkingSetMB?.toFixed(2),
        r.PeakPrivateMB?.toFixed(2),
        r.RecallAtK?.toFixed(4),
        r.Mrr?.toFixed(4)
      ];
      for (const v of values) {
        const td = document.createElement("td");
        td.textContent = String(v);
        tr.appendChild(td);
      }
      tbody.appendChild(tr);
    }
  </script>
</body>
</html>
"@
    Set-Content -Path $dashboardPath -Value $html -Encoding UTF8
    Write-Host "Resource profile dashboard written to: $dashboardPath"
}
