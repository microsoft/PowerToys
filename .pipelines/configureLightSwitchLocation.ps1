# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# The pipeline launches LightSwitch tests directly under this same elevated user's token.
function Get-LightSwitchLocationIdentity {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Light Switch location setup requires the elevated UI-test pipeline account.'
    }
    $identity.User.Value
}

function Get-LightSwitchLocationTargets {
    $consent = 'SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location'
    @(
        @{ Path = "HKLM:\$consent"; Name = 'Value'; Kind = 'String'; Value = 'Allow' }
        @{ Path = "HKCU:\$consent"; Name = 'Value'; Kind = 'String'; Value = 'Allow' }
        @{ Path = 'HKLM:\SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration'; Name = 'Status'; Kind = 'DWord'; Value = 1 }
    )
}

function Read-LightSwitchLocationValue {
    param([hashtable] $Target)
    $snapshot = @{ KeyExisted = Test-Path -LiteralPath $Target.Path; ValueExisted = $false; Value = $null }
    if ($snapshot.KeyExisted) {
        $key = Get-Item -LiteralPath $Target.Path -ErrorAction Stop
        try {
            $snapshot.ValueExisted = $key.GetValueNames() -contains $Target.Name
            if ($snapshot.ValueExisted) {
                if ($key.GetValueKind($Target.Name).ToString() -ne $Target.Kind) {
                    throw "Unexpected registry type for $($Target.Path)\$($Target.Name)."
                }
                $snapshot.Value = $key.GetValue($Target.Name)
            }
        }
        finally { $key.Dispose() }
    }
    [pscustomobject]$snapshot
}

function Write-LightSwitchLocationValue {
    param([hashtable] $Target, [psobject] $Snapshot)
    if ($Snapshot.ValueExisted) {
        # Create only missing keys; never delete and recreate ConsentStore's OS-owned metadata.
        if (-not (Test-Path -LiteralPath $Target.Path)) {
            New-Item -Path $Target.Path -ErrorAction Stop | Out-Null
        }
        New-ItemProperty -LiteralPath $Target.Path -Name $Target.Name -Value $Snapshot.Value `
            -PropertyType $Target.Kind -Force -ErrorAction Stop | Out-Null
    }
    elseif ((Read-LightSwitchLocationValue $Target).ValueExisted) {
        Remove-ItemProperty -LiteralPath $Target.Path -Name $Target.Name -ErrorAction Stop
    }
    $actual = Read-LightSwitchLocationValue $Target
    if ($actual.ValueExisted -ne $Snapshot.ValueExisted -or $actual.Value -cne $Snapshot.Value) {
        throw "Could not set or restore $($Target.Path)\$($Target.Name)."
    }
    if (-not $Snapshot.KeyExisted -and $actual.KeyExisted) {
        $key = Get-Item -LiteralPath $Target.Path -ErrorAction Stop
        try { $empty = $key.ValueCount -eq 0 -and $key.SubKeyCount -eq 0 }
        finally { $key.Dispose() }
        if ($empty) {
            Remove-Item -LiteralPath $Target.Path -ErrorAction Stop
        }
    }
}

function Restore-LightSwitchLocation {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $StatePath)
    if (-not (Test-Path -LiteralPath $StatePath)) { return }
    $sid = Get-LightSwitchLocationIdentity
    $state = Get-Content -LiteralPath $StatePath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    $targets = @(Get-LightSwitchLocationTargets)
    if ($state.Version -ne 1 -or $state.UserSid -ne $sid -or
        $state.ServiceStatus -notin @('Running', 'Stopped') -or @($state.Values).Count -ne $targets.Count) {
        throw 'Invalid Light Switch location snapshot or a different UI-test account; preserving the snapshot.'
    }
    for ($i = 0; $i -lt $targets.Count; $i++) {
        $value = $state.Values[$i]
        if ($value.KeyExisted -isnot [bool] -or $value.ValueExisted -isnot [bool] -or
            ($value.ValueExisted -and -not $value.KeyExisted) -or
            (-not $value.ValueExisted -and $null -ne $value.Value) -or
            ($value.ValueExisted -and $targets[$i].Kind -eq 'String' -and $value.Value -isnot [string]) -or
            ($value.ValueExisted -and $targets[$i].Kind -eq 'DWord' -and $value.Value -isnot [int] -and $value.Value -isnot [long])) {
            throw "Invalid Light Switch location snapshot value $i; preserving the snapshot."
        }
    }
    for ($i = 0; $i -lt $targets.Count; $i++) {
        Write-LightSwitchLocationValue $targets[$i] $state.Values[$i]
    }
    if ($state.ServiceStatus -eq 'Running') {
        Restart-Service -Name lfsvc -ErrorAction Stop
    }
    else {
        Stop-Service -Name lfsvc -ErrorAction Stop
    }
    (Get-Service -Name lfsvc -ErrorAction Stop).WaitForStatus(
        [ServiceProcess.ServiceControllerStatus]$state.ServiceStatus, [TimeSpan]::FromSeconds(30))
    for ($i = 0; $i -lt $targets.Count; $i++) {
        $actual = Read-LightSwitchLocationValue $targets[$i]
        if ($actual.ValueExisted -ne $state.Values[$i].ValueExisted -or $actual.Value -cne $state.Values[$i].Value) {
            throw "Location value $i changed during service restoration; preserving the snapshot."
        }
    }
    Remove-Item -LiteralPath $StatePath -ErrorAction Stop
    Write-Host 'Restored Light Switch location prerequisites and original lfsvc state.'
}

function Enable-LightSwitchLocation {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string] $StatePath)
    $sid = Get-LightSwitchLocationIdentity
    Restore-LightSwitchLocation -StatePath $StatePath
    $targets = @(Get-LightSwitchLocationTargets)
    $service = Get-Service -Name lfsvc -ErrorAction Stop
    if ($service.Status -notin @('Running', 'Stopped') -or $service.StartType -eq 'Disabled') {
        throw 'lfsvc must be available and not disabled; location setup does not change policies or service startup type.'
    }
    $state = @{
        Version = 1
        UserSid = $sid
        ServiceStatus = $service.Status.ToString()
        Values = @($targets | ForEach-Object { Read-LightSwitchLocationValue $_ })
    }
    New-Item -ItemType Directory -Path (Split-Path $StatePath -Parent) -Force -ErrorAction Stop | Out-Null
    $state | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $StatePath -ErrorAction Stop
    # The caller's finally (and the pipeline's always cleanup) restores partial setup failures too.
    foreach ($target in $targets) {
        Write-LightSwitchLocationValue $target ([pscustomobject]@{
            KeyExisted = $true; ValueExisted = $true; Value = $target.Value
        })
    }
    Restart-Service -Name lfsvc -ErrorAction Stop
    (Get-Service -Name lfsvc -ErrorAction Stop).WaitForStatus(
        [ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(30))
    foreach ($target in $targets) {
        $actual = Read-LightSwitchLocationValue $target
        if (-not $actual.ValueExisted -or $actual.Value -cne $target.Value) {
            throw "Location prerequisite did not remain enabled after lfsvc restarted: $($target.Path)\$($target.Name)."
        }
    }
    Write-Host "Enabled the three Light Switch location prerequisites for pipeline user $sid."
}
