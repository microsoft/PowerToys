# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Runs tools\MonitorReportTool in the guest's interactive session and saves the report it writes.

.DESCRIPTION
Runs inside the guest in the standard user's interactive session, under PowerShell 7, launched by a
limited interactive scheduled task. MonitorReportTool is a window-less application: it appends its
report to monitor_ids.txt on the current user's desktop and exits. This wrapper waits for it, then
copies only the text this run appended, so earlier runs never leak into the result.

The report identifies each display device and monitor by the IDs Windows assigns, which tells the
virtual monitors apart by EDID. Its CCD section is not used for layout checks; positions and DPI come
from Capture-MonitorLayout.ps1.

.PARAMETER ToolPath
Guest path of PowerToys.MonitorReportTool.exe.

.PARAMETER OutputPath
Text file that receives this run's report.

.PARAMETER TimeoutSeconds
Upper bound on the tool's run.

.EXAMPLE
pwsh -NoProfile -File C:\PowerToysUiTestRun\Multimon\Invoke-MonitorReportTool.ps1 -ToolPath C:\PowerToysUiTestRun\Multimon\tools\PowerToys.MonitorReportTool.exe -OutputPath C:\PowerToysUiTestRun\Multimon\out\monitor_ids.txt
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
    [string]$ToolPath,
    [Parameter(Mandatory)]
    [string]$OutputPath,
    [ValidateRange(5, 600)]
    [int]$TimeoutSeconds = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$logPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'monitor_ids.txt'
$offset = (Test-Path -LiteralPath $logPath -PathType Leaf) ? (Get-Item -LiteralPath $logPath).Length : 0

$process = Start-Process -FilePath $ToolPath -PassThru
if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
    $process.Kill()
    throw "MonitorReportTool did not exit within $TimeoutSeconds s and was stopped."
}
if ($process.ExitCode -ne 0) {
    throw "MonitorReportTool exited $($process.ExitCode)."
}
if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
    throw "MonitorReportTool exited 0 but wrote no $logPath."
}

$stream = [System.IO.File]::Open($logPath, 'Open', 'Read', 'ReadWrite')
try {
    [void]$stream.Seek($offset, 'Begin')
    $reader = [System.IO.StreamReader]::new($stream)
    $report = $reader.ReadToEnd()
}
finally {
    $stream.Dispose()
}
if ([string]::IsNullOrWhiteSpace($report)) {
    throw "MonitorReportTool appended nothing to $logPath."
}

New-Item -ItemType Directory -Path (Split-Path -Path $OutputPath -Parent) -Force | Out-Null
Set-Content -LiteralPath $OutputPath -Value $report -Encoding utf8

[pscustomobject]@{
    OutputPath = $OutputPath
    Lines      = @($report -split "`r?`n").Count
}
