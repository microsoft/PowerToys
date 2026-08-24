[CmdletBinding()]
param(
    [string]$PackageName = 'Microsoft.PowerToys.WsPuvr.Updater',
    [string]$PackageFamilyName = 'Microsoft.PowerToys.WsPuvr.Updater_t8ed0av59w5q6'
)

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Machine multi-user validation requires an elevated administrator token.'
}

$testUsers = @('PtPuvrUserA', 'PtPuvrUserB')
$evidenceRoot = Join-Path $env:ProgramData 'PtPuvrMachineLifecycle'
$resultPath = Join-Path $PSScriptRoot 'artifacts\machine-multi-user-result.json'
$accounts = @()

function New-TestAccount([string]$name) {
    if (Get-LocalUser -Name $name -ErrorAction SilentlyContinue) {
        throw "Test account already exists: $name"
    }

    $password = 'Aa1!' + [Guid]::NewGuid().ToString('N').Substring(0, 20)
    $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
    $account = New-LocalUser `
        -Name $name `
        -Password $securePassword `
        -PasswordNeverExpires `
        -UserMayNotChangePassword
    return [pscustomobject]@{
        Name = $name
        Password = $password
        Sid = $account.Sid.Value
    }
}

function Invoke-TestUserProcess(
    $account,
    [string]$script
) {
    $commandPath = Join-Path $evidenceRoot (
        "$($account.Name)-" +
        [Guid]::NewGuid().ToString('N').Substring(0, 8) +
        '.ps1')
    Set-Content -LiteralPath $commandPath -Value $script -Encoding utf8
    $securePassword =
        ConvertTo-SecureString $account.Password -AsPlainText -Force
    $credential = [pscredential]::new(
        "$env:COMPUTERNAME\$($account.Name)",
        $securePassword)
    try {
        $process = Start-Process `
            -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" `
            -Credential $credential `
            -LoadUserProfile `
            -WorkingDirectory "$env:SystemRoot\System32" `
            -ArgumentList (
                '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass ' +
                "-File $commandPath") `
            -Wait `
            -PassThru
        if ($process.ExitCode -ne 0) {
            throw "Test-user process failed for $($account.Name): $($process.ExitCode)"
        }
    }
    finally {
        Remove-Item -LiteralPath $commandPath -Force -ErrorAction SilentlyContinue
    }
}

function Escape-SingleQuoted([string]$value) {
    return $value.Replace("'", "''")
}

try {
    New-Item -ItemType Directory -Path $evidenceRoot -Force | Out-Null
    & icacls.exe $evidenceRoot `
        /inheritance:r `
        /grant:r `
        '*S-1-5-18:(OI)(CI)F' `
        '*S-1-5-32-544:(OI)(CI)F' `
        '*S-1-5-32-545:(OI)(CI)M' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not secure the test evidence directory: $LASTEXITCODE"
    }

    $provisioned = Get-AppxProvisionedPackage -Online |
        Where-Object DisplayName -eq $PackageName
    if (-not $provisioned) {
        throw "Package is not provisioned for the machine: $PackageName"
    }

    $accounts = @(
        foreach ($userName in $testUsers) {
            New-TestAccount $userName
        }
    )

    $registrationResults = @(
        foreach ($account in $accounts) {
            $outputPath = Join-Path $evidenceRoot "$($account.Name)-register.json"
            $escapedOutputPath = Escape-SingleQuoted $outputPath
            $escapedPackageName = Escape-SingleQuoted $PackageName
            $escapedFamilyName = Escape-SingleQuoted $PackageFamilyName
            $script = @"
`$ErrorActionPreference = 'Stop'
`$package = Get-AppxPackage -Name '$escapedPackageName' -ErrorAction SilentlyContinue
`$automaticRegistration = [bool]`$package
if (-not `$package) {
    Add-AppxPackage -RegisterByFamilyName -MainPackage '$escapedFamilyName'
    `$package = Get-AppxPackage -Name '$escapedPackageName' -ErrorAction Stop
}
[ordered]@{
    user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    automaticRegistration = `$automaticRegistration
    packageFullName = `$package.PackageFullName
    status = [string]`$package.Status
} | ConvertTo-Json | Set-Content -LiteralPath '$escapedOutputPath' -Encoding utf8
"@
            Invoke-TestUserProcess $account $script
            Get-Content -LiteralPath $outputPath -Raw | ConvertFrom-Json
        }
    )

    foreach ($registration in $registrationResults) {
        if ($registration.packageFullName -notlike
            'Microsoft.PowerToys.WsPuvr.Updater_5.0.0.0_*') {
            throw "Unexpected package for $($registration.user): " +
                $registration.packageFullName
        }
    }

    $firstAccount = $accounts[0]
    $removeOutputPath = Join-Path $evidenceRoot "$($firstAccount.Name)-remove.json"
    $escapedRemoveOutputPath = Escape-SingleQuoted $removeOutputPath
    $escapedPackageName = Escape-SingleQuoted $PackageName
    $removeScript = @"
`$ErrorActionPreference = 'Stop'
`$package = Get-AppxPackage -Name '$escapedPackageName' -ErrorAction Stop
`$fullName = `$package.PackageFullName
Remove-AppxPackage -Package `$fullName
`$remaining = Get-AppxPackage -Name '$escapedPackageName' -ErrorAction SilentlyContinue
[ordered]@{
    user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    removedPackage = `$fullName
    remains = [bool]`$remaining
} | ConvertTo-Json | Set-Content -LiteralPath '$escapedRemoveOutputPath' -Encoding utf8
"@
    Invoke-TestUserProcess $firstAccount $removeScript
    $firstUserRemoval =
        Get-Content -LiteralPath $removeOutputPath -Raw |
        ConvertFrom-Json

    $secondUserPackage = Get-AppxPackage `
        -User $accounts[1].Sid `
        -Name $PackageName `
        -ErrorAction SilentlyContinue
    $provisionedAfterRemoval = Get-AppxProvisionedPackage -Online |
        Where-Object DisplayName -eq $PackageName
    $updaterService = Get-CimInstance Win32_Service `
        -Filter "Name='PtPuvrUpdater'"

    if ($firstUserRemoval.remains -or
        -not $secondUserPackage -or
        -not $provisionedAfterRemoval -or
        -not $updaterService) {
        throw 'The package or updater service did not survive one-user removal.'
    }

    $allUsersPackage = Get-AppxPackage -AllUsers -Name $PackageName
    $result = [ordered]@{
        timestamp = (Get-Date).ToString('o')
        registrations = $registrationResults
        firstUserRemoval = $firstUserRemoval
        secondUserStillRegistered = [bool]$secondUserPackage
        machineProvisioningStillPresent = [bool]$provisionedAfterRemoval
        updaterServiceExists = [bool]$updaterService
        updaterServiceState = $updaterService.State
        updaterServiceAccount = $updaterService.StartName
        registeredUserEntries = @(
            $allUsersPackage.PackageUserInformation |
            ForEach-Object { [string]$_ }
        )
        verdict = 'PASS'
    }
    $result |
        ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath $resultPath -Encoding utf8
    $result | ConvertTo-Json -Depth 8
}
finally {
    foreach ($account in $accounts) {
        $package = Get-AppxPackage `
            -User $account.Sid `
            -Name $PackageName `
            -ErrorAction SilentlyContinue
        if ($package) {
            Remove-AppxPackage `
                -Package $package.PackageFullName `
                -User $account.Sid `
                -ErrorAction SilentlyContinue
        }
    }
    foreach ($account in $accounts) {
        Remove-LocalUser -Name $account.Name -ErrorAction SilentlyContinue
        Get-CimInstance Win32_UserProfile `
            -Filter "SID='$($account.Sid)'" `
            -ErrorAction SilentlyContinue |
            Remove-CimInstance -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $evidenceRoot -Recurse -Force -ErrorAction SilentlyContinue
}
