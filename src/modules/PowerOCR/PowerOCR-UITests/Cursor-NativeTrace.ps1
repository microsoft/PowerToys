# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Configures or saves Visual Studio native cursor tracepoints for the current PowerOCR task.
.DESCRIPTION
Uses the already running Visual Studio instance. Does not build, start, stop, or attach
to a process. Run Setup before F5, reproduce briefly with the native trace launch profile,
then run Save. Save disables only this script's tracepoints and exports their output.
#>
[CmdletBinding()]
param(
    [ValidateSet('Setup', 'Save', 'Remove', 'Status')]
    [string]$Mode = 'Setup',
    [string]$VisualStudioProgId = 'VisualStudio.DTE.18.0'
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell provides the desktop COM automation support used by EnvDTE.
if ($PSVersionTable.PSEdition -eq 'Core') {
    $desktopPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    & $desktopPowerShell -NoProfile -NonInteractive -STA -ExecutionPolicy Bypass -File $PSCommandPath -Mode $Mode -VisualStudioProgId $VisualStudioProgId
    exit $LASTEXITCODE
}

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..')).TrimEnd('\', '/')
$dte = [Runtime.InteropServices.Marshal]::GetActiveObject($VisualStudioProgId)
$solutionRoot = [IO.Path]::GetDirectoryName($dte.Solution.FullName)
if (-not [string]::Equals($solutionRoot, $repoRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "The active Visual Studio solution is not this checkout: $($dte.Solution.FullName)"
}

$interopPath = Join-Path ([IO.Path]::GetDirectoryName($dte.FullName)) 'PublicAssemblies/Microsoft.VisualStudio.Interop.dll'
[void][Reflection.Assembly]::LoadFrom($interopPath)
$prefix = 'PowerOCR.CursorNativeTrace.'
# ManagedCommon.Logger deletes non-current-version directories directly under Logs.
# Keep native capture state and exports beside Logs, never inside that cleanup scope.
$logRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys\TextExtractor\CursorNativeStacks'
$statePath = Join-Path $logRoot 'VisualStudioCursorSession.json'

function Get-TracepointProperty($point, [string]$name) {
    # Preserve COM collections such as Children instead of enumerating them on return.
    return ,([EnvDTE90a.Breakpoint3].GetProperty($name).GetValue($point, $null))
}

function Set-TracepointProperty($point, [string]$name, $value) {
    [EnvDTE90a.Breakpoint3].GetProperty($name).SetValue($point, $value, $null)
}

function Get-CursorTracepoints {
    foreach ($raw in $dte.Debugger.Breakpoints) {
        $tag = Get-TracepointProperty $raw 'Tag'
        if ($tag -and $tag.StartsWith($prefix, [StringComparison]::Ordinal)) {
            Write-Output $raw
        }
    }
}

if ($Mode -eq 'Setup') {
    if ([int]$dte.Debugger.CurrentMode -ne 1) {
        throw 'Finish the current debug session before configuring cursor tracepoints.'
    }

    $platform = [EnvDTE80.SolutionConfiguration2].GetProperty('PlatformName').GetValue($dte.Solution.SolutionBuild.ActiveConfiguration, $null)
    if ($platform -ne 'x64') { throw "These RCX tracepoints require the x64 solution configuration; current platform is $platform." }

    Add-Type -AssemblyName System.Windows.Forms
    $arrow = [Windows.Forms.Cursors]::Arrow.Handle.ToInt64()
    $arrowHex = '0x{0:X}' -f $arrow
    $condition = "@rcx == $arrowHex"
    $sessionId = [Guid]::NewGuid().ToString('N')
    foreach ($point in @(Get-CursorTracepoints)) {
        $point.Delete()
    }

    $created = New-Object 'System.Collections.Generic.List[object]'
    try {
        foreach ($function in @('user32.dll!SetCursor', 'win32u.dll!NtUserSetCursor')) {
            $points = $dte.Debugger.Breakpoints.Add(
                $function, '', 1, 1, $condition,
                [EnvDTE.dbgBreakpointConditionType]::dbgBreakpointConditionTypeWhenTrue,
                'C++', '', 0, '', 0,
                [EnvDTE.dbgHitCountType]::dbgHitCountTypeNone)
            foreach ($raw in $points) {
                $created.Add($raw)
                Set-TracepointProperty $raw 'Enabled' $false
                Set-TracepointProperty $raw 'Tag' ($prefix + $sessionId)
                Set-TracepointProperty $raw 'FilterBy' 'ProcessName = "PowerToys.PowerOCR.exe"'
                $message = '[PowerOCRCursorNative] session=' + $sessionId + ' api=' + $function + ' hCursor={@rcx} pid=$PID tid=$TID tick=$TICK' + [Environment]::NewLine + '$CALLSTACK' + [Environment]::NewLine + '[/PowerOCRCursorNative]'
                Set-TracepointProperty $raw 'Message' $message
                Set-TracepointProperty $raw 'BreakWhenHit' $false
            }
        }
        New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
        [pscustomobject]@{ SessionId=$sessionId; StartedUtc=[DateTime]::UtcNow.ToString('O'); Solution=$dte.Solution.FullName; Arrow=$arrowHex } |
            ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8
        foreach ($raw in $created) { Set-TracepointProperty $raw 'Enabled' $true }
    }
    catch {
        $setupError = $_
        foreach ($raw in $created) {
            try { Set-TracepointProperty $raw 'Enabled' $false; $raw.Delete() }
            catch { Write-Warning 'Could not remove a partially configured cursor tracepoint; inspect the Breakpoints window.' }
        }
        throw $setupError
    }

    Write-Host "Configured x64 Arrow tracepoints ($arrowHex). Use the PowerOCR (native cursor trace) launch profile."
    Write-Host 'Reproduce for about 5 seconds, then Esc. Run this script with -Mode Save before starting another debugging session.'
}

if ($Mode -eq 'Save' -or $Mode -eq 'Remove') {
    foreach ($point in @(Get-CursorTracepoints)) {
        try { Set-TracepointProperty $point 'Enabled' $false }
        catch { Write-Warning "Could not disable a cursor tracepoint: $($_.Exception.Message)" }
    }

    if ($Mode -eq 'Save') {
        if (Test-Path -LiteralPath $statePath) {
            $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        }
        else {
            $sessions = @(Get-CursorTracepoints | ForEach-Object { (Get-TracepointProperty $_ 'Tag').Substring($prefix.Length) } | Select-Object -Unique)
            $recoveredId = [Guid]::Empty
            if ($sessions.Count -ne 1 -or -not [Guid]::TryParseExact($sessions[0], 'N', [ref]$recoveredId)) {
                throw 'No unique capture session was found in Visual Studio; preserve the Debug output before running Setup again.'
            }
            $state = [pscustomobject]@{ SessionId=$sessions[0]; Solution=$dte.Solution.FullName; RecoveredFromDebugger=$true }
            New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
            $state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding UTF8
        }
        $blocks = New-Object 'System.Collections.Generic.List[string]'
        $toolWindows = [EnvDTE80.DTE2].GetProperty('ToolWindows').GetValue($dte, $null)
        $outputWindow = [EnvDTE80.ToolWindows].GetProperty('OutputWindow').GetValue($toolWindows, $null)
        $panes = [EnvDTE.OutputWindow].GetProperty('OutputWindowPanes').GetValue($outputWindow, $null)
        foreach ($pane in $panes) {
            $paneGuid = [EnvDTE.OutputWindowPane].GetProperty('Guid').GetValue($pane, $null)
            if ([Guid]$paneGuid -ne [Guid]'FC076020-078A-11D1-A7DF-00A0C9110051') { continue }
            $document = [EnvDTE.OutputWindowPane].GetProperty('TextDocument').GetValue($pane, $null)
            $start = [EnvDTE.TextDocument].GetProperty('StartPoint').GetValue($document, $null)
            $end = [EnvDTE.TextDocument].GetProperty('EndPoint').GetValue($document, $null)
            $edit = [EnvDTE.TextPoint].GetMethod('CreateEditPoint').Invoke($start, @())
            $text = [EnvDTE.EditPoint].GetMethod('GetText').Invoke($edit, [object[]]@($end))
            foreach ($match in [regex]::Matches($text, '(?s)\[PowerOCRCursorNative\].*?\[/PowerOCRCursorNative\]')) {
                if ($match.Value.Contains('session=' + $state.SessionId + ' ')) { $blocks.Add($match.Value) }
            }
        }

        if ($blocks.Count -eq 0) {
            throw 'No complete cursor stack records were found. Check that native debugging is enabled and the tracepoints are bound in the Breakpoints window.'
        }

        New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
        $outputPath = Join-Path $logRoot ('CursorNativeStacks_{0:yyyyMMdd_HHmmss_fff}.log' -f [DateTime]::Now)
        $header = "Visual Studio native cursor stacks; saved=$([DateTime]::UtcNow.ToString('O')); solution=$($dte.Solution.FullName); records=$($blocks.Count). Tracepoint stops affect timing; records describe function entry, not a measured display transition."
        [IO.File]::WriteAllText($outputPath, $header + [Environment]::NewLine + [string]::Join([Environment]::NewLine, $blocks), [Text.UTF8Encoding]::new($false))
        $processMaps = @()
        foreach ($target in $dte.Debugger.DebuggedProcesses) {
            if ([IO.Path]::GetFileName($target.Name) -ne 'PowerToys.PowerOCR.exe') { continue }
            try {
                $process = Get-Process -Id $target.ProcessID -ErrorAction Stop
                $modules = @($process.Modules | ForEach-Object {
                    [pscustomobject]@{ Name=$_.ModuleName; Path=$_.FileName; BaseAddress=('0x{0:X}' -f $_.BaseAddress.ToInt64()); Size=$_.ModuleMemorySize; Version=$_.FileVersionInfo.FileVersion }
                })
                $processMaps += [pscustomobject]@{ ProcessId=$process.Id; StartTime=$process.StartTime; Modules=$modules }
            }
            catch {
                $processMaps += [pscustomobject]@{ ProcessId=$target.ProcessID; Modules=@(); Error=$_.Exception.Message }
            }
        }
        [pscustomobject]@{ SessionId=$state.SessionId; CapturedUtc=[DateTime]::UtcNow.ToString('O'); Processes=$processMaps } |
            ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($outputPath + '.modules.json') -Encoding UTF8
        if ($processMaps.Count -eq 0) { Write-Warning 'The PowerOCR target has exited or detached; module bases were unavailable. Save before stopping debugging when possible.' }
        Write-Host "Saved $($blocks.Count) stack records: $outputPath"
    }
    else {
        foreach ($point in @(Get-CursorTracepoints)) {
            $point.Delete()
        }
        Write-Host 'Removed only the PowerOCR cursor tracepoints.'
    }
}

Get-CursorTracepoints | ForEach-Object {
    $children = Get-TracepointProperty $_ 'Children'
    [pscustomobject]@{
        Name = Get-TracepointProperty $_ 'Name'
        Function = Get-TracepointProperty $_ 'FunctionName'
        Enabled = Get-TracepointProperty $_ 'Enabled'
        BreakWhenHit = Get-TracepointProperty $_ 'BreakWhenHit'
        Condition = Get-TracepointProperty $_ 'Condition'
        Filter = Get-TracepointProperty $_ 'FilterBy'
        BoundLocations = [EnvDTE.Breakpoints].GetProperty('Count').GetValue($children, $null)
        Hits = Get-TracepointProperty $_ 'CurrentHits'
        Message = Get-TracepointProperty $_ 'Message'
    }
} | ConvertTo-Json -Depth 3
