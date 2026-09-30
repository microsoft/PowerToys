# Automated end-to-end test for the "Copy as UNC path" PowerScript.
#
# It runs the deployed script through PowerScripts.Host.exe against a path on a mapped network drive,
# then asserts that the clipboard received the correct \\server\share\... UNC path.
#
# Drive selection order:
#   1. -DriveLetter you pass in (must be an existing mapped network drive), else
#   2. the first existing mapped network drive (DriveType = 4) on the machine, else
#   3. if running elevated, it provisions a temporary loopback SMB share via setup-network-drive.ps1
#      and tears it down at the end.
#
# Usage (from a normal or elevated PowerShell):
#   .\test-copy-as-unc.ps1
#   .\test-copy-as-unc.ps1 -DriveLetter Z

[CmdletBinding()]
param(
    [string]$DriveLetter,
    [string]$HostExe
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-HostExe {
    param([string]$Explicit)
    if ($Explicit -and (Test-Path $Explicit)) { return (Resolve-Path $Explicit).Path }
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Microsoft\PowerToys\PowerScripts\PowerScripts.Host.exe"),
        "C:\Program Files\PowerToys\modules\PowerScripts\PowerScripts.Host.exe",
        (Join-Path $here "..\..\PowerScripts.Host\bin\Debug\net10.0-windows\PowerScripts.Host.exe")
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return (Resolve-Path $c).Path } }
    throw "Could not find PowerScripts.Host.exe. Pass -HostExe <path>."
}

# Resolve a local path on a mapped drive to its UNC form (same WNetGetUniversalName call the script uses).
Add-Type -Namespace PsTest -Name Unc -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("mpr.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
public static extern int WNetGetUniversalName(string lpLocalPath, int dwInfoLevel, System.IntPtr lpBuffer, ref int lpBufferSize);
'@
function Get-ExpectedUnc {
    param([string]$Path)
    if ($Path -match '^\\\\') { return $Path }
    $size = 2048
    $buf = [System.Runtime.InteropServices.Marshal]::AllocHGlobal($size)
    try {
        $rc = [PsTest.Unc]::WNetGetUniversalName($Path, 1, $buf, [ref]$size)
        if ($rc -eq 0) {
            $ptr = [System.Runtime.InteropServices.Marshal]::ReadIntPtr($buf)
            return [System.Runtime.InteropServices.Marshal]::PtrToStringUni($ptr)
        }
        return $null
    }
    finally { [System.Runtime.InteropServices.Marshal]::FreeHGlobal($buf) }
}

function Test-IsAdmin {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    (New-Object System.Security.Principal.WindowsPrincipal($id)).IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Closes the script's "Copy as UNC path" confirmation box so the run can complete unattended.
Add-Type -Namespace PsTest -Name Win -MemberDefinition @'
[System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
public static extern System.IntPtr FindWindow(string c, string n);
[System.Runtime.InteropServices.DllImport("user32.dll")]
public static extern System.IntPtr SendMessage(System.IntPtr h, uint m, System.IntPtr w, System.IntPtr l);
'@
function Close-ResultBox {
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 300
        $h = [PsTest.Win]::FindWindow($null, "PowerScripts - Copy as UNC path")
        if ($h -ne [IntPtr]::Zero) { [PsTest.Win]::SendMessage($h, 0x10, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; return }
    }
}

$hostExe = Resolve-HostExe -Explicit $HostExe
Write-Host "Host: $hostExe"

# --- Pick / provision a mapped network drive -------------------------------------------------------
$provisioned = $false
$shareName = "PSUncTest"

function Get-NetworkDrive {
    param([string]$Preferred)
    if ($Preferred) {
        $p = $Preferred.TrimEnd(':')
        $d = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='${p}:'" -ErrorAction SilentlyContinue
        if ($d -and $d.DriveType -eq 4) { return "${p}:" }
        throw "Drive ${p}: is not a mapped network drive."
    }
    $d = Get-CimInstance Win32_LogicalDisk -Filter "DriveType=4" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($d) { return $d.DeviceID }
    return $null
}

$drive = Get-NetworkDrive -Preferred $DriveLetter
if (-not $drive) {
    if (Test-IsAdmin) {
        Write-Host "No mapped network drive found; provisioning a temporary loopback share..."
        & (Join-Path $here "setup-network-drive.ps1") -ShareName $shareName | Out-Null
        $provisioned = $true
        $drive = Get-NetworkDrive
        if (-not $drive) { throw "Provisioning did not yield a network drive." }
    }
    else {
        throw "No mapped network drive available. Either run this elevated (to auto-provision one), map a drive yourself, or run setup-network-drive.ps1 as admin and pass -DriveLetter."
    }
}
Write-Host "Using mapped network drive: $drive"

# Ensure there is a file to target.
$testFile = Join-Path $drive "sample.txt"
if (-not (Test-Path $testFile)) {
    "PowerScripts UNC test file." | Set-Content -Path $testFile -Encoding UTF8
}

$expected = Get-ExpectedUnc -Path $testFile
Write-Host "Expected UNC: $expected"

# --- Run the script through the Host and check the clipboard --------------------------------------
& $hostExe trust approve copy-as-unc | Out-Null
Set-Clipboard -Value ""   # clear so we can detect the write

$job = Start-Job -ScriptBlock { param($h, $f) & $h run copy-as-unc --files $f *>&1 } -ArgumentList $hostExe, $testFile
Close-ResultBox
Wait-Job $job -Timeout 30 | Out-Null
$hostOutput = Receive-Job $job
Remove-Job $job -Force -ErrorAction SilentlyContinue

Start-Sleep -Milliseconds 300
$clip = Get-Clipboard -Raw

Write-Host ""
Write-Host "Host output: $hostOutput"
Write-Host "Clipboard  : $clip"
Write-Host ""

$pass = $expected -and ($clip.Trim() -eq $expected.Trim()) -and ($clip.Trim().StartsWith("\\"))
if ($pass) {
    Write-Host "PASS: clipboard contains the expected UNC path." -ForegroundColor Green
}
else {
    Write-Host "FAIL: clipboard did not match the expected UNC path." -ForegroundColor Red
}

if ($provisioned) {
    Write-Host "Cleaning up provisioned share..."
    & (Join-Path $here "teardown-network-drive.ps1") -ShareName $shareName -DriveLetter ($drive.TrimEnd(':')) | Out-Null
}

if (-not $pass) { exit 1 }
