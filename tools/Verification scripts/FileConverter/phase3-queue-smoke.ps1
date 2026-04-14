param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path,
    [int]$PipeConnectTimeoutMs = 2000,
    [switch]$LeavePowerToysRunning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Stop-PowerToysProcesses {
    Get-Process PowerToys, PowerToys.Settings, PowerToys.QuickAccess -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

function Send-PipePayload {
    param(
        [string]$PipeSimpleName,
        [string]$Payload,
        [int]$ConnectTimeoutMs
    )

    $client = [System.IO.Pipes.NamedPipeClientStream]::new(
        ".",
        $PipeSimpleName,
        [System.IO.Pipes.PipeDirection]::Out
    )

    try {
        $client.Connect($ConnectTimeoutMs)
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Payload)
        $client.Write($bytes, 0, $bytes.Length)
        $client.Flush()
    }
    finally {
        $client.Dispose()
    }
}

$powerToysExe = Join-Path $RepoRoot "x64\Debug\PowerToys.exe"
$sampleDir = Join-Path $RepoRoot "x64\Debug\WinUI3Apps\FileConverterSmokeTest"
$input1 = Join-Path $sampleDir "sample.bmp"
$input2 = Join-Path $sampleDir "sample2.bmp"
$missing = Join-Path $sampleDir "missing-does-not-exist.bmp"
$output1 = Join-Path $sampleDir "sample_converted.png"
$output2 = Join-Path $sampleDir "sample2_converted.png"

if (-not (Test-Path $powerToysExe)) {
    throw "PowerToys executable not found at: $powerToysExe"
}

if (-not (Test-Path $input1)) {
    throw "Sample input file not found at: $input1"
}

Copy-Item -LiteralPath $input1 -Destination $input2 -Force
Remove-Item $output1, $output2 -ErrorAction SilentlyContinue

Stop-PowerToysProcesses
$pt = Start-Process -FilePath $powerToysExe -PassThru
Start-Sleep -Milliseconds 250
$pt = Get-Process -Id $pt.Id -ErrorAction Stop
$pipeSimpleName = "powertoys_fileconverter_$($pt.SessionId)"

$escapedInput1 = $input1 -replace "\\", "\\\\"
$escapedInput2 = $input2 -replace "\\", "\\\\"
$escapedMissing = $missing -replace "\\", "\\\\"

$payload1 = ('{{"action":"FormatConvert","destination":"png","files":["{0}","{1}"]}}' -f $escapedInput1, $escapedMissing)
$payload2 = ('{{"action":"FormatConvert","destination":"png","files":["{0}"]}}' -f $escapedInput2)

Send-PipePayload -PipeSimpleName $pipeSimpleName -Payload $payload1 -ConnectTimeoutMs $PipeConnectTimeoutMs
Send-PipePayload -PipeSimpleName $pipeSimpleName -Payload $payload2 -ConnectTimeoutMs $PipeConnectTimeoutMs

$deadline = [DateTime]::UtcNow.AddSeconds(10)
while ([DateTime]::UtcNow -lt $deadline) {
    if ((Test-Path $output1) -and (Test-Path $output2)) {
        break
    }

    Start-Sleep -Milliseconds 100
}

$ok1 = Test-Path $output1
$ok2 = Test-Path $output2

if (-not $LeavePowerToysRunning) {
    Stop-PowerToysProcesses
}

if (-not $ok1 -or -not $ok2) {
    throw "Phase 3 queue smoke failed. output1=$ok1 output2=$ok2"
}

"Phase 3 queue smoke passed. output1=$ok1 output2=$ok2"
exit 0
