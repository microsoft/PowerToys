# Copyright (c) Microsoft Corporation.
# The Microsoft Corporation licenses this file to you under the MIT license.

#Requires -Version 5.1

<#
.SYNOPSIS
    Removes PowerToys per-user and per-machine installations.

.DESCRIPTION
    Recovers systems where per-user and per-machine PowerToys installations block
    each other's bootstrapper. The script uninstalls the PowerToys MSI products
    directly, then runs each cached WiX bootstrapper to remove its registration.

    The script removes the current user's per-user installation and the
    machine-wide installation. Run it from every affected Windows profile to
    remove per-user installations belonging to other users.

.PARAMETER RemoveSettings
    Also removes the current user's PowerToys settings, logs, and update cache
    from %LOCALAPPDATA%\Microsoft\PowerToys.

.EXAMPLE
    .\Uninstall-PowerToys.ps1 -WhatIf

.EXAMPLE
    .\Uninstall-PowerToys.ps1

.EXAMPLE
    .\Uninstall-PowerToys.ps1 -RemoveSettings
#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [switch]$RemoveSettings
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not [Environment]::Is64BitProcess) {
    throw 'Run this script from a 64-bit PowerShell process so all installer registry entries are visible.'
}

$bundleUpgradeCode = '{6341382D-C0A9-4238-9188-BE9607E3FAB2}'
$msiDefinitions = @(
    [pscustomobject]@{
        Scope = 'PerUser'
        UpgradeCode = '{D8B559DB-4C98-487A-A33F-50A8EEE42726}'
    },
    [pscustomobject]@{
        Scope = 'PerMachine'
        UpgradeCode = '{42B84BF7-5FBF-473B-9C8B-049DC16F7708}'
    }
)
$successfulUninstallExitCodes = @(0, 1605, 1614, 1641, 3010)
$rebootRequiredExitCodes = @(1641, 3010)
$script:rebootRequired = $false
$script:failures = [System.Collections.Generic.List[string]]::new()

if ($null -eq ('PowerToysCleanup.NativeMethods' -as [type])) {
    Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;
using System.Text;

namespace PowerToysCleanup
{
    public static class NativeMethods
    {
        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        public static extern uint MsiEnumRelatedProducts(
            string upgradeCode,
            uint reserved,
            uint index,
            StringBuilder productCode);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        public static extern int MsiQueryProductState(string productCode);

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        public static extern uint MsiGetProductInfo(
            string productCode,
            string property,
            StringBuilder value,
            ref uint valueLength);
    }
}
'@
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-MsiStateName {
    param(
        [int]$State
    )

    switch ($State) {
        -1 { return 'Unknown' }
        0 { return 'Broken' }
        1 { return 'Advertised' }
        2 { return 'Absent' }
        3 { return 'Local' }
        4 { return 'Source' }
        5 { return 'Default' }
        default { return "State $State" }
    }
}

function Get-MsiProductProperty {
    param(
        [string]$ProductCode,
        [string]$Property
    )

    [uint32]$capacity = 256
    while ($true) {
        $value = [Text.StringBuilder]::new([int]$capacity)
        [uint32]$valueLength = $capacity
        $result = [PowerToysCleanup.NativeMethods]::MsiGetProductInfo(
            $ProductCode,
            $Property,
            $value,
            [ref]$valueLength)

        if ($result -eq 0) {
            return $value.ToString()
        }

        if ($result -eq 234) {
            $capacity = $valueLength + 1
            continue
        }

        if ($result -eq 1605) {
            return $null
        }

        throw "MsiGetProductInfo failed for $ProductCode property $Property with error $result."
    }
}

function Test-MicrosoftSignedFile {
    param(
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or
        -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    try {
        $signature = Get-AuthenticodeSignature -LiteralPath $Path -ErrorAction Stop
        if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate) {
            return $false
        }

        $signerName = $signature.SignerCertificate.GetNameInfo(
            [Security.Cryptography.X509Certificates.X509NameType]::SimpleName,
            $false)
        return [string]::Equals(
            $signerName,
            'Microsoft Corporation',
            [StringComparison]::Ordinal)
    } catch {
        return $false
    }
}

function Test-PowerToysMsiProduct {
    param(
        [object]$Product
    )

    $productName = Get-MsiProductProperty -ProductCode $Product.ProductCode -Property 'ProductName'
    $publisher = Get-MsiProductProperty -ProductCode $Product.ProductCode -Property 'Publisher'
    $localPackage = Get-MsiProductProperty -ProductCode $Product.ProductCode -Property 'LocalPackage'

    return [string]::Equals(
        $productName,
        'PowerToys (Preview)',
        [StringComparison]::Ordinal) -and
        [string]::Equals(
            $publisher,
            'Microsoft Corporation',
            [StringComparison]::Ordinal) -and
        (Test-MicrosoftSignedFile -Path $localPackage)
}

function Get-PowerToysMsiProducts {
    $products = foreach ($definition in $msiDefinitions) {
        for ([uint32]$index = 0; ; $index++) {
            $productCode = [Text.StringBuilder]::new(39)
            $result = [PowerToysCleanup.NativeMethods]::MsiEnumRelatedProducts(
                $definition.UpgradeCode,
                0,
                $index,
                $productCode)

            if ($result -eq 259) {
                break
            }

            if ($result -ne 0) {
                throw "MsiEnumRelatedProducts failed for $($definition.Scope) with error $result."
            }

            $code = $productCode.ToString()
            $state = [PowerToysCleanup.NativeMethods]::MsiQueryProductState($code)
            [pscustomobject]@{
                Scope = $definition.Scope
                ProductCode = $code
                State = $state
                StateName = Get-MsiStateName -State $state
            }
        }
    }

    return @($products | Group-Object ProductCode | ForEach-Object { $_.Group[0] })
}

function Test-BundleUpgradeCode {
    param(
        [object]$Value
    )

    foreach ($code in @($Value)) {
        if ([string]::Equals(
            [string]$code,
            $bundleUpgradeCode,
            [StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Get-ObjectPropertyValue {
    param(
        [object]$InputObject,
        [string]$Name
    )

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Get-PowerToysBundles {
    $locations = @(
        [pscustomobject]@{
            Scope = 'PerUser'
            Path = 'Registry::HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
        },
        [pscustomobject]@{
            Scope = 'PerUser'
            Path = 'Registry::HKEY_CURRENT_USER\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
        },
        [pscustomobject]@{
            Scope = 'PerMachine'
            Path = 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
        },
        [pscustomobject]@{
            Scope = 'PerMachine'
            Path = 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
        }
    )

    $bundles = foreach ($location in $locations) {
        foreach ($entry in @(Get-ItemProperty -Path $location.Path -ErrorAction SilentlyContinue)) {
            $entryBundleUpgradeCode = Get-ObjectPropertyValue -InputObject $entry -Name 'BundleUpgradeCode'
            if (-not (Test-BundleUpgradeCode -Value $entryBundleUpgradeCode)) {
                continue
            }

            [pscustomobject]@{
                Scope = $location.Scope
                DisplayName = [string](Get-ObjectPropertyValue -InputObject $entry -Name 'DisplayName')
                DisplayVersion = [string](Get-ObjectPropertyValue -InputObject $entry -Name 'DisplayVersion')
                CachePath = [string](Get-ObjectPropertyValue -InputObject $entry -Name 'BundleCachePath')
                QuietUninstallString = [string](Get-ObjectPropertyValue -InputObject $entry -Name 'QuietUninstallString')
                UninstallString = [string](Get-ObjectPropertyValue -InputObject $entry -Name 'UninstallString')
                RegistryPath = [string](Get-ObjectPropertyValue -InputObject $entry -Name 'PSPath')
            }
        }
    }

    return @($bundles | Group-Object RegistryPath | ForEach-Object { $_.Group[0] })
}

function Get-ExecutableFromCommandLine {
    param(
        [string]$CommandLine
    )

    if ([string]::IsNullOrWhiteSpace($CommandLine)) {
        return $null
    }

    if ($CommandLine -match '^\s*"([^"]+)"') {
        return $matches[1]
    }

    if ($CommandLine -match '^\s*(.+?\.exe)(?:\s|$)') {
        return $matches[1].Trim('"')
    }

    return $null
}

function Get-BundleExecutable {
    param(
        [object]$Bundle
    )

    $candidates = @(
        $Bundle.CachePath,
        (Get-ExecutableFromCommandLine -CommandLine $Bundle.QuietUninstallString),
        (Get-ExecutableFromCommandLine -CommandLine $Bundle.UninstallString)
    )

    foreach ($candidate in $candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate) -or
            -not (Test-MicrosoftSignedFile -Path $candidate)) {
            continue
        }

        try {
            $versionInfo = (Get-Item -LiteralPath $candidate -ErrorAction Stop).VersionInfo
            if ([string]::Equals(
                $versionInfo.CompanyName,
                'Microsoft Corporation',
                [StringComparison]::Ordinal) -and
                [string]::Equals(
                    $versionInfo.InternalName,
                    'burn',
                    [StringComparison]::OrdinalIgnoreCase) -and
                $versionInfo.ProductName -like 'PowerToys (Preview)*') {
                return $candidate
            }
        } catch {
            continue
        }
    }

    return $null
}

function Stop-PowerToysProcesses {
    $processes = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.ProcessName -eq 'PowerToys' -or $_.ProcessName.StartsWith('PowerToys.')
    })

    foreach ($process in $processes) {
        try {
            Write-Host "Stopping $($process.ProcessName) (PID $($process.Id))..."
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
        } catch {
            $script:failures.Add("Could not stop $($process.ProcessName) (PID $($process.Id)): $($_.Exception.Message)")
        }
    }

    if ($processes.Count -gt 0) {
        Start-Sleep -Seconds 1
    }
}

function Invoke-Uninstaller {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$Description
    )

    Write-Host "$Description..."
    try {
        $process = Start-Process -FilePath $FilePath -ArgumentList $Arguments -Wait -PassThru
    } catch {
        $script:failures.Add("$Description could not start: $($_.Exception.Message)")
        return
    }

    if ($rebootRequiredExitCodes -contains $process.ExitCode) {
        $script:rebootRequired = $true
    }

    if ($successfulUninstallExitCodes -notcontains $process.ExitCode) {
        $script:failures.Add("$Description failed with exit code $($process.ExitCode).")
    }
}

function Remove-KnownArtifact {
    param(
        [string]$Path,
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return
    }

    Write-Host "Removing $Description at $Path..."
    try {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
    } catch {
        $script:failures.Add("Could not remove $Description at ${Path}: $($_.Exception.Message)")
    }
}

function Remove-KnownRegistryValue {
    param(
        [string]$Path,
        [string]$Name,
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path)) {
        return
    }

    try {
        $key = Get-Item -LiteralPath $Path -ErrorAction Stop
        if ($key.GetValueNames() -notcontains $Name) {
            return
        }

        Write-Host "Removing $Description at $Path..."
        Remove-ItemProperty -LiteralPath $Path -Name $Name -Force -ErrorAction Stop
    } catch {
        $script:failures.Add("Could not remove $Description at ${Path}: $($_.Exception.Message)")
    }
}

$products = @(Get-PowerToysMsiProducts)
$bundles = @(Get-PowerToysBundles)

Write-Host 'Detected PowerToys MSI products:'
if ($products.Count -eq 0) {
    Write-Host '  None'
} else {
    foreach ($product in $products) {
        Write-Host "  $($product.Scope): $($product.ProductCode) [$($product.StateName)]"
    }
}

Write-Host 'Detected PowerToys bundles:'
if ($bundles.Count -eq 0) {
    Write-Host '  None'
} else {
    foreach ($bundle in $bundles) {
        Write-Host "  $($bundle.Scope): $($bundle.DisplayName) $($bundle.DisplayVersion)"
    }
}

$target = "$($products.Count) MSI product(s), $($bundles.Count) bundle(s), and known PowerToys installation artifacts"
if ($RemoveSettings) {
    $target += ', including the current user settings'
}

if (-not $PSCmdlet.ShouldProcess($target, 'Remove PowerToys')) {
    return
}

if (-not (Test-IsAdministrator)) {
    throw 'Run this script from an elevated PowerShell window so it can remove machine-wide installations.'
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logDirectory = Join-Path $env:TEMP "PowerToys-Cleanup-$timestamp"
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

Stop-PowerToysProcesses

foreach ($product in $products) {
    if ($product.State -eq -1 -or $product.State -eq 2) {
        Write-Host "Skipping inactive $($product.Scope) MSI $($product.ProductCode) [$($product.StateName)]."
        continue
    }

    if (-not (Test-PowerToysMsiProduct -Product $product)) {
        $script:failures.Add(
            "Refusing to run the uninstaller for $($product.Scope) MSI $($product.ProductCode) " +
            'because its cached package is not an authentic Microsoft-signed PowerToys MSI.')
        continue
    }

    $logPath = Join-Path $logDirectory "$($product.Scope)-$($product.ProductCode.Trim('{}')).log"
    $arguments = @(
        '/x',
        $product.ProductCode,
        '/quiet',
        '/norestart',
        '/L*v',
        "`"$logPath`""
    )
    Invoke-Uninstaller `
        -FilePath (Join-Path $env:SystemRoot 'System32\msiexec.exe') `
        -Arguments $arguments `
        -Description "Uninstalling $($product.Scope) MSI $($product.ProductCode)"
}

foreach ($bundle in $bundles) {
    $bundleExecutable = Get-BundleExecutable -Bundle $bundle
    if ($null -eq $bundleExecutable) {
        $script:failures.Add(
            "A trusted cached bootstrapper for $($bundle.Scope) $($bundle.DisplayVersion) was not found. " +
            "Its registry entry remains at $($bundle.RegistryPath).")
        continue
    }

    Invoke-Uninstaller `
        -FilePath $bundleExecutable `
        -Arguments @('/uninstall', '/quiet', '/norestart') `
        -Description "Removing $($bundle.Scope) bundle $($bundle.DisplayVersion)"
}

$remainingProducts = @(Get-PowerToysMsiProducts | Where-Object {
    $_.State -eq 0 -or $_.State -eq 1 -or $_.State -ge 3
})
$remainingBundles = @(Get-PowerToysBundles)

if ($remainingProducts.Count -gt 0) {
    $script:failures.Add(
        "Active MSI products remain: $($remainingProducts.ProductCode -join ', ').")
}

if ($remainingBundles.Count -gt 0) {
    $script:failures.Add(
        "Registered bundles remain: $($remainingBundles.DisplayVersion -join ', ').")
}

if ($remainingProducts.Count -eq 0 -and $remainingBundles.Count -eq 0) {
    $installScopeRegistryKeys = @(
        'Registry::HKEY_CURRENT_USER\SOFTWARE\Classes\PowerToys',
        'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\PowerToys'
    )
    foreach ($registryKey in $installScopeRegistryKeys) {
        Remove-KnownRegistryValue `
            -Path $registryKey `
            -Name 'InstallScope' `
            -Description 'legacy install-scope registry value'
        Remove-KnownArtifact `
            -Path "$registryKey\components" `
            -Description 'legacy installer component registry key'
    }

    $installDirectories = @(
        (Join-Path $env:LOCALAPPDATA 'PowerToys'),
        (Join-Path $env:ProgramFiles 'PowerToys')
    )
    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $installDirectories += Join-Path ${env:ProgramFiles(x86)} 'PowerToys'
    }

    foreach ($installDirectory in @($installDirectories | Select-Object -Unique)) {
        Remove-KnownArtifact -Path $installDirectory -Description 'installation directory'
    }

    if ($RemoveSettings) {
        Remove-KnownArtifact `
            -Path (Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys') `
            -Description 'current user settings and logs'
        Remove-KnownArtifact `
            -Path 'Registry::HKEY_CURRENT_USER\SOFTWARE\Classes\PowerToys' `
            -Description 'current user registry settings'
    }
}

if ($script:failures.Count -gt 0) {
    Write-Host ''
    Write-Host 'PowerToys cleanup did not complete:' -ForegroundColor Red
    foreach ($failure in $script:failures) {
        Write-Host "  - $failure" -ForegroundColor Red
    }
    Write-Host "MSI logs: $logDirectory"
    throw 'One or more PowerToys cleanup operations failed.'
}

Write-Host ''
Write-Host 'PowerToys was removed successfully.' -ForegroundColor Green
Write-Host "MSI logs: $logDirectory"
if ($script:rebootRequired) {
    Write-Host 'Restart Windows to complete the cleanup.' -ForegroundColor Yellow
}
