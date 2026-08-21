[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipBuild,
    [switch]$TrustMachine
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $SkipBuild) {
    & (Join-Path $root 'Build.ps1') -Configuration $Configuration -Clean
}

$sdkRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$signtool = Get-ChildItem $sdkRoot -Directory |
    ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Sort-Object -Descending |
    Select-Object -First 1
if (-not $signtool) {
    throw 'signtool.exe was not found.'
}
if ($TrustMachine -and -not (
        [Security.Principal.WindowsPrincipal]::new(
            [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
                [Security.Principal.WindowsBuiltInRole]::Administrator))) {
    throw '-TrustMachine requires an elevated PowerShell session.'
}

$binRoot = Join-Path $root "artifacts\bin\x64\$Configuration"
$releaseRoot = Join-Path $root 'artifacts\release'
$bundleRoot = Join-Path $root 'artifacts\simulated-bundles'
$publisher = 'CN=PowerToys Workspaces Protected Runtime Prototype Test'
$foreignPublisher = 'CN=PowerToys Workspaces Protected Runtime Prototype Foreign Signer Test'
$ownershipPath = Join-Path $releaseRoot 'certificate-ownership.json'
$certificateStores = @(
    'Cert:\CurrentUser\My',
    'Cert:\CurrentUser\TrustedPeople',
    'Cert:\LocalMachine\My',
    'Cert:\LocalMachine\TrustedPeople'
)

$definitions = @(
    [ordered]@{ id = 'track1-1.0.0.0'; track = 1; version = '1.0.0.0'; ready = $true; source = 'runtime-track-1-1.0.0.0'; file = 'PtPuvrRuntime-Track1-1.0.0.0.exe' },
    [ordered]@{ id = 'track1-1.1.0.0'; track = 1; version = '1.1.0.0'; ready = $true; source = 'runtime-track-1-1.1.0.0'; file = 'PtPuvrRuntime-Track1-1.1.0.0.exe' },
    [ordered]@{ id = 'track1-1.2.0.0'; track = 1; version = '1.2.0.0'; ready = $false; source = 'runtime-track-1-1.2.0.0'; file = 'PtPuvrRuntime-Track1-1.2.0.0.exe' },
    [ordered]@{ id = 'track1-1.3.0.0'; track = 1; version = '1.3.0.0'; ready = $true; source = 'runtime-track-1-1.3.0.0'; file = 'PtPuvrRuntime-Track1-1.3.0.0.exe' },
    [ordered]@{ id = 'track1-1.4.0.0'; track = 1; version = '1.4.0.0'; ready = $true; source = 'runtime-track-1-1.4.0.0'; file = 'PtPuvrRuntime-Track1-1.4.0.0.exe' },
    [ordered]@{ id = 'track1-1.5.0.0'; track = 1; version = '1.5.0.0'; ready = $true; source = 'runtime-track-1-1.5.0.0'; file = 'PtPuvrRuntime-Track1-1.5.0.0.exe' },
    [ordered]@{ id = 'track1-1.6.0.0'; track = 1; version = '1.6.0.0'; ready = $true; source = 'runtime-track-1-1.6.0.0'; file = 'PtPuvrRuntime-Track1-1.6.0.0.exe' },
    [ordered]@{ id = 'track1-1.7.0.0'; track = 1; version = '1.7.0.0'; ready = $true; source = 'runtime-track-1-1.7.0.0'; file = 'PtPuvrRuntime-Track1-1.7.0.0.exe' },
    [ordered]@{ id = 'track1-1.8.0.0'; track = 1; version = '1.8.0.0'; ready = $true; source = 'runtime-track-1-1.8.0.0'; file = 'PtPuvrRuntime-Track1-1.8.0.0.exe' },
    [ordered]@{ id = 'track2-2.0.0.0'; track = 2; version = '2.0.0.0'; ready = $true; source = 'runtime-track-2-2.0.0.0'; file = 'PtPuvrRuntime-Track2-2.0.0.0.exe' }
)

function Sign-AndVerify([string]$Path, [string]$Thumbprint, [string]$ExpectedSignerSha256) {
    & $signtool sign /fd SHA256 /sha1 $Thumbprint /s My $Path
    if ($LASTEXITCODE -ne 0) {
        throw "signtool sign failed: $Path"
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    $actualSignerSha256 = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($signature.SignerCertificate.RawData))
    if ($signature.Status -ne 'Valid' -or $actualSignerSha256 -ne $ExpectedSignerSha256) {
        throw "Authenticode verification failed: $Path"
    }
}

function Test-ExactCertificatePresence([string]$Store, [string]$Thumbprint) {
    return Test-Path -LiteralPath "$Store\$Thumbprint"
}

function Read-CertificateOwnership([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }
    $ownership = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($ownership.format -ne 1) {
        throw "Certificate ownership format is unsupported: $Path"
    }
    $records = @($ownership.certificates)
    if ($records.Count -ne 2) {
        throw "Certificate ownership must contain exactly primary and foreign records: $Path"
    }
    $roles = @()
    $thumbprints = @()
    foreach ($record in $records) {
        if ($record.role -notin @('primary', 'foreign') -or
            [string]$record.thumbprint -notmatch '^[0-9A-F]{40}$') {
            throw "Certificate ownership identity is invalid: $Path"
        }
        $roles += $record.role
        $thumbprints += $record.thumbprint
        $stores = @($record.stores)
        if ($stores.Count -ne $certificateStores.Count) {
            throw "Certificate ownership store count is invalid for $($record.role): $Path"
        }
        foreach ($store in $certificateStores) {
            $entry = @($stores | Where-Object { $_.path -eq $store })
            if ($entry.Count -ne 1 -or
                $entry[0].preRunPresent -isnot [bool] -or
                $entry[0].introducedByRun -isnot [bool] -or
                ($entry[0].preRunPresent -and $entry[0].introducedByRun)) {
                throw "Certificate ownership store state is invalid for $($record.role) at ${store}: $Path"
            }
        }
    }
    $sortedRoles = @($roles | Sort-Object) -join '|'
    if ($sortedRoles -ne 'foreign|primary' -or
        @($thumbprints | Sort-Object -Unique).Count -ne 2) {
        throw "Certificate ownership records are not unique: $Path"
    }
    return $ownership
}

function Write-DurableCertificateOwnership([object]$Ownership) {
    $json = $Ownership | ConvertTo-Json -Depth 8
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
    $temporaryPath = Join-Path $releaseRoot (
        'certificate-ownership.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    $stream = [IO.FileStream]::new(
        $temporaryPath,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None,
        4096,
        [IO.FileOptions]::WriteThrough)
    try {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally {
        $stream.Dispose()
    }
    try {
        [IO.File]::Move($temporaryPath, $ownershipPath, $true)
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}

function Restore-CertificateOwnership([object]$Ownership) {
    foreach ($record in $Ownership.certificates) {
        foreach ($store in $record.stores) {
            if ($store.introducedByRun) {
                $certificatePath = "$($store.path)\$($record.thumbprint)"
                if (Test-Path -LiteralPath $certificatePath) {
                    Remove-Item -LiteralPath $certificatePath -Force
                }
            }
        }
    }
    foreach ($record in $Ownership.certificates) {
        foreach ($store in $record.stores) {
            if ((Test-ExactCertificatePresence $store.path $record.thumbprint) -ne $store.preRunPresent) {
                throw "Certificate ownership restoration failed for $($record.role) at $($store.path)."
            }
        }
    }
}

function New-CertificateOwnership(
    [Security.Cryptography.X509Certificates.X509Certificate2]$Primary,
    [Security.Cryptography.X509Certificates.X509Certificate2]$Foreign
) {
    $records = @(
        [ordered]@{ role = 'primary'; certificate = $Primary },
        [ordered]@{ role = 'foreign'; certificate = $Foreign }
    )
    return [ordered]@{
        format = 1
        certificates = @(
            foreach ($record in $records) {
                [ordered]@{
                    role = $record.role
                    thumbprint = $record.certificate.Thumbprint
                    stores = @(
                        foreach ($store in $certificateStores) {
                            $signingStore = $store -eq 'Cert:\CurrentUser\My'
                            [ordered]@{
                                path = $store
                                preRunPresent = if ($signingStore) {
                                    $false
                                }
                                else {
                                    Test-ExactCertificatePresence $store $record.certificate.Thumbprint
                                }
                                introducedByRun = $signingStore
                            }
                        }
                    )
                }
            }
        )
    }
}

$certificate = $null
$foreignCertificate = $null
$certificateOwnership = $null

try {
    $previousOwnership = Read-CertificateOwnership $ownershipPath
    if ($previousOwnership) {
        Restore-CertificateOwnership $previousOwnership
        Write-Host 'Restored only the exact certificates introduced by the prior package run.'
    }
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $bundleRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null

    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $publisher `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable `
        -NotAfter (Get-Date).AddYears(2)
    $certificateSha256 = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($certificate.RawData))
    $foreignCertificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $foreignPublisher `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable `
        -NotAfter (Get-Date).AddYears(2)
    $foreignCertificateSha256 = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($foreignCertificate.RawData))

    $certificatePath = Join-Path $releaseRoot 'PtPuvr-TestOnly.cer'
    Export-Certificate -Cert $certificate -FilePath $certificatePath -Force | Out-Null
    $foreignCertificatePath = Join-Path $releaseRoot 'PtPuvr-ForeignSigner-TestOnly.cer'
    Export-Certificate -Cert $foreignCertificate -FilePath $foreignCertificatePath -Force | Out-Null
    $trustStore = if ($TrustMachine) {
        'Cert:\LocalMachine\TrustedPeople'
    }
    else {
        'Cert:\CurrentUser\TrustedPeople'
    }
    $certificateOwnership = New-CertificateOwnership $certificate $foreignCertificate
    Write-DurableCertificateOwnership $certificateOwnership
    foreach ($ownedCertificate in $certificateOwnership.certificates) {
        $ownedStore = @($ownedCertificate.stores | Where-Object { $_.path -eq $trustStore })
        if ($ownedStore.Count -ne 1) {
            throw "Certificate ownership store lookup failed: $trustStore"
        }
        if (-not $ownedStore[0].preRunPresent) {
            $certificateToImport = if ($ownedCertificate.role -eq 'primary') {
                $certificatePath
            }
            else {
                $foreignCertificatePath
            }
            $ownedStore[0].introducedByRun = $true
            Write-DurableCertificateOwnership $certificateOwnership
            Import-Certificate -FilePath $certificateToImport -CertStoreLocation $trustStore | Out-Null
        }
        if (-not (Test-ExactCertificatePresence $trustStore $ownedCertificate.thumbprint)) {
            throw "Trusted certificate import failed for $($ownedCertificate.role)."
        }
    }

    $updaterSource = Join-Path $binRoot 'PtPuvrUpdater.exe'
    if (-not (Test-Path -LiteralPath $updaterSource -PathType Leaf)) {
        throw "Updater build output is missing: $updaterSource"
    }
    if ((Get-Item -LiteralPath $updaterSource).VersionInfo.FileVersion -ne '5.0.0.0') {
        throw "Unexpected updater version: $updaterSource"
    }
    $updaterArtifact = Join-Path $releaseRoot 'PtPuvrUpdater.exe'
    Copy-Item -LiteralPath $updaterSource -Destination $updaterArtifact
    Sign-AndVerify $updaterArtifact $certificate.Thumbprint $certificateSha256

    $runtimeMetadata = @()
    foreach ($definition in $definitions) {
        $source = Join-Path (Join-Path $binRoot $definition.source) 'PtPuvrRuntime.exe'
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Runtime build output is missing: $source"
        }
        if ((Get-Item -LiteralPath $source).VersionInfo.FileVersion -ne $definition.version) {
            throw "Unexpected runtime version: $source"
        }
        $artifact = Join-Path $releaseRoot $definition.file
        Copy-Item -LiteralPath $source -Destination $artifact
        Sign-AndVerify $artifact $certificate.Thumbprint $certificateSha256
        $runtimeMetadata += [ordered]@{
            id = $definition.id
            track = $definition.track
            version = $definition.version
            readiness = if ($definition.ready) { 'healthy' } else { 'intentional-failure' }
            file = $definition.file
            sha256 = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash
        }
    }

    $wrongProductSource = Join-Path $binRoot 'runtime-wrong-product\PtPuvrRuntime.exe'
    $wrongProductArtifact = Join-Path $releaseRoot 'PtPuvrRuntime-WrongProduct.exe'
    Copy-Item -LiteralPath $wrongProductSource -Destination $wrongProductArtifact
    Sign-AndVerify $wrongProductArtifact $certificate.Thumbprint $certificateSha256

    $foreignSignerArtifact = Join-Path $releaseRoot 'PtPuvrRuntime-ForeignSigner.exe'
    Copy-Item -LiteralPath (Join-Path $releaseRoot 'PtPuvrRuntime-Track1-1.1.0.0.exe') -Destination $foreignSignerArtifact
    Sign-AndVerify $foreignSignerArtifact $foreignCertificate.Thumbprint $foreignCertificateSha256

    $tamperedArtifact = Join-Path $releaseRoot 'PtPuvrRuntime-Tampered.exe'
    Copy-Item -LiteralPath (Join-Path $releaseRoot 'PtPuvrRuntime-Track1-1.1.0.0.exe') -Destination $tamperedArtifact
    $tamperedBytes = [IO.File]::ReadAllBytes($tamperedArtifact)
    $tamperedBytes[$tamperedBytes.Length - 1] = $tamperedBytes[$tamperedBytes.Length - 1] -bxor 0x5a
    [IO.File]::WriteAllBytes($tamperedArtifact, $tamperedBytes)

    $bundles = @(
        [ordered]@{ name = 'PowerToys-0.101'; runtimeId = 'track1-1.0.0.0' },
        [ordered]@{ name = 'PowerToys-0.110'; runtimeId = 'track2-2.0.0.0' }
    )
    $bundleMetadata = @()
    foreach ($bundleDefinition in $bundles) {
        $bundle = Join-Path $bundleRoot $bundleDefinition.name
        New-Item -ItemType Directory -Path $bundle -Force | Out-Null
        $runtime = $runtimeMetadata | Where-Object { $_.id -eq $bundleDefinition.runtimeId } |
            Select-Object -First 1
        Copy-Item -LiteralPath $updaterArtifact -Destination (Join-Path $bundle 'PtPuvrUpdater.exe')
        Copy-Item -LiteralPath (Join-Path $releaseRoot $runtime.file) -Destination (Join-Path $bundle 'PtPuvrRuntime.exe')
        [ordered]@{
            updaterVersion = '5.0.0.0'
            runtimeTrack = $runtime.track
            runtimeVersion = $runtime.version
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $bundle 'bundle.json') -Encoding utf8NoBOM
        $bundleMetadata += [ordered]@{
            name = $bundleDefinition.name
            runtimeId = $runtime.id
            updaterFile = "$($bundleDefinition.name)\PtPuvrUpdater.exe"
            runtimeFile = "$($bundleDefinition.name)\PtPuvrRuntime.exe"
            updaterSha256 = (Get-FileHash -LiteralPath (Join-Path $bundle 'PtPuvrUpdater.exe') -Algorithm SHA256).Hash
            runtimeSha256 = (Get-FileHash -LiteralPath (Join-Path $bundle 'PtPuvrRuntime.exe') -Algorithm SHA256).Hash
        }
    }

    [ordered]@{
        signerSubject = $publisher
        certificateFile = 'PtPuvr-TestOnly.cer'
        certificateThumbprint = $certificate.Thumbprint
        trustedSignerSha256 = $certificateSha256
        foreignSignerCertificateFile = 'PtPuvr-ForeignSigner-TestOnly.cer'
        foreignSignerCertificateThumbprint = $foreignCertificate.Thumbprint
        foreignSignerSignerSha256 = $foreignCertificateSha256
        updater = [ordered]@{
            version = '5.0.0.0'
            file = 'PtPuvrUpdater.exe'
            sha256 = (Get-FileHash -LiteralPath $updaterArtifact -Algorithm SHA256).Hash
        }
        runtimes = $runtimeMetadata
        negativeCandidates = [ordered]@{
            tampered = [ordered]@{
                track = 1
                file = 'PtPuvrRuntime-Tampered.exe'
            }
            wrongProduct = [ordered]@{
                track = 1
                file = 'PtPuvrRuntime-WrongProduct.exe'
            }
            foreignSigner = [ordered]@{
                track = 1
                file = 'PtPuvrRuntime-ForeignSigner.exe'
            }
        }
        simulatedBundles = $bundleMetadata
    } | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $releaseRoot 'artifacts.json') -Encoding utf8NoBOM
    Write-DurableCertificateOwnership $certificateOwnership

    Write-Host 'Built and signed ordinary PE updater and runtime artifacts.'
}
catch {
    $packagingFailure = $_
    if ($certificateOwnership) {
        try {
            Restore-CertificateOwnership $certificateOwnership
        }
        catch {
            throw "Packaging failed and cleanup of exact run-owned certificates also failed: $($_.Exception.Message)"
        }
    }
    throw $packagingFailure
}
finally {
    if ($certificate -and (Test-Path -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)")) {
        Remove-Item "Cert:\CurrentUser\My\$($certificate.Thumbprint)" -Force
    }
    if ($foreignCertificate) {
        if (Test-Path -LiteralPath "Cert:\CurrentUser\My\$($foreignCertificate.Thumbprint)") {
            Remove-Item "Cert:\CurrentUser\My\$($foreignCertificate.Thumbprint)" -Force
        }
    }
}
