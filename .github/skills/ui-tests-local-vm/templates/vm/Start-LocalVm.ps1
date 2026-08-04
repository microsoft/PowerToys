[CmdletBinding()]
param(
    [switch]$WaitForWinRM,
    [ValidateRange(1, 720)]
    [int]$TimeoutMinutes = 45,
    [ValidateSet('Default', 'Constrained')]
    [string]$ResourceProfile,
    [switch]$PlanOnly
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

function ConvertTo-Gibibytes {
    param([Parameter(Mandatory)][string]$Size)

    if ($Size -notmatch '^(?<Value>\d+(?:\.\d+)?)(?<Unit>[GgMm])$') {
        throw "RAM size '$Size' must use G or M units."
    }

    $value = [double]::Parse($Matches.Value, [Globalization.CultureInfo]::InvariantCulture)
    if ($Matches.Unit -in @('M', 'm')) {
        return $value / 1024
    }

    return $value
}

$effectiveProfile = if ($PSBoundParameters.ContainsKey('ResourceProfile')) {
    $ResourceProfile
}
elseif ($configuration.VM_RESOURCE_PROFILE) {
    switch ($configuration.VM_RESOURCE_PROFILE.ToLowerInvariant()) {
        'default' { 'Default' }
        'constrained' { 'Constrained' }
        default { throw 'VM_RESOURCE_PROFILE must be default or constrained.' }
    }
}
else {
    'Default'
}

switch ($effectiveProfile) {
    'Default' {
        $effectiveRamSize = if ($configuration.VM_RAM_SIZE) { $configuration.VM_RAM_SIZE } else { '8G' }
        $effectiveCpuCores = if ($configuration.VM_CPU_CORES) { [int]$configuration.VM_CPU_CORES } else { 4 }
    }
    'Constrained' {
        $effectiveRamSize = if ($configuration.VM_CONSTRAINED_RAM_SIZE) { $configuration.VM_CONSTRAINED_RAM_SIZE } else { '4G' }
        $effectiveCpuCores = if ($configuration.VM_CONSTRAINED_CPU_CORES) { [int]$configuration.VM_CONSTRAINED_CPU_CORES } else { 1 }
    }
}

$env:VM_RAM_SIZE = $effectiveRamSize
$env:VM_CPU_CORES = $effectiveCpuCores.ToString([Globalization.CultureInfo]::InvariantCulture)
$minimumWslMemoryGiB = [math]::Ceiling((ConvertTo-Gibibytes $effectiveRamSize) + 4)
$resourcePlan = [pscustomobject]@{
    ResourceProfile = $effectiveProfile
    VmRamSize = $effectiveRamSize
    VmCpuCores = $effectiveCpuCores
    MinimumWslMemoryGiB = $minimumWslMemoryGiB
}
if ($PlanOnly) {
    $resourcePlan | ConvertTo-Json
    return
}

foreach ($command in @('docker.exe', 'wsl.exe')) {
    if ($null -eq (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "$command was not found."
    }
}

$dockerDesktopOutput = & docker desktop start 2>&1
if ($LASTEXITCODE -ne 0) {
    throw 'Docker Desktop failed to start.'
}
Write-Verbose ($dockerDesktopOutput | Out-String)

& docker context use desktop-linux | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Could not select the Docker Desktop Linux context.'
}

$wslMemoryOutput = & wsl.exe -d docker-desktop -u root -- cat /proc/meminfo 2>&1
$wslMemoryLine = $wslMemoryOutput | Where-Object { $_ -match '^MemTotal:\s+\d+\s+kB$' } | Select-Object -First 1
if ($LASTEXITCODE -ne 0 -or $wslMemoryLine -notmatch '^MemTotal:\s+(?<KiB>\d+)\s+kB$') {
    throw 'Could not determine the Docker Desktop WSL2 memory ceiling.'
}

$wslMemoryKiB = [long]$Matches.KiB
$wslMemoryGiB = [math]::Round($wslMemoryKiB / 1MB, 1)
if ($wslMemoryGiB -lt $minimumWslMemoryGiB) {
    throw "Docker Desktop WSL2 exposes $wslMemoryGiB GiB, but the $effectiveProfile profile requires at least $minimumWslMemoryGiB GiB for a $effectiveRamSize guest. Increase [wsl2] memory in %UserProfile%\.wslconfig, run 'wsl --shutdown', and restart Docker Desktop."
}

$kvmOutput = & wsl.exe -d docker-desktop -u root -- sh -lc 'modprobe kvm && (modprobe kvm_intel 2>/dev/null || modprobe kvm_amd 2>/dev/null)' 2>&1
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to load KVM modules in the docker-desktop WSL distribution.'
}
Write-Verbose ($kvmOutput | Out-String)

$composeOutput = & docker compose --env-file $environmentFile -f $composeFile up -d 2>&1
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to start the dockur Windows stack.'
}
Write-Verbose ($composeOutput | Out-String)

$winRmPort = if ($configuration.VM_WINRM_PORT) { [int]$configuration.VM_WINRM_PORT } else { 15986 }
$winRmScheme = if ($configuration.VM_WINRM_SCHEME) { $configuration.VM_WINRM_SCHEME } else { 'https' }
if ($winRmScheme -notin @('http', 'https')) {
    throw 'VM_WINRM_SCHEME must be http or https.'
}
$viewerPort = if ($configuration.VM_VIEWER_PORT) { [int]$configuration.VM_VIEWER_PORT } else { 8006 }
$rdpPort = if ($configuration.VM_RDP_PORT) { [int]$configuration.VM_RDP_PORT } else { 13389 }

if ($WaitForWinRM) {
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    $winRmUri = "${winRmScheme}://127.0.0.1:$winRmPort/wsman"
    do {
        try {
            $probeParameters = @{
                Uri = $winRmUri
                Method = 'Get'
                SkipHttpErrorCheck = $true
                TimeoutSec = 3
            }
            if ($winRmScheme -eq 'https') {
                $probeParameters.SkipCertificateCheck = $true
            }
            $response = Invoke-WebRequest @probeParameters
            if ([int]$response.StatusCode -ge 200 -and [int]$response.StatusCode -lt 500) {
                break
            }
        }
        catch {
        }

        if ([DateTime]::UtcNow -ge $deadline) {
            throw "WinRM did not become responsive at $winRmUri within $TimeoutMinutes minute(s). The container and named volume remain intact; if Windows Setup is visibly progressing, rerun this command with a longer timeout."
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
    ResourceProfile = $effectiveProfile
    Ram = $effectiveRamSize
    CpuCores = $effectiveCpuCores
    WslMemoryGiB = $wslMemoryGiB
    Viewer = "http://127.0.0.1:$viewerPort/"
    Rdp = "127.0.0.1:$rdpPort"
    WinRM = "${winRmScheme}://127.0.0.1:$winRmPort/wsman"
} | ConvertTo-Json
