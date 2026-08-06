# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Guest transport abstraction for the local UI-test VM.

.DESCRIPTION
Both supported backends expose the same contract to the controller:

  Docker  dockur/windows in Docker Desktop. The control channel is HTTPS WinRM on a loopback port
          and the exchange is an SMB share, so the host and the guest see the same files.
  HyperV  A Hyper-V virtual machine. The control channel is PowerShell Direct over VMBus and the
          exchange is guest-local storage that this module mirrors in both directions.

Callers never branch on the backend: they resolve a context, ask for the guest exchange path, and
use the Copy/Read/Write functions.
#>

Set-StrictMode -Version 3.0

$script:DefaultGuestExchangeRoot = 'C:\PowerToysUiTestExchange'

function Test-HostElevation {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    return ([Security.Principal.WindowsPrincipal]$identity).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-HyperVAccess {
    <#
    .SYNOPSIS
    Reports whether this shell can actually manage Hyper-V.

    .DESCRIPTION
    Elevation is the usual way to get access, but membership in the local Hyper-V Administrators
    group also grants it, so probe the capability instead of assuming the token shape.
    #>
    try {
        Import-Module Hyper-V -ErrorAction Stop
        Get-VMHost -ErrorAction Stop | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

$script:HyperVAccessMessage = 'BLOCKED: Hyper-V is not accessible from this shell. Run from an elevated PowerShell 7 terminal, or add this account to the local "Hyper-V Administrators" group (a one-time elevated change that takes effect after signing out and back in).'

function Get-HyperVAccessMessage {
    return $script:HyperVAccessMessage
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
        [ValidateSet('Docker', 'HyperV')]
        [string]$Backend,

        [Parameter(Mandatory)]
        [string]$HostExchangeRoot,

        # Docker: loopback port published by compose. Ignored by HyperV.
        [int]$WinRmPort = 15986,
        [switch]$UseHttpWinRM,
        # Docker: guest-visible root of the dockur SMB share.
        [string]$GuestShareRoot = '\\host.lan\Data',
        # Docker: host folder bind-mounted as the share, used to compute the guest-relative path.
        [string]$HostShareRoot,

        # HyperV: virtual machine name and the guest-local exchange root.
        [string]$VmName,
        [string]$GuestExchangeRoot = $script:DefaultGuestExchangeRoot
    )

    $hostExchangePath = [IO.Path]::GetFullPath($HostExchangeRoot)
    if (-not (Test-Path $hostExchangePath -PathType Container)) {
        throw "Host exchange root was not found: $hostExchangePath"
    }

    if ($Backend -eq 'Docker') {
        if ([string]::IsNullOrWhiteSpace($HostShareRoot)) {
            throw 'HostShareRoot is required for the Docker backend.'
        }
        $shareRoot = [IO.Path]::GetFullPath($HostShareRoot)
        $relative = [IO.Path]::GetRelativePath($shareRoot, $hostExchangePath)
        if ($relative -eq '..' -or $relative.StartsWith("..$([IO.Path]::DirectorySeparatorChar)")) {
            throw "ExchangeRoot must be inside the VM shared root '$shareRoot'."
        }
        $guestExchange = if ($relative -eq '.') {
            $GuestShareRoot.TrimEnd('\')
        }
        else {
            Join-Path $GuestShareRoot.TrimEnd('\') $relative
        }

        $scheme = if ($UseHttpWinRM) { 'http' } else { 'https' }
        return [pscustomobject]@{
            Backend = 'Docker'
            HostExchangeRoot = $hostExchangePath
            GuestExchangeRoot = $guestExchange
            SharesFileSystem = $true
            ConnectionUri = "${scheme}://127.0.0.1:$WinRmPort/wsman"
            UseHttpWinRM = [bool]$UseHttpWinRM
            VmName = $null
        }
    }

    if ([string]::IsNullOrWhiteSpace($VmName)) {
        throw 'VmName is required for the HyperV backend.'
    }
    $exchangeName = Split-Path $hostExchangePath -Leaf
    return [pscustomobject]@{
        Backend = 'HyperV'
        HostExchangeRoot = $hostExchangePath
        GuestExchangeRoot = (Join-Path $GuestExchangeRoot.TrimEnd('\') $exchangeName)
        SharesFileSystem = $false
        ConnectionUri = "vmbus://$VmName"
        UseHttpWinRM = $false
        VmName = $VmName
    }
}

function New-LocalVmSession {
    <#
    .SYNOPSIS
    Opens an administrative PSSession to the guest, retrying until the deadline.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][pscredential]$Credential,
        [ValidateRange(1, 240)][int]$TimeoutMinutes = 45
    )

    if ($Context.Backend -eq 'HyperV' -and -not (Test-HyperVAccess)) {
        throw (Get-HyperVAccessMessage)
    }

    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    $lastError = $null
    do {
        try {
            if ($Context.Backend -eq 'Docker') {
                $sessionOption = if ($Context.UseHttpWinRM) {
                    New-PSSessionOption
                }
                else {
                    New-PSSessionOption -SkipCACheck -SkipCNCheck -SkipRevocationCheck
                }
                $authentication = if ($Context.UseHttpWinRM) { 'Negotiate' } else { 'Basic' }
                return New-PSSession `
                    -ConnectionUri $Context.ConnectionUri -Authentication $authentication `
                    -Credential $Credential -SessionOption $sessionOption -ErrorAction Stop
            }

            return New-PSSession -VMName $Context.VmName -Credential $Credential -ErrorAction Stop
        }
        catch {
            $lastError = $_
            if ([DateTime]::UtcNow -ge $deadline) {
                throw "Could not establish a guest session at $($Context.ConnectionUri). $($_.Exception.Message)"
            }
            Start-Sleep -Seconds 5
        }
    } while ($true)

    throw $lastError
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

    if ($Context.SharesFileSystem) {
        return
    }

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
    Copies exchange files from the host into the guest, skipping files that already match by hash.

    .OUTPUTS
    The names of the files that were actually transferred.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string[]]$FileName,
        [switch]$Force
    )

    if ($Context.SharesFileSystem) {
        return @()
    }
    if ($null -eq $Session) {
        throw 'A guest session is required to copy files for this backend.'
    }

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
        # Copy-VMFile moves ~80 MB/s over the Guest Service Interface, where Copy-Item -ToSession
        # manages ~17 MB/s and stalls outright on archives approaching a gigabyte.
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
    destination (request, plan, probe script) survives the transfer.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$RelativePath
    )

    if ($Context.SharesFileSystem) {
        return
    }
    if ($null -eq $Session) {
        throw 'A guest session is required to copy files for this backend.'
    }

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
        [System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$RelativePath
    )

    if ($Context.SharesFileSystem -or $null -eq $Session) {
        return
    }

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
        [System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Value
    )

    $hostPath = Join-Path $Context.HostExchangeRoot $RelativePath
    New-Item (Split-Path $hostPath -Parent) -ItemType Directory -Force | Out-Null
    $Value | Set-Content $hostPath -Encoding utf8

    if ($Context.SharesFileSystem) {
        return
    }
    if ($null -eq $Session) {
        throw 'A guest session is required to write guest files for this backend.'
    }

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
    Reads an exchange-relative JSON file, tolerating the create/write race on both transports.

    .OUTPUTS
    The parsed object, or $null when the file is absent or not yet complete.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [System.Management.Automation.Runspaces.PSSession]$Session,
        [Parameter(Mandatory)][string]$RelativePath,
        [ValidateRange(1, 100)][int]$Attempts = 1
    )

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        $text = $null
        try {
            if ($Context.SharesFileSystem) {
                $hostPath = Join-Path $Context.HostExchangeRoot $RelativePath
                if (Test-Path $hostPath -PathType Leaf) {
                    $text = Get-Content $hostPath -Raw
                }
            }
            else {
                if ($null -eq $Session) {
                    throw 'A guest session is required to read guest files for this backend.'
                }
                $text = Invoke-Command -Session $Session -ScriptBlock {
                    param($Path)
                    if (Test-Path $Path -PathType Leaf) {
                        Get-Content $Path -Raw -ErrorAction SilentlyContinue
                    }
                } -ArgumentList (Join-Path $Context.GuestExchangeRoot $RelativePath)
            }

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

Export-ModuleMember -Function `
    Test-HostElevation, Test-HyperVAccess, Get-HyperVAccessMessage, New-LocalVmContext, `
    New-LocalVmSession, Initialize-GuestExchange, Copy-ToGuest, Copy-FromGuest, Remove-GuestItem, `
    Write-GuestText, Read-GuestJson
