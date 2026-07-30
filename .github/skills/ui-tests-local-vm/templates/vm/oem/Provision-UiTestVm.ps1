$ErrorActionPreference = 'Stop'

$standardUser = 'PTUser'
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

Enable-PSRemoting -Force -SkipNetworkProfileCheck
Set-Service -Name WinRM -StartupType Automatic
Set-Item -Path WSMan:\localhost\Service\Auth\Basic -Value $true
Set-Item -Path WSMan:\localhost\Service\AllowUnencrypted -Value $false
New-ItemProperty `
    -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' `
    -Name LocalAccountTokenFilterPolicy -PropertyType DWord -Value 1 -Force | Out-Null

$httpsListener = Get-ChildItem WSMan:\localhost\Listener |
    Where-Object { $_.Keys -contains 'Transport=HTTPS' } |
    Select-Object -First 1
if ($null -eq $httpsListener) {
    $certificate = New-SelfSignedCertificate `
        -DnsName @($env:COMPUTERNAME, 'localhost') `
        -CertStoreLocation Cert:\LocalMachine\My `
        -NotAfter ([DateTime]::UtcNow.AddYears(10))
    New-Item -Path WSMan:\localhost\Listener `
        -Transport HTTPS -Address * -CertificateThumbPrint $certificate.Thumbprint -Force | Out-Null
}
if ($null -eq (Get-NetFirewallRule -Name PowerToysUiTestVm-WinRM-HTTPS -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule `
        -Name PowerToysUiTestVm-WinRM-HTTPS `
        -DisplayName 'PowerToys UI Test VM HTTPS WinRM' `
        -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5986 | Out-Null
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

$dotNetSdk = Get-ChildItem C:\OEM -Filter 'dotnet-sdk-10*-win-x64.exe' -File | Select-Object -First 1
if ($null -ne $dotNetSdk) {
    Invoke-OfflineInstaller -Path $dotNetSdk.FullName -Arguments @('/install', '/quiet', '/norestart')
}
else {
    $desktopRuntime = Get-ChildItem C:\OEM -Filter 'windowsdesktop-runtime-10*-win-x64.exe' -File | Select-Object -First 1
    if ($null -ne $desktopRuntime) {
        Invoke-OfflineInstaller -Path $desktopRuntime.FullName -Arguments @('/install', '/quiet', '/norestart')
    }
}

$webView2Installer = Get-ChildItem C:\OEM -Filter 'MicrosoftEdgeWebView2RuntimeInstaller*.exe' -File | Select-Object -First 1
if ($null -ne $webView2Installer) {
    Invoke-OfflineInstaller -Path $webView2Installer.FullName -Arguments @('/silent', '/install')
}

[ordered]@{
    ProvisionedUtc = [DateTime]::UtcNow.ToString('O')
    ComputerName = $env:COMPUTERNAME
    StandardUser = $standardUser
    StandardUserIsAdministrator = $false
    WorkRoot = $workRoot
    HttpsWinRM = 5986
    DotNetSdkInstaller = if ($null -ne $dotNetSdk) { $dotNetSdk.Name } else { $null }
    WebView2Installer = if ($null -ne $webView2Installer) { $webView2Installer.Name } else { $null }
} | ConvertTo-Json | Set-Content C:\OEM\ProvisioningReady.json -Encoding utf8
