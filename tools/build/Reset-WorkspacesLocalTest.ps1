# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.
[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$PrepareMigration,
    [ValidateSet('x64','ARM64')][string]$Platform = 'x64'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$package = Join-Path $repo "$Platform\Debug"
$tools = Join-Path $package 'ProtectedStorageLocalTest'
$owner = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$service = "PowerToysProtectedStorageLocalTest_$owner"
$settings = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToysLocalE2E'
$workspaces = Join-Path $settings 'Workspaces'
$legacy = Join-Path $workspaces 'workspaces.json'
$operation = [guid]::NewGuid().ToString('N')
. (Join-Path $PSScriptRoot 'ProtectedStorage.LocalDeployment.ps1')

function New-MigrationDocument {
    $monitor = @{
        id='MigrationTestPrimary'; 'instance-id'=''; 'monitor-number'=1; dpi=96
        'monitor-rect-dpi-aware'=@{left=0;top=0;width=1920;height=1080}
        'monitor-rect-dpi-unaware'=@{left=0;top=0;width=1920;height=1080}
    }
    function App([string]$Name,[string]$Path,[int]$X,[int]$Y,[bool]$Minimized=$false) {
        @{
            application=$Name; 'application-path'=$Path; title="Migration test - $Name"
            'command-line-arguments'=''; 'is-elevated'=$false; 'can-launch-elevated'=$true
            minimized=$Minimized; maximized=$false; monitor=1
            position=@{X=$X;Y=$Y;width=800;height=600}
        }
    }
    # Application IDs/package metadata are deliberately absent, as in legacy files.
    return @{
        workspaces=@(
            @{
                id='{795B9C05-561C-462E-BDB3-894A14D4D001}'; name='Migration - Notes'
                'creation-time'=1700000000; 'last-launched-time'=1700000100
                'is-shortcut-needed'=$false; 'move-existing-windows'=$false
                'monitor-configuration'=@($monitor)
                applications=@((App 'Notepad' (Join-Path $env:windir 'System32\notepad.exe') 80 80))
            },
            @{
                id='{795B9C05-561C-462E-BDB3-894A14D4D002}'; name='Migration - Two Windows'
                'creation-time'=1700000200; 'last-launched-time'=0
                'is-shortcut-needed'=$false; 'move-existing-windows'=$true
                'monitor-configuration'=@($monitor)
                applications=@(
                    (App 'File Explorer' (Join-Path $env:windir 'explorer.exe') 120 100),
                    (App 'Notepad' (Join-Path $env:windir 'System32\notepad.exe') 960 100 $true)
                )
            }
        )
    }
}

function Validate-Migration([byte[]]$Bytes) {
    $library = [Reflection.Assembly]::LoadFrom((Join-Path $package 'PowerToys.WorkspacesCsharpLibrary.dll'))
    $validator = $library.GetType('WorkspacesCsharpLibrary.Data.WorkspacesValidator', $true)
    $memory = [ReadOnlyMemory[byte]]::new($Bytes)
    $projects = $validator.GetMethod('Parse').Invoke($null, @($memory,$false,$true))
    if ($projects.Count -ne 2 -or ((@($projects | ForEach-Object {$_.Applications.Count}) -join ',') -ne '1,2')) {
        throw 'Migration sample did not validate as the expected two complete workspaces.'
    }
    foreach ($project in $projects) {
        foreach ($app in $project.Applications) {
            $parsed = [guid]::Empty
            if (![guid]::TryParse($app.Id,[ref]$parsed)) { throw 'Legacy application ID conversion failed.' }
        }
    }
}

Assert-PlainPath $package
Assert-PlainPath $workspaces
if (!(Test-Path -LiteralPath (Join-Path $package 'local-e2e.json'))) {
    throw 'Build the local E2E version first; installed-release data is never reset by this script.'
}
$identityAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $package 'PowerToys.ProtectedStorage.Client.Managed.dll'))
$identity = $identityAssembly.GetType('PowerToys.ProtectedStorage.StorageIdentity',$true)
if (!$identity.GetProperty('IsLocalTest').GetValue($null) -or
    $identity.GetProperty('WorkspacesDirectory').GetValue($null) -ne $workspaces) {
    throw 'The selected build is not bound to the expected isolated migration directory.'
}
$document = New-MigrationDocument
$encoding = [Text.UTF8Encoding]::new($true)
$json = $document | ConvertTo-Json -Depth 12
$bytes = [byte[]]($encoding.GetPreamble() + $encoding.GetBytes($json))
if ($PrepareMigration) { Validate-Migration $bytes }
$description = 'Back up and reset this owner''s local-test service, Code/Data/Policy/initialization and client workspace files'
if ($PrepareMigration) { $description += '; seed two legacy workspaces without provisioning the service' }
if (!$PSCmdlet.ShouldProcess("$service; $workspaces", $description)) { return }
if (![Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run the reset command as administrator in the original test user account.'
}

& (Join-Path $PSScriptRoot 'Build-PowerToysLocalE2E.ps1') -Mode Stop -Platform $Platform
$clientBackup = Join-Path $settings "ResetBackups\$operation"
Assert-PlainPath $clientBackup
New-Item -ItemType Directory -Path $clientBackup | Out-Null
foreach ($name in @('settings.json','oobe_settings.json','last_version_run.json')) {
    $file = Join-Path $settings $name
    if (Test-Path -LiteralPath $file) {
        Assert-PlainPath $file
        Copy-Item -LiteralPath $file -Destination (Join-Path $clientBackup $name)
    }
}
if (Test-Path -LiteralPath $workspaces) {
    $links = @(Get-ChildItem -LiteralPath $workspaces -Force -Recurse |
        Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint})
    if ($links.Count) { throw 'Workspace files contain reparse points; reset refused.' }
    Copy-Item -LiteralPath $workspaces -Destination $clientBackup -Recurse
}
$began = [DateTime]::UtcNow
& (Join-Path $PSScriptRoot 'Build-PowerToysLocalE2E.ps1') -Mode Reset -SkipBuild -BackupBeforeReset -Platform $Platform
$resultFile = Get-ChildItem -LiteralPath (Join-Path $tools 'Results') -Filter 'clean-backup-*.json' -File |
    Where-Object LastWriteTimeUtc -ge $began | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if (!$resultFile) { throw 'Reset did not leave a fresh cleanup result.' }
$result = Get-Content -LiteralPath $resultFile.FullName -Raw | ConvertFrom-Json
if (!$result.ok -or !$result.serviceRemoved -or !$result.testDataRemoved) {
    throw 'The local store was not fully reset; do not claim a migration-ready state.'
}
if (Get-Service -Name $service -ErrorAction SilentlyContinue) { throw 'Local test service remains registered.' }
if (Test-Path -LiteralPath $legacy) { throw 'Unexpected legacy source exists after reset; refusing to overwrite it.' }
if ($PrepareMigration) {
    # Use captured monitor identities if the saved collection had them. No app data
    # or commands from the backup are copied into the synthetic migration fixture.
    $monitorBackup = $result.backupPath
    if (!$monitorBackup) {
        $backups = Join-Path $env:ProgramData "PowerToysProtectedStorageLocalTest\Backups\$owner"
        if (Test-Path -LiteralPath $backups) {
            Assert-PlainPath $backups
            $latest = Get-ChildItem -LiteralPath $backups -Directory |
                Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'workspaces.json') } |
                Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
            if ($latest) { $monitorBackup = $latest.FullName }
        }
    }
    if ($monitorBackup -and (Test-Path -LiteralPath (Join-Path $monitorBackup 'workspaces.json'))) {
        $export = Join-Path $monitorBackup 'workspaces.json'
        Assert-PlainPath $export
        if ((Get-Item -LiteralPath $export).Length -gt 8MB) { throw 'Workspace backup exceeds the supported size bound.' }
        $previous = Get-Content -LiteralPath $export -Raw | ConvertFrom-Json
        $monitors = @($previous.workspaces | ForEach-Object {$_.'monitor-configuration'} | Select-Object -First 1)
        if ($monitors.Count -eq 1) {
            foreach ($project in $document.workspaces) {
                $project.'monitor-configuration'=$monitors
                foreach ($app in $project.applications) {$app.monitor=$monitors[0].'monitor-number'}
            }
        }
    }
    $json = $document | ConvertTo-Json -Depth 12
    $bytes = [byte[]]($encoding.GetPreamble() + $encoding.GetBytes($json))
    Validate-Migration $bytes
    $stream = [IO.File]::Open($legacy,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes); $stream.Flush($true) } finally { $stream.Dispose() }
    [IO.File]::WriteAllBytes((Join-Path $clientBackup 'migration-source.json'),$bytes)
}
$report = [ordered]@{
    ownerSid=$owner; scope='Current owner, PowerToysProtectedStorageLocalTest only'
    serviceAbsent=$true; testDataReset=$true; profileCleanupPending=[bool]$result.profileCleanupPending
    profileNativeCode=$result.profileNativeCode; storageBackup=$result.backupPath; clientBackup=$clientBackup
    migrationPrepared=[bool]$PrepareMigration; legacyPath=$(if($PrepareMigration){$legacy}else{$null})
    expectedWorkspaces=$(if($PrepareMigration){@('Migration - Notes','Migration - Two Windows')}else{@()})
    legacySha256=$(if($PrepareMigration){(Get-FileHash -LiteralPath $legacy -Algorithm SHA256).Hash}else{$null})
    unrelatedPrototypesPreserved=$true
}
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $clientBackup 'reset-result.json') -Encoding utf8
$report | ConvertTo-Json -Depth 5
Write-Host 'Ready for first-use testing. Open the local PowerToys, then Workspaces > Set up. No migration has been consumed.'
