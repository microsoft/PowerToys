# Copyright (c) Microsoft Corporation.
# Licensed under the MIT license.

function Assert-PlainPath([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            if ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Refusing a reparse point: $current"
            }
        }
        $parent = [IO.Directory]::GetParent($current)
        $current = if ($parent) { $parent.FullName } else { $null }
    }
}

function Invoke-LocalStorageLifecycle(
    [string]$Verb, [string]$NativeRoot, [string]$BundleRoot, [string]$ResultsRoot, [string]$OwnerSid) {
    $app = 'PowerToysProtectedStorageLocalTest'
    $inputHelper = Join-Path $NativeRoot 'ProtectedStorage.LocalLifecycle.exe'
    if (!(Test-Path -LiteralPath $inputHelper)) {
        throw 'Build the local-test lifecycle helper first. No installed service has been changed.'
    }
    if (![Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Local service deployment requires an elevated terminal.'
    }
    $stage = Join-Path $env:ProgramFiles "${app}Tools\$([guid]::NewGuid().ToString('N'))"
    Assert-PlainPath $stage
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    $security = [Security.AccessControl.DirectorySecurity]::new()
    $security.SetSecurityDescriptorSddlForm('O:BAG:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)')
    Set-Acl -LiteralPath $stage -AclObject $security
    $helper = Join-Path $stage 'Lifecycle.exe'
    Copy-Item -LiteralPath $inputHelper -Destination $helper
    $broker = "${app}Provision_$([guid]::NewGuid().ToString('N'))"
    $command = "`"$helper`" --service $Verb $OwnerSid `"$BundleRoot`""
    & sc.exe create $broker binPath= $command start= demand obj= LocalSystem | Out-Null
    if ($LASTEXITCODE) { throw "Temporary test lifecycle creation failed: $LASTEXITCODE" }
    $terminalRecorded = $false
    try {
        & sc.exe start $broker | Out-Null
        if ($LASTEXITCODE) { throw "Temporary test lifecycle start failed: $LASTEXITCODE" }
        $resultPath = Join-Path $stage 'result.json'
        $controller = Get-Service -Name $broker
        $controller.WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(90))
        if (!(Test-Path -LiteralPath $resultPath)) { throw "Lifecycle outcome unknown; inspect $stage. Do not replay." }
        $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        if ($result.ok -isnot [bool]) { throw "Malformed lifecycle result; retained evidence at $stage." }
        New-Item -ItemType Directory -Path $ResultsRoot -Force | Out-Null
        $saved = Join-Path $ResultsRoot "$Verb-$([DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff')).json"
        Copy-Item -LiteralPath $resultPath -Destination $saved
        $terminalRecorded = $true
        if (!$result.ok) { throw "Local lifecycle failed: $($result | ConvertTo-Json -Compress). Evidence: $saved" }
        $result | ConvertTo-Json -Compress | Write-Host
        if ($result.profileCleanupPending) {
            Write-Warning "The isolated VA profile is still in use (code $($result.profileNativeCode)). Test service/code/data were removed; the exact profile remains recorded for retry."
        }
    }
    finally {
        $brokerState = Get-Service -Name $broker -ErrorAction SilentlyContinue
        if ($brokerState -and $brokerState.Status -eq 'Stopped') {
            & sc.exe delete $broker | Out-Null
            if ($LASTEXITCODE) { throw "Temporary test lifecycle deletion failed: $LASTEXITCODE" }
            if ($terminalRecorded) {
                Remove-Item -LiteralPath $stage -Recurse -Force
            } else {
                Write-Warning "Unknown lifecycle outcome; protected evidence retained at $stage."
            }
        } elseif ($brokerState) {
            Write-Warning "Lifecycle still active; retained $broker and $stage for inspection."
        }
    }
}
