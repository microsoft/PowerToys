# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Adds virtual monitors to a provisioned local UI-test guest, applies a layout, and captures a baseline.

.DESCRIPTION
Runs on the host after Initialize-LocalVmHost.ps1 has created and provisioned the guest, with the
guest running and its standard user logged on. Every installation happens inside the guest.

1. Copies the guest scripts from templates\multimon, the built IndirectDisplay sample driver,
   upstream Capture-MonitorLayout.ps1, and, when it is built, MonitorReportTool into the guest work
   root. The guest signs the driver catalog with built-in PowerShell, so no signing tool is copied.
2. Installs the driver over PowerShell Direct with Install-IddSampleDriver.ps1.
3. In the standard user's desktop: sets the pointer baseline, applies the layout preset with one
   wallpaper color per monitor, and records Capture-MonitorLayout.ps1, MonitorReportTool, and a
   full virtual-desktop screenshot.
4. Checks that the captured layout has the monitors the preset attached, as layout-applied.json
   records them, with the synthetic display as primary. Then
   it checks, with Get-SessionWindows.ps1, that Start and Search are closed, and that no guest task
   of this script still runs. Then it optionally disconnects the guest network and takes the
   multimon checkpoint through Reset-LocalVm.ps1. If the checkpoint fails, it reconnects the
   network.

Multimon.CheckpointName may hold only letters, digits, '.', '_', and '-', and must differ from
BaselineCheckpointName, because Reset-LocalVm.ps1 -CreateBaseline replaces a checkpoint of the same
name.

Settings come from the optional Multimon block in vm.config.psd1; parameters override them. See
references\multimon.md for building the driver and for what each preset exercises.

.PARAMETER VmRoot
Scaffold root holding vm.config.psd1.

.PARAMETER ConfigPath
VM configuration. Defaults to vm.config.psd1 under VmRoot.

.PARAMETER CredentialPath
DPAPI credential file for the guest administrator.

.PARAMETER DriverPath
Host folder holding the driver package Build-IddSampleDriver.ps1 writes. Overrides
Multimon.DriverPath.

.PARAMETER Preset
A preset of templates\multimon\Set-MonitorLayout.ps1 other than Single, whose checkpoint would hold
one monitor.

.PARAMETER WallpaperPath
Optional host folder of images, one per monitor in file-name order. When it is empty, the guest
generates the seven 75% color-bar colors (gray, yellow, cyan, green, magenta, red, blue).

.PARAMETER MonitorReportToolPath
Host path of PowerToys.MonitorReportTool.exe. Defaults to the Release build for the platform that
matches ProcessorArchitecture. The report is skipped when the file does not exist.

.PARAMETER EvidenceRoot
Host folder that receives this run's evidence. Defaults to evidence\multimon\<preset> under VmRoot.
Each run deletes the folder and rebuilds it, so it must be inside evidence\multimon under VmRoot.

.PARAMETER SkipCheckpoint
Applies and records the layout without the pre-checkpoint checks, the network disconnect, or the
checkpoint.

.PARAMETER DiagnoseWindows
Records every visible window and console process in the standard user's session with
Get-SessionWindows.ps1: one snapshot before the first interactive step and after each one, as
windows-NN-<step>.json in the evidence folder, plus a watcher that samples the whole sequence every
250 ms, as diag\windows-watch.json. Use it when desktop.png shows a window nobody opened.

.EXAMPLE
pwsh .github\skills\ui-tests-local-vm\scripts\Initialize-LocalVmMultimon.ps1 -VmRoot X:\PowerToysUiTestVm -WhatIf
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$VmRoot,
    [string]$ConfigPath,
    [ValidateNotNullOrEmpty()]
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'),
    [string]$DriverPath,
    [string]$Preset = '',
    [string]$WallpaperPath,
    [string]$MonitorReportToolPath,
    [string]$EvidenceRoot,
    [switch]$SkipCheckpoint,
    [switch]$DiagnoseWindows
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module -Name (Join-Path $PSScriptRoot 'LocalVmGuest.psm1') -Force
Import-Module -Name (Join-Path $PSScriptRoot 'LocalVmMultimon.psm1') -Force

$vmRootPath = [System.IO.Path]::GetFullPath($VmRoot)
if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $vmRootPath 'vm.config.psd1'
}
if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "Configuration was not found: $ConfigPath"
}
$configuration = Import-PowerShellDataFile -LiteralPath $ConfigPath
$multimon = $configuration.ContainsKey('Multimon') ? $configuration.Multimon : @{}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$guestTemplates = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\templates\multimon'))
$captureLayoutScript = Join-Path $repositoryRoot 'src\modules\MouseUtils\CursorWrap\CursorWrapTests\Capture-MonitorLayout.ps1'

$vmName = [string]$configuration.VmName
$standardUser = [string]$configuration.StandardUser
$driverPackage = Get-MultimonDriverPackage -Configuration $configuration -VmRoot $vmRootPath -Override $DriverPath
$driverRoot = $driverPackage.Path
$layoutPreset = Get-MultimonSetting -Configuration $configuration -Name 'Preset' -Override $Preset -Default 'SideBySide'
$wallpaperSource = Get-MultimonSetting -Configuration $configuration -Name 'WallpaperPath' -Override $WallpaperPath -Default ''
$platform = Get-MultimonPlatform -Configuration $configuration
$defaultReportTool = Join-Path $repositoryRoot "$platform\Release\MonitorReportTool\PowerToys.MonitorReportTool.exe"
$reportTool = Get-MultimonSetting -Configuration $configuration -Name 'MonitorReportToolPath' -Override $MonitorReportToolPath -Default $defaultReportTool
$checkpointName = Get-MultimonSetting -Configuration $configuration -Name 'CheckpointName' -Override '' -Default 'multimon-baseline'

# Single detaches every virtual monitor, so its checkpoint would hold no multi-monitor layout.
$presets = @(Get-MultimonPreset | Where-Object { $_ -ne 'Single' })
if ($layoutPreset -notin $presets) {
    throw "Preset '$layoutPreset', from -Preset or Multimon.Preset in $ConfigPath, is not one of $($presets -join ', ')."
}

# Reset-LocalVm.ps1 -CreateBaseline replaces a checkpoint of the same name, and Get-VMCheckpoint reads
# the name as a wildcard pattern.
if ($checkpointName -notmatch '^[A-Za-z0-9._-]+\z') {
    throw "Multimon.CheckpointName '$checkpointName' in $ConfigPath may hold only letters, digits, '.', '_', and '-'."
}
if ($checkpointName -eq [string]$configuration.BaselineCheckpointName) {
    throw "Multimon.CheckpointName '$checkpointName' in $ConfigPath names the provisioning baseline, which the multimon checkpoint would replace. Choose another name."
}

$disconnectNetwork = $false
if ($multimon.ContainsKey('DisconnectNetwork')) {
    if ($multimon.DisconnectNetwork -isnot [bool]) {
        throw "Multimon.DisconnectNetwork in $ConfigPath is '$($multimon.DisconnectNetwork)'. Write `$true or `$false."
    }
    $disconnectNetwork = $multimon.DisconnectNetwork
}

# The evidence folder is deleted and rebuilt on every run, so it must sit inside the scaffold's
# multimon evidence folder.
$evidenceParent = Join-Path $vmRootPath 'evidence\multimon'
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    $EvidenceRoot = Join-Path $evidenceParent $layoutPreset
}
$EvidenceRoot = [System.IO.Path]::GetFullPath($EvidenceRoot).TrimEnd('\')
if (-not $EvidenceRoot.StartsWith("$evidenceParent\", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "EvidenceRoot $EvidenceRoot is not inside $evidenceParent. The script deletes and rebuilds the evidence folder, so it accepts only a folder there."
}

$driverFiles = $driverPackage.Files
foreach ($required in @(@($driverFiles | ForEach-Object { Join-Path $driverRoot $_ }) + $captureLayoutScript)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required file is missing: $required. See references\multimon.md."
    }
}

if (-not (Test-HyperVAccess)) {
    throw (Get-HyperVAccessMessage)
}
$vm = Get-VM -Name $vmName -ErrorAction SilentlyContinue
if ($null -eq $vm) {
    throw "Virtual machine '$vmName' does not exist. Create it with Initialize-LocalVmHost.ps1."
}
if ($vm.State -ne 'Running') {
    throw "'$vmName' is $($vm.State). Start it with Start-LocalVm.ps1 -Wait so the standard user is logged on."
}
$credential = Import-Clixml -LiteralPath $CredentialPath

$guestPaths = Get-MultimonGuestPath
$guestRoot = $guestPaths.Root
$guestOutput = $guestPaths.Output

$payload = [System.Collections.Generic.List[object]]::new()
foreach ($file in Get-ChildItem -LiteralPath $guestTemplates -Filter '*.ps1' -File) {
    $payload.Add([pscustomobject]@{ Source = $file.FullName; Destination = "$guestRoot\$($file.Name)" })
}
# The unsigned build goes to a staging folder. The install script signs a copy in driver and keeps
# that copy while the build is unchanged.
foreach ($name in $driverFiles) {
    $payload.Add([pscustomobject]@{ Source = (Join-Path $driverRoot $name); Destination = "$guestRoot\driver-source\$name" })
}
$reportToolName = Split-Path -Path $reportTool -Leaf
$hasReportTool = Test-Path -LiteralPath $reportTool -PathType Leaf
if ($hasReportTool) {
    $payload.Add([pscustomobject]@{ Source = $reportTool; Destination = "$guestRoot\tools\$reportToolName" })
}
else {
    Write-Warning "MonitorReportTool was not found at $reportTool; its report is skipped. See references\multimon.md to build it."
}
$guestWallpapers = ''
if (-not [string]::IsNullOrWhiteSpace($wallpaperSource)) {
    $guestWallpapers = "$guestRoot\wallpapers-source"
    foreach ($file in Get-ChildItem -LiteralPath $wallpaperSource -File) {
        $payload.Add([pscustomobject]@{ Source = $file.FullName; Destination = "$guestWallpapers\$($file.Name)" })
    }
}

# Set-MonitorLayout.ps1 uses every image in the folder in file-name order, so a file left from an
# earlier run would shift the assignment.
$clearWallpapers = -not [string]::IsNullOrWhiteSpace($guestWallpapers)
if ($clearWallpapers -and $PSCmdlet.ShouldProcess("${vmName}:$guestWallpapers", 'Empty the guest wallpaper folder')) {
    Invoke-Command -VMName $vmName -Credential $credential -ArgumentList $guestWallpapers -ScriptBlock {
        param($Folder)
        if (Test-Path -LiteralPath $Folder) {
            Get-ChildItem -LiteralPath $Folder -Force | Remove-Item -Recurse -Force
        }
    }
}

foreach ($item in $payload) {
    if ($PSCmdlet.ShouldProcess("${vmName}:$($item.Destination)", 'Copy-VMFile')) {
        Copy-VMFile -Name $vmName -SourcePath $item.Source -DestinationPath $item.Destination -FileSource Host -CreateFullPath -Force
    }
}

if ($PSCmdlet.ShouldProcess($vmName, 'Install the IndirectDisplay sample driver and create its root device')) {
    $install = Invoke-Command -VMName $vmName -Credential $credential `
        -FilePath (Join-Path $guestTemplates 'Install-IddSampleDriver.ps1') `
        -ArgumentList "$guestRoot\driver-source", "$guestRoot\driver", $driverPackage.Name
    $install | Format-List | Out-String | Write-Verbose
    if ($install.RebootRequired) {
        throw 'Windows requested a restart to finish the driver installation. Run Stop-LocalVm.ps1, then Start-LocalVm.ps1 -Wait, then rerun this script; the installation steps it already completed are skipped.'
    }
}

# Display settings, wallpapers, and screenshots belong to the standard user's session, so every
# remaining guest step runs as an interactive task.
$interactive = @{
    VmName         = $vmName
    HostOutputRoot = $EvidenceRoot
    CredentialPath = $CredentialPath
    StandardUser   = $standardUser
    GuestRoot      = $guestRoot
}
$interactiveScript = Join-Path $PSScriptRoot 'Invoke-LocalVmInteractive.ps1'
$sessionWindowsScript = Join-Path $guestTemplates 'Get-SessionWindows.ps1'
$layoutArguments = @('-Preset', $layoutPreset, '-WorkRoot', $guestRoot, '-ReportPath', "$guestOutput\layout-applied.json")
if (-not [string]::IsNullOrWhiteSpace($guestWallpapers)) {
    $layoutArguments += @('-WallpaperPath', $guestWallpapers)
}

$diagnose = $DiagnoseWindows.IsPresent
$diagnosisIndex = 0
function Invoke-WindowDiagnosis {
    param([Parameter(Mandatory)][string]$Step)

    if (-not $diagnose) {
        return
    }
    $script:diagnosisIndex++
    $snapshotName = 'windows-{0:D2}-{1}.json' -f $script:diagnosisIndex, $Step
    & $interactiveScript @interactive -ScriptPath $sessionWindowsScript -DpiReportOnly `
        -ScriptArguments '-Label', $Step, '-OutputPath', "$guestOutput\$snapshotName" | Out-Null
}

# A snapshot between steps misses a window that lives only while a step runs, so a watcher samples the
# whole sequence. Its first-seen times, read against the snapshots' CapturedAt times (both guest
# clock), place each window inside one step.
$guestDiag = $guestPaths.Diag
$watchStopFile = "$guestDiag\stop"
$watchRunName = 'Get-SessionWindows-watch'
# Set by Open-WindowWatch to what Invoke-LocalVmInteractive.ps1 -NoWait returns: the task name and
# the guest paths of its exit code and log.
$windowWatch = $null

function Open-WindowWatch {
    if (-not $diagnose) {
        return
    }
    Invoke-Command -VMName $vmName -Credential $credential -ArgumentList $guestDiag -ScriptBlock {
        param($Diag)
        New-Item -ItemType Directory -Path $Diag -Force | Out-Null
        Get-ChildItem -LiteralPath $Diag -Force | Remove-Item -Recurse -Force
    }
    $watchArguments = @(
        '-Label', 'run', '-WatchSeconds', '1800', '-StopFile', $watchStopFile, '-OutputPath', "$guestDiag\windows-watch.json"
    )
    $script:windowWatch = & $interactiveScript @interactive -ScriptPath $sessionWindowsScript -RunName $watchRunName `
        -NoWait -TimeoutSeconds 1800 -DpiReportOnly -ScriptArguments $watchArguments
}

function Close-WindowWatch {
    if (-not $diagnose) {
        return
    }
    # The wrapper can fail after the watcher started and before it returns the object, for example on
    # the round trip that reads the started task back. The same names then come from the module.
    $watch = $script:windowWatch
    if ($null -eq $watch) {
        $watch = Get-MultimonRun -Root $guestRoot -RunName $watchRunName -NoWait
    }
    $closeArguments = @($watchStopFile, $watch.ExitCodePath, $watch.Task)
    $watchExitCode = [string](Invoke-Command -VMName $vmName -Credential $credential -ArgumentList $closeArguments -ScriptBlock {
            param($Stop, $ExitCodePath, $TaskName)
            New-Item -ItemType File -Path $Stop -Force | Out-Null
            # The wait ends early when no watcher runs, so a watcher that never started costs nothing.
            $deadline = (Get-Date).AddSeconds(60)
            while (-not (Test-Path -LiteralPath $ExitCodePath -PathType Leaf) -and (Get-Date) -lt $deadline -and
                @(Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue | Where-Object { $_.State -in 'Running', 'Queued' }).Count -gt 0) {
                Start-Sleep -Milliseconds 500
            }
            # Unregistering alone leaves a running instance alive, so a watcher that ignored the stop
            # file is ended first.
            Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
            Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false -ErrorAction SilentlyContinue
            if (Test-Path -LiteralPath $ExitCodePath -PathType Leaf) {
                (Get-Content -LiteralPath $ExitCodePath -Raw).Trim()
            }
            else {
                ''
            }
        })
    $hostDiag = Join-Path $EvidenceRoot 'diag'
    New-Item -ItemType Directory -Path $hostDiag -Force | Out-Null
    $session = New-PSSession -VMName $vmName -Credential $credential
    try {
        # Stop-ScheduledTask ends only the watcher's console host. A watcher pwsh that ignored the
        # stop file would keep its log open through the copy and run on into the checkpoint.
        try {
            $tree = Stop-MultimonRunProcess -Session $session -LogPath $watch.LogPath
            $problems = @(@($tree.Failures) + @(
                    if ($tree.LogLocked) { "$($watch.LogPath) is still open for writing" }
                    if (-not [string]::IsNullOrEmpty($tree.LogError)) { "checking $($watch.LogPath) failed: $($tree.LogError)" }
                ))
            if ($problems.Count -gt 0) {
                Write-Warning "Ending the window watcher's processes left a problem: $($problems -join '; ')."
            }
        }
        catch {
            Write-Warning "Ending the window watcher's processes failed: $($_.Exception.Message)"
        }
        Copy-Item -Path "$guestDiag\*" -Destination $hostDiag -FromSession $session -Recurse -Force
    }
    finally {
        Remove-PSSession -Session $session
    }
    if ($watchExitCode -ne '0') {
        $watchLog = Join-Path $hostDiag (Split-Path -Path $watch.LogPath -Leaf)
        Write-Warning "The window watcher ended with exit code '$watchExitCode'. Its log is $watchLog."
    }
}

if ($PSCmdlet.ShouldProcess($vmName, "Apply the $layoutPreset layout and record it")) {
    if (Test-Path -LiteralPath $EvidenceRoot) {
        Remove-Item -LiteralPath $EvidenceRoot -Recurse -Force
    }
    # The watcher runs for up to 30 minutes on its own, so it is closed even when a step fails.
    try {
        Open-WindowWatch
        Invoke-WindowDiagnosis -Step 'before'
        & $interactiveScript @interactive -ScriptPath (Join-Path $guestTemplates 'Set-PointerBaseline.ps1') | Out-Null
        Invoke-WindowDiagnosis -Step 'after-pointer'
        & $interactiveScript @interactive -ScriptPath (Join-Path $guestTemplates 'Set-MonitorLayout.ps1') `
            -ScriptArguments $layoutArguments | Out-Null
        Invoke-WindowDiagnosis -Step 'after-layout'
        & $interactiveScript @interactive -ScriptPath $captureLayoutScript `
            -ScriptArguments '-OutputPath', "$guestOutput\cursorwrap_monitor_layout.json", '-AddDeviceNames' | Out-Null
        Invoke-WindowDiagnosis -Step 'after-capture'
        if ($hasReportTool) {
            $reportArguments = @('-ToolPath', "$guestRoot\tools\$reportToolName", '-OutputPath', "$guestOutput\monitor_ids.txt")
            & $interactiveScript @interactive -ScriptPath (Join-Path $guestTemplates 'Invoke-MonitorReportTool.ps1') `
                -ScriptArguments $reportArguments | Out-Null
            Invoke-WindowDiagnosis -Step 'after-report'
        }
        & $interactiveScript @interactive -ScriptPath (Join-Path $guestTemplates 'Save-VirtualScreenshot.ps1') `
            -ScriptArguments '-OutputPath', "$guestOutput\desktop.png" | Out-Null
        Invoke-WindowDiagnosis -Step 'after-screenshot'
    }
    finally {
        # A failure here must not hide the step's own error. The checkpoint step refuses while the
        # watcher still runs.
        try {
            Close-WindowWatch
        }
        catch {
            Write-Warning "Closing the window watcher failed: $($_.Exception.Message)"
        }
    }
}

# Checkpoint only a guest whose captured layout has the expected shape: the monitors
# layout-applied.json lists, which Set-MonitorLayout.ps1 checked against the preset it applied.
# Windows always places the primary display at (0,0), so the check compares the captured primary with
# the one the layout set. Under -WhatIf no layout was captured, and a file from an earlier run proves
# nothing.
$monitorCount = $null
if (-not $WhatIfPreference) {
    $layoutReport = Join-Path $EvidenceRoot 'cursorwrap_monitor_layout.json'
    $appliedReport = Join-Path $EvidenceRoot 'layout-applied.json'
    foreach ($report in $layoutReport, $appliedReport) {
        if (-not (Test-Path -LiteralPath $report -PathType Leaf)) {
            throw "No layout record at $report. Not checkpointing a guest whose layout is unconfirmed."
        }
    }
    $layout = Get-Content -LiteralPath $layoutReport -Raw | ConvertFrom-Json
    $applied = Get-Content -LiteralPath $appliedReport -Raw | ConvertFrom-Json
    $plannedMonitors = @($applied.Monitors)
    $primaries = @($layout.monitors | Where-Object { $_.primary })
    $plannedPrimaries = @($plannedMonitors | Where-Object { $_.Role -eq 'Primary' })
    $capturedPrimary = ($primaries | ForEach-Object { $_.device_name }) -join ' '
    $plannedPrimary = ($plannedPrimaries | ForEach-Object { $_.Device }) -join ' '
    if ($layout.monitor_count -ne $plannedMonitors.Count -or $primaries.Count -ne 1 -or $plannedPrimaries.Count -ne 1 -or $capturedPrimary -ne $plannedPrimary) {
        throw "The captured layout has $($layout.monitor_count) monitors with primary '$capturedPrimary'; the $layoutPreset layout applied $($plannedMonitors.Count) with primary '$plannedPrimary', the synthetic display. Inspect $EvidenceRoot\desktop.png."
    }
    $monitorCount = $layout.monitor_count
}

$disconnectedAdapters = @()
if (-not $SkipCheckpoint) {
    if ($PSCmdlet.ShouldProcess($vmName, 'Check the guest desktop and tasks before the checkpoint')) {
        # Windows opens Start once when the standard user's first sign-in completes, which a guest
        # restored from provisioned-baseline can still be doing. A checkpoint taken then restores with
        # Start open over every test, so the checkpoint waits for a desktop with Start and Search closed.
        & $interactiveScript @interactive -ScriptPath $sessionWindowsScript -DpiReportOnly `
            -ScriptArguments '-Label', 'before-checkpoint', '-OutputPath', "$guestOutput\windows-before-checkpoint.json" | Out-Null
        $surfaceReport = Join-Path $EvidenceRoot 'windows-before-checkpoint.json'
        $surfaces = Get-Content -LiteralPath $surfaceReport -Raw | ConvertFrom-Json
        $openSurfaces = @(
            if ($surfaces.StartOpen) { 'Start' }
            if ($surfaces.SearchOpen) { 'Search' }
        )
        if ($openSurfaces.Count -gt 0) {
            $verb = if ($openSurfaces.Count -gt 1) { 'are' } else { 'is' }
            throw "BLOCKED: $($openSurfaces -join ' and ') $verb open on the $vmName desktop, so '$checkpointName' would restore with it over the tests. The usual cause is a standard user whose first sign-in had not completed. Restore provisioned-baseline with Reset-LocalVm.ps1 -Restore, restart the guest once with Stop-LocalVm.ps1 and then Start-LocalVm.ps1 -Wait, and rerun this script. The window record is $surfaceReport."
        }

        # A checkpoint saves memory, so a guest task still running would resume in every restore.
        $runningTasks = @(Invoke-Command -VMName $vmName -Credential $credential -ArgumentList (Get-MultimonTaskName -RunName '*') -ScriptBlock {
                param($Pattern)
                Get-ScheduledTask -TaskName $Pattern -ErrorAction SilentlyContinue |
                    Where-Object { $_.State -in 'Running', 'Queued' } |
                    ForEach-Object { $_.TaskName }
            })
        if ($runningTasks.Count -gt 0) {
            throw "BLOCKED: guest tasks $($runningTasks -join ', ') are still running, so '$checkpointName' would restore with them running. End them in the guest with Stop-ScheduledTask and Unregister-ScheduledTask, or restart the guest with Stop-LocalVm.ps1 and Start-LocalVm.ps1 -Wait, then rerun this script."
        }
    }
    if ($disconnectNetwork -and $PSCmdlet.ShouldProcess($vmName, 'Disconnect every network adapter before the checkpoint')) {
        # The switch names are kept so a failed checkpoint can reconnect each adapter.
        $disconnectedAdapters = @(Get-VMNetworkAdapter -VMName $vmName |
                Where-Object { -not [string]::IsNullOrEmpty($_.SwitchName) } |
                ForEach-Object { [pscustomobject]@{ Adapter = $_; SwitchName = $_.SwitchName } })
        foreach ($entry in $disconnectedAdapters) {
            Disconnect-VMNetworkAdapter -VMNetworkAdapter $entry.Adapter
        }
    }
    if ($PSCmdlet.ShouldProcess($vmName, "Create checkpoint '$checkpointName'")) {
        try {
            & ([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\templates\vm\Reset-LocalVm.ps1'))) `
                -ConfigPath $ConfigPath -CreateBaseline -CheckpointName $checkpointName | Out-Null
        }
        catch {
            $checkpointError = $_.Exception.Message
            $networkState = ''
            if ($disconnectedAdapters.Count -gt 0) {
                try {
                    foreach ($entry in $disconnectedAdapters) {
                        Connect-VMNetworkAdapter -VMNetworkAdapter $entry.Adapter -SwitchName $entry.SwitchName
                    }
                    $networkState = ' The network adapters were reconnected.'
                }
                catch {
                    $switches = ($disconnectedAdapters | ForEach-Object { "$($_.Adapter.Name) to $($_.SwitchName)" }) -join ', '
                    $networkState = " Reconnecting the network adapters failed ($($_.Exception.Message)), so the guest is offline. Reconnect them with Connect-VMNetworkAdapter: $switches."
                }
            }
            throw "Creating checkpoint '$checkpointName' failed: $checkpointError$networkState"
        }
    }
}

[pscustomobject]@{
    VmName              = $vmName
    Preset              = $layoutPreset
    Monitors            = $monitorCount
    Evidence            = $EvidenceRoot
    NetworkDisconnected = $disconnectedAdapters.Count -gt 0
    Checkpoint          = $SkipCheckpoint ? $null : $checkpointName
}
