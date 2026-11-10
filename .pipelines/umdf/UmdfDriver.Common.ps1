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

function Invoke-UmdfSystemDeviceRemoval {
    param(
        [Parameter(Mandatory)][string]$InstanceId,
        [Parameter(Mandatory)][string]$ResultsDirectory,
        [Parameter(Mandatory)][switch]$IsolatedGuest,
        [ValidateRange(1, 150)][int]$TimeoutSeconds = 120
    )
    if (-not $IsolatedGuest -or -not (Test-UmdfAdministrator)) { throw 'SYSTEM removal requires approved isolated-guest administration.' }
    $computer = Get-CimInstance Win32_ComputerSystem -ErrorAction Stop
    if ($computer.Manufacturer -ne 'Microsoft Corporation' -or $computer.Model -ne 'Virtual Machine') {
        throw 'SYSTEM removal is restricted to the approved Hyper-V guest.'
    }
    if ($InstanceId -notmatch '^ROOT\\DISPLAY\\[0-9A-Fa-f]{4}$') { throw 'Invalid owned display instance ID.' }
    $owned = @(Get-UmdfOwnedDevices -HardwareId 'ROOT\PtDisplayIddPoc' -InstancePattern 'ROOT\DISPLAY\*')
    if ($owned.Count -ne 1 -or $owned[0].InstanceId -ne $InstanceId) { throw 'SYSTEM removal requires exactly the known display fixture.' }
    $binary = Join-Path ([Environment]::SystemDirectory) 'pnputil.exe'
    $signature = Get-AuthenticodeSignature -LiteralPath $binary -ErrorAction Stop
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notlike '*O=Microsoft Corporation*') {
        throw 'SYSTEM removal requires the trusted inbox PnP utility.'
    }
    $name = "PowerToys-UmdfRemoval-$([guid]::NewGuid().ToString('N'))"
    $sddl = 'O:BAG:SYD:P(A;;GA;;;SY)(A;;GA;;;BA)'
    $registered = $null
    $running = $null
    $folder = $null
    $evidence = [ordered]@{
        InstanceId = $InstanceId; TaskName = $name; Principal = 'S-1-5-18'; LogonType = 5
        Binary = $binary; Arguments = "/remove-device $InstanceId"
        CallerSessionId = [Diagnostics.Process]::GetCurrentProcess().SessionId
        TaskEngineSessionId = $null; ExitCode = $null; Error = $null
    }
    try {
        $scheduler = New-Object -ComObject 'Schedule.Service'
        $scheduler.Connect()
        $folder = $scheduler.GetFolder('\')
        $definition = $scheduler.NewTask(0)
        $definition.Principal.UserId = 'S-1-5-18'
        $definition.Principal.LogonType = 5
        $definition.Principal.RunLevel = 1
        $definition.Settings.Enabled = $true
        $definition.Settings.Hidden = $true
        $definition.Settings.AllowDemandStart = $true
        $definition.Settings.DisallowStartIfOnBatteries = $false
        $definition.Settings.StopIfGoingOnBatteries = $false
        $definition.Settings.ExecutionTimeLimit = 'PT2M'
        $action = $definition.Actions.Create(0)
        # The privileged action executes only the inbox binary, never a workspace script.
        $action.Path = $binary
        $action.Arguments = $evidence.Arguments
        $action.WorkingDirectory = [Environment]::SystemDirectory
        $registered = $folder.RegisterTaskDefinition($name, $definition, 18, 'S-1-5-18', $null, 5, $sddl)
        $security = [Security.AccessControl.RawSecurityDescriptor]::new($registered.GetSecurityDescriptor(5))
        if ($security.Owner.Value -notin 'S-1-5-18', 'S-1-5-32-544' -or
            -not ($security.ControlFlags -band [Security.AccessControl.ControlFlags]::DiscretionaryAclProtected) -or
            @($security.DiscretionaryAcl | Where-Object {
                $_.AceQualifier -ne [Security.AccessControl.AceQualifier]::AccessAllowed -or
                $_.SecurityIdentifier.Value -notin 'S-1-5-18', 'S-1-5-32-544'
            }).Count) { throw 'The removal task did not retain its protected SYSTEM/administrator-only ACL.' }
        $started = (Get-Date).AddSeconds(-1)
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $running = $registered.Run($null)
        do {
            if ($null -eq $evidence.TaskEngineSessionId -and $running.EnginePID -gt 0) {
                try { $evidence.TaskEngineSessionId = (Get-Process -Id $running.EnginePID -ErrorAction Stop).SessionId }
                catch { Write-Warning "Task engine context unavailable: $_" }
            }
            if ($registered.State -notin 2, 4 -and $registered.LastRunTime -ge $started -and
                $registered.LastTaskResult -notin 267009, 267011) {
                $evidence.ExitCode = [int]$registered.LastTaskResult
                break
            }
            if ($watch.Elapsed.TotalSeconds -ge $TimeoutSeconds) { throw "SYSTEM device removal exceeded its $TimeoutSeconds-second deadline." }
            Start-Sleep -Milliseconds 200
        } while ($true)
        return $evidence.ExitCode
    }
    catch {
        $evidence.Error = $_.Exception.Message
        throw
    }
    finally {
        try {
            if ($null -ne $registered) {
                if ($registered.State -in 2, 4) {
                    $registered.Stop(0)
                    $stopDeadline = [datetime]::UtcNow.AddSeconds(5)
                    while ($registered.State -in 2, 4 -and [datetime]::UtcNow -lt $stopDeadline) { Start-Sleep -Milliseconds 100 }
                }
                if ($registered.State -in 2, 4) { throw "Owned removal task remains active: $name" }
                $folder.DeleteTask($name, 0)
            }
        }
        finally {
            $evidence | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $ResultsDirectory "system-removal-$name.json")
        }
    }
}

function Remove-UmdfDevices {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Devices,
        [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.List[string]]$Errors,
        [switch]$RequireImmediateRemoval,
        [ref]$RebootRequired,
        [switch]$SystemContext,
        [string]$ResultsDirectory
    )
    foreach ($device in $Devices) {
        if ($SystemContext) {
            $removeExit = Invoke-UmdfSystemDeviceRemoval -InstanceId $device.InstanceId -ResultsDirectory $ResultsDirectory -IsolatedGuest
        }
        else {
            & pnputil.exe /remove-device $device.InstanceId
            $removeExit = $LASTEXITCODE
        }
        if ($RequireImmediateRemoval -and $removeExit -eq 3010) {
            if ($null -ne $RebootRequired) { $RebootRequired.Value = $true }
            $Errors.Add("Device removal requires a reboot: $($device.InstanceId), exit $removeExit")
        }
        elseif ($removeExit -notin @(0, 3010)) { $Errors.Add("Device removal failed: $($device.InstanceId), exit $removeExit") }
    }
}

function Remove-UmdfPackages {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$InfNames,
        [Parameter(Mandatory)][string]$HardwareId,
        [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.List[string]]$Errors,
        [switch]$RequireImmediateRemoval,
        [ref]$RebootRequired
    )
    foreach ($name in $InfNames) {
        if ($name -notmatch '^oem[0-9]+\.inf$') { throw 'Invalid published driver filename.' }
        $inf = Join-Path $env:WINDIR "INF\$name"
        if (Test-Path -LiteralPath $inf) {
            if (-not (Select-String -LiteralPath $inf -SimpleMatch $HardwareId -Quiet)) {
                throw "Refusing to remove unrelated driver package: $name"
            }
            & pnputil.exe /delete-driver $name /uninstall /force
            if ($RequireImmediateRemoval -and $LASTEXITCODE -eq 3010) {
                if ($null -ne $RebootRequired) { $RebootRequired.Value = $true }
                $Errors.Add("Package removal requires a reboot: $name, exit $LASTEXITCODE")
            }
            elseif ($LASTEXITCODE -notin @(0, 3010) -or (Test-Path -LiteralPath $inf)) { $Errors.Add("Package remains: $name, exit $LASTEXITCODE") }
        }
    }
}
