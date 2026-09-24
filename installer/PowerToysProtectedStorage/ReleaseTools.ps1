# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.

function Get-CarrierVersion {
    param([Parameter(Mandatory)][string]$Version)
    if ($Version -notmatch '^(0|[1-9]\d{0,2})\.(0|[1-9]\d{0,2})\.(0|[1-9]\d{0,4})(?:\.0)?$' -or
        [int]$Matches[1] -gt 255 -or [int]$Matches[2] -gt 255 -or [int]$Matches[3] -gt 65535) {
        throw 'Version must be MSI major.minor.build, optionally followed by .0; nonzero revisions are not supported.'
    }
    $msiVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3])"
    [pscustomobject]@{ Msi = $msiVersion; PE = "$msiVersion.0" }
}

function Get-ReleaseHash {
    param([Parameter(Mandatory)][string]$Path)
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-CertificateDiagnosticHash {
    param([Parameter(Mandatory)]$Certificate)
    $hash = [Security.Cryptography.SHA256]::Create()
    try {
        ([BitConverter]::ToString($hash.ComputeHash($Certificate.RawData))).Replace('-', '').ToLowerInvariant()
    } finally {
        $hash.Dispose()
    }
}

function Assert-ProductionTrustPolicy {
    param([Parameter(Mandatory)][string]$TrustPolicy)
    if ($TrustPolicy -cne 'microsoft-production-v1') {
        throw 'Only the fixed microsoft-production-v1 release trust policy is supported.'
    }
}

function Invoke-ReleaseTrustVerifier {
    param(
        [Parameter(Mandatory)][string]$TrustVerifierPath,
        [Parameter(Mandatory)][ValidateSet('verify-file', 'verify-detached')][string]$Operation,
        [Parameter(Mandatory)][string]$Path,
        [string]$SignaturePath
    )
    foreach ($file in @($TrustVerifierPath, $Path) + @($SignaturePath | Where-Object { $_ })) {
        if (![IO.Path]::IsPathFullyQualified($file) -or !(Test-Path -LiteralPath $file -PathType Leaf) -or $file.Contains('"')) {
            throw "Release verification requires an existing absolute file path: $file"
        }
    }
    $arguments = @($Operation, "`"$Path`"")
    if ($Operation -eq 'verify-detached') {
        if (!$SignaturePath) { throw 'Detached verification requires a signature path.' }
        $arguments += "`"$SignaturePath`""
    }
    $process = Start-Process -FilePath $TrustVerifierPath -ArgumentList $arguments -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Production release trust verification failed ($($process.ExitCode)): $Path"
    }
}

function Assert-ReleaseSignature {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$TrustVerifierPath
    )
    Invoke-ReleaseTrustVerifier -TrustVerifierPath $TrustVerifierPath -Operation verify-file -Path $Path
}

function Assert-DetachedSignature {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$SignaturePath,
        [Parameter(Mandatory)][string]$TrustVerifierPath
    )
    Invoke-ReleaseTrustVerifier -TrustVerifierPath $TrustVerifierPath -Operation verify-detached -Path $Path -SignaturePath $SignaturePath
}

function Get-ReleaseTimestampReferences {
    Add-Type -AssemblyName System.Security.Cryptography.Pkcs
    # Cryptography assemblies were consolidated after .NET 6 (PowerShell 7.2).
    @(
        [Security.Cryptography.RSA].Assembly.Location
        [Security.Cryptography.HashAlgorithmName].Assembly.Location
        [Security.Cryptography.Oid].Assembly.Location
        [Security.Cryptography.OidCollection].Assembly.Location
        [Security.Cryptography.AsnEncodedData].Assembly.Location
        [Security.Cryptography.X509Certificates.X509Certificate2].Assembly.Location
        [Security.Cryptography.Pkcs.SignedCms].Assembly.Location
        [Net.Http.HttpClient].Assembly.Location
        [Net.HttpStatusCode].Assembly.Location
        'System.Runtime', 'System.Collections', 'System.Threading', 'System.Threading.Tasks',
        'System.Memory', 'System.Console'
    ) | Select-Object -Unique
}

function Import-ReleaseTimestamping {
    if (!('PowerToys.ProtectedStorage.Build.Timestamping' -as [type])) {
        Add-Type -Path "$PSScriptRoot\Timestamping.cs" -ReferencedAssemblies (Get-ReleaseTimestampReferences)
    }
}

function Assert-ReleaseClientCatalog {
    param([Parameter(Mandatory)]$Clients)
    $expected = @(
        'PowerToys.WorkspacesEditor.exe|workspaces.writer',
        'PowerToys.WorkspacesEditor.exe|workspaces.preview',
        'PowerToys.WorkspacesLauncher.exe|workspaces.launcher',
        'PowerToys.WorkspacesLauncher.exe|workspaces.preview',
        'PowerToys.WorkspacesSnapshotTool.exe|workspaces.preview',
        'PowerToys.WorkspacesWindowArranger.exe|workspaces.arranger',
        'Microsoft.CmdPal.Ext.PowerToys.exe|workspaces.reader',
        'PowerToys.ProtectedStorageMsiAction.exe|maintenance'
    )
    $seen = @()
    $hashes = @{}
    foreach ($client in $Clients) {
        $identity = "$($client.image)|$($client.role)"
        if ($identity -cnotin $expected -or $identity -cin $seen -or $client.sha256 -cnotmatch '^[0-9a-f]{64}$') {
            throw "Unexpected, duplicate, or unhashed release client: $identity"
        }
        if ($hashes.ContainsKey($client.image) -and $hashes[$client.image] -cne $client.sha256) {
            throw "Different roles must bind the same final bytes for client: $($client.image)"
        }
        $hashes[$client.image] = $client.sha256
        $seen += $identity
    }
    if ($seen.Count -ne $expected.Count) { throw 'The signed release catalog must contain the complete fixed client-role inventory.' }
}

function Assert-ReleaseDocuments {
    param(
        [Parameter(Mandatory)][string]$PackageRoot,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$TrustVerifierPath
    )
    $peVersion = (Get-CarrierVersion $Version).PE
    Assert-DetachedSignature -Path "$PackageRoot\manifest.txt" -SignaturePath "$PackageRoot\manifest.p7s" -TrustVerifierPath $TrustVerifierPath
    Assert-DetachedSignature -Path "$PackageRoot\ClientCatalog.json" -SignaturePath "$PackageRoot\ClientCatalog.p7s" -TrustVerifierPath $TrustVerifierPath
    $expectedManifest = "format=1`napp=PowerToysProtectedStorage`nversion=$peVersion`nbootstrap_sha256=$(Get-ReleaseHash "$PackageRoot\Bootstrap.exe")`nruntime_sha256=$(Get-ReleaseHash "$PackageRoot\Runtime.exe")`ncatalog_sha256=$(Get-ReleaseHash "$PackageRoot\ClientCatalog.json")`n"
    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes("$PackageRoot\manifest.txt")) -cne
        [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($expectedManifest))) {
        throw 'Signed manifest does not describe the current release payloads.'
    }
    $expectedPolicy = "format=2`napp=PowerToysProtectedStorage`nsigner_policy=microsoft-production-v1`nminimum_version=$peVersion`n"
    if ([Convert]::ToBase64String([IO.File]::ReadAllBytes("$PackageRoot\policy.txt")) -cne
        [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($expectedPolicy))) {
        throw 'Seed policy does not exactly match the fixed production trust policy and release version.'
    }
    $catalog = Get-Content -LiteralPath "$PackageRoot\ClientCatalog.json" -Raw | ConvertFrom-Json
    if ($catalog.format -ne 1 -or $catalog.app -cne 'PowerToysProtectedStorage' -or $catalog.version -cne $peVersion) {
        throw 'Signed catalog release identity mismatch.'
    }
    Assert-ReleaseClientCatalog $catalog.clients
    $maintenance = @($catalog.clients | Where-Object { $_.role -eq 'maintenance' })
    if ($maintenance.Count -ne 1 -or $maintenance[0].image -cne 'PowerToys.ProtectedStorageMsiAction.exe' -or
        $maintenance[0].sha256 -cne (Get-ReleaseHash "$PackageRoot\PowerToys.ProtectedStorageMsiAction.exe")) {
        throw 'Signed catalog does not bind the current MSI action.'
    }
}
