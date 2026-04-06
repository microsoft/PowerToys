param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path,
    [int]$PipeConnectTimeoutMs = 1000,
    [int]$SendAttempts = 20,
    [switch]$LeavePowerToysRunning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Stop-PowerToysProcesses {
    Get-Process PowerToys, PowerToys.Settings, PowerToys.QuickAccess -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

function Start-PowerToys {
    param(
        [string]$ExePath
    )

    if (-not (Test-Path $ExePath)) {
        throw "PowerToys executable not found at: $ExePath"
    }

    $proc = Start-Process -FilePath $ExePath -PassThru
    Wait-Process -Id $proc.Id -Timeout 2 -ErrorAction SilentlyContinue | Out-Null

    $running = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    if ($null -eq $running) {
        throw "PowerToys process exited before pipe checks."
    }

    return $running
}

function Send-PipePayload {
    param(
        [string]$PipeSimpleName,
        [string]$Payload,
        [int]$ConnectTimeoutMs,
        [int]$Attempts
    )

    for ($i = 0; $i -lt $Attempts; $i++) {
        $client = [System.IO.Pipes.NamedPipeClientStream]::new(
            ".",
            $PipeSimpleName,
            [System.IO.Pipes.PipeDirection]::Out
        )

        try {
            $client.Connect($ConnectTimeoutMs)

            $bytes = [System.Text.Encoding]::UTF8.GetBytes($Payload)
            $client.Write($bytes, 0, $bytes.Length)
            return $true
        }
        catch {
            if ($i -lt ($Attempts - 1)) {
                Start-Sleep -Milliseconds 100
            }
        }
        finally {
            $client.Dispose()
        }
    }

    return $false
}

$powerToysExe = Join-Path $RepoRoot "x64\Debug\PowerToys.exe"
$sampleInput = Join-Path $RepoRoot "x64\Debug\WinUI3Apps\FileConverterSmokeTest\sample.bmp"
$outputFile = Join-Path $RepoRoot "x64\Debug\WinUI3Apps\FileConverterSmokeTest\sample_converted.png"
$runnerLog = Join-Path $RepoRoot "src\runner\x64\Debug\runner.log"

if (-not (Test-Path $sampleInput)) {
    throw "Sample input file not found at: $sampleInput"
}

$escapedInput = $sampleInput -replace "\\", "\\\\"

$cases = @(
    [pscustomobject]@{
        Name = "invalid-json"
        Payload = "not-json"
    },
    [pscustomobject]@{
        Name = "missing-files"
        Payload = '{"action":"FormatConvert","destination":"png"}'
    },
    [pscustomobject]@{
        Name = "wrong-action"
        Payload = ('{{"action":"NoOp","destination":"png","files":["{0}"]}}' -f $escapedInput)
    },
    [pscustomobject]@{
        Name = "bad-files-array"
        Payload = '{"action":"FormatConvert","destination":"png","files":[123,""]}'
    }
)

$results = @()

for ($caseIndex = 0; $caseIndex -lt $cases.Count; $caseIndex++) {
    $case = $cases[$caseIndex]

    Stop-PowerToysProcesses
    $pt = Start-PowerToys -ExePath $powerToysExe
    $pipeSimpleName = "powertoys_fileconverter_$($pt.SessionId)"

    if (Test-Path $outputFile) {
        Remove-Item $outputFile -Force
    }

    $sent = Send-PipePayload `
        -PipeSimpleName $pipeSimpleName `
        -Payload $case.Payload `
        -ConnectTimeoutMs $PipeConnectTimeoutMs `
        -Attempts $SendAttempts

    $deadline = [DateTime]::UtcNow.AddSeconds(2)
    while ([DateTime]::UtcNow -lt $deadline -and -not (Test-Path $outputFile)) {
        Start-Sleep -Milliseconds 50
    }

    $createdOutput = Test-Path $outputFile
    if ($createdOutput) {
        Remove-Item $outputFile -Force
    }

    $results += [pscustomobject]@{
        Case = $case.Name
        SentToPipe = $sent
        OutputCreated = $createdOutput
        Passed = ($sent -and -not $createdOutput)
    }

    if (-not $LeavePowerToysRunning -or $caseIndex -lt ($cases.Count - 1)) {
        Stop-PowerToysProcesses
    }
}

"Negative FileConverter Pipe Smoke Results"
$results | Format-Table -AutoSize | Out-String

if (Test-Path $runnerLog) {
    $interesting = Select-String -Path $runnerLog -Pattern "File Converter|malformed request|skipped|conversion failed" -CaseSensitive:$false -ErrorAction SilentlyContinue
    if ($interesting) {
        "Recent listener diagnostics from runner.log"
        $interesting | Select-Object -Last 20 | ForEach-Object { $_.Line }
    }
    else {
        "No matching listener diagnostics found in runner.log."
    }
}
else {
    "runner.log not found; diagnostics may be routed through ETW."
}

if (-not $LeavePowerToysRunning) {
    Stop-PowerToysProcesses
}

$failed = @($results | Where-Object { -not $_.Passed })
if ($failed.Count -gt 0) {
    Write-Error "One or more negative smoke cases failed."
    exit 1
}

"All negative smoke cases passed."
exit 0
