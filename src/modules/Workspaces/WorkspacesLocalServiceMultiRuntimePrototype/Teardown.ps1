[CmdletBinding()]
param(
    [switch]$PreserveTrustedCertificates
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$releaseRoot = Join-Path $root 'artifacts\release'
$metadataPath = Join-Path $releaseRoot 'artifacts.json'
$ownershipPath = Join-Path $releaseRoot 'certificate-ownership.json'
$msiPath = Join-Path $root 'artifacts\msi\PtPuvrControlPlane.msi'
$installRoot = Join-Path $env:ProgramFiles 'PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$storeRoot = Join-Path $env:ProgramData 'Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$endpointRegistryPath = 'HKLM:\SOFTWARE\Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototype'
$cleanupOutcomeRegistryPath = 'HKLM:\SOFTWARE\Microsoft\PowerToys\WorkspacesProtectedRuntimeControlPlanePrototypeValidation'
$ownerNames = @('PtPuvrOwnerA', 'PtPuvrOwnerB')

function Assert-True($Value, [string]$Label) {
    if (-not $Value) {
        throw "Assertion failed: $Label"
    }
}

function Test-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-CertificateEntries([string]$Store, [string]$Thumbprint) {
    return @(
        Get-ChildItem -Path $Store | Where-Object { $_.Thumbprint -eq $Thumbprint }
    )
}

function Remove-ExactCertificateEntries([string]$Store, [string]$Thumbprint) {
    Get-CertificateEntries $Store $Thumbprint | ForEach-Object {
        Remove-Item -LiteralPath $_.PSPath -Force
    }
}

function Get-OwnershipRecord([string]$Role) {
    $records = @($ownership.certificates | Where-Object { $_.role -eq $Role })
    Assert-True ($records.Count -eq 1) "one certificate ownership record for $Role"
    return $records[0]
}

function Restore-OwnedCertificates {
    foreach ($role in @('code', 'metadata', 'foreign')) {
        $record = Get-OwnershipRecord $role
        foreach ($store in $record.stores) {
            if ($store.introducedByRun) {
                Remove-ExactCertificateEntries $store.path $record.thumbprint
            }

            $actual = @(Get-CertificateEntries $store.path $record.thumbprint).Count -ge 1
            Assert-True (
                $actual -eq [bool]$store.preRunPresent
            ) "certificate restoration for $role at $($store.path)"
        }
    }
}

function Get-MsiRegistrations {
    $registrations = [System.Collections.Generic.List[object]]::new()
    foreach ($uninstallRoot in @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
    )) {
        if (-not (Test-Path -LiteralPath $uninstallRoot)) {
            continue
        }

        Get-ChildItem -LiteralPath $uninstallRoot | ForEach-Object {
            $entry = Get-ItemProperty -LiteralPath $_.PSPath
            $displayName = $entry.PSObject.Properties['DisplayName']
            if ($null -ne $displayName -and $displayName.Value -eq $metadata.msi.productName) {
                $registrations.Add($entry)
            }
        }
    }

    return @($registrations)
}

function Get-RuntimeServiceNames {
    return @(
        Get-CimInstance Win32_Service |
            Where-Object { $_.Name -like 'PtPuvrRuntime_*' } |
            Select-Object -ExpandProperty Name
    )
}

function Remove-ExactPrototypeResidue([string]$Path) {
    $parent = Split-Path -Parent $Path
    $leaf = Split-Path -Leaf $Path
    $escapedLeaf = [regex]::Escape($leaf)
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if (Test-Path -LiteralPath $Path) {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path -LiteralPath $parent -PathType Container) {
            Get-ChildItem -LiteralPath $parent -Force -Directory |
                Where-Object { $_.Name -match "^$escapedLeaf\.PtPuvrDelete-[0-9a-f]{32}$" } |
                ForEach-Object {
                    if ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                        Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
                    }
                    else {
                        Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
                    }
                }
        }
        $residue = @()
        if (Test-Path -LiteralPath $Path) {
            $residue += $Path
        }
        if (Test-Path -LiteralPath $parent -PathType Container) {
            $residue += @(
                Get-ChildItem -LiteralPath $parent -Force -Directory |
                    Where-Object { $_.Name -match "^$escapedLeaf\.PtPuvrDelete-[0-9a-f]{32}$" }
            )
        }
        if ($residue.Count -eq 0) {
            return
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Exact prototype residue could not be removed: $Path"
}

function Remove-ExactRegistryKey([string]$Path) {
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    Assert-True (-not (Test-Path -LiteralPath $Path)) "registry key removed: $Path"
}

function Get-TestUser([string]$Name) {
    $user = Get-LocalUser -Name $Name -ErrorAction SilentlyContinue
    if ($null -eq $user) {
        return $null
    }

    Assert-True (
        $user.FullName -eq "Control-plane prototype $Name"
    ) "test user $Name has the expected prototype identity"
    return $user
}

function Remove-TestUserAndProfile([string]$Name) {
    $user = Get-TestUser $Name
    if ($null -eq $user) {
        return
    }

    $profile = Get-CimInstance Win32_UserProfile |
        Where-Object { $_.SID -eq $user.SID.Value } |
        Select-Object -First 1
    $profilePath = if ($null -ne $profile) { $profile.LocalPath } else { $null }

    Remove-LocalUser -Name $Name

    if ($profilePath -and (Test-Path -LiteralPath $profilePath)) {
        $expectedProfileRoot = [IO.Path]::GetFullPath((Join-Path $env:SystemDrive 'Users'))
        $resolvedProfile = [IO.Path]::GetFullPath($profilePath)
        Assert-True (
            $resolvedProfile.StartsWith($expectedProfileRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and
            [IO.Path]::GetFileName($resolvedProfile) -eq $Name
        ) "resolved profile is the exact prototype profile for $Name"
        Remove-Item -LiteralPath $resolvedProfile -Recurse -Force
    }
}

if (-not (Test-Elevated)) {
    throw 'Teardown operations require an elevated administrator token.'
}
if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $ownershipPath -PathType Leaf)) {
    throw 'Package metadata is required for exact control-plane teardown.'
}

$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
$ownership = Get-Content -LiteralPath $ownershipPath -Raw | ConvertFrom-Json
Assert-True ($metadata.format -eq 2) 'artifact metadata format 2'
Assert-True ($ownership.format -eq 2) 'certificate ownership format 2'

$failure = $null
try {
    $leasePath = Join-Path $storeRoot 'leases.txt'
    if (Test-Path -LiteralPath $leasePath -PathType Leaf) {
        $leases = @(
            Get-Content -LiteralPath $leasePath |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        )
        Assert-True (
            $leases.Count -eq 0
        ) 'MSI teardown is refused while protected leases remain; release each owner through the MSI-installed Program Files PtPuvrUserClient.exe first'
    }

    $registrations = @(Get-MsiRegistrations)
    if ($registrations.Count -gt 0) {
        Assert-True (
            Test-Path -LiteralPath $msiPath -PathType Leaf
        ) 'companion MSI is available for MSI-owned uninstall'

        Remove-ExactRegistryKey $cleanupOutcomeRegistryPath
        & msiexec.exe /x $msiPath /qn /norestart
        if ($LASTEXITCODE -notin @(0, 3010)) {
            throw "Companion MSI uninstall failed with exit code $LASTEXITCODE."
        }
    }

    Assert-True (
        @(Get-MsiRegistrations).Count -eq 0
    ) 'companion MSI registration removed'
    Assert-True (
        $null -eq (Get-Service -Name PtPuvrHost -ErrorAction SilentlyContinue)
    ) 'stable host service removed by MSI'
    Assert-True (
        @(Get-RuntimeServiceNames).Count -eq 0
    ) 'all prototype runtime services removed'
    Remove-ExactPrototypeResidue $installRoot
    Remove-ExactPrototypeResidue $storeRoot
    Assert-True (-not (Test-Path -LiteralPath $installRoot)) 'MSI-owned Program Files root removed'
    Assert-True (-not (Test-Path -LiteralPath $storeRoot)) 'MSI-owned ProgramData root removed'
    Remove-ExactRegistryKey $endpointRegistryPath
    Assert-True (-not (Test-Path -LiteralPath $endpointRegistryPath)) 'endpoint registry key removed'
    Remove-ExactRegistryKey $cleanupOutcomeRegistryPath
    Assert-True (-not (Test-Path -LiteralPath $cleanupOutcomeRegistryPath)) 'cleanup outcome registry key removed'

    foreach ($ownerName in $ownerNames) {
        Remove-TestUserAndProfile $ownerName
        Assert-True (
            $null -eq (Get-LocalUser -Name $ownerName -ErrorAction SilentlyContinue)
        ) "prototype local user $ownerName removed"
    }
}
catch {
    $failure = $_
}

if ($null -eq $failure -and -not $PreserveTrustedCertificates) {
    try {
        Restore-OwnedCertificates
    }
    catch {
        if ($null -eq $failure) {
            $failure = $_
        }
    }
}

if ($null -ne $failure) {
    throw $failure
}

Write-Output 'CONTROL-PLANE TEARDOWN PASS'
