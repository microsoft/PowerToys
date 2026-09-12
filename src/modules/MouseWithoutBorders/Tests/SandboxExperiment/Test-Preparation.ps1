# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Non-launch checks using only the PowerShell parser and isolated file fixtures.
.DESCRIPTION
Run from the source checkout, preferably with Windows PowerShell 5.1. Never starts
PowerToys/Sandbox, invokes UI, or reads/writes live PowerToys settings.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Experiment.Common.ps1"
$script:checks = 0
function Assert-Check {
    param([bool]$Condition, [string]$Label)
    if (-not $Condition) { throw "FAIL: $Label" }
    $script:checks++
}
function Assert-Throws {
    param([scriptblock]$Action, [string]$Pattern)
    try { & $Action; throw 'DID NOT THROW' }
    catch { Assert-Check ($_.ToString() -match $Pattern) $Pattern }
}

$scripts = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1')
foreach ($file in $scripts) {
    $tokens = $null
    $parseErrors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
    Assert-Check ($parseErrors.Count -eq 0) "Parse $($file.Name): $parseErrors"
}
$fixture = Join-Path $PSScriptRoot ('.preparation-check-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path "$fixture\settings\MouseWithoutBorders", "$fixture\endpoint", "$fixture\payload"
try {
    $moduleSettings = New-MwbExperimentSettings 'PEER' '192.0.2.1'
    foreach ($name in @('UseService', 'ShowOriginalUI', 'WrapMouse', 'ShareClipboard', 'TransferFile')) {
        Assert-Check ($moduleSettings.properties.$name.value -ceq $false) "MWB $name uses the BoolProperty value wrapper"
    }
    Assert-Check ($moduleSettings.properties.Name2IP.value -eq 'PEER 192.0.2.1') 'MWB name mapping is retained in the typed settings schema'
    Assert-Throws { & "$PSScriptRoot\Prepare-Experiment.ps1" -Destination "$fixture\forbidden" } 'outside the source checkout'
    Assert-Check (-not (Test-Path "$fixture\forbidden")) 'prepare refusal creates nothing'
    Assert-Throws { & "$PSScriptRoot\Start-Experiment.ps1" -Destination "$fixture\missing" } 'explicit -AllowNonConsole'
    $globalBytes = [byte[]](239, 187, 191, 123, 125, 13, 10)
    $moduleBytes = [byte[]](255, 254, 123, 0, 125, 0)
    [IO.File]::WriteAllBytes("$fixture\settings\settings.json", $globalBytes)
    [IO.File]::WriteAllBytes("$fixture\settings\MouseWithoutBorders\settings.json", $moduleBytes)
    Backup-EndpointSettings "$fixture\endpoint" "$fixture\settings"
    foreach ($relative in @('settings.json', 'oobe_settings.json', 'MouseWithoutBorders\settings.json')) {
        Write-ExperimentJson "$fixture\settings\$relative" @{ Changed = $true }
        $written = [IO.File]::ReadAllBytes("$fixture\settings\$relative")
        Assert-Check (-not ($written[0] -eq 239 -and $written[1] -eq 187 -and $written[2] -eq 191)) 'UTF8 without BOM'
    }
    Write-ExperimentJson "$fixture\settings\settings.json" @{ Changed = 'again' }
    Assert-Check ((Read-ExperimentJson "$fixture\settings\settings.json").Changed -eq 'again') 'shared-file rewrite'
    $held = [IO.File]::Open("$fixture\settings\settings.json", [IO.FileMode]::Open, [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite)
    try {
        Write-ExperimentJson "$fixture\settings\settings.json" @{ Changed = 'while-mapped' }
        Assert-Check ((Read-ExperimentJson "$fixture\settings\settings.json").Changed -eq 'while-mapped') 'mapped reader without delete sharing'
    }
    finally { $held.Dispose() }
    Assert-Check ((ConvertTo-ExperimentArguments @('connect', '--id', 'sample')) -eq
        '"connect" "--id" "sample"') 'native arguments quoted individually'
    Assert-Check ((ConvertTo-ExperimentArguments @('C:\with space\', 'a"b')) -eq
        '"C:\with space\\" "a\"b"') 'native quotes and trailing backslashes preserved'
    Restore-EndpointSettings "$fixture\endpoint"
    Assert-Check ([Convert]::ToBase64String([IO.File]::ReadAllBytes("$fixture\settings\settings.json")) -eq
        [Convert]::ToBase64String($globalBytes)) 'global restored byte-for-byte'
    Assert-Check ([Convert]::ToBase64String([IO.File]::ReadAllBytes("$fixture\settings\MouseWithoutBorders\settings.json")) -eq
        [Convert]::ToBase64String($moduleBytes)) 'MWB restored byte-for-byte'
    Assert-Check (-not (Test-Path "$fixture\settings\oobe_settings.json")) 'prior absence restored'
    Restore-EndpointSettings "$fixture\endpoint"
    [IO.File]::WriteAllText("$fixture\endpoint\backup\0.bin", 'corrupt')
    Assert-Throws { Restore-EndpointSettings "$fixture\endpoint" } 'Backup integrity failure'
    Assert-Check ([Convert]::ToBase64String([IO.File]::ReadAllBytes("$fixture\settings\settings.json")) -eq
        [Convert]::ToBase64String($globalBytes)) 'corrupt backup did not overwrite target'
    [IO.File]::WriteAllText("$fixture\payload\binary.bin", 'original')
    $manifest = [pscustomobject]@{ Files = @([pscustomobject]@{
        Kind = 'Product'; RelativePath = 'binary.bin'; Sha256 = (Get-FileHash "$fixture\payload\binary.bin").Hash
    }) }
    Assert-ExperimentPayload $manifest "$fixture\payload" "$fixture\payload"
    [IO.File]::WriteAllText("$fixture\payload\binary.bin", 'stale')
    Assert-Throws { Assert-ExperimentPayload $manifest "$fixture\payload" "$fixture\payload" } 'Payload changed or is missing'
    Assert-Check ((ConvertTo-MwbMachineName 'SHORT-HOST') -eq 'SHORT-HOST') 'short DNS name preserved'
    $longName = '12345678-1234-1234-1234-123456789012'
    Assert-Check ((ConvertTo-MwbMachineName $longName) -eq $longName.Substring(0, 32)) 'Sandbox name follows MWB 32-character limit'
    Assert-Throws { ConvertTo-MwbMachineName '' } 'DNS host name is required'
    $id = [Guid]::NewGuid().ToString()
    foreach ($entry in @($id, [pscustomobject]@{ Id = $id }, [pscustomobject]@{ SandboxId = $id })) {
        Assert-Check ((Get-ExperimentSandboxIds ([pscustomobject]@{ WindowsSandboxEnvironments = @($entry) })) -eq $id) 'Sandbox GUID schema'
    }
    Assert-Throws { Get-ExperimentSandboxIds ([pscustomobject]@{
        WindowsSandboxEnvironments = @([pscustomobject]@{ Unknown = $id })
    }) } 'Unrecognized wsb list schema'
    $processRecord = Get-ExperimentProcessRecord (Get-Process -Id $PID)
    Assert-Check (Test-ExperimentProcess $processRecord) 'process identity matches'
    Write-ExperimentJson "$fixture\process-record.json" $processRecord
    $processRecord = Read-ExperimentJson "$fixture\process-record.json"
    Assert-Check (Test-ExperimentProcess $processRecord) 'serialized process timestamp retains ownership'
    $childStart = New-Object Diagnostics.ProcessStartInfo
    $childStart.FileName = (Get-Process -Id $PID).MainModule.FileName
    $childStart.Arguments = '-NoLogo -NoProfile -NonInteractive -Command "Start-Sleep -Seconds 4"'
    $childStart.UseShellExecute = $false
    $childStart.CreateNoWindow = $true
    $child = [Diagnostics.Process]::Start($childStart)
    try {
        $childRecord = Get-ExperimentProcessRecord $child
        Assert-Check ($childRecord.Id -eq $child.Id -and $childRecord.Path -ieq $childStart.FileName) 'newly launched process identity becomes ready'
        $trackingRoot = Join-Path $fixture 'tracking'
        $null = New-Item -ItemType Directory -Path $trackingRoot
        Write-ExperimentJson "$trackingRoot\processes.json" @{
            ProductRoot = Split-Path $childStart.FileName
            Processes = @($processRecord)
        }
        Update-EndpointProcesses $trackingRoot
        $tracked = Read-ExperimentJson "$trackingRoot\processes.json"
        Assert-Check ($child.Id -in @($tracked.Processes | ForEach-Object Id)) 'child discovery normalizes local and UTC timestamps'
        $child.WaitForExit()
    }
    finally { $child.Dispose() }
    $processRecord.StartTimeUtc = [DateTime]::UtcNow.AddYears(-1).ToString('o')
    Assert-Check (-not (Test-ExperimentProcess $processRecord)) 'reused PID timestamp refused'
    function Test-ExperimentProcess { param($Record) return $true }
    $readinessRoot = Join-Path $fixture 'readiness'
    $null = New-Item -ItemType Directory -Path $readinessRoot
    Write-ExperimentJson "$readinessRoot\processes.json" @{ Processes = @(
        @{ Id = 1; Path = 'C:\Fixture\PowerToys.exe' }
    ) }
    Assert-Check ($null -eq (Get-EndpointSettingsProcess $readinessRoot -AllowStarting)) 'startup tolerates missing children only'
    Assert-Throws { Get-EndpointSettingsProcess $readinessRoot } 'have not both started'
    Write-ExperimentJson "$readinessRoot\processes.json" @{ Processes = @() }
    Assert-Throws { Get-EndpointSettingsProcess $readinessRoot -AllowStarting } 'Missing Runner'
    Write-ExperimentJson "$readinessRoot\processes.json" @{ Processes = @(
        @{ Id = 1; Path = 'C:\Fixture\PowerToys.exe' },
        @{ Id = 2; Path = 'C:\Fixture\PowerToys.MouseWithoutBorders.exe' },
        @{ Id = 3; Path = 'C:\Fixture\PowerToys.Settings.exe' }
    ) }
    Assert-Check ((Get-EndpointSettingsProcess $readinessRoot) -eq 3) 'complete endpoint selects recorded Settings PID'
    function Get-ExperimentDesktop { $script:desktop }
    $script:desktop = [pscustomobject]@{
        IsSystem = $false; SessionId = 5; ConsoleSessionId = 1
        WtsState = 0; InputDesktop = 'Default'; ForegroundHwnd = 123; InputAvailable = $true
    }
    Assert-Throws { Assert-ExperimentDesktop } 'explicit -AllowNonConsole'
    $null = Assert-ExperimentDesktop -AllowNonConsole
    $null = Wait-ExperimentDesktop -DeadlineUtc ([DateTime]::UtcNow.AddSeconds(1)) -AllowNonConsole
    $script:desktop.ForegroundHwnd = 0
    Assert-Check ((Assert-ExperimentDesktop -AllowNonConsole).InputAvailable) 'window focus transition does not invalidate an active input desktop'
    foreach ($field in @('IsSystem', 'SessionId', 'WtsState', 'InputDesktop', 'InputAvailable')) {
        $before = $script:desktop.$field
        $script:desktop.$field = switch ($field) {
            IsSystem { $true }
            SessionId { 0 }
            WtsState { 4 }
            InputDesktop { 'Winlogon' }
            InputAvailable { $false }
        }
        Assert-Throws { Assert-ExperimentDesktop -AllowNonConsole } 'active, unlocked default input desktop'
        $script:desktop.$field = $before
    }
    Write-Host "PASS: $($scripts.Count) scripts parsed by PowerShell $($PSVersionTable.PSVersion); $script:checks non-launch checks."
}
finally { Remove-Item -LiteralPath $fixture -Recurse -Force }
