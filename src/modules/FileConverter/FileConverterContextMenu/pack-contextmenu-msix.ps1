param(
    [string]$Platform = "x64",
    [string]$Configuration = "Debug",
    [int]$KeepRecent = 5,
    [switch]$UseDevIdentity,
    [string]$DevPublisher = "CN=PowerToys-Dev",
    [string]$DevIdentityName,
    [switch]$CreateDevCertificate,
    [switch]$SignPackage,
    [switch]$RegisterPackage,
    [switch]$UseLooseRegister
)

$ErrorActionPreference = "Stop"

function Get-MakeAppxPath {
    $command = Get-Command MakeAppx.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $kitsRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (-not (Test-Path $kitsRoot)) {
        throw "MakeAppx.exe was not found in PATH and Windows Kits bin folder does not exist."
    }

    $candidates = Get-ChildItem -Path $kitsRoot -Recurse -Filter MakeAppx.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\MakeAppx\.exe$" } |
        Sort-Object FullName -Descending

    if ($null -eq $candidates -or $candidates.Count -eq 0) {
        throw "MakeAppx.exe was not found under $kitsRoot."
    }

    return $candidates[0].FullName
}

function Get-SignToolPath {
    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $kitsRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (-not (Test-Path $kitsRoot)) {
        throw "signtool.exe was not found in PATH and Windows Kits bin folder does not exist."
    }

    $candidates = Get-ChildItem -Path $kitsRoot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
        Sort-Object FullName -Descending

    if ($null -eq $candidates -or $candidates.Count -eq 0) {
        throw "signtool.exe was not found under $kitsRoot."
    }

    return $candidates[0].FullName
}

function Get-OrCreateDevCertificate {
    param(
        [string]$Subject,
        [bool]$CreateIfMissing
    )

    $cert = Get-ChildItem -Path Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $Subject } |
        Sort-Object NotAfter -Descending |
        Select-Object -First 1

    if ($null -ne $cert) {
        return $cert
    }

    if (-not $CreateIfMissing) {
        throw "No certificate with subject '$Subject' was found in Cert:\CurrentUser\My. Re-run with -CreateDevCertificate."
    }

    Write-Host "Creating new dev code-signing certificate:" $Subject
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject -CertStoreLocation "Cert:\CurrentUser\My"
    return $cert
}

function Ensure-CertificateTrustedForCurrentUser {
    param([System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate)

    $trustedPeople = Get-ChildItem -Path Cert:\CurrentUser\TrustedPeople |
        Where-Object { $_.Thumbprint -eq $Certificate.Thumbprint } |
        Select-Object -First 1
    $trustedRoot = Get-ChildItem -Path Cert:\CurrentUser\Root |
        Where-Object { $_.Thumbprint -eq $Certificate.Thumbprint } |
        Select-Object -First 1

    if (($null -ne $trustedPeople) -and ($null -ne $trustedRoot)) {
        return
    }

    $tempCer = Join-Path ([System.IO.Path]::GetTempPath()) ("powertoys-dev-" + [System.Guid]::NewGuid().ToString("N") + ".cer")
    try {
        Export-Certificate -Cert $Certificate -FilePath $tempCer -Type CERT | Out-Null
        if ($null -eq $trustedPeople) {
            Import-Certificate -FilePath $tempCer -CertStoreLocation "Cert:\CurrentUser\TrustedPeople" | Out-Null
            Write-Host "Imported certificate into CurrentUser\\TrustedPeople:" $Certificate.Thumbprint
        }

        if ($null -eq $trustedRoot) {
            Import-Certificate -FilePath $tempCer -CertStoreLocation "Cert:\CurrentUser\Root" | Out-Null
            Write-Host "Imported certificate into CurrentUser\\Root:" $Certificate.Thumbprint
        }
    }
    finally {
        Remove-Item -Path $tempCer -Force -ErrorAction SilentlyContinue
    }
}

function Set-ManifestDevIdentity {
    param(
        [string]$SourceManifestPath,
        [string]$DestinationManifestPath,
        [string]$Publisher,
        [string]$IdentityName
    )

    [xml]$manifest = Get-Content -Path $SourceManifestPath
    $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace("appx", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
    $identityNode = $manifest.SelectSingleNode("/appx:Package/appx:Identity", $ns)

    if ($null -eq $identityNode) {
        throw "Manifest Identity node was not found in $SourceManifestPath."
    }

    $identityNode.Publisher = $Publisher
    if (-not [string]::IsNullOrWhiteSpace($IdentityName)) {
        $identityNode.Name = $IdentityName
    }

    $manifest.Save($DestinationManifestPath)
}

function Get-ManifestIdentity {
    param([string]$ManifestPath)

    [xml]$manifest = Get-Content -Path $ManifestPath
    $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace("appx", "http://schemas.microsoft.com/appx/manifest/foundation/windows10")
    $identityNode = $manifest.SelectSingleNode("/appx:Package/appx:Identity", $ns)
    if ($null -eq $identityNode) {
        throw "Manifest Identity node was not found in $ManifestPath."
    }

    return @{
        Name = [string]$identityNode.Name
        Publisher = [string]$identityNode.Publisher
    }
}

function Get-ManifestComDllPath {
    param([string]$ManifestPath)

    [xml]$manifest = Get-Content -Path $ManifestPath
    $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace("com", "http://schemas.microsoft.com/appx/manifest/com/windows10")
    $classNode = $manifest.SelectSingleNode("//com:Class", $ns)

    if ($null -eq $classNode) {
        throw "Manifest com:Class node was not found in $ManifestPath."
    }

    return [string]$classNode.Path
}

function Invoke-LooseRegistration {
    param(
        [string]$OutDir,
        [string]$ManifestPath,
        [string]$BuiltContextMenuDllPath,
        [string]$AssetsSourcePath
    )

    $registerRoot = Join-Path $OutDir "FileConverterDevRegister"
    New-Item -Path $registerRoot -ItemType Directory -Force | Out-Null
    New-Item -Path (Join-Path $registerRoot "Assets") -ItemType Directory -Force | Out-Null

    Copy-Item -Path $ManifestPath -Destination (Join-Path $registerRoot "AppxManifest.xml") -Force
    Copy-Item -Path $BuiltContextMenuDllPath -Destination (Join-Path $registerRoot "PowerToys.FileConverterContextMenu.dll") -Force
    Copy-Item -Path $AssetsSourcePath -Destination (Join-Path $registerRoot "Assets\\FileConverter") -Recurse -Force

    Add-AppxPackage -Register -Path (Join-Path $registerRoot "AppxManifest.xml") -ExternalLocation $registerRoot
    return $registerRoot
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..\..\..\..")).Path
$outDir = Join-Path $repoRoot "$Platform\$Configuration\WinUI3Apps"

New-Item -Path $outDir -ItemType Directory -Force | Out-Null

$contextMenuDll = Join-Path $outDir "PowerToys.FileConverterContextMenu.dll"
if (-not (Test-Path $contextMenuDll)) {
    throw "Context menu DLL was not found at $contextMenuDll. Build FileConverterContextMenu first."
}

$stagingRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("FileConverterContextMenuPackage_" + [System.Guid]::NewGuid().ToString("N"))
New-Item -Path $stagingRoot -ItemType Directory -Force | Out-Null

try {
    $sourceManifest = Join-Path $scriptDir "AppxManifest.xml"
    $stagedManifest = Join-Path $stagingRoot "AppxManifest.xml"

    if ($UseDevIdentity) {
        Set-ManifestDevIdentity -SourceManifestPath $sourceManifest -DestinationManifestPath $stagedManifest -Publisher $DevPublisher -IdentityName $DevIdentityName
    }
    else {
        Copy-Item -Path $sourceManifest -Destination $stagedManifest -Force
    }

    Copy-Item -Path (Join-Path $scriptDir "Assets") -Destination (Join-Path $stagingRoot "Assets") -Recurse -Force

    $manifestComDllPath = Get-ManifestComDllPath -ManifestPath $stagedManifest
    if ([System.IO.Path]::GetFileName($manifestComDllPath) -ne "PowerToys.FileConverterContextMenu.dll") {
        throw "Manifest com:Class Path must point to PowerToys.FileConverterContextMenu.dll. Current value: $manifestComDllPath"
    }

    $stagedComDll = Join-Path $stagingRoot "PowerToys.FileConverterContextMenu.dll"
    Copy-Item -Path $contextMenuDll -Destination $stagedComDll -Force

    $makeAppx = Get-MakeAppxPath
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $timestampedPackage = Join-Path $outDir "FileConverterContextMenuPackage.$timestamp.msix"
    $stablePackage = Join-Path $outDir "FileConverterContextMenuPackage.msix"

    Write-Host "Using MakeAppx:" $makeAppx
    Write-Host "Packaging to:" $timestampedPackage

    & $makeAppx pack /d $stagingRoot /p $timestampedPackage /nv
    if ($LASTEXITCODE -ne 0) {
        throw "MakeAppx packaging failed with exit code $LASTEXITCODE."
    }

    $manifestIdentity = Get-ManifestIdentity -ManifestPath $stagedManifest
    Write-Host "Manifest Identity Name:" $manifestIdentity.Name
    Write-Host "Manifest Publisher:" $manifestIdentity.Publisher

    if ($SignPackage -or $RegisterPackage) {
        $cert = Get-OrCreateDevCertificate -Subject $manifestIdentity.Publisher -CreateIfMissing:$CreateDevCertificate
        Ensure-CertificateTrustedForCurrentUser -Certificate $cert

        $signTool = Get-SignToolPath
        Write-Host "Using SignTool:" $signTool
        & $signTool sign /fd SHA256 /sha1 $cert.Thumbprint /s My $timestampedPackage
        if ($LASTEXITCODE -ne 0) {
            throw "SignTool failed for $timestampedPackage with exit code $LASTEXITCODE."
        }
    }

    if ($RegisterPackage) {
        $registerSucceeded = $false
        if (-not $UseLooseRegister) {
            try {
                Write-Host "Registering sparse package from MSIX:" $timestampedPackage
                Add-AppxPackage -Path $timestampedPackage -ExternalLocation $outDir -ForceUpdateFromAnyVersion
                $registerSucceeded = $true
            }
            catch {
                Write-Warning "MSIX registration failed. Falling back to loose registration with manifest + external location."
            }
        }

        if (-not $registerSucceeded) {
            $looseRoot = Invoke-LooseRegistration -OutDir $outDir -ManifestPath $stagedManifest -BuiltContextMenuDllPath $contextMenuDll -AssetsSourcePath (Join-Path $scriptDir "Assets\\FileConverter")
            Write-Host "Loose registration root:" $looseRoot
        }
    }

    try {
        Copy-Item -Path $timestampedPackage -Destination $stablePackage -Force -ErrorAction Stop
        Write-Host "Updated stable package:" $stablePackage

        if ($SignPackage -or $RegisterPackage) {
            & $signTool sign /fd SHA256 /sha1 $cert.Thumbprint /s My $stablePackage
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Stable package signing failed with exit code $LASTEXITCODE."
            }
        }
    }
    catch {
        Write-Warning "Stable package copy skipped because destination appears locked."
    }

    $allPackages = Get-ChildItem -Path $outDir -Filter "FileConverterContextMenuPackage.*.msix" |
        Sort-Object LastWriteTime -Descending

    if ($allPackages.Count -gt $KeepRecent) {
        $allPackages | Select-Object -Skip $KeepRecent | Remove-Item -Force -ErrorAction SilentlyContinue
    }

    if ($RegisterPackage) {
        $installed = Get-AppxPackage | Where-Object { $_.Name -eq $manifestIdentity.Name }
        if ($null -eq $installed) {
            throw "Package registration did not produce an installed package for identity '$($manifestIdentity.Name)'."
        }

        Write-Host "Registered package(s):"
        $installed | Select-Object Name, PackageFullName, Publisher, Version, InstallLocation | Format-Table -AutoSize
    }

    Write-Host "Timestamped package ready:" $timestampedPackage
}
finally {
    Remove-Item -Path $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
}
