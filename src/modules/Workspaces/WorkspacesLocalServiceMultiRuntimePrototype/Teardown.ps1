[CmdletBinding()]
param(
    [string]$FirstOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1122',
    [string]$SecondOwnerSid = 'S-1-5-21-1959867211-618815089-525172305-1123',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Teardown requires an elevated administrator token.'
}

$controller = Join-Path $PSScriptRoot "artifacts\bin\x64\$Configuration\PtLsmrController.exe"
if (Test-Path $controller) {
    foreach ($owner in $FirstOwnerSid, $SecondOwnerSid) {
        & $controller --cleanup --owner-sid $owner
        if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1060) {
            throw "Updater cleanup failed for ${owner}: $LASTEXITCODE"
        }
    }
}

$updater = Get-Service -Name PtLsmrUpdater -ErrorAction SilentlyContinue
if ($updater) {
    if ($updater.Status -ne 'Stopped') {
        Stop-Service -Name PtLsmrUpdater -Force
        $updater.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    sc.exe delete PtLsmrUpdater | Out-Host
}

$packageName = 'Microsoft.PowerToys.WsLocalSvcMultiRt'
Get-AppxPackage -AllUsers -Name $packageName -ErrorAction SilentlyContinue |
    ForEach-Object {
        try {
            Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction Stop
        }
        catch {
            Write-Warning "Exact package removal is pending or failed: $($_.Exception.Message)"
        }
    }

$installRoot = Join-Path $env:ProgramFiles 'PowerToys\WorkspacesLocalServiceMultiRuntimePrototype'
$storeRoot = Join-Path $env:ProgramData 'Microsoft\PowerToys\WorkspacesLocalServiceMultiRuntimePrototype'
Remove-Item $installRoot -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $storeRoot -Recurse -Force -ErrorAction SilentlyContinue
$certificateSubject = 'CN=PowerToys Workspaces LocalService Multi Runtime Prototype Test'
foreach ($store in 'Cert:\CurrentUser\My', 'Cert:\CurrentUser\TrustedPeople',
         'Cert:\LocalMachine\My', 'Cert:\LocalMachine\TrustedPeople') {
    $certificates = @(Get-ChildItem $store -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -eq $certificateSubject })
    foreach ($certificate in $certificates) {
        Remove-Item -LiteralPath $certificate.PSPath -Force
    }
}

Get-Service -Name 'PtLsmr*' -ErrorAction SilentlyContinue
Get-AppxPackage -AllUsers -Name $packageName -ErrorAction SilentlyContinue |
    Select-Object PackageFullName, PackageUserInformation
