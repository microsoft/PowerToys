# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Captures the whole virtual desktop, every monitor included, to one PNG.

.DESCRIPTION
Runs inside the guest in the standard user's interactive session, under PowerShell 7, launched by a
limited interactive scheduled task. VMConnect and Get-VmConsoleImage.ps1 show only the Hyper-V
synthetic display, so this capture is how the virtual monitors are seen. Per-monitor wallpaper colors
mark each monitor's extent.

The capture needs per-monitor DPI awareness to cover the virtual desktop in physical pixels. The
script sets it on its own thread with Enable-PerMonitorDpiAwareness.ps1, because a pwsh process can
start with its awareness already fixed (see references\multimon.md). It fails when Windows refuses
the thread awareness, rather than writing a scaled image.

.PARAMETER OutputPath
PNG file to write.

.EXAMPLE
pwsh -NoProfile -File C:\PowerToysUiTestRun\Multimon\Save-VirtualScreenshot.ps1 -OutputPath C:\PowerToysUiTestRun\Multimon\out\desktop.png
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Awareness is set before System.Windows.Forms reads any metric, because the first read caches them.
# Every later statement runs on this thread, which holds the per-monitor context.
$awareness = & (Join-Path $PSScriptRoot 'Enable-PerMonitorDpiAwareness.ps1')

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$bounds = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bitmap = [System.Drawing.Bitmap]::new($bounds.Width, $bounds.Height)
try {
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
    }
    finally {
        $graphics.Dispose()
    }
    New-Item -ItemType Directory -Path (Split-Path -Path $OutputPath -Parent) -Force | Out-Null
    $bitmap.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $bitmap.Dispose()
}

[pscustomobject]@{
    OutputPath    = $OutputPath
    VirtualScreen = "$($bounds.X),$($bounds.Y) $($bounds.Width)x$($bounds.Height)"
    DpiAwareness  = $awareness
}
