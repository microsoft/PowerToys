[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipBuild,
    [switch]$TrustMachine
)

$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Core' -or $PSVersionTable.PSVersion -lt [version]'7.0') {
    throw 'Package.ps1 requires PowerShell 7 or later (pwsh.exe).'
}
$root = $PSScriptRoot
$repositoryRoot = (& git -C $root rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw 'Package.ps1 requires the prototype to be inside a Git worktree.'
}
$sourceCommit = (& git -C $root rev-parse --verify HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-fA-F]{40}$') {
    throw 'Package.ps1 could not resolve the source HEAD commit.'
}
$sourceChanges = @(& git -C $repositoryRoot status --porcelain)
if ($LASTEXITCODE -ne 0) {
    throw 'Package.ps1 could not inspect source worktree status.'
}
$sourceTreeClean = $sourceChanges.Count -eq 0
if (-not $SkipBuild) {
    & (Join-Path $root 'Build.ps1') -Configuration $Configuration -Clean
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if ($TrustMachine -and -not $principal.IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw '-TrustMachine requires an elevated PowerShell session.'
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

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
    Select-Object -First 1
if (-not $msbuild) {
    throw 'MSBuild was not found.'
}

$binRoot = Join-Path $root "artifacts\bin\x64\$Configuration"
$releaseRoot = Join-Path $root 'artifacts\release'
$releaseSetsRoot = Join-Path $root 'artifacts\release-sets'
$generatedRoot = Join-Path $root 'artifacts\generated'
$payloadRoot = Join-Path $root 'artifacts\msi-payload'
$msiRoot = Join-Path $root 'artifacts\msi'
$ownershipPath = Join-Path $releaseRoot 'certificate-ownership.json'
$certificateStores = @(
    'Cert:\CurrentUser\My',
    'Cert:\CurrentUser\TrustedPeople',
    'Cert:\LocalMachine\TrustedPeople'
)
$publisher = 'CN=PowerToys Workspaces Control-Plane Code Test'
$metadataPublisher = 'CN=PowerToys Workspaces Control-Plane Metadata Test'
$foreignPublisher = 'CN=PowerToys Workspaces Control-Plane Foreign Test'

function Get-CertificateSha256([Security.Cryptography.X509Certificates.X509Certificate2]$Certificate) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Certificate.RawData))
}

function Test-CertificatePresent([string]$Store, [string]$Thumbprint) {
    return @(
        Get-ChildItem -Path $Store | Where-Object { $_.Thumbprint -eq $Thumbprint }
    ).Count -ne 0
}

function Remove-ExactCertificates([string]$Store, [string]$Thumbprint) {
    @(
        Get-ChildItem -Path $Store | Where-Object { $_.Thumbprint -eq $Thumbprint }
    ) | ForEach-Object {
        Remove-Item -LiteralPath $_.PSPath -Force
    }
}

function Restore-ExactCertificateOwnership([object]$Ownership) {
    if (-not $Ownership) {
        return
    }
    if ($Ownership.format -ne 2) {
        throw 'Certificate ownership state has an unsupported format.'
    }
    foreach ($record in @($Ownership.certificates)) {
        if ($record.role -notin @('code', 'metadata', 'foreign') -or
            $record.thumbprint -notmatch '^[0-9A-F]{40}$') {
            throw 'Certificate ownership identity is invalid.'
        }
        foreach ($store in @($record.stores)) {
            if ($store.path -notin $certificateStores -or
                $store.preRunPresent -isnot [bool] -or
                $store.introducedByRun -isnot [bool]) {
                throw 'Certificate ownership store state is invalid.'
            }
            if ($store.introducedByRun -and (Test-CertificatePresent $store.path $record.thumbprint)) {
                Remove-ExactCertificates $store.path $record.thumbprint
            }
            if ((Test-CertificatePresent $store.path $record.thumbprint) -ne $store.preRunPresent) {
                throw "Could not restore certificate state for $($record.role) at $($store.path)."
            }
        }
    }
}

function New-CertificateOwnership([object[]]$Records) {
    return [ordered]@{
        format = 2
        certificates = @(
            foreach ($record in $Records) {
                [ordered]@{
                    role = $record.role
                    thumbprint = $record.certificate.Thumbprint
                    stores = @(
                        foreach ($store in $certificateStores) {
                            [ordered]@{
                                path = $store
                                preRunPresent = $false
                                introducedByRun = $store -eq 'Cert:\CurrentUser\My'
                            }
                        }
                    )
                }
            }
        )
    }
}

function Save-CertificateOwnership([object]$Ownership) {
    $Ownership | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath $ownershipPath -Encoding utf8NoBOM
}

function Mark-AndImportCertificate(
    [object]$Ownership,
    [string]$Role,
    [string]$CertificatePath,
    [string]$Store
) {
    $record = @($Ownership.certificates | Where-Object { $_.role -eq $Role })
    if ($record.Count -ne 1) {
        throw "Certificate ownership role is not unique: $Role"
    }
    $entry = @($record[0].stores | Where-Object { $_.path -eq $Store })
    if ($entry.Count -ne 1) {
        throw "Certificate ownership store is not unique: $Role $Store"
    }
    if (-not (Test-CertificatePresent $Store $record[0].thumbprint)) {
        $entry[0].introducedByRun = $true
        Save-CertificateOwnership $Ownership
        Import-Certificate -FilePath $CertificatePath -CertStoreLocation $Store | Out-Null
    }
    if (-not (Test-CertificatePresent $Store $record[0].thumbprint)) {
        throw "Could not trust $Role certificate in $Store."
    }
}

function Sign-AndVerify(
    [string]$Path,
    [Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
    [string]$ExpectedSignerSha256
) {
    & $signtool sign /fd SHA256 /sha1 $Certificate.Thumbprint /s My $Path | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "signtool sign failed: $Path"
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    $actual = if ($signature.SignerCertificate) {
        Get-CertificateSha256 $signature.SignerCertificate
    }
    if ($signature.Status -ne 'Valid' -or $actual -ne $ExpectedSignerSha256) {
        throw "Authenticode verification failed: $Path"
    }
}

function Invoke-PrototypeBuild([string]$Project, [string[]]$Properties) {
    & $msbuild $Project /m /t:Rebuild "/p:Configuration=$Configuration" '/p:Platform=x64' `
        @Properties /v:minimal /nologo | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed: $Project"
    }
}

function Write-RcData([string]$DataPath, [string]$ResourceName, [string]$RcPath) {
    $resourcePath = $DataPath.Replace('\', '/')
    "$ResourceName RCDATA `"$resourcePath`"" |
        Set-Content -LiteralPath $RcPath -Encoding ascii
}

function New-PrototypeCodeSigningCertificate([string]$Subject, [string]$Role) {
    $rsa = [Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new(
            $Subject,
            $rsa,
            [Security.Cryptography.HashAlgorithmName]::SHA256,
            [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $keyUsage = [Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
            [Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature,
            $true)
        $request.CertificateExtensions.Add($keyUsage)
        $oids = [Security.Cryptography.OidCollection]::new()
        [void]$oids.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.3'))
        $request.CertificateExtensions.Add(
            [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($oids, $false))
        $certificate = $request.CreateSelfSigned((Get-Date).AddMinutes(-5), (Get-Date).AddYears(2))
        try {
            $entropy = New-Object byte[] 32
            $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
            try {
                $generator.GetBytes($entropy)
            }
            finally {
                $generator.Dispose()
            }
            $passwordText = [Convert]::ToBase64String($entropy)
            $password = ConvertTo-SecureString -String $passwordText -AsPlainText -Force
            $pfxPath = Join-Path $generatedRoot "$Role-signing-key.pfx"
            try {
                [IO.File]::WriteAllBytes(
                    $pfxPath,
                    $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $passwordText))
                Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $password |
                    Out-Null
            }
            finally {
                if (Test-Path -LiteralPath $pfxPath) {
                    Remove-Item -LiteralPath $pfxPath -Force
                }
            }
            $stored = Get-ChildItem -LiteralPath "Cert:\CurrentUser\My\$($certificate.Thumbprint)"
            if (-not $stored -or -not $stored.HasPrivateKey) {
                throw "Could not persist the $Role signing certificate private key."
            }
            return $stored
        }
        finally {
            $certificate.Dispose()
        }
    }
    finally {
        $rsa.Dispose()
    }
}

function Build-Policy(
    [string]$TargetName,
    [string]$VersionResource,
    [string]$Kind,
    [string]$Pin
) {
    $dataPath = Join-Path $generatedRoot "$TargetName.data.txt"
    $rcPath = Join-Path $generatedRoot "$TargetName.data.rc"
    @(
        'schemaVersion=1'
        "kind=$Kind"
        if ($Kind -eq 'code') { "codeSignerSha256=$Pin" } else { "metadataSignerSha256=$Pin" }
    ) | Set-Content -LiteralPath $dataPath -Encoding utf8NoBOM
    Write-RcData $dataPath 'PTPUVR_POLICY' $rcPath
    $outDirectory = Join-Path $generatedRoot "$TargetName\"
    $intDirectory = Join-Path $root "artifacts\obj\$TargetName\x64\$Configuration\"
    Invoke-PrototypeBuild (Join-Path $root 'Policy\PtPuvrPolicy.vcxproj') @(
        "/p:TargetName=$TargetName",
        "/p:PolicyVersionResource=$VersionResource",
        "/p:PolicyDataFile=$rcPath",
        "/p:OutDir=$outDirectory",
        "/p:IntDir=$intDirectory"
    )
    return (Join-Path $outDirectory "$TargetName.exe")
}

function Build-Manifest([pscustomobject]$Definition) {
    $runtime = $Definition.runtime
    $engineKey = if ($Definition.PSObject.Properties.Name -contains 'engineKey') {
        [string]$Definition.engineKey
    }
    else {
        [string]$Definition.engineVersion
    }
    $manifestReleaseId = if ($Definition.PSObject.Properties.Name -contains 'manifestReleaseId') {
        [string]$Definition.manifestReleaseId
    }
    else {
        [string]$Definition.id
    }
    if ($runtime -is [array] -or
        $runtime.PSObject.Properties.Name -notcontains 'length' -or
        $runtime.length -isnot [long] -or
        $runtime.length -le 0 -or
        $runtime.length -gt (64MB) -or
        $runtime.track -notin @(1, 2) -or
        [string]::IsNullOrWhiteSpace($runtime.version) -or
        [string]::IsNullOrWhiteSpace($Definition.runtimeFile) -or
        $Definition.runtimeLength -isnot [long] -or
        $Definition.runtimeLength -le 0 -or
        $Definition.runtimeLength -gt (64MB) -or
        [string]::IsNullOrWhiteSpace($Definition.runtimeHash)) {
        throw "Release manifest definition has an invalid runtime descriptor: $($Definition.id)"
    }
    if (-not $Definition.allowRuntimeLengthMismatch -and
        $Definition.runtimeLength -ne [long]$runtime.length) {
        throw "Release manifest runtime length does not match its source: $($Definition.id)"
    }
    if ($Definition.engineVersion -eq 'none') {
        if ($Definition.engineFile -ne 'none' -or
            $Definition.engineLength -ne 'none' -or
            $Definition.engineHash -ne 'none') {
            throw "Release manifest definition has an invalid no-engine descriptor: $($Definition.id)"
        }
    }
    elseif ($Definition.engineLength -isnot [long] -or
        $Definition.engineLength -le 0 -or
        $Definition.engineLength -gt (64MB) -or
        $Definition.engineLength -ne [long]$engines[$engineKey].length) {
        throw "Release manifest definition has an invalid engine length: $($Definition.id)"
    }
    $dataPath = Join-Path $generatedRoot "$($Definition.id).manifest.txt"
    $rcPath = Join-Path $generatedRoot "$($Definition.id).manifest.rc"
    $lines = @(
        'schemaVersion=2'
        "releaseId=$manifestReleaseId"
        "securityEpoch=$($Definition.epoch)"
        "minimumHostVersion=$($Definition.minimumHostVersion)"
        "runtimeTrack=$($runtime.track)"
        "runtimeVersion=$($runtime.version)"
        "runtimeFile=$($Definition.runtimeFile)"
        "runtimeLength=$($Definition.runtimeLength)"
        "runtimeSha256=$($Definition.runtimeHash)"
        "engineVersion=$($Definition.engineVersion)"
        "engineFile=$($Definition.engineFile)"
        "engineLength=$($Definition.engineLength)"
        "engineSha256=$($Definition.engineHash)"
    )
    if ($Definition.engineCrashPhase -ne 'none') {
        $lines += "testEngineCrashPhase=$($Definition.engineCrashPhase)"
    }
    if ($Definition.runtimeCrashPhase -ne 'none') {
        $lines += "testRuntimeCrashPhase=$($Definition.runtimeCrashPhase)"
    }
    $lines | Set-Content -LiteralPath $dataPath -Encoding utf8NoBOM
    Write-RcData $dataPath 'PTPUVR_MANIFEST' $rcPath
    $outDirectory = Join-Path $generatedRoot "manifest-$($Definition.id)\"
    $intDirectory = Join-Path $root "artifacts\obj\manifest-$($Definition.id)\x64\$Configuration\"
    Invoke-PrototypeBuild (Join-Path $root 'Manifest\PtPuvrManifest.vcxproj') @(
        "/p:ManifestDataFile=$rcPath",
        "/p:OutDir=$outDirectory",
        "/p:IntDir=$intDirectory"
    )
    return (Join-Path $outDirectory 'PtPuvrReleaseManifest.exe')
}

$codeCertificate = $null
$metadataCertificate = $null
$foreignCertificate = $null
$certificateOwnership = $null

try {
    if (Test-Path -LiteralPath $ownershipPath -PathType Leaf) {
        Restore-ExactCertificateOwnership (Get-Content -LiteralPath $ownershipPath -Raw | ConvertFrom-Json)
    }
    foreach ($directory in $releaseRoot, $releaseSetsRoot, $generatedRoot, $payloadRoot, $msiRoot) {
        Remove-Item -LiteralPath $directory -Recurse -Force -ErrorAction SilentlyContinue
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    $codeCertificate = New-PrototypeCodeSigningCertificate $publisher 'code'
    $metadataCertificate = New-PrototypeCodeSigningCertificate $metadataPublisher 'metadata'
    $foreignCertificate = New-PrototypeCodeSigningCertificate $foreignPublisher 'foreign'

    $codePin = Get-CertificateSha256 $codeCertificate
    $metadataPin = Get-CertificateSha256 $metadataCertificate
    $foreignPin = Get-CertificateSha256 $foreignCertificate
    $codeCertificatePath = Join-Path $releaseRoot 'PtPuvr-CodeSigner-TestOnly.cer'
    $metadataCertificatePath = Join-Path $releaseRoot 'PtPuvr-MetadataSigner-TestOnly.cer'
    $foreignCertificatePath = Join-Path $releaseRoot 'PtPuvr-ForeignSigner-TestOnly.cer'
    Export-Certificate -Cert $codeCertificate -FilePath $codeCertificatePath -Force | Out-Null
    Export-Certificate -Cert $metadataCertificate -FilePath $metadataCertificatePath -Force | Out-Null
    Export-Certificate -Cert $foreignCertificate -FilePath $foreignCertificatePath -Force | Out-Null

    $certificateOwnership = New-CertificateOwnership @(
        [pscustomobject]@{ role = 'code'; certificate = $codeCertificate }
        [pscustomobject]@{ role = 'metadata'; certificate = $metadataCertificate }
        [pscustomobject]@{ role = 'foreign'; certificate = $foreignCertificate }
    )
    Save-CertificateOwnership $certificateOwnership
    foreach ($record in @(
            [pscustomobject]@{ role = 'code'; path = $codeCertificatePath }
            [pscustomobject]@{ role = 'metadata'; path = $metadataCertificatePath }
            [pscustomobject]@{ role = 'foreign'; path = $foreignCertificatePath }
        )) {
        Mark-AndImportCertificate $certificateOwnership $record.role $record.path 'Cert:\CurrentUser\TrustedPeople'
        if ($TrustMachine) {
            Mark-AndImportCertificate $certificateOwnership $record.role $record.path 'Cert:\LocalMachine\TrustedPeople'
        }
    }

    $codePolicySource = Build-Policy 'PtPuvrCodePolicy' 'CodePolicyVersion.rc' 'code' $codePin
    $metadataPolicySource = Build-Policy 'PtPuvrMetadataPolicy' 'MetadataPolicyVersion.rc' 'metadata' $metadataPin

    function Copy-SignedCodeArtifact([string]$Source, [string]$Name) {
        $destination = Join-Path $releaseRoot $Name
        Copy-Item -LiteralPath $Source -Destination $destination
        Sign-AndVerify $destination $codeCertificate $codePin
        return [pscustomobject]@{
            file = $Name
            path = $destination
            sha256 = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
            length = (Get-Item -LiteralPath $destination).Length
            version = (Get-Item -LiteralPath $destination).VersionInfo.FileVersion
        }
    }

    $hostArtifact = Copy-SignedCodeArtifact (Join-Path $binRoot 'PtPuvrHost.exe') 'PtPuvrHost.exe'
    $userClient = Copy-SignedCodeArtifact (Join-Path $binRoot 'PtPuvrUserClient.exe') 'PtPuvrUserClient.exe'
    $codePolicy = Copy-SignedCodeArtifact $codePolicySource 'PtPuvrCodePolicy.exe'
    $metadataPolicy = Copy-SignedCodeArtifact $metadataPolicySource 'PtPuvrMetadataPolicy.exe'

    $engines = @{}
    foreach ($version in '5.0.0.0', '5.1.0.0', '5.2.0.0', '5.3.0.0', '5.4.0.0') {
        $engines[$version] = Copy-SignedCodeArtifact `
            (Join-Path $binRoot "engine-$version\PtPuvrUpdater.exe") `
            "PtPuvrEngine-$version.exe"
        if ($engines[$version].version -ne $version) {
            throw "Engine version mismatch: $version"
        }
    }
    $engines['5.4.0.0-collision'] = Copy-SignedCodeArtifact `
        (Join-Path $binRoot 'engine-5.4.0.0-collision\PtPuvrUpdater.exe') `
        'PtPuvrEngine-5.4.0.0-Collision.exe'
    if ($engines['5.4.0.0-collision'].version -ne '5.4.0.0') {
        throw 'Engine version mismatch: 5.4.0.0 collision'
    }
    if ($engines['5.4.0.0-collision'].sha256 -eq $engines['5.4.0.0'].sha256) {
        throw 'Engine collision artifact must differ from the active 5.4.0.0 engine.'
    }

    $runtimeDefinitions = @(
        [pscustomobject]@{ id = 'runtime-100'; track = 1; version = '1.0.0.0'; source = 'runtime-track-1-1.0.0.0'; file = 'PtPuvrRuntime-Track1-1.0.0.0.exe'; readiness = 'healthy' }
        [pscustomobject]@{ id = 'runtime-110'; track = 1; version = '1.1.0.0'; source = 'runtime-track-1-1.1.0.0'; file = 'PtPuvrRuntime-Track1-1.1.0.0.exe'; readiness = 'healthy' }
        [pscustomobject]@{ id = 'runtime-110-collision'; track = 1; version = '1.1.0.0'; source = 'runtime-track-1-1.1.0.0-collision'; file = 'PtPuvrRuntime-Track1-1.1.0.0-Collision.exe'; readiness = 'healthy-byte-different' }
        [pscustomobject]@{ id = 'runtime-120'; track = 1; version = '1.2.0.0'; source = 'runtime-track-1-1.2.0.0'; file = 'PtPuvrRuntime-Track1-1.2.0.0.exe'; readiness = 'intentional-failure' }
        [pscustomobject]@{ id = 'runtime-130'; track = 1; version = '1.3.0.0'; source = 'runtime-track-1-1.3.0.0'; file = 'PtPuvrRuntime-Track1-1.3.0.0.exe'; readiness = 'healthy-crash-retry' }
        [pscustomobject]@{ id = 'runtime-200'; track = 2; version = '2.0.0.0'; source = 'runtime-track-2-2.0.0.0'; file = 'PtPuvrRuntime-Track2-2.0.0.0.exe'; readiness = 'healthy' }
    )
    $runtimes = @{}
    foreach ($definition in $runtimeDefinitions) {
        $artifact = Copy-SignedCodeArtifact `
            (Join-Path $binRoot "$($definition.source)\PtPuvrRuntime.exe") `
            $definition.file
        if ($artifact.version -ne $definition.version) {
            throw "Runtime version mismatch: $($definition.id)"
        }
        $artifact | Add-Member -NotePropertyName id -NotePropertyValue $definition.id
        $artifact | Add-Member -NotePropertyName track -NotePropertyValue $definition.track
        $artifact | Add-Member -NotePropertyName readiness -NotePropertyValue $definition.readiness
        $runtimes[$definition.id] = $artifact
    }

    $foreignRuntimePath = Join-Path $releaseRoot 'PtPuvrRuntime-ForeignCodeSigner.exe'
    Copy-Item -LiteralPath $runtimes['runtime-110'].path -Destination $foreignRuntimePath
    Sign-AndVerify $foreignRuntimePath $foreignCertificate $foreignPin
    $foreignRuntime = [pscustomobject]@{
        file = 'PtPuvrRuntime-ForeignCodeSigner.exe'
        path = $foreignRuntimePath
        sha256 = (Get-FileHash -LiteralPath $foreignRuntimePath -Algorithm SHA256).Hash
        length = (Get-Item -LiteralPath $foreignRuntimePath).Length
        version = '1.1.0.0'
        track = 1
    }

    $wrongProductPath = Join-Path $releaseRoot 'PtPuvrRuntime-WrongProduct.exe'
    Copy-Item -LiteralPath (Join-Path $binRoot 'runtime-wrong-product\PtPuvrRuntime.exe') -Destination $wrongProductPath
    Sign-AndVerify $wrongProductPath $codeCertificate $codePin

    $releaseDefinitions = @(
        [pscustomobject]@{ id = 'release-101'; epoch = 101; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-100']; runtimeFile = $runtimes['runtime-100'].file; runtimeHash = $runtimes['runtime-100'].sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-102'; epoch = 102; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = $runtimes['runtime-110'].file; runtimeHash = $runtimes['runtime-110'].sha256; engineVersion = '5.1.0.0'; engineFile = $engines['5.1.0.0'].file; engineHash = $engines['5.1.0.0'].sha256; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-103-readiness'; epoch = 103; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-120']; runtimeFile = $runtimes['runtime-120'].file; runtimeHash = $runtimes['runtime-120'].sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-104-engine-fail'; epoch = 104; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = $runtimes['runtime-110'].file; runtimeHash = $runtimes['runtime-110'].sha256; engineVersion = '5.2.0.0'; engineFile = $engines['5.2.0.0'].file; engineHash = $engines['5.2.0.0'].sha256; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-105-engine-before'; epoch = 105; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = $runtimes['runtime-110'].file; runtimeHash = $runtimes['runtime-110'].sha256; engineVersion = '5.3.0.0'; engineFile = $engines['5.3.0.0'].file; engineHash = $engines['5.3.0.0'].sha256; engineCrashPhase = 'before-active-switch'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-106-engine-after'; epoch = 106; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = $runtimes['runtime-110'].file; runtimeHash = $runtimes['runtime-110'].sha256; engineVersion = '5.3.0.0'; engineFile = $engines['5.3.0.0'].file; engineHash = $engines['5.3.0.0'].sha256; engineCrashPhase = 'after-active-switch-before-journal-clear'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-107-runtime-crash'; epoch = 107; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-130']; runtimeFile = $runtimes['runtime-130'].file; runtimeHash = $runtimes['runtime-130'].sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'after-inventory-before-sync'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-108-engine-stop'; epoch = 108; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-130']; runtimeFile = $runtimes['runtime-130'].file; runtimeHash = $runtimes['runtime-130'].sha256; engineVersion = '5.4.0.0'; engineFile = $engines['5.4.0.0'].file; engineHash = $engines['5.4.0.0'].sha256; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-109-same-version-collision'; epoch = 109; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110-collision']; runtimeFile = $runtimes['runtime-110-collision'].file; runtimeHash = $runtimes['runtime-110-collision'].sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-110-engine-version-collision'; epoch = 110; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-130']; runtimeFile = $runtimes['runtime-130'].file; runtimeHash = $runtimes['runtime-130'].sha256; engineVersion = '5.4.0.0'; engineKey = '5.4.0.0-collision'; engineFile = $engines['5.4.0.0-collision'].file; engineHash = $engines['5.4.0.0-collision'].sha256; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-101-collision'; manifestReleaseId = 'release-101'; epoch = 150; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = $runtimes['runtime-110'].file; runtimeHash = $runtimes['runtime-110'].sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-201-metadata-signer'; epoch = 201; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = $runtimes['runtime-110'].file; runtimeHash = $runtimes['runtime-110'].sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'foreign'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-202-tampered-manifest'; epoch = 202; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = $runtimes['runtime-110'].file; runtimeHash = $runtimes['runtime-110'].sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $true }
        [pscustomobject]@{ id = 'release-203-hash-mismatch'; epoch = 203; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = $runtimes['runtime-110'].file; runtimeHash = ('0' * 64); engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-204-traversal'; epoch = 204; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = '..\PtPuvrRuntime-Track1-1.1.0.0.exe'; runtimeHash = $runtimes['runtime-110'].sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-205-stale'; epoch = 1; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = $runtimes['runtime-110'].file; runtimeHash = $runtimes['runtime-110'].sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-206-host-floor'; epoch = 206; minimumHostVersion = '5.9.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = $runtimes['runtime-110'].file; runtimeHash = $runtimes['runtime-110'].sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-207-code-signer'; epoch = 207; minimumHostVersion = '5.0.0.0'; runtime = $foreignRuntime; runtimeFile = $foreignRuntime.file; runtimeHash = $foreignRuntime.sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-208-runtime-downgrade'; epoch = 208; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-100']; runtimeFile = $runtimes['runtime-100'].file; runtimeHash = $runtimes['runtime-100'].sha256; engineVersion = 'none'; engineFile = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false }
        [pscustomobject]@{ id = 'release-209-size-mismatch'; epoch = 209; minimumHostVersion = '5.0.0.0'; runtime = $runtimes['runtime-110']; runtimeFile = $runtimes['runtime-110'].file; runtimeLength = ([long]$runtimes['runtime-110'].length + 1); runtimeHash = $runtimes['runtime-110'].sha256; engineVersion = 'none'; engineFile = 'none'; engineLength = 'none'; engineHash = 'none'; engineCrashPhase = 'none'; runtimeCrashPhase = 'none'; manifestSigner = 'metadata'; tamperManifest = $false; allowRuntimeLengthMismatch = $true }
    )

    foreach ($definition in $releaseDefinitions) {
        if ($definition.PSObject.Properties.Name -notcontains 'runtimeLength') {
            $definition | Add-Member -NotePropertyName runtimeLength -NotePropertyValue ([long]$definition.runtime.length)
        }
        if ($definition.PSObject.Properties.Name -notcontains 'engineLength') {
            $engineLength = if ($definition.engineVersion -eq 'none') {
                'none'
            }
            else {
                $engineKey = if ($definition.PSObject.Properties.Name -contains 'engineKey') {
                    [string]$definition.engineKey
                }
                else {
                    [string]$definition.engineVersion
                }
                [long]$engines[$engineKey].length
            }
            $definition | Add-Member -NotePropertyName engineLength -NotePropertyValue $engineLength
        }
        if ($definition.PSObject.Properties.Name -notcontains 'allowRuntimeLengthMismatch') {
            $definition | Add-Member -NotePropertyName allowRuntimeLengthMismatch -NotePropertyValue $false
        }
    }

    $releaseSetMetadata = @()
    foreach ($definition in $releaseDefinitions) {
        $manifestSource = Build-Manifest $definition
        $setPath = Join-Path $releaseSetsRoot $definition.id
        New-Item -ItemType Directory -Path $setPath -Force | Out-Null
        $manifestPath = Join-Path $setPath 'PtPuvrReleaseManifest.exe'
        Copy-Item -LiteralPath $manifestSource -Destination $manifestPath
        if ($definition.manifestSigner -eq 'metadata') {
            Sign-AndVerify $manifestPath $metadataCertificate $metadataPin
        }
        else {
            Sign-AndVerify $manifestPath $foreignCertificate $foreignPin
        }
        if ($definition.tamperManifest) {
            $bytes = [IO.File]::ReadAllBytes($manifestPath)
            $bytes[$bytes.Length - 1] = $bytes[$bytes.Length - 1] -bxor 0x5A
            [IO.File]::WriteAllBytes($manifestPath, $bytes)
        }
        if ((Get-Item -LiteralPath $manifestPath).Length -gt 1MB) {
            throw "Signed release manifest exceeds the 1 MiB product bound: $($definition.id)"
        }
        if ($definition.runtimeFile -notmatch '[\\/]') {
            Copy-Item -LiteralPath $definition.runtime.path -Destination (Join-Path $setPath $definition.runtimeFile)
        }
        if ($definition.engineVersion -ne 'none') {
            $engineKey = if ($definition.PSObject.Properties.Name -contains 'engineKey') {
                [string]$definition.engineKey
            }
            else {
                [string]$definition.engineVersion
            }
            Copy-Item -LiteralPath $engines[$engineKey].path -Destination (Join-Path $setPath $definition.engineFile)
        }
        $releaseSetMetadata += [ordered]@{
            id = $definition.id
            releaseId = if ($definition.PSObject.Properties.Name -contains 'manifestReleaseId') {
                $definition.manifestReleaseId
            } else {
                $definition.id
            }
            securityEpoch = [uint64]$definition.epoch
            runtimeVersion = $definition.runtime.version
            engineVersion = $definition.engineVersion
            path = $definition.id
            negative = $definition.id -match '^release-20' -or
                $definition.id -eq 'release-109-same-version-collision' -or
                $definition.id -eq 'release-110-engine-version-collision' -or
                $definition.id -eq 'release-101-collision'
        }
    }

    foreach ($artifact in $hostArtifact, $userClient, $codePolicy, $metadataPolicy, $engines['5.0.0.0']) {
        Copy-Item -LiteralPath $artifact.path -Destination (Join-Path $payloadRoot $artifact.file)
    }
    @(
        [ordered]@{ file = 'code-signer-sha256.txt'; value = $codePin }
        [ordered]@{ file = 'metadata-signer-sha256.txt'; value = $metadataPin }
    ) | ForEach-Object {
        Set-Content -LiteralPath (Join-Path $payloadRoot $_.file) -Value $_.value -Encoding ascii -NoNewline
    }

    $wixProject = Join-Path $root 'Installer\PtPuvrControlPlane.wixproj'
    & dotnet build $wixProject -c Release "-p:PayloadDir=$payloadRoot" --nologo
    if ($LASTEXITCODE -ne 0) {
        throw 'WiX v5 MSI build failed.'
    }
    $builtMsi = Get-ChildItem -LiteralPath (Join-Path $root 'Installer\bin') -Recurse -Filter 'PtPuvrControlPlane.msi' |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if (-not $builtMsi) {
        throw 'WiX v5 build did not produce PtPuvrControlPlane.msi.'
    }
    $msiPath = Join-Path $msiRoot 'PtPuvrControlPlane.msi'
    Copy-Item -LiteralPath $builtMsi.FullName -Destination $msiPath
    Sign-AndVerify $msiPath $codeCertificate $codePin

    [ordered]@{
        format = 2
        sourceCommit = $sourceCommit
        sourceTreeClean = $sourceTreeClean
        codeSigner = [ordered]@{
            certificateFile = [IO.Path]::GetFileName($codeCertificatePath)
            thumbprint = $codeCertificate.Thumbprint
            signerSha256 = $codePin
        }
        metadataSigner = [ordered]@{
            certificateFile = [IO.Path]::GetFileName($metadataCertificatePath)
            thumbprint = $metadataCertificate.Thumbprint
            signerSha256 = $metadataPin
        }
        foreignSigner = [ordered]@{
            certificateFile = [IO.Path]::GetFileName($foreignCertificatePath)
            thumbprint = $foreignCertificate.Thumbprint
            signerSha256 = $foreignPin
        }
        host = $hostArtifact
        userClient = $userClient
        engines = @($engines.Values | Sort-Object version)
        runtimes = @($runtimes.Values | Sort-Object id)
        releaseSets = $releaseSetMetadata
        msi = [ordered]@{
            file = 'PtPuvrControlPlane.msi'
            path = 'msi\PtPuvrControlPlane.msi'
            sha256 = (Get-FileHash -LiteralPath $msiPath -Algorithm SHA256).Hash
            productName = 'PowerToys Workspaces Protected Runtime Control-Plane Prototype'
            upgradeCode = '{5B4C4E51-C55B-4F91-984A-D4A0D7D4FA31}'
        }
    } | ConvertTo-Json -Depth 10 |
        Set-Content -LiteralPath (Join-Path $releaseRoot 'artifacts.json') -Encoding utf8NoBOM
    Save-CertificateOwnership $certificateOwnership
    Write-Host "Built signed host, engines, user client, metadata release sets, and WiX v5 companion MSI: $msiPath"
}
catch {
    $failure = $_
    if ($certificateOwnership) {
        Restore-ExactCertificateOwnership $certificateOwnership
    }
    throw $failure
}
finally {
    foreach ($certificate in $codeCertificate, $metadataCertificate, $foreignCertificate) {
        if ($certificate -and (Test-CertificatePresent 'Cert:\CurrentUser\My' $certificate.Thumbprint)) {
            Remove-ExactCertificates 'Cert:\CurrentUser\My' $certificate.Thumbprint
        }
    }
}
