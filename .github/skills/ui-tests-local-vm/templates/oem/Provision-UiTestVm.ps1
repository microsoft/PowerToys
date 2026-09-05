<#
.SYNOPSIS
Provisions the PowerToys UI-test guest: standard-user desktop, auto-logon, and optional tooling.

.DESCRIPTION
The host reaches this guest over PowerShell Direct, so no remote listener, no firewall opening, and
no certificate is created. The guest keeps its default inbound posture.
#>

[CmdletBinding()]
param(
    [string]$StandardUser = 'PTUser'
)

$ErrorActionPreference = 'Stop'

$standardUser = $StandardUser
$workRoot = 'C:\PowerToysUiTestRun'

function Invoke-OfflineInstaller {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )

    $process = Start-Process $Path -ArgumentList $Arguments -Wait -PassThru
    if ($process.ExitCode -notin @(0, 3010)) {
        throw "Installer '$Path' failed with exit code $($process.ExitCode)."
    }
}

$autoLogonScript = 'C:\OEM\Set-UiTestAutoLogon.ps1'
if (-not (Test-Path $autoLogonScript -PathType Leaf)) {
    throw "Auto-logon provisioning helper was not found: $autoLogonScript"
}
& $autoLogonScript -StandardUser $standardUser | Out-Null
Remove-LocalGroupMember -Group 'Administrators' -Member $standardUser -ErrorAction SilentlyContinue
if ($null -eq (Get-LocalGroupMember -Group 'Users' -Member $standardUser -ErrorAction SilentlyContinue)) {
    Add-LocalGroupMember -Group 'Users' -Member $standardUser
}
$remoteDesktopUsers = Get-LocalGroup -SID 'S-1-5-32-555' -ErrorAction SilentlyContinue
if ($null -ne $remoteDesktopUsers -and
    $null -eq (Get-LocalGroupMember -Group $remoteDesktopUsers.Name -Member $standardUser -ErrorAction SilentlyContinue)) {
    Add-LocalGroupMember -Group $remoteDesktopUsers.Name -Member $standardUser
}

New-Item $workRoot -ItemType Directory -Force | Out-Null
$acl = Get-Acl $workRoot
$rule = [Security.AccessControl.FileSystemAccessRule]::new(
    "$env:COMPUTERNAME\$standardUser",
    [Security.AccessControl.FileSystemRights]::Modify,
    [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit',
    [Security.AccessControl.PropagationFlags]::None,
    [Security.AccessControl.AccessControlType]::Allow)
$acl.SetAccessRule($rule)
Set-Acl $workRoot $acl

powercfg.exe /change monitor-timeout-ac 0 | Out-Null
powercfg.exe /change standby-timeout-ac 0 | Out-Null
powercfg.exe /hibernate off | Out-Null

# Display settings belong to the interactive session, which does not exist yet while provisioning
# runs, so apply them from a logon task in the standard user's own session instead.
$resolutionScript = Join-Path $workRoot 'Set-GuestResolution.ps1'
$resolutionTaskRegistered = $false
if (Test-Path C:\OEM\Set-GuestResolution.ps1 -PathType Leaf) {
    Copy-Item C:\OEM\Set-GuestResolution.ps1 $resolutionScript -Force
    $resolutionAction = New-ScheduledTaskAction -Execute 'powershell.exe' `
        -Argument "-NoLogo -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$resolutionScript`""
    $resolutionTrigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:COMPUTERNAME\$standardUser"
    $resolutionPrincipal = New-ScheduledTaskPrincipal `
        -UserId "$env:COMPUTERNAME\$standardUser" -LogonType Interactive -RunLevel Limited
    $resolutionSettings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Minutes 2)
    Register-ScheduledTask -TaskName 'PowerToysUiTest-Resolution' -Force -InputObject (New-ScheduledTask `
        -Action $resolutionAction -Trigger $resolutionTrigger `
        -Principal $resolutionPrincipal -Settings $resolutionSettings) | Out-Null
    $resolutionTaskRegistered = $true
}

$dotNetSdk = Get-ChildItem C:\OEM -Filter 'dotnet-sdk-10*-win-*.exe' -File | Select-Object -First 1
if ($null -ne $dotNetSdk) {
    Invoke-OfflineInstaller -Path $dotNetSdk.FullName -Arguments @('/install', '/quiet', '/norestart')
}
else {
    $desktopRuntime = Get-ChildItem C:\OEM -Filter 'windowsdesktop-runtime-10*-win-*.exe' -File | Select-Object -First 1
    if ($null -ne $desktopRuntime) {
        Invoke-OfflineInstaller -Path $desktopRuntime.FullName -Arguments @('/install', '/quiet', '/norestart')
    }
}

$webView2Installer = Get-ChildItem C:\OEM -Filter 'MicrosoftEdgeWebView2RuntimeInstaller*.exe' -File | Select-Object -First 1
if ($null -ne $webView2Installer) {
    Invoke-OfflineInstaller -Path $webView2Installer.FullName -Arguments @('/silent', '/install')
}

# ScreenRecorderLib is a mixed-mode assembly importing VCRUNTIME140/MSVCP140. A clean Windows image
# has neither, so without this the harness silently captures no video.
$vcRedist = Get-ChildItem C:\OEM -Filter 'vc_redist.*.exe' -File | Select-Object -First 1
if ($null -ne $vcRedist) {
    $vcSignature = Get-AuthenticodeSignature $vcRedist.FullName
    if ($vcSignature.Status -ne 'Valid' -or
        $vcSignature.SignerCertificate.Subject -notlike 'CN=Microsoft Corporation*') {
        throw "Refusing untrusted Visual C++ redistributable '$($vcRedist.FullName)'."
    }
    Invoke-OfflineInstaller -Path $vcRedist.FullName -Arguments @('/install', '/quiet', '/norestart')
}

$powerShellArchitecture = if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }
$availablePowerShellMsis = @(Get-ChildItem C:\OEM -Filter "PowerShell-*-win-$powerShellArchitecture.msi" -File)
$powerShellMsi = Get-ChildItem C:\OEM -Filter "PowerShell-*-win-$powerShellArchitecture.msi" -File |
    Where-Object { Test-Path "$($_.FullName).sha256" -PathType Leaf } |
    Sort-Object Name -Descending |
    Select-Object -First 1
if ($availablePowerShellMsis.Count -gt 0 -and $null -eq $powerShellMsi) {
    throw "PowerShell MSI trust metadata is missing. Re-stage the OEM payload with Initialize-LocalVmHost.ps1."
}
if ($null -ne $powerShellMsi) {
    $expectedPowerShellHash = (Get-Content "$($powerShellMsi.FullName).sha256" -Raw).Trim()
    $actualPowerShellHash = (Get-FileHash $powerShellMsi.FullName -Algorithm SHA256).Hash
    $powerShellSignature = Get-AuthenticodeSignature $powerShellMsi.FullName
    if ($actualPowerShellHash -ne $expectedPowerShellHash -or
        $powerShellSignature.Status -ne 'Valid' -or
        $powerShellSignature.SignerCertificate.Subject -notlike 'CN=Microsoft Corporation*') {
        throw "Refusing unverified PowerShell MSI '$($powerShellMsi.FullName)'."
    }
    Invoke-OfflineInstaller -Path msiexec.exe -Arguments @(
        '/i', $powerShellMsi.FullName, '/qn', '/norestart',
        'ADD_PATH=1', 'REGISTER_MANIFEST=1',
        'ENABLE_PSREMOTING=0', 'USE_MU=0', 'ENABLE_MU=0')
}
$powerShellExecutable = 'C:\Program Files\PowerShell\7\pwsh.exe'

$windowsApplicationId = '55c92734-d682-4d71-983e-d6ec3f16059f'
$windowsLicense = Get-CimInstance SoftwareLicensingProduct -ErrorAction SilentlyContinue |
    Where-Object {
        $_.ApplicationID -eq $windowsApplicationId -and
        -not [string]::IsNullOrWhiteSpace($_.PartialProductKey) -and
        $_.Name -like 'Windows*'
    } |
    Select-Object -First 1
$windowsVersion = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$fullWindowsBuild = "$($windowsVersion.CurrentBuild).$($windowsVersion.UBR)"
$windowsBuild = [int]$windowsVersion.CurrentBuild
$dotNet10CetReady = $windowsBuild -ge 22000 -or
    ($windowsBuild -ge 19041 -and $windowsBuild -le 19045 -and [int]$windowsVersion.UBR -ge 5007)

[ordered]@{
    ProvisionedUtc = [DateTime]::UtcNow.ToString('O')
    ComputerName = $env:COMPUTERNAME
    StandardUser = $standardUser
    StandardUserIsAdministrator = $false
    WorkRoot = $workRoot
    ResolutionTaskRegistered = $resolutionTaskRegistered
    DotNetSdkInstaller = if ($null -ne $dotNetSdk) { $dotNetSdk.Name } else { $null }
    WebView2Installer = if ($null -ne $webView2Installer) { $webView2Installer.Name } else { $null }
    VcRedistInstaller = if ($null -ne $vcRedist) { $vcRedist.Name } else { $null }
    ScreenRecordingSupported = (Test-Path "$env:WINDIR\System32\VCRUNTIME140.dll")
    # Record the executable's file-version resource without launching .NET before Win10 reaches its
    # CET servicing floor. This is intentionally not the semantic $PSVersionTable value.
    PowerShellVersion = if (Test-Path $powerShellExecutable) {
        [Diagnostics.FileVersionInfo]::GetVersionInfo($powerShellExecutable).FileVersion
    } else { $null }
    WindowsBuild = $fullWindowsBuild
    DotNet10CetReady = $dotNet10CetReady
    WindowsLicenseDescription = [string]$windowsLicense.Description
    WindowsLicenseStatus = [int]$windowsLicense.LicenseStatus
    WindowsGracePeriodMinutes = [int]$windowsLicense.GracePeriodRemaining
} | ConvertTo-Json | Set-Content C:\OEM\ProvisioningReady.json -Encoding utf8
