# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Guest transport for the local Hyper-V UI-test VM.

.DESCRIPTION
The control channel is PowerShell Direct over VMBus: no listener, no published port, no certificate,
and no network dependency. Bulk payloads move with Copy-VMFile over the Guest Service Interface,
measured at ~82 MB/s against ~17 MB/s for the PowerShell Direct session copy - and the session copy
stalls outright on archives approaching a gigabyte, so it is only a fallback.

The exchange is guest-local storage that this module mirrors in both directions, so the host never
shares a folder with the guest.
#>

Set-StrictMode -Version 3.0

$script:DefaultGuestExchangeRoot = 'C:\PowerToysUiTestExchange'

function Test-HyperVAccess {
    <#
    .SYNOPSIS
    Reports whether this shell can manage Hyper-V.

    .DESCRIPTION
    Tests the capability rather than the token shape. Elevation is the usual way to get it, but
    membership in the local Hyper-V Administrators group survives UAC filtering and is enough for
    Get-VM, Copy-VMFile, and PowerShell Direct.
    #>
    try {
        Import-Module Hyper-V -ErrorAction Stop
        Get-VM -ErrorAction Stop | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Get-HyperVAccessMessage {
    return 'BLOCKED: Hyper-V is not accessible from this shell. Run from an elevated PowerShell 7 terminal, or add this account to the local "Hyper-V Administrators" group (a one-time elevated change that takes effect after signing out and back in).'
}

function Test-LocalVmHostSetup {
    <#
    .SYNOPSIS
    Reports which of the human-only host prerequisites are in place.

    .DESCRIPTION
    Three things must exist before any agent-driven run can work, and none of them can be created by
    an agent: Hyper-V access for this shell, the DPAPI guest-administrator credential, and the guest
    itself. Initialize-LocalVmHost.ps1 performs all three; this function only observes them.
    #>
    [CmdletBinding()]
    param(
        [string]$VmName,
        [string]$CredentialPath,
        [string]$AdminUserName = 'PTAdmin'
    )

    if ([string]::IsNullOrWhiteSpace($CredentialPath)) {
        $CredentialPath = Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'
    }

    $hyperVAccess = Test-HyperVAccess

    $credentialDetail = 'ok'
    $credentialReady = $false
    if (-not (Test-Path $CredentialPath -PathType Leaf)) {
        $credentialDetail = "missing: $CredentialPath"
    }
    else {
        try {
            $credential = Import-Clixml $CredentialPath
            if ($credential -isnot [pscredential]) {
                $credentialDetail = 'file does not contain a PSCredential'
            }
            else {
                $credentialUser = $credential.UserName -replace '^.*\\', ''
                if ($credentialUser -ne $AdminUserName) {
                    $credentialDetail = "stored for '$credentialUser' but the configuration expects '$AdminUserName'"
                }
                else {
                    $credentialReady = $true
                }
            }
        }
        catch {
            # A credential saved by another Windows user or on another host cannot be decrypted here.
            $credentialDetail = "unreadable ($($_.Exception.Message))"
        }
    }

    $guestReady = $false
    $guestDetail = 'not checked (no Hyper-V access)'
    if ($hyperVAccess -and -not [string]::IsNullOrWhiteSpace($VmName)) {
        $guest = Get-VM -Name $VmName -ErrorAction SilentlyContinue
        if ($null -eq $guest) {
            $guestDetail = "missing: no virtual machine named '$VmName'"
        }
        else {
            $guestReady = $true
            $guestDetail = "ok (State=$($guest.State))"
        }
    }
    elseif ($hyperVAccess) {
        $guestDetail = 'not checked (no VmName supplied)'
        $guestReady = $true
    }

    $missing = @()
    if (-not $hyperVAccess) { $missing += 'HyperVAccess' }
    if (-not $credentialReady) { $missing += 'Credential' }
    if (-not $guestReady) { $missing += 'Guest' }

    return [pscustomobject]@{
        HyperVAccess = $hyperVAccess
        HyperVAccessDetail = if ($hyperVAccess) { 'ok' } else { 'missing: elevate, or join the local "Hyper-V Administrators" group and sign out/in' }
        Credential = $credentialReady
        CredentialDetail = $credentialDetail
        CredentialPath = $CredentialPath
        Guest = $guestReady
        GuestDetail = $guestDetail
        VmName = $VmName
        Missing = $missing
        IsReady = $missing.Count -eq 0
    }
}

function Get-LocalVmSetupMessage {
    <#
    .SYNOPSIS
    Builds the BLOCKED message naming the one command a human must run.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Status,
        [string]$VmRoot = '<VmRoot>',
        [string]$InstallMedia = '<windows.iso>',
        [string]$ConfigPath,
        [string]$CredentialPath
    )

    $configArgument = if ([string]::IsNullOrWhiteSpace($ConfigPath)) { '' } else { " -ConfigPath `"$ConfigPath`"" }
    $credentialArgument = if ([string]::IsNullOrWhiteSpace($CredentialPath)) { '' } else { " -CredentialPath `"$CredentialPath`"" }
    $lines = @(
        "BLOCKED: local-VM host setup is incomplete ($($Status.Missing -join ', ')).",
        "  Hyper-V access : $($Status.HyperVAccessDetail)",
        "  Credential     : $($Status.CredentialDetail)",
        "  Guest          : $($Status.GuestDetail)",
        '',
        'These steps need a human: they require elevation (which no tool call can approve) and a',
        'password (which must never be routed through a model). Ask the user to run, once:',
        '',
        "  pwsh -File <skill>\scripts\Initialize-LocalVmHost.ps1 -VmRoot `"$VmRoot`" -InstallMedia `"$InstallMedia`"$configArgument$credentialArgument",
        '',
        'Do not continue or work around this - re-check with -CheckOnly after they confirm.')
    return ($lines -join [Environment]::NewLine)
}

function New-LocalVmContext {
    <#
    .SYNOPSIS
    Describes how to reach the guest without connecting to it.

    .DESCRIPTION
    The returned context is safe to serialize into a plan: it contains no credential material.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$VmName,

        [Parameter(Mandatory)]
        [string]$HostExchangeRoot,

        [string]$GuestExchangeRoot = $script:DefaultGuestExchangeRoot
    )

    $hostExchangePath = [IO.Path]::GetFullPath($HostExchangeRoot)
    if (-not (Test-Path $hostExchangePath -PathType Container)) {
        throw "Host exchange root was not found: $hostExchangePath"
    }

    $exchangeName = Split-Path $hostExchangePath -Leaf
    return [pscustomobject]@{
        VmName = $VmName
        HostExchangeRoot = $hostExchangePath
        GuestExchangeRoot = (Join-Path $GuestExchangeRoot.TrimEnd('\') $exchangeName)
        ControlChannel = "vmbus://$VmName"
    }
}

function New-LocalVmSession {
    <#
    .SYNOPSIS
    Opens an administrative PowerShell Direct session to the guest, retrying until the deadline.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][pscredential]$Credential,
        [ValidateRange(1, 240)][int]$TimeoutMinutes = 45
    )

    if (-not (Test-HyperVAccess)) {
        throw (Get-HyperVAccessMessage)
    }

    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    do {
        try {
            return New-PSSession -VMName $Context.VmName -Credential $Credential -ErrorAction Stop
        }
        catch {
            if ([DateTime]::UtcNow -ge $deadline) {
                throw "Could not establish PowerShell Direct to '$($Context.VmName)'. $($_.Exception.Message)"
            }
            Start-Sleep -Seconds 5
        }
    } while ($true)
}

function Initialize-GuestExchange {
    <#
    .SYNOPSIS
    Ensures the guest exchange directory exists and is writable by the interactive test user.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$StandardUser
    )

    Invoke-Command -Session $Session -ScriptBlock {
        param($Path, $User)

        $ErrorActionPreference = 'Stop'
        New-Item $Path -ItemType Directory -Force | Out-Null
        $acl = Get-Acl $Path
        $rule = [Security.AccessControl.FileSystemAccessRule]::new(
            "$env:COMPUTERNAME\$User",
            [Security.AccessControl.FileSystemRights]::Modify,
            [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit',
            [Security.AccessControl.PropagationFlags]::None,
            [Security.AccessControl.AccessControlType]::Allow)
        $acl.SetAccessRule($rule)
        Set-Acl $Path $acl
    } -ArgumentList $Context.GuestExchangeRoot, $StandardUser
}

function Copy-ToGuest {
    <#
    .SYNOPSIS
    Copies exchange files into the guest, skipping files that already match by hash.

    .OUTPUTS
    The names of the files that were actually transferred.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string[]]$FileName,
        [switch]$Force
    )

    $guestHashes = @{}
    if (-not $Force) {
        $guestHashes = Invoke-Command -Session $Session -ScriptBlock {
            param($Root, $Names)

            $result = @{}
            foreach ($name in $Names) {
                $path = Join-Path $Root $name
                if (Test-Path $path -PathType Leaf) {
                    $result[$name] = (Get-FileHash $path -Algorithm SHA256).Hash
                }
            }
            return $result
        } -ArgumentList $Context.GuestExchangeRoot, $FileName
    }

    $copied = @()
    foreach ($name in $FileName) {
        $source = Join-Path $Context.HostExchangeRoot $name
        if (-not (Test-Path $source -PathType Leaf)) {
            throw "Required exchange file is missing: $source"
        }
        if (-not $Force -and $guestHashes.ContainsKey($name) -and
            $guestHashes[$name] -eq (Get-FileHash $source -Algorithm SHA256).Hash) {
            continue
        }

        $destination = Join-Path $Context.GuestExchangeRoot $name
        $copied += $name
        try {
            Copy-VMFile -Name $Context.VmName -SourcePath $source -DestinationPath $destination `
                -CreateFullPath -FileSource Host -Force -ErrorAction Stop
        }
        catch {
            Write-Verbose "Copy-VMFile failed for '$name', falling back to the session copy: $($_.Exception.Message)"
            Invoke-Command -Session $Session -ScriptBlock {
                param($Path)
                New-Item (Split-Path $Path -Parent) -ItemType Directory -Force | Out-Null
            } -ArgumentList $destination
            Copy-Item $source -Destination $destination -ToSession $Session -Force
        }
    }

    return $copied
}

function Copy-FromGuest {
    <#
    .SYNOPSIS
    Merges a guest directory subtree into the matching host exchange location.

    .DESCRIPTION
    Children are copied individually so that host-authored evidence already present in the
    destination (request, plan, probe script) survives the transfer. Copy-VMFile is host-to-guest
    only, so evidence returns over the session - it is small.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$RelativePath
    )

    $source = Join-Path $Context.GuestExchangeRoot $RelativePath
    $destination = Join-Path $Context.HostExchangeRoot $RelativePath
    $children = Invoke-Command -Session $Session -ScriptBlock {
        param($Path)
        if (Test-Path $Path -PathType Container) {
            @(Get-ChildItem $Path -Force | Select-Object -ExpandProperty FullName)
        }
        else {
            @()
        }
    } -ArgumentList $source
    if (@($children).Count -eq 0) {
        return
    }

    New-Item $destination -ItemType Directory -Force | Out-Null
    foreach ($child in $children) {
        Copy-Item $child -Destination $destination -FromSession $Session -Recurse -Force
    }
}

function Remove-GuestItem {
    <#
    .SYNOPSIS
    Deletes an exchange-relative path in the guest after its evidence has been exported.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$RelativePath
    )

    Invoke-Command -Session $Session -ScriptBlock {
        param($Path)
        Remove-Item $Path -Recurse -Force -ErrorAction SilentlyContinue
    } -ArgumentList (Join-Path $Context.GuestExchangeRoot $RelativePath)
}

function Write-GuestText {
    <#
    .SYNOPSIS
    Writes a UTF-8 text file at an exchange-relative path on both sides.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Value
    )

    $hostPath = Join-Path $Context.HostExchangeRoot $RelativePath
    New-Item (Split-Path $hostPath -Parent) -ItemType Directory -Force | Out-Null
    $Value | Set-Content $hostPath -Encoding utf8

    Invoke-Command -Session $Session -ScriptBlock {
        param($Path, $Text)

        $ErrorActionPreference = 'Stop'
        New-Item (Split-Path $Path -Parent) -ItemType Directory -Force | Out-Null
        Set-Content -Path $Path -Value $Text -Encoding utf8
    } -ArgumentList (Join-Path $Context.GuestExchangeRoot $RelativePath), $Value
}

function Read-GuestJson {
    <#
    .SYNOPSIS
    Reads an exchange-relative JSON file, tolerating the create/write race.

    .OUTPUTS
    The parsed object, or $null when the file is absent or not yet complete.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$RelativePath,
        [ValidateRange(1, 100)][int]$Attempts = 1
    )

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            $text = Invoke-Command -Session $Session -ScriptBlock {
                param($Path)
                if (Test-Path $Path -PathType Leaf) {
                    Get-Content $Path -Raw -ErrorAction SilentlyContinue
                }
            } -ArgumentList (Join-Path $Context.GuestExchangeRoot $RelativePath)

            if (-not [string]::IsNullOrWhiteSpace($text)) {
                return $text | ConvertFrom-Json
            }
        }
        catch {
        }
        if ($attempt -lt $Attempts) {
            Start-Sleep -Milliseconds 100
        }
    }

    return $null
}

function Get-GuestPowerShellPath {
    <#
    .SYNOPSIS
    Returns the guest path of PowerShell 7, which every interactive guest task runs under.
    #>
    return 'C:\Program Files\PowerShell\7\pwsh.exe'
}

function Start-InteractiveTask {
    <#
    .SYNOPSIS
    Registers and starts a one-shot limited scheduled task in the standard user's interactive session.

    .DESCRIPTION
    PowerShell Direct lands in session 0, which has no desktop. A task with an interactive principal
    runs in the logged-on user's session instead. An earlier task of the same name is replaced.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$TaskName,
        [Parameter(Mandatory)][string]$Executable,
        [Parameter(Mandatory)][string]$Arguments,
        [Parameter(Mandatory)][string]$UserName,
        [Parameter(Mandatory)][int]$ExecutionTimeLimitSeconds
    )

    # A module function does not see its caller's preference, so a guest error must stop here.
    $ErrorActionPreference = 'Stop'
    return Invoke-Command -Session $Session -ScriptBlock {
        param($Name, $FilePath, $ArgumentList, $InteractiveUser, $LimitSeconds)

        $ErrorActionPreference = 'Stop'
        Unregister-ScheduledTask -TaskName $Name -Confirm:$false -ErrorAction SilentlyContinue
        $action = New-ScheduledTaskAction -Execute $FilePath -Argument $ArgumentList
        $principal = New-ScheduledTaskPrincipal `
            -UserId "$env:COMPUTERNAME\$InteractiveUser" `
            -LogonType Interactive -RunLevel Limited
        # Priority 7 defaults to background CPU/I/O scheduling and propagates to
        # product children. Interactive UI tests need normal scheduling under VM load.
        $settings = New-ScheduledTaskSettingsSet -Priority 4 `
            -ExecutionTimeLimit (New-TimeSpan -Seconds $LimitSeconds) `
            -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
        $task = New-ScheduledTask -Action $action -Principal $principal -Settings $settings
        Register-ScheduledTask -TaskName $Name -InputObject $task -Force | Out-Null
        Start-ScheduledTask -TaskName $Name
        $registered = Get-ScheduledTask -TaskName $Name
        [pscustomobject]@{
            TaskName = $Name
            State = [string]$registered.State
            UserId = $registered.Principal.UserId
            RunLevel = [string]$registered.Principal.RunLevel
        }
    } -ArgumentList $TaskName, $Executable, $Arguments, $UserName, $ExecutionTimeLimitSeconds
}

function Get-InteractiveTaskInfo {
    <#
    .SYNOPSIS
    Reads a guest task's state, retrying a failed read before giving up.

    .DESCRIPTION
    State is 'Missing' when no task has the name. LastRunTimeUtc is set only once the task ran. With
    ExitCodePath, ExitCode holds that guest file's trimmed content, or $null while the file is absent.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$TaskName,
        [string]$ExitCodePath = '',
        [ValidateRange(1, 10)][int]$Attempts = 3
    )

    # A module function does not see its caller's preference, so a guest error must stop here.
    $ErrorActionPreference = 'Stop'
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            return Invoke-Command -Session $Session -ScriptBlock {
                param($Name, $CodePath)

                $task = Get-ScheduledTask -TaskName $Name -ErrorAction SilentlyContinue
                $info = Get-ScheduledTaskInfo -TaskName $Name -ErrorAction SilentlyContinue
                $state = if ($null -ne $task) { [string]$task.State } else { 'Missing' }
                $lastTaskResult = if ($null -ne $info) { [int]$info.LastTaskResult } else { $null }
                $lastRunTimeUtc = if ($null -ne $info -and $info.LastRunTime.Year -gt 2000) {
                    $info.LastRunTime.ToUniversalTime().ToString('o')
                }
                else {
                    $null
                }
                $exitCode = $null
                if (-not [string]::IsNullOrEmpty($CodePath) -and (Test-Path -LiteralPath $CodePath -PathType Leaf)) {
                    $content = Get-Content -LiteralPath $CodePath -Raw
                    if ($null -ne $content) {
                        $exitCode = $content.Trim()
                    }
                }
                [pscustomobject]@{
                    State = $state
                    LastTaskResult = $lastTaskResult
                    LastRunTimeUtc = $lastRunTimeUtc
                    ExitCode = $exitCode
                }
            } -ArgumentList $TaskName, $ExitCodePath -ErrorAction Stop
        }
        catch {
            if ($attempt -eq $Attempts) {
                throw
            }
            Start-Sleep -Milliseconds 250
        }
    }
}

Export-ModuleMember -Function `
    Test-HyperVAccess, Get-HyperVAccessMessage, Test-LocalVmHostSetup, Get-LocalVmSetupMessage, `
    New-LocalVmContext, New-LocalVmSession, `
    Initialize-GuestExchange, Copy-ToGuest, Copy-FromGuest, Remove-GuestItem, Write-GuestText, `
    Read-GuestJson, Get-GuestPowerShellPath, Start-InteractiveTask, Get-InteractiveTaskInfo
