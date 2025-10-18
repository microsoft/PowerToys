<#
.SYNOPSIS
    Determine whether a given process (by PID) runs with an MSIX/UWP package identity.
.DESCRIPTION
    Calls the Windows API GetPackageFullName to check if the target process executes under an MSIX/Sparse App/UWP package identity.
    Returns the package full name when identity is present, or "No package identity" otherwise.
.PARAMETER ProcessId
    The process ID to inspect.
.EXAMPLE
    .\Check-ProcessIdentity.ps1 -pid 12345
#>
param(
    [Parameter(Mandatory=$true)]
    [int]$ProcessId
)

Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class P {
    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern IntPtr OpenProcess(uint a, bool b, int p);
    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern int GetPackageFullName(IntPtr h, ref int l, StringBuilder b);
    public static string G(int pid) {
        IntPtr h = OpenProcess(0x1000, false, pid);
        if (h == IntPtr.Zero) return "Failed to open process";
        int len = 0;
        GetPackageFullName(h, ref len, null);
        if (len == 0) { CloseHandle(h); return "No package identity"; }
        var sb = new StringBuilder(len);
        int r = GetPackageFullName(h, ref len, sb);
        CloseHandle(h);
        return r == 0 ? sb.ToString() : "Error:" + r;
    }
}
'@

$result = [P]::G($ProcessId)
Write-Output $result
