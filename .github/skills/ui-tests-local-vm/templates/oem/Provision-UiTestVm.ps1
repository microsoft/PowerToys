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

$passwordBytes = New-Object byte[] 32
$random = [Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $random.GetBytes($passwordBytes)
}
finally {
    $random.Dispose()
}
$plainPassword = [Convert]::ToBase64String($passwordBytes) + 'aA1!'
$securePassword = ConvertTo-SecureString $plainPassword -AsPlainText -Force
if ($null -eq (Get-LocalUser -Name $standardUser -ErrorAction SilentlyContinue)) {
    New-LocalUser `
        -Name $standardUser -Password $securePassword `
        -AccountNeverExpires -PasswordNeverExpires `
        -Description 'PowerToys standard-user UI-test account' | Out-Null
}
else {
    Set-LocalUser `
        -Name $standardUser -Password $securePassword `
        -AccountNeverExpires $true -PasswordNeverExpires $true
}
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

$winlogon = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
Set-ItemProperty $winlogon AutoAdminLogon '1'
Set-ItemProperty $winlogon ForceAutoLogon '1'
Set-ItemProperty $winlogon DefaultUserName $standardUser
Set-ItemProperty $winlogon DefaultDomainName $env:COMPUTERNAME
Set-ItemProperty $winlogon DefaultPassword $plainPassword

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

$windowsApplicationId = '55c92734-d682-4d71-983e-d6ec3f16059f'
$windowsLicense = Get-CimInstance SoftwareLicensingProduct -ErrorAction SilentlyContinue |
    Where-Object {
        $_.ApplicationID -eq $windowsApplicationId -and
        -not [string]::IsNullOrWhiteSpace($_.PartialProductKey) -and
        $_.Name -like 'Windows*'
    } |
    Select-Object -First 1

[ordered]@{
    ProvisionedUtc = [DateTime]::UtcNow.ToString('O')
    ComputerName = $env:COMPUTERNAME
    StandardUser = $standardUser
    StandardUserIsAdministrator = $false
    WorkRoot = $workRoot
    ResolutionTaskRegistered = $resolutionTaskRegistered
    DotNetSdkInstaller = if ($null -ne $dotNetSdk) { $dotNetSdk.Name } else { $null }
    WebView2Installer = if ($null -ne $webView2Installer) { $webView2Installer.Name } else { $null }
    WindowsLicenseDescription = [string]$windowsLicense.Description
    WindowsLicenseStatus = [int]$windowsLicense.LicenseStatus
    WindowsGracePeriodMinutes = [int]$windowsLicense.GracePeriodRemaining
} | ConvertTo-Json | Set-Content C:\OEM\ProvisioningReady.json -Encoding utf8
