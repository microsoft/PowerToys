# scripts/pt-admin-probe.ps1
# Verify the current session is elevated AND that PT runner inherits the admin token.

if (-not ('PtTok' -as [type])) {
    Add-Type -TypeDefinition @'
        using System;
        using System.Runtime.InteropServices;
        public static class PtTok {
            [DllImport("advapi32.dll", SetLastError=true)]
            public static extern bool OpenProcessToken(IntPtr h, uint da, out IntPtr t);
            [DllImport("advapi32.dll", SetLastError=true)]
            public static extern bool GetTokenInformation(IntPtr t, uint c, IntPtr ti, uint l, out uint rl);
            [DllImport("kernel32.dll")] public static extern IntPtr GetCurrentProcess();
            [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint da, bool inh, int pid);
            [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        }
'@
}

function Test-PtAdmin {
    <#
    .SYNOPSIS
    Check whether the current session is elevated by reading the process token's TokenElevation
    information class (20). Returns $true if elevated.
    #>
    [CmdletBinding()] param()
    $t = [IntPtr]::Zero
    [PtTok]::OpenProcessToken([PtTok]::GetCurrentProcess(), 8, [ref]$t) | Out-Null
    $ti = [Runtime.InteropServices.Marshal]::AllocHGlobal(4)
    $rl = 0
    try {
        [PtTok]::GetTokenInformation($t, 20, $ti, 4, [ref]$rl) | Out-Null
        return ([Runtime.InteropServices.Marshal]::ReadInt32($ti) -eq 1)
    } finally {
        [Runtime.InteropServices.Marshal]::FreeHGlobal($ti)
        [PtTok]::CloseHandle($t) | Out-Null
    }
}

function Test-ProcessElevated {
    <#
    .SYNOPSIS
    Check whether a specific PID is elevated (TokenElevation = 1).
    #>
    [CmdletBinding()] param([Parameter(Mandatory)][int]$ProcessId)
    $proc = [PtTok]::OpenProcess(0x1000, $false, $ProcessId)  # PROCESS_QUERY_LIMITED_INFORMATION
    if ($proc -eq [IntPtr]::Zero) { return $null }
    try {
        $t = [IntPtr]::Zero
        if (-not [PtTok]::OpenProcessToken($proc, 8, [ref]$t)) { return $null }
        try {
            $ti = [Runtime.InteropServices.Marshal]::AllocHGlobal(4)
            $rl = 0
            try {
                [PtTok]::GetTokenInformation($t, 20, $ti, 4, [ref]$rl) | Out-Null
                return ([Runtime.InteropServices.Marshal]::ReadInt32($ti) -eq 1)
            } finally { [Runtime.InteropServices.Marshal]::FreeHGlobal($ti) }
        } finally { [PtTok]::CloseHandle($t) | Out-Null }
    } finally { [PtTok]::CloseHandle($proc) | Out-Null }
}

function Test-PtRunnerAdmin {
    <#
    .SYNOPSIS
    Check whether the PT runner (PowerToys.exe) is currently running elevated.
    .OUTPUTS
    PSCustomObject with .Found (bool), .Pid (int), .Elevated (bool|$null)
    #>
    $pt = Get-Process PowerToys -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $pt) { return [pscustomobject]@{ Found=$false; Pid=$null; Elevated=$null } }
    [pscustomobject]@{
        Found    = $true
        Pid      = $pt.Id
        Elevated = (Test-ProcessElevated -ProcessId $pt.Id)
    }
}
