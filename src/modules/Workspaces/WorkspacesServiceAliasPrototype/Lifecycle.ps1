[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('install', 'test', 'update', 'invalid-update', 'tamper', 'break-1069', 'repair', 'before-reboot', 'after-reboot', 'uninstall', 'two-owner')]
    [string]$Verb,
    [string]$OwnerSid,
    [string]$SecondOwnerSid,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$metadataPath = Join-Path $root 'artifacts\packages\packages.json'
if (-not (Test-Path $metadataPath)) {
    throw "Run .\Package.ps1 -TrustMachine from an elevated PowerShell session first."
}
$metadata = Get-Content $metadataPath -Raw | ConvertFrom-Json
$controller = Join-Path $root "artifacts\bin\x64\$Configuration\PtAliasProtoController.exe"
$launcher = Join-Path $root "artifacts\bin\x64\$Configuration\PtAliasProtoLauncher.exe"
if (-not (Test-Path $controller) -or -not (Test-Path $launcher)) {
    throw "Run .\Build.ps1 first."
}
if (-not $OwnerSid) {
    $OwnerSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
}

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "This lifecycle verb requires an elevated PowerShell session."
    }
}
function Invoke-Controller([string[]]$Arguments) {
    & $controller @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Controller failed ($LASTEXITCODE): $($Arguments -join ' ')"
    }
}
function Get-InstanceSuffix([string]$Sid) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::Unicode.GetBytes($Sid)
        $hash = $sha.ComputeHash($bytes)
        return (($hash[0..3] | ForEach-Object { $_.ToString('x2') }) -join '')
    } finally {
        $sha.Dispose()
    }
}
function Assert-Healthy([string]$Sid, [string]$ExpectedFullName) {
    $text = & $controller status --owner-sid $Sid 2>&1
    if ($LASTEXITCODE -ne 0) { throw ($text -join [Environment]::NewLine) }
    $joined = $text -join [Environment]::NewLine
    if ($joined -notmatch 'workerPid=(?!0)\d+' -or
        $joined -notmatch 'serviceSidPresent=1' -or
        -not $joined.Contains("package=$ExpectedFullName")) {
        throw "Health verification failed:`n$joined"
    }
    $joined | Write-Host
}
function Install-One([string]$Sid) {
    Invoke-Controller @('install', '--launcher', $launcher, '--package-full-name', $metadata.packages.v1.fullName, '--owner-sid', $Sid)
    Assert-Healthy $Sid $metadata.packages.v1.fullName
}

switch ($Verb) {
    'install' {
        Assert-Administrator
        Install-One $OwnerSid
    }
    'test' {
        $status = & $controller status --owner-sid $OwnerSid 2>&1
        if ($LASTEXITCODE -ne 0) { throw ($status -join [Environment]::NewLine) }
        $expected = if (($status -join "`n").Contains($metadata.packages.v2.fullName)) { $metadata.packages.v2.fullName } else { $metadata.packages.v1.fullName }
        Assert-Healthy $OwnerSid $expected
    }
    'update' {
        Assert-Administrator
        Assert-Healthy $OwnerSid $metadata.packages.v1.fullName
        Add-AppxPackage -Path $metadata.packages.v2.path -Stage
        Invoke-Controller @('ensure-package', '--package-full-name', $metadata.packages.v2.fullName, '--owner-sid', $OwnerSid)
        Assert-Healthy $OwnerSid $metadata.packages.v2.fullName
    }
    'invalid-update' {
        Assert-Administrator
        $status = & $controller status --owner-sid $OwnerSid 2>&1
        if ($LASTEXITCODE -ne 0) { throw ($status -join [Environment]::NewLine) }
        $lastGood = if (($status -join "`n").Contains($metadata.packages.v2.fullName)) { $metadata.packages.v2.fullName } else { $metadata.packages.v1.fullName }
        & $controller ensure-package --package-full-name $metadata.invalidUnstagedFullName --owner-sid $OwnerSid
        if ($LASTEXITCODE -eq 0) { throw "A valid-but-unstaged update was unexpectedly accepted." }
        Assert-Healthy $OwnerSid $lastGood
    }
    'tamper' {
        Assert-Administrator
        $suffix = Get-InstanceSuffix $OwnerSid
        $marker = Join-Path $env:ProgramData "Microsoft\PowerToys\PtAliasProto\$suffix\tamper-code-executed.marker"
        Remove-Item $marker -Force -ErrorAction SilentlyContinue
        Invoke-Controller @('tamper-alias', '--owner-sid', $OwnerSid)
        $status = & $controller status --owner-sid $OwnerSid 2>&1
        $expected = if (($status -join "`n").Contains($metadata.packages.v2.fullName)) { $metadata.packages.v2.fullName } else { $metadata.packages.v1.fullName }
        Invoke-Controller @('ensure-package', '--package-full-name', $expected, '--owner-sid', $OwnerSid)
        Assert-Healthy $OwnerSid $expected
        if (Test-Path $marker) {
            throw "The unpackaged tamper target executed before identity verification."
        }
    }
    'break-1069' {
        Assert-Administrator
        Invoke-Controller @('break-1069', '--owner-sid', $OwnerSid)
    }
    'repair' {
        Assert-Administrator
        Invoke-Controller @('repair', '--owner-sid', $OwnerSid)
        $status = & $controller status --owner-sid $OwnerSid 2>&1
        $expected = if (($status -join "`n").Contains($metadata.packages.v2.fullName)) { $metadata.packages.v2.fullName } else { $metadata.packages.v1.fullName }
        Assert-Healthy $OwnerSid $expected
    }
    'before-reboot' {
        Assert-Healthy $OwnerSid $metadata.packages.v2.fullName
        [ordered]@{
            ownerSid = $OwnerSid
            expectedPackage = $metadata.packages.v2.fullName
            utc = [DateTime]::UtcNow.ToString('o')
        } | ConvertTo-Json | Set-Content (Join-Path $root 'artifacts\reboot-checkpoint.json') -Encoding utf8NoBOM
        Write-Host "Checkpoint written. Reboot manually; this script never initiates reboot."
    }
    'after-reboot' {
        $checkpoint = Get-Content (Join-Path $root 'artifacts\reboot-checkpoint.json') -Raw | ConvertFrom-Json
        Assert-Healthy $checkpoint.ownerSid $checkpoint.expectedPackage
    }
    'uninstall' {
        Assert-Administrator
        $preStatus = & $controller status --owner-sid $OwnerSid 2>&1
        if ($LASTEXITCODE -ne 0) { throw ($preStatus -join [Environment]::NewLine) }
        $accountSidMatch = [regex]::Match(($preStatus -join "`n"), '(?m)^accountSid=(S-[0-9-]+)$')
        if (-not $accountSidMatch.Success) { throw "Could not capture the service account SID before uninstall." }
        $accountSid = $accountSidMatch.Groups[1].Value
        $profilePath = $null
        $profileKey = "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$accountSid"
        if (Test-Path $profileKey) {
            $profilePath = [Environment]::ExpandEnvironmentVariables(
                (Get-ItemProperty $profileKey -Name ProfileImagePath).ProfileImagePath)
        }
        $output = & $controller uninstall --owner-sid $OwnerSid 2>&1
        if ($LASTEXITCODE -ne 0) { throw ($output -join [Environment]::NewLine) }
        $output | Write-Host
        $suffix = Get-InstanceSuffix $OwnerSid
        $service = Get-Service -Name "PtAliasProtoSvc_$suffix" -ErrorAction SilentlyContinue
        $account = Get-LocalUser -Name "PtAliasProto$suffix" -ErrorAction SilentlyContinue
        $store = Join-Path $env:ProgramData "Microsoft\PowerToys\PtAliasProto\$suffix"
        $launcherDirectory = Join-Path $env:ProgramFiles "PowerToys\PtAliasProto\$suffix"
        if ($service -or $account -or (Test-Path $store) -or (Test-Path $launcherDirectory)) {
            throw "Exact service/account/store/launcher cleanup verification failed for $suffix."
        }
        $pending = @($output | Where-Object { "$_" -match '^(PACKAGE|PROFILE)_CLEANUP_PENDING\b' })
        if ($pending.Count -ne 0 -or (Test-Path $profileKey) -or ($profilePath -and (Test-Path $profilePath))) {
            throw "Uninstall completed core teardown but OS-deferred package/profile cleanup remains:`n$($pending -join [Environment]::NewLine)"
        }
        if (-not (Get-Service -Name 'PtAliasProtoSvc_*' -ErrorAction SilentlyContinue)) {
            foreach ($package in $metadata.packages.psobject.Properties.Value) {
                Invoke-Controller @('unstage-package', '--package-full-name', $package.fullName)
            }
        }
        Write-Host "Verified exact service, account, prototype store, launcher, and profile cleanup."
    }
    'two-owner' {
        Assert-Administrator
        if (-not $SecondOwnerSid) {
            throw "Pass -SecondOwnerSid S-1-... . Example: .\Lifecycle.ps1 -Verb two-owner -SecondOwnerSid '<SID>'"
        }
        Install-One $OwnerSid
        $blocked = & $controller install --launcher $launcher --package-full-name $metadata.packages.v1.fullName --owner-sid $SecondOwnerSid 2>&1
        if ($LASTEXITCODE -eq 0 -or ($blocked -join "`n") -notmatch 'error 5:') {
            throw "Expected the second session-0 packaged worker installation to roll back with error 5:`n$($blocked -join [Environment]::NewLine)"
        }
        if (($blocked -join "`n") -match 'ROLLBACK_PROFILE_CLEANUP_PENDING') {
            throw "Failed second-owner installation left OS-deferred profile cleanup:`n$($blocked -join [Environment]::NewLine)"
        }
        $secondSuffix = Get-InstanceSuffix $SecondOwnerSid
        if ((Get-Service "PtAliasProtoSvc_$secondSuffix" -ErrorAction SilentlyContinue) -or
            (Get-LocalUser "PtAliasProto$secondSuffix" -ErrorAction SilentlyContinue) -or
            (Test-Path (Join-Path $env:ProgramData "Microsoft\PowerToys\PtAliasProto\$secondSuffix")) -or
            (Test-Path (Join-Path $env:ProgramFiles "PowerToys\PtAliasProto\$secondSuffix")) -or
            (Get-ChildItem "$env:SystemRoot\ServiceProfiles" -Directory -Filter "PtAliasProto$secondSuffix*" -ErrorAction SilentlyContinue)) {
            throw "Failed second-owner installation did not roll back its service/account/store/launcher/profile."
        }
        Invoke-Controller @('stop-worker', '--owner-sid', $OwnerSid)
        Install-One $SecondOwnerSid
        Write-Host "CONFIRMED DESIGN BLOCKER: the second owner installs only after the first owner's packaged worker stops."
    }
}
