# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Register', 'Unregister', 'Status')]
    [string] $Action,

    [Parameter(Mandatory = $true)]
    [string] $TryRunDirectory
)

$ErrorActionPreference = 'Stop'
$tryRunRoot = (Resolve-Path -LiteralPath $TryRunDirectory).ProviderPath
$tryRunBroker = Join-Path $tryRunRoot 'Explorer/PowerToys.TryRun.Explorer.exe'
if (-not (Test-Path -LiteralPath $tryRunBroker -PathType Leaf)) {
    throw 'Build TryRun.slnx first. The Explorer helper is missing.'
}
$tryRunBroker = (Resolve-Path -LiteralPath $tryRunBroker).ProviderPath

# Obtain the desktop's Shell application through a running Explorer window.
# A newly created Shell.Application object can retain the caller's virtualized
# registration view. The desktop object starts the helper in Explorer's context.
$tryRunShell = New-Object -ComObject Shell.Application
$tryRunShellWindows = $tryRunShell.Windows()
$tryRunExplorer = @($tryRunShellWindows) | Where-Object {
    $_.FullName -eq (Join-Path $env:WINDIR 'explorer.exe')
} | Select-Object -First 1
if ($null -eq $tryRunExplorer) {
    throw 'Open a File Explorer window, then run this command again.'
}

$tryRunOperation = '--' + $Action.ToLowerInvariant()
$tryRunCorrelation = [guid]::NewGuid().ToString('N')
$tryRunReceipt = Join-Path (Split-Path $tryRunBroker) ('TryRun-registration-' + $tryRunCorrelation + '.json')
$tryRunDesktopShell = $tryRunExplorer.Document.Application
$tryRunDesktopShell.ShellExecute($tryRunBroker, "$tryRunOperation $tryRunCorrelation", (Split-Path $tryRunBroker), 'open', 0)
$tryRunDeadline = [DateTime]::UtcNow.AddSeconds(75)
$tryRunResult = $null
try {
    while ([DateTime]::UtcNow -lt $tryRunDeadline) {
        if (Test-Path -LiteralPath $tryRunReceipt -PathType Leaf) {
            try {
                $tryRunResult = [IO.File]::ReadAllText($tryRunReceipt) | ConvertFrom-Json
                break
            }
            catch [IO.IOException] {
                # The helper writes with exclusive access and then closes it.
            }
        }
        Start-Sleep -Milliseconds 200
    }
    if ($null -eq $tryRunResult) {
        throw 'Explorer registration did not respond. Check the selected build and try again.'
    }
    if ($tryRunResult.CorrelationId -ne $tryRunCorrelation -or $tryRunResult.Operation -ne $tryRunOperation -or $tryRunResult.Broker -ne $tryRunBroker) {
        throw 'Explorer returned an unexpected registration receipt.'
    }
    if ($tryRunResult.ExitCode -ne 0) {
        throw "Explorer integration $Action failed. $($tryRunResult.Error)"
    }
    Write-Output "Try Run Explorer integration: $Action succeeded in the desktop user's registry view."
}
finally {
    # Remove only this invocation's generated receipt, never a selected input.
    if (Test-Path -LiteralPath $tryRunReceipt -PathType Leaf) {
        Remove-Item -LiteralPath $tryRunReceipt
    }
    [Runtime.InteropServices.Marshal]::ReleaseComObject($tryRunShell) | Out-Null
}
