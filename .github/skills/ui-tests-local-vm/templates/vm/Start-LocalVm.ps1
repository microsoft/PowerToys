[CmdletBinding()]
param(
    [switch]$WaitForWinRM,
    [ValidateRange(1, 120)]
    [int]$TimeoutMinutes = 45
)

$ErrorActionPreference = 'Stop'

$composeFile = Join-Path $PSScriptRoot 'compose.yml'
$environmentFile = Join-Path $PSScriptRoot '.env'
if (-not (Test-Path $environmentFile -PathType Leaf)) {
    throw "Create $environmentFile from .env.example first."
}
$configuration = @{}
Get-Content $environmentFile | ForEach-Object {
    if ($_ -match '^(?<Name>[A-Za-z_][A-Za-z0-9_]*)=(?<Value>.*)$') {
        $configuration[$Matches.Name] = $Matches.Value
    }
}
if ([string]::IsNullOrWhiteSpace($configuration.VM_ADMIN_PASSWORD) -or
    $configuration.VM_ADMIN_PASSWORD -eq 'replace-with-a-unique-password') {
    throw 'Set a unique VM_ADMIN_PASSWORD in .env before starting the VM.'
}
foreach ($command in @('docker.exe', 'wsl.exe')) {
    if ($null -eq (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command was not found."
    }
}

& docker desktop start
if ($LASTEXITCODE -ne 0) {
    throw 'Docker Desktop failed to start.'
}

& docker context use desktop-linux | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Could not select the Docker Desktop Linux context.'
}

& wsl.exe -d docker-desktop -u root -- sh -lc 'modprobe kvm && (modprobe kvm_intel 2>/dev/null || modprobe kvm_amd 2>/dev/null)'
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to load KVM modules in the docker-desktop WSL distribution.'
}

& docker compose --env-file $environmentFile -f $composeFile up -d
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to start the dockur Windows stack.'
}

$winRmPort = if ($configuration.VM_WINRM_PORT) { [int]$configuration.VM_WINRM_PORT } else { 15986 }
$viewerPort = if ($configuration.VM_VIEWER_PORT) { [int]$configuration.VM_VIEWER_PORT } else { 8006 }
$rdpPort = if ($configuration.VM_RDP_PORT) { [int]$configuration.VM_RDP_PORT } else { 13389 }

if ($WaitForWinRM) {
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    do {
        $client = [Net.Sockets.TcpClient]::new()
        try {
            $connect = $client.BeginConnect('127.0.0.1', $winRmPort, $null, $null)
            if ($connect.AsyncWaitHandle.WaitOne(1000) -and $client.Connected) {
                $client.EndConnect($connect)
                break
            }
        }
        catch {
        }
        finally {
            $client.Dispose()
        }

        if ([DateTime]::UtcNow -ge $deadline) {
            throw "HTTPS WinRM did not become reachable on 127.0.0.1:$winRmPort within $TimeoutMinutes minute(s)."
        }
        Start-Sleep -Seconds 5
    } while ($true)
}

$containerName = if ($configuration.VM_CONTAINER_NAME) { $configuration.VM_CONTAINER_NAME } else { 'powertoys-ui-windows' }
$containerState = & docker inspect $containerName --format '{{.State.Status}}' 2>$null
if ($LASTEXITCODE -ne 0 -or $containerState -ne 'running') {
    throw "The dockur container '$containerName' is not running."
}

[pscustomobject]@{
    Container = $containerName
    State = $containerState
    Viewer = "http://127.0.0.1:$viewerPort/"
    Rdp = "127.0.0.1:$rdpPort"
    WinRM = "https://127.0.0.1:$winRmPort/wsman"
} | ConvertTo-Json
