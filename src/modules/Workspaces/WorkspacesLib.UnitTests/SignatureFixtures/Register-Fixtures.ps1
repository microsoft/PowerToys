#Requires -Version 7.4
# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows) {
    throw 'Fixture shortcut registration requires Windows and PowerShell 7.4+.'
}
. (Join-Path $PSScriptRoot 'FixtureCommon.ps1')
. (Join-Path $PSScriptRoot 'FixtureRegistration.ps1')
$OutputDirectory = Resolve-FixtureOutputDirectory $OutputDirectory
$manifest = Get-Content -LiteralPath (Join-Path $OutputDirectory 'fixtures.json') -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.files.Count -ne $FixtureExpectations.Count -or
    (Compare-Object @($manifest.files.file | Sort-Object) @($FixtureExpectations.Keys | Sort-Object))) {
    throw 'The fixture manifest must contain exactly the six known relative fixture paths.'
}
$fixtures = @($manifest.files | Where-Object { $_.file -notlike 'diagnostics\*' })
if (!$Remove) {
    foreach ($fixture in $fixtures) {
        $path = Join-Path $OutputDirectory $fixture.file
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $fixture.sha256) {
            throw "Fixture changed after generation: $path"
        }
    }
}

# Each generated output directory owns a separate current-user Start menu folder.
$pathHash = [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($OutputDirectory.ToUpperInvariant()))
$id = [Convert]::ToHexString($pathHash).Substring(0, 16)
$programs = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
if (!$programs) {
    throw 'The current user has no Start menu Programs directory.'
}
$shortcutFolder = Join-Path $programs "PowerToys Signature Fixtures ($id)"
$owner = "PowerToys generated signature fixtures ($id)"
$shell = New-Object -ComObject WScript.Shell
$changes = @(Update-FixtureShortcuts -Fixtures $fixtures -OutputDirectory $OutputDirectory `
    -ShortcutFolder $shortcutFolder -Owner $owner -Shell $shell -Remove:$Remove -WhatIf:$WhatIfPreference)
$changes | Format-Table shortcut, action, changed -AutoSize
if (@($changes | Where-Object changed).Count -eq 0) {
    Write-Host 'No shortcuts changed. No Shell notification or registration report was written.'
    return
}

if (!('Workspaces.SignatureFixtures.ShellNotify' -as [type])) {
    Add-Type -Namespace Workspaces.SignatureFixtures -Name ShellNotify -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
[System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
public static extern void SHChangeNotify(int eventId, uint flags, string item, System.IntPtr other);
'@
}
[Workspaces.SignatureFixtures.ShellNotify]::SHChangeNotify(0x1000, 0x1005, $programs, [IntPtr]::Zero)

$metadata = @()
$failure = $null
if (!$Remove) {
    try {
        $shellApplication = New-Object -ComObject Shell.Application
        $deadline = [DateTime]::UtcNow.AddSeconds(45)
        do {
            $appsFolder = $shellApplication.NameSpace('shell:AppsFolder')
            if (!$appsFolder) {
                throw 'The desktop Shell did not expose AppsFolder.'
            }
            $metadata = @(Get-FixtureAppsFolderMetadata -AppsFolder $appsFolder -Targets @($changes.target))
            $missing = @($changes | Where-Object {
                $target = $_.target
                @($metadata | Where-Object { $_.path -eq $target -and $_.canLaunchElevated }).Count -eq 0
            })
            if ($missing.Count -eq 0) {
                break
            }
            Start-Sleep -Seconds 2
        } while ([DateTime]::UtcNow -lt $deadline)
        if ($missing.Count -gt 0) {
            throw "AppsFolder has not exposed HostEnvironment=0 for: $($missing.target -join ', ')"
        }
        foreach ($fixture in $fixtures) {
            if ((Get-FileHash -LiteralPath (Join-Path $OutputDirectory $fixture.file) -Algorithm SHA256).Hash -ne $fixture.sha256) {
                throw "Fixture bytes changed during registration: $($fixture.file)"
            }
        }
    }
    catch {
        $failure = $_.Exception.Message
    }
}

[ordered]@{
    updatedAtUtc = [DateTimeOffset]::UtcNow
    action = if ($Remove) { 'remove' } else { 'register' }
    shortcutFolder = $shortcutFolder
    shortcuts = $changes
    appsFolderMetadata = $metadata
    metadataVerified = !$Remove -and !$failure
    workspaceJsonModified = $false
    error = $failure
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'shell-registration.json')
if ($failure) {
    throw "$failure. Shortcuts were created, but metadata was not verified. Retry or use -Remove; no workspace JSON was modified."
}
if ($Remove) {
    Write-Host 'Removed only owned fixture shortcuts. Executables and certificate stores were not modified.'
}
else {
    $metadata | Format-Table path, hostEnvironment, canLaunchElevated -AutoSize
    Write-Host 'AppsFolder metadata verified. Open fixtures non-elevated and capture a NEW workspace before enabling Run as administrator.'
}
