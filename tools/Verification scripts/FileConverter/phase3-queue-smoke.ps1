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

function Test-ShutdownWithIdlePipe {
    param(
        [System.Diagnostics.Process]$Runner,
        [string]$PipeSimpleName,
        [int]$ConnectTimeoutMs
    )

    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class FileConverterSmokeRunnerWindow
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string className, string title);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
'@

    $client = [System.IO.Pipes.NamedPipeClientStream]::new(".", $PipeSimpleName, [System.IO.Pipes.PipeDirection]::Out)
    try {
        $window = [FileConverterSmokeRunnerWindow]::FindWindow("PToyTrayIconWindow", "PToyTrayIconWindow")
        [uint32]$owner = 0
        [void][FileConverterSmokeRunnerWindow]::GetWindowThreadProcessId($window, [ref]$owner)
        if ($window -eq [IntPtr]::Zero -or $owner -ne $Runner.Id) {
            throw "Could not find the tray window owned by the test Runner."
        }

        $client.Connect($ConnectTimeoutMs)
        $wmClose = 0x0010
        if (-not [FileConverterSmokeRunnerWindow]::PostMessage($window, $wmClose, [IntPtr]::Zero, [IntPtr]::Zero)) {
            throw "Could not request graceful shutdown of the test Runner."
        }

        if (-not $Runner.WaitForExit(10000)) {
            throw "Runner did not exit while an idle File Converter pipe client was connected."
        }
    }
    finally {
        $client.Dispose()
        if (-not $Runner.HasExited) {
            Stop-Process -Id $Runner.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

$powerToysExe = Join-Path $RepoRoot "x64\Debug\PowerToys.exe"
$sampleDir = Join-Path $RepoRoot "x64\Debug\WinUI3Apps\FileConverterSmokeTest"
$input1 = Join-Path $sampleDir "sample.bmp"
$input2 = Join-Path $sampleDir "sample2.bmp"
$missing = Join-Path $sampleDir "missing-does-not-exist.bmp"
$output1 = Join-Path $sampleDir "sample_converted.png"
$output2 = Join-Path $sampleDir "sample2_converted.png"
$collisionOutput = Join-Path $sampleDir "sample_converted (2).png"

if (-not (Test-Path $powerToysExe)) {
    throw "PowerToys executable not found at: $powerToysExe"
}

if (-not (Test-Path $input1)) {
    throw "Sample input file not found at: $input1"
}

Copy-Item -LiteralPath $input1 -Destination $input2 -Force
Remove-Item -LiteralPath $output1, $output2, $collisionOutput -ErrorAction SilentlyContinue

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
# Pad valid JSON to span more than one 4096-byte server read.
Send-PipePayload -PipeSimpleName $pipeSimpleName -Payload ((" " * 4096) + $payload2) -ConnectTimeoutMs $PipeConnectTimeoutMs

$deadline = [DateTime]::UtcNow.AddSeconds(10)
while ([DateTime]::UtcNow -lt $deadline) {
    if ((Test-Path $output1) -and (Test-Path $output2)) {
        break
    }

    Start-Sleep -Milliseconds 100
}

$ok1 = Test-Path $output1
$ok2 = Test-Path $output2

if (-not $ok1 -or -not $ok2) {
    if (-not $LeavePowerToysRunning) {
        Stop-PowerToysProcesses
    }
    throw "Phase 3 queue smoke failed. output1=$ok1 output2=$ok2"
}

try {
    $originalHash = (Get-FileHash -LiteralPath $output1).Hash
    $idleClient = [System.IO.Pipes.NamedPipeClientStream]::new(".", $pipeSimpleName, [System.IO.Pipes.PipeDirection]::Out)
    try {
        $idleClient.Connect($PipeConnectTimeoutMs)
        Send-PipePayload -PipeSimpleName $pipeSimpleName -Payload $payload1 -ConnectTimeoutMs ([Math]::Max(8000, $PipeConnectTimeoutMs))
    }
    finally {
        $idleClient.Dispose()
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while ([DateTime]::UtcNow -lt $deadline -and -not (Test-Path -LiteralPath $collisionOutput)) {
        Start-Sleep -Milliseconds 100
    }
    if (-not (Test-Path -LiteralPath $collisionOutput) -or (Get-FileHash -LiteralPath $output1).Hash -ne $originalHash) {
        throw "The conversion after the idle client must preserve the existing output and create a new file."
    }

    if (-not $LeavePowerToysRunning) {
        Test-ShutdownWithIdlePipe -Runner $pt -PipeSimpleName $pipeSimpleName -ConnectTimeoutMs $PipeConnectTimeoutMs
        "Runner exited while an idle File Converter pipe client was connected."
    }
}
finally {
    if (-not $LeavePowerToysRunning -and -not $pt.HasExited) {
        Stop-Process -Id $pt.Id -Force -ErrorAction SilentlyContinue
    }
}

"Phase 3 queue smoke passed. output1=$ok1 output2=$ok2"
exit 0
