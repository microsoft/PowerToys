# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

function Get-MwbModernRecoveryIdentity {
    param($Modern, $Provision, [string]$ControlRoot)
    $id = [guid]::Parse([string]$Provision.RunId)
    if ($id -eq [guid]::Empty -or $Provision.SandboxBackend -cne 'WinApp' -or
        $Modern.Backend -cne 'WinApp' -or [guid]::Parse([string]$Modern.InstanceId) -ne $id) {
        throw 'Modern Sandbox recovery does not match the protected run identity.'
    }
    $stateRoot = Join-Path $ControlRoot 'winapp-target'
    $cli = [IO.Path]::GetFullPath([string]$Provision.SandboxWinAppPath)
    if ($cli -notmatch '^[A-Za-z]:\\' -or [IO.Path]::GetFileName($cli) -ine 'winapp.exe' -or
        [IO.Path]::GetFullPath([string]$Modern.WinAppPath) -ine $cli -or
        $Provision.SandboxWinAppSha256 -notmatch '^[0-9a-fA-F]{64}$' -or
        $Modern.WinAppSha256 -ine $Provision.SandboxWinAppSha256 -or
        [IO.Path]::GetFullPath([string]$Modern.TargetStateRoot).TrimEnd('\') -ine $stateRoot.TrimEnd('\')) {
        throw 'Modern Sandbox recovery tool or private state identity changed.'
    }
    if ($Modern.CreationAttempted -isnot [bool] -or $Modern.CreationConfirmed -isnot [bool]) {
        throw 'Modern Sandbox creation evidence is malformed.'
    }
    [pscustomobject]@{ InstanceId = $id; WinAppPath = $cli; TargetStateRoot = $stateRoot }
}

function Get-MwbTrustedSandboxCli {
    param([switch]$ResolvedExecutable)
    $alias = Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps\wsb.exe'
    if (-not (Test-Path -LiteralPath $alias -PathType Leaf) -or
        -not ((Get-Item -LiteralPath $alias -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'The registered Windows Sandbox CLI alias is unavailable for this user.'
    }
    $packages = @(Get-AppxPackage -Name 'MicrosoftWindows.WindowsSandbox' | Where-Object {
        $_.PackageFamilyName -ceq 'MicrosoftWindows.WindowsSandbox_cw5n1h2txyewy'
    })
    if ($packages.Count -ne 1) { throw 'The expected Windows Sandbox package is not uniquely registered.' }
    if ($ResolvedExecutable) {
        $executable = Join-Path $packages[0].InstallLocation 'wsb.exe'
        if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
            throw 'The registered Sandbox provider executable is unavailable.'
        }
        return $executable
    }
    $alias
}

function Invoke-MwbSandboxControl {
    param([string]$Executable, [ValidateSet('List', 'Stop')][string]$Action, [guid]$InstanceId)
    if ($Action -eq 'Stop' -and $InstanceId -eq [guid]::Empty) { throw 'A nonempty owned Sandbox ID is required.' }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $Executable
    $start.Arguments = if ($Action -eq 'List') { 'list --raw' } else { "stop --id $InstanceId --raw" }
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill()
            if (-not $process.WaitForExit(5000)) { throw 'The owned Sandbox control command did not stop.' }
            throw "Sandbox control $Action exceeded its deadline."
        }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 5000)) {
            throw 'Sandbox control output did not complete.'
        }
        if ($process.ExitCode -ne 0) { throw "Sandbox control $Action failed with exit code $($process.ExitCode)." }
        $text = $stdout.GetAwaiter().GetResult()
        if ($Action -eq 'List') { $text | ConvertFrom-Json }
    }
    finally { $process.Dispose() }
}

function Get-MwbModernInstanceIds {
    param($Inventory)
    if (-not $Inventory.PSObject.Properties['WindowsSandboxEnvironments']) {
        throw 'Sandbox inventory did not contain the expected instance array.'
    }
    $ids = @()
    foreach ($instance in @($Inventory.WindowsSandboxEnvironments)) {
        $id = [guid]::Parse([string]$instance.Id)
        if ($id -eq [guid]::Empty -or $id -in $ids) { throw 'Sandbox inventory contains invalid or duplicate identities.' }
        $ids += $id
    }
    $ids
}

function Remove-MwbPrivateTargetState {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $pending = [Collections.Generic.Stack[string]]::new()
    $pending.Push([IO.Path]::GetFullPath($Path))
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        if ((Get-Item -LiteralPath $directory -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw 'Private Sandbox state contains a reparse point; refusing recursive cleanup.'
        }
        foreach ($entry in Get-ChildItem -LiteralPath $directory -Force) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Private Sandbox state contains a reparse point; refusing recursive cleanup.'
            }
            if ($entry.PSIsContainer) { $pending.Push($entry.FullName) }
        }
    }
    Remove-Item -LiteralPath ([IO.Path]::GetFullPath($Path)) -Recurse -Force
}
