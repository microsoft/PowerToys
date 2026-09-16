# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Register', 'Unregister', 'Status', 'LaunchTest')]
    [string] $Action,
    [Parameter(Mandatory = $true)]
    [string] $TryRunDirectory
)

$ErrorActionPreference = 'Stop'
$tryRunRoot = (Resolve-Path -LiteralPath $TryRunDirectory).ProviderPath
$extensionRoot = Join-Path $tryRunRoot 'CmdPal'
$manifest = Join-Path $extensionRoot 'AppxManifest.xml'
$extension = Join-Path $extensionRoot 'PowerToys.TryRun.CmdPal.exe'
$packageName = 'PowerToys.TryRun.CmdPal.Dev'
foreach ($file in @($manifest, $extension, (Join-Path $tryRunRoot 'PowerToys.TryRun.exe'))) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Build TryRun.slnx first. Missing: $file" }
}

$xmlSettings = [System.Xml.XmlReaderSettings]::new()
$xmlSettings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
$xmlSettings.XmlResolver = $null
$xmlReader = [System.Xml.XmlReader]::Create($manifest, $xmlSettings)
try {
    $packageManifest = [System.Xml.XmlDocument]::new()
    $packageManifest.XmlResolver = $null
    $packageManifest.Load($xmlReader)
}
finally { $xmlReader.Dispose() }
$manifestNamespaces = [System.Xml.XmlNamespaceManager]::new($packageManifest.NameTable)
$manifestNamespaces.AddNamespace('p', 'http://schemas.microsoft.com/appx/manifest/foundation/windows10')
$identity = $packageManifest.SelectSingleNode('/p:Package/p:Identity', $manifestNamespaces)
$applications = $packageManifest.SelectNodes('/p:Package/p:Applications/p:Application', $manifestNamespaces)
if ($null -eq $identity -or $identity.GetAttribute('Name') -ne $packageName -or $identity.GetAttribute('Publisher') -ne 'CN=PowerToys.TryRun.Development' -or $applications.Count -ne 1 -or $applications[0].GetAttribute('Executable') -ne 'PowerToys.TryRun.CmdPal.exe') {
    throw 'The manifest does not identify the expected Try Run development extension.'
}

$packages = @(Get-AppxPackage -Name $packageName)
if ($packages.Count -gt 1) { throw 'Multiple Try Run development packages were found; resolve the package conflict first.' }
if ($packages.Count -eq 1 -and $packages[0].Publisher -ne 'CN=PowerToys.TryRun.Development') { throw 'The package name is owned by a different publisher.' }

switch ($Action) {
    'Register' { Add-AppxPackage -Register $manifest }
    'Unregister' {
        if ($packages.Count -eq 1 -and $packages[0].InstallLocation.TrimEnd('\') -ne $extensionRoot.TrimEnd('\')) { throw 'A different Try Run build is registered. Use its directory to unregister it.' }
        if ($packages.Count -eq 1) { Remove-AppxPackage -Package $packages[0].PackageFullName }
        Write-Output 'Try Run Command Palette extension unregistered for this user.'
        return
    }
}

$installed = Get-AppxPackage -Name $packageName
if ($null -eq $installed -or $installed.InstallLocation.TrimEnd('\') -ne $extensionRoot.TrimEnd('\')) {
    throw 'This Try Run build is not registered. Run this script with -Action Register.'
}

# Probe COM activation from the desktop context, the same view used by CmdPal.
$shell = New-Object -ComObject Shell.Application
try {
    $explorer = @($shell.Windows()) | Where-Object { $_.FullName -eq (Join-Path $env:WINDIR 'explorer.exe') } | Select-Object -First 1
    if ($null -eq $explorer) { throw 'Open a File Explorer window, then run Status again to verify activation.' }
    $correlation = [guid]::NewGuid().ToString('N')
    $receipt = Join-Path $extensionRoot ("CmdPal-probe-$correlation.json")
    $probeAction = if ($Action -eq 'LaunchTest') { '--probe-launch' } else { '--probe-registration' }
    $explorer.Document.Application.ShellExecute($extension, "$probeAction $correlation", $extensionRoot, 'open', 0)
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    try {
        $result = $null
        while ([DateTime]::UtcNow -lt $deadline) {
            if (Test-Path -LiteralPath $receipt) {
                try { $result = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json; break }
                catch [System.IO.IOException] { }
            }
            Start-Sleep -Milliseconds 200
        }
        if ($null -eq $result) { throw 'The CmdPal extension activation probe did not respond.' }
        if ($result.CorrelationId -ne $correlation -or $null -ne $result.Error) { throw "CmdPal extension activation failed: $($result.Error)" }
        if ($Action -eq 'LaunchTest' -and -not $result.Launched) { throw 'The extension did not launch Try Run.' }
        Write-Output "Try Run Command Palette extension ready: $($result.Commands -join ', ')."
    }
    finally {
        if (Test-Path -LiteralPath $receipt) { Remove-Item -LiteralPath $receipt }
    }
}
finally { [Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null }
