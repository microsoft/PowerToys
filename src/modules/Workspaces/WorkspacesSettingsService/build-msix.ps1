<#
.SYNOPSIS
  Build the PowerToys Settings Service MSIX from a built service exe.

.DESCRIPTION
  Stages AppxManifest + logo + the built PowerToys.PTSettingsSvc.exe into a
  layout, packs it with makeappx, and (optionally) signs it with a cert.
  Invoked by the signed build pipeline (steps-build-installer-vnext.yml) and
  usable locally against a built vcxproj.
#>
[CmdletBinding()]
param(
    [string]$Config   = 'Release',
    [string]$ExePath  = "$PSScriptRoot\..\..\..\..\x64\$Config\WorkspacesSettingsService\PowerToys.PTSettingsSvc.exe",
    [string]$OutMsix  = "$PSScriptRoot\package\PTSettingsSvc.msix",
    [string]$Version  = '',
    [string]$Arch     = 'x64',
    [string]$PfxPath  = '',
    [string]$PfxPass  = ''
)

$ErrorActionPreference = 'Stop'
$pkgSrc  = Join-Path $PSScriptRoot 'package'
$staging = Join-Path $env:TEMP 'ptsettingssvc-msix'
$sdkBin  = (Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter makeappx.exe |
            Where-Object { $_.FullName -match 'x64' } | Select-Object -Last 1).DirectoryName

if (-not (Test-Path $ExePath)) { throw "Service exe not found: $ExePath (build the vcxproj first)." }

# 1x1 transparent logo if none present.
$logo = Join-Path $pkgSrc 'logo.png'
if (-not (Test-Path $logo)) {
    [IO.File]::WriteAllBytes($logo,[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII='))
}

Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $staging | Out-Null
Copy-Item (Join-Path $pkgSrc 'AppxManifest.xml') $staging
Copy-Item $logo $staging
Copy-Item $ExePath $staging

# Stamp the package version (must be 4-part) to keep it in lockstep with the build.
# Use case-sensitive -creplace so the lowercase `version` in the XML declaration
# is left untouched (only the Identity's `Version` attribute is replaced).
if ($Version) {
    $v = if (($Version -split '\.').Count -eq 3) { "$Version.0" } else { $Version }
    $mf = Join-Path $staging 'AppxManifest.xml'
    (Get-Content $mf -Raw) -creplace 'Version="[0-9.]+"', "Version=`"$v`"" | Set-Content $mf -Encoding utf8
}

# Stamp the package architecture (MSIX uses lowercase x64/arm64).
$arch = $Arch.ToLowerInvariant()
$mf = Join-Path $staging 'AppxManifest.xml'
(Get-Content $mf -Raw) -creplace 'ProcessorArchitecture="[a-zA-Z0-9]+"', "ProcessorArchitecture=`"$arch`"" | Set-Content $mf -Encoding utf8

& "$sdkBin\makeappx.exe" pack /d $staging /p $OutMsix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx failed ($LASTEXITCODE)." }
Write-Output "packed: $OutMsix"

if ($PfxPath) {
    & "$sdkBin\signtool.exe" sign /fd SHA256 /f $PfxPath /p $PfxPass $OutMsix | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)." }
    Write-Output "signed: $OutMsix"
}
