# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Obtains Windows installation media for the local UI-test guest from a public Microsoft source.

.DESCRIPTION
Three sources are supported:

  Local  Validate an ISO you already have and report its SHA-256. This is the default.
  Url    Download a pinned Microsoft URL, such as an Evaluation Center link, and verify its hash.
  Fido   Resolve an official Microsoft retail ISO link with Fido, the GPL-3.0 download helper used
         by Rufus. Fido is fetched from a pinned tag and refused unless its SHA-256 matches the
         hash pinned below, because upstream does not publish an Authenticode-signed script. Fido
         is the only public route that also resolves arm64 Windows 11 media. It is downloaded on
         demand and never vendored into this repository.

The script never bypasses hash verification. If verification fails it stops and tells you to obtain
the media manually.

.EXAMPLE
pwsh ./Get-WindowsMedia.ps1 -Source Fido -Windows 11 -Edition Pro -Architecture arm64 -UrlOnly

.EXAMPLE
pwsh ./Get-WindowsMedia.ps1 -Source Fido -Windows 11 -Edition Pro -DestinationRoot D:\media

.EXAMPLE
pwsh ./Get-WindowsMedia.ps1 -Source Local -Path D:\media\Win11_24H2_English_x64.iso
#>

[CmdletBinding()]
param(
    [ValidateSet('Fido', 'Url', 'Local')]
    [string]$Source = 'Local',
    [string]$DestinationRoot = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm-Media'),

    [string]$Windows = '11',
    [string]$Release = 'Latest',
    [string]$Edition,
    [string]$Language = 'English',
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture,

    [string]$Url,
    [string]$Path,
    [string]$ExpectedSha256,

    # Pin the helper. Review the script, then update both values together, before raising the tag.
    [string]$FidoTag = 'v1.70',
    [string]$FidoSha256 = '24C86067FA399D2FD75EF0693A2EC79CA8DB162827F808CAAC03541CBF640C13',
    [switch]$UrlOnly
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this script with PowerShell 7 (pwsh).'
}

if ([string]::IsNullOrWhiteSpace($Architecture)) {
    $Architecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
}

function Save-LargeFile {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][string]$Destination
    )

    New-Item (Split-Path $Destination -Parent) -ItemType Directory -Force | Out-Null
    $bits = Get-Command Start-BitsTransfer -ErrorAction SilentlyContinue
    if ($null -ne $bits) {
        Start-BitsTransfer -Source $Uri -Destination $Destination -Description 'Windows media'
        return
    }
    Invoke-WebRequest -Uri $Uri -OutFile $Destination
}

function Get-MediaResult {
    param(
        [Parameter(Mandatory)][string]$IsoPath,
        [string]$ResolvedFrom,
        [string]$ResolvedHost
    )

    $item = Get-Item $IsoPath
    $hash = (Get-FileHash $IsoPath -Algorithm SHA256).Hash
    if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256) -and $hash -ne $ExpectedSha256.ToUpperInvariant()) {
        throw "SHA-256 mismatch for $IsoPath. Expected $ExpectedSha256 but found $hash."
    }
    return [pscustomobject]@{
        Path = $item.FullName
        SizeGB = [math]::Round($item.Length / 1GB, 2)
        Sha256 = $hash
        Source = $ResolvedFrom
        ResolvedHost = $ResolvedHost
        Architecture = $Architecture
    }
}

switch ($Source) {
    'Local' {
        if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path $Path -PathType Leaf)) {
            throw 'Specify an existing ISO with -Path when using -Source Local.'
        }
        Get-MediaResult -IsoPath $Path -ResolvedFrom 'Local' | ConvertTo-Json
        return
    }

    'Url' {
        if ([string]::IsNullOrWhiteSpace($Url)) {
            throw 'Specify -Url when using -Source Url.'
        }
        if ($UrlOnly) {
            [pscustomobject]@{ Url = $Url; Architecture = $Architecture } | ConvertTo-Json
            return
        }
        $fileName = [IO.Path]::GetFileName(([uri]$Url).AbsolutePath)
        if ([string]::IsNullOrWhiteSpace($fileName)) {
            $fileName = 'windows-media.iso'
        }
        $destination = Join-Path $DestinationRoot $fileName
        Write-Host "Downloading $fileName..."
        Save-LargeFile -Uri $Url -Destination $destination
        Get-MediaResult -IsoPath $destination -ResolvedFrom 'Url' -ResolvedHost ([uri]$Url).DnsSafeHost | ConvertTo-Json
        return
    }
}

$fidoRoot = Join-Path $DestinationRoot "fido-$FidoTag"
$fidoPath = Join-Path $fidoRoot 'Fido.ps1'
if (-not (Test-Path $fidoPath -PathType Leaf)) {
    New-Item $fidoRoot -ItemType Directory -Force | Out-Null
    $fidoUri = "https://raw.githubusercontent.com/pbatard/Fido/$FidoTag/Fido.ps1"
    Write-Host "Downloading the Fido helper from the pinned tag $FidoTag..."
    Invoke-WebRequest -Uri $fidoUri -OutFile $fidoPath
}

$signature = Get-AuthenticodeSignature $fidoPath
$fidoHash = (Get-FileHash $fidoPath -Algorithm SHA256).Hash
if ($fidoHash -ne $FidoSha256.ToUpperInvariant()) {
    Remove-Item $fidoPath -Force -ErrorAction SilentlyContinue
    throw "BLOCKED: the Fido helper at tag $FidoTag hashed $fidoHash instead of the pinned $FidoSha256. Review the upstream change and pass -FidoSha256 deliberately, or download Windows media manually from the Microsoft Evaluation Center."
}
Write-Host "Fido $FidoTag verified by SHA-256 (Authenticode status: $($signature.Status))."

$fidoArguments = @(
    '-NoLogo', '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $fidoPath,
    '-Win', $Windows, '-Rel', $Release, '-Lang', $Language, '-Arch', $Architecture, '-GetUrl'
)
if (-not [string]::IsNullOrWhiteSpace($Edition)) {
    $fidoArguments += @('-Ed', $Edition)
}

# Fido targets Windows PowerShell and instantiates WinForms types when it is not in command-line mode.
$fidoOutput = & powershell.exe @fidoArguments 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Fido failed with exit code $LASTEXITCODE. $($fidoOutput | Out-String)"
}
$resolvedUrl = @($fidoOutput | Where-Object { $_ -match '^https://' }) | Select-Object -Last 1
if ([string]::IsNullOrWhiteSpace($resolvedUrl)) {
    throw "Fido did not return a download URL. $($fidoOutput | Out-String)"
}
$resolvedUri = [uri]$resolvedUrl
$resolvedHost = $resolvedUri.DnsSafeHost.ToLowerInvariant()
if ($resolvedHost -ne 'microsoft.com' -and -not $resolvedHost.EndsWith('.microsoft.com', [StringComparison]::Ordinal)) {
    throw "BLOCKED: the mobile-user-agent resolver returned non-Microsoft host '$resolvedHost'. Refusing to download $resolvedUrl"
}

if ($UrlOnly) {
    [pscustomobject]@{
        Url = $resolvedUrl
        ResolvedHost = $resolvedHost
        Architecture = $Architecture
        FidoTag = $FidoTag
    } | ConvertTo-Json
    return
}

$fileName = [IO.Path]::GetFileName(([uri]$resolvedUrl).AbsolutePath)
$destination = Join-Path $DestinationRoot $fileName
Write-Host "Downloading $fileName (several GB)..."
Save-LargeFile -Uri $resolvedUrl -Destination $destination
Get-MediaResult -IsoPath $destination -ResolvedFrom "Microsoft ISO page via verified Fido $FidoTag" -ResolvedHost $resolvedHost | ConvertTo-Json
