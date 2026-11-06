# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Test-UmdfAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try { ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
    finally { $identity.Dispose() }
}

function Save-UmdfJournal {
    param([Parameter(Mandatory)]$State, [Parameter(Mandatory)][string]$StatePath)
    $temporary = "$StatePath.tmp"
    $State | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $temporary
    Move-Item -LiteralPath $temporary -Destination $StatePath -Force
}

function Read-UmdfJournal {
    param(
        [Parameter(Mandatory)][string]$StatePath,
        [Parameter(Mandatory)][string]$HardwareId,
        [Parameter(Mandatory)][string]$Subject,
        [Parameter(Mandatory)][ValidateSet('InfNames', 'BeforeInfs')][string]$InfProperty
    )
    $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
    if ($state.HardwareId -ne $HardwareId -or $state.Subject -ne $Subject -or
        $state.Thumbprint -notmatch '^[A-Fa-f0-9]{40}$' -or
        $state.PSObject.Properties['ImportedStores'].Value -isnot [Array] -or
        $state.PSObject.Properties[$InfProperty].Value -isnot [Array] -or
        @($state.ImportedStores | Where-Object { $_ -notin @('Root', 'TrustedPublisher') }).Count -or
        @($state.$InfProperty | Where-Object { $_ -notmatch '^oem[0-9]+\.inf$' }).Count) {
        throw 'Invalid ownership journal.'
    }
    $state
}

function Get-UmdfOwnedDevices {
    param([Parameter(Mandatory)][string]$HardwareId, [Parameter(Mandatory)][string]$InstancePattern)
    @(Get-PnpDevice -PresentOnly -ErrorAction Stop | Where-Object InstanceId -Like $InstancePattern | Where-Object {
        $HardwareId -in (Get-PnpDeviceProperty -InstanceId $_.InstanceId -KeyName DEVPKEY_Device_HardwareIds -ErrorAction Stop).Data
    })
}

function Assert-UmdfFileManifest {
    param(
        [Parameter(Mandatory)][string]$Directory,
        [Parameter(Mandatory)][string]$ManifestPath,
        [Parameter(Mandatory)][string[]]$RequiredFiles,
        [string]$Kind = 'artifact',
        [switch]$Relative
    )
    $root = [IO.Path]::GetFullPath((Resolve-Path $Directory).Path).TrimEnd('\')
    $entries = @(Get-Content $ManifestPath -Raw | ConvertFrom-Json)
    foreach ($required in $RequiredFiles) {
        $matching = @($entries | Where-Object {
            $(if ($Relative) { $_.Path } else { Split-Path $_.Path -Leaf }) -eq $required
        })
        if ($matching.Count -ne 1) { throw "Missing or mismatched $Kind hash: $required" }
    }
    foreach ($entry in $entries) {
        if ($Relative -and [IO.Path]::IsPathRooted($entry.Path)) { throw "The $Kind manifest path must be relative: $($entry.Path)" }
        $name = if ($Relative) { $entry.Path } else { Split-Path $entry.Path -Leaf }
        $path = [IO.Path]::GetFullPath((Join-Path $root $name))
        if (-not $path.StartsWith("$root\", [StringComparison]::OrdinalIgnoreCase) -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Hash) {
            throw "Missing or mismatched $Kind hash: $($entry.Path)"
        }
    }
}

function Read-UmdfPublicCertificate {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Subject)
    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($Path)
    if ($certificate.Subject -ne $Subject -or $certificate.NotAfter -lt (Get-Date) -or
        $certificate.NotBefore -gt (Get-Date) -or $certificate.HasPrivateKey) {
        $certificate.Dispose()
        throw 'Unexpected, expired, or non-public test certificate.'
    }
    $certificate
}

function Assert-UmdfPackageSignatures {
    param(
        [Parameter(Mandatory)][string]$DriverDirectory,
        [Parameter(Mandatory)][string[]]$Files,
        [Parameter(Mandatory)][string]$Thumbprint
    )
    foreach ($file in $Files) {
        $signature = Get-AuthenticodeSignature (Join-Path $DriverDirectory $file)
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $Thumbprint) {
            throw "Driver signature validation failed: $file, $($signature.Status)"
        }
    }
}

function Export-UmdfDriverDiagnostics {
    param(
        [Parameter(Mandatory)][string]$ResultsDirectory,
        [Parameter(Mandatory)][datetime]$Started,
        [Parameter(Mandatory)][hashtable]$EventLogs,
        [int]$SetupLogTail = 350,
        [string]$MessagePattern
    )
    foreach ($name in $EventLogs.Keys) {
        try {
            Get-WinEvent -FilterHashtable @{ LogName = $EventLogs[$name]; StartTime = $Started } -ErrorAction Stop |
                Where-Object { -not $MessagePattern -or $_.Message -match $MessagePattern } |
                Select-Object Id, TimeCreated, Message | ConvertTo-Json -Depth 4 |
                Set-Content (Join-Path $ResultsDirectory $name)
        }
        catch { Write-Warning "Diagnostic log unavailable: $($EventLogs[$name]): $_" }
    }
    Get-Content "$env:WINDIR\INF\setupapi.dev.log" -Tail $SetupLogTail |
        Set-Content (Join-Path $ResultsDirectory 'setupapi-tail.log')
}

function Test-UmdfMachineCertificate {
    param([ValidateSet('Root', 'TrustedPublisher')][string]$Store, [string]$Thumbprint)
    $machineStore = [Security.Cryptography.X509Certificates.X509Store]::new(
        $Store, [Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    try {
        $machineStore.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly -bor [Security.Cryptography.X509Certificates.OpenFlags]::OpenExistingOnly)
        $machineStore.Certificates.Thumbprint -contains $Thumbprint
    }
    finally { $machineStore.Close() }
}

function Add-UmdfTestTrust {
    param(
        [Parameter(Mandatory)]$State,
        [Parameter(Mandatory)][string]$StatePath,
        [Parameter(Mandatory)][string]$CertificatePath,
        [Parameter(Mandatory)][ValidateSet('Root', 'TrustedPublisher')][string]$Store
    )
    if (-not (Test-UmdfMachineCertificate -Store $Store -Thumbprint $State.Thumbprint)) {
        if ($Store -notin $State.ImportedStores) { $State.ImportedStores += $Store }
        Save-UmdfJournal -State $State -StatePath $StatePath
        & (Join-Path $env:WINDIR 'System32\certutil.exe') -f -addstore $Store $CertificatePath
        if ($LASTEXITCODE) { throw "Machine certificate import failed: store=$Store, exit=$LASTEXITCODE." }
        if (-not (Test-UmdfMachineCertificate -Store $Store -Thumbprint $State.Thumbprint)) {
            throw "The approved test certificate was not persisted in the requested machine store: $Store"
        }
    }
}

function Remove-UmdfTestTrust {
    param([Parameter(Mandatory)]$State, [Parameter(Mandatory)][string]$Subject)
    foreach ($store in $State.ImportedStores) {
        $path = "Cert:\LocalMachine\$store\$($State.Thumbprint)"
        if (Test-Path $path) {
            if ((Get-Item $path).Subject -ne $Subject) { throw 'Refusing to remove unrelated trust.' }
            Remove-Item $path
        }
    }
}

function Remove-UmdfDevices {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Devices,
        [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.List[string]]$Errors
    )
    foreach ($device in $Devices) {
        & pnputil.exe /remove-device $device.InstanceId
        if ($LASTEXITCODE -notin @(0, 3010)) { $Errors.Add("Device removal failed: $($device.InstanceId), exit $LASTEXITCODE") }
    }
}

function Remove-UmdfPackages {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$InfNames,
        [Parameter(Mandatory)][string]$HardwareId,
        [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.List[string]]$Errors
    )
    foreach ($name in $InfNames) {
        if ($name -notmatch '^oem[0-9]+\.inf$') { throw 'Invalid published driver filename.' }
        $inf = Join-Path $env:WINDIR "INF\$name"
        if (Test-Path -LiteralPath $inf) {
            if (-not (Select-String -LiteralPath $inf -SimpleMatch $HardwareId -Quiet)) {
                throw "Refusing to remove unrelated driver package: $name"
            }
            & pnputil.exe /delete-driver $name /uninstall /force
            if ($LASTEXITCODE -notin @(0, 3010) -or (Test-Path -LiteralPath $inf)) { $Errors.Add("Package remains: $name, exit $LASTEXITCODE") }
        }
    }
}
