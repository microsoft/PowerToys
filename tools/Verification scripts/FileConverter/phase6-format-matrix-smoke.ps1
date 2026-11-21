param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path,
    [int]$PipeConnectTimeoutMs = 2000,
    [int]$PerCaseTimeoutMs = 6000,
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

    if (-not (Test-Path -LiteralPath $ExePath)) {
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
        [int]$Attempts = 30,
        [int]$RetryDelayMs = 100
    )

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
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
            return
        }
        catch {
            if ($attempt -eq $Attempts) {
                throw "Failed to send payload to pipe '$PipeSimpleName' after $Attempts attempts: $($_.Exception.Message)"
            }

            Start-Sleep -Milliseconds $RetryDelayMs
        }
        finally {
            $client.Dispose()
        }
    }
}

function Wait-ForFile {
    param(
        [string]$Path,
        [int]$TimeoutMs
    )

    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMs)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path) {
            return $true
        }

        Start-Sleep -Milliseconds 100
    }

    return (Test-Path -LiteralPath $Path)
}

$powerToysExe = Join-Path $RepoRoot "x64\Debug\PowerToys.exe"
$sampleDir = Join-Path $RepoRoot "x64\Debug\WinUI3Apps\FileConverterSmokeTest"
$sourcePath = Join-Path $sampleDir "sample.bmp"
$baseName = "sample_converted"

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Sample input file not found at: $sourcePath"
}

$escapedInput = $sourcePath -replace "\\", "\\\\"

$cases = @(
    @{ Name = "png";  Destination = "png";  Extension = ".png";  Required = $true },
    @{ Name = "jpg";  Destination = "jpg";  Extension = ".jpg";  Required = $true },
    @{ Name = "jpeg"; Destination = "jpeg"; Extension = ".jpg";  Required = $true },
    @{ Name = "bmp";  Destination = "bmp";  Extension = ".bmp";  Required = $true },
    @{ Name = "tif";  Destination = "tif";  Extension = ".tiff"; Required = $true },
    @{ Name = "tiff"; Destination = "tiff"; Extension = ".tiff"; Required = $true },
    @{ Name = "webp"; Destination = "webp"; Extension = ".webp"; Required = $false },
    @{ Name = "heic"; Destination = "heic"; Extension = ".heic"; Required = $false },
    @{ Name = "heif"; Destination = "heif"; Extension = ".heic"; Required = $false }
)

$results = @()

foreach ($case in $cases) {
    Stop-PowerToysProcesses
    $pt = Start-PowerToys -ExePath $powerToysExe
    $pipeSimpleName = "powertoys_fileconverter_$($pt.SessionId)"

    $outputPath = Join-Path $sampleDir ($baseName + $case.Extension)
    Remove-Item -LiteralPath $outputPath -ErrorAction SilentlyContinue

    $payload = ('{{"action":"FormatConvert","destination":"{0}","files":["{1}"]}}' -f $case.Destination, $escapedInput)
    Send-PipePayload -PipeSimpleName $pipeSimpleName -Payload $payload -ConnectTimeoutMs $PipeConnectTimeoutMs

    $created = Wait-ForFile -Path $outputPath -TimeoutMs $PerCaseTimeoutMs
    $results += [PSCustomObject]@{
        Name = $case.Name
        Destination = $case.Destination
        Output = $outputPath
        Created = $created
        Required = $case.Required
    }

    if ($case.Required -and -not $created) {
        if (-not $LeavePowerToysRunning) {
            Stop-PowerToysProcesses
        }

        throw "Phase 6 matrix smoke failed for required destination '$($case.Destination)'. Expected output '$outputPath'."
    }

    if (-not $LeavePowerToysRunning) {
        Stop-PowerToysProcesses
    }
}

Stop-PowerToysProcesses
$pt = Start-PowerToys -ExePath $powerToysExe
$pipeSimpleName = "powertoys_fileconverter_$($pt.SessionId)"

$preUnsupportedFiles = @(
    Get-ChildItem -LiteralPath $sampleDir -Filter ($baseName + ".*") -File -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Name
)
$unsupportedPayload = ('{{"action":"FormatConvert","destination":"gif","files":["{0}"]}}' -f $escapedInput)
Send-PipePayload -PipeSimpleName $pipeSimpleName -Payload $unsupportedPayload -ConnectTimeoutMs $PipeConnectTimeoutMs
Start-Sleep -Milliseconds 1500
$postUnsupportedFiles = @(
    Get-ChildItem -LiteralPath $sampleDir -Filter ($baseName + ".*") -File -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Name
)
$newUnsupportedFiles = @($postUnsupportedFiles | Where-Object { $_ -notin $preUnsupportedFiles })

if (-not $LeavePowerToysRunning) {
    Stop-PowerToysProcesses
}

if ($newUnsupportedFiles.Count -gt 0) {
    throw "Phase 6 matrix smoke failed. Unsupported destination 'gif' unexpectedly created output: $($newUnsupportedFiles -join ', ')."
}

$requiredPassed = ($results | Where-Object { $_.Required -and $_.Created }).Count
$requiredTotal = ($results | Where-Object { $_.Required }).Count
$optionalPassed = ($results | Where-Object { -not $_.Required -and $_.Created }).Count
$optionalTotal = ($results | Where-Object { -not $_.Required }).Count

"Phase 6 matrix smoke passed. Required=$requiredPassed/$requiredTotal Optional=$optionalPassed/$optionalTotal"
$results | ForEach-Object {
    " - $($_.Name): created=$($_.Created) output=$($_.Output)"
}

exit 0
