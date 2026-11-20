# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
    Sets pointer speed to 10 and turns off Enhance Pointer Precision for the interactive user.

.DESCRIPTION
    Runs inside the guest in the standard user's interactive session, under PowerShell 7, launched
    by a limited interactive scheduled task. Relative SendInput motion passes through pointer
    ballistics (MOUSEINPUT remarks on learn.microsoft.com), so a scripted
    push of N mickeys moves N pixels only when speed is 10 and acceleration is off. A distance or
    velocity assertion in a test relies on that mapping.

.EXAMPLE
    pwsh -NoProfile -File C:\PowerToysUiTestRun\Multimon\Set-PointerBaseline.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;

public static class PointerSettings
{
    const uint SPI_GETMOUSE = 0x0003;
    const uint SPI_SETMOUSE = 0x0004;
    const uint SPI_GETMOUSESPEED = 0x0070;
    const uint SPI_SETMOUSESPEED = 0x0071;
    const uint SPIF_UPDATEINIFILE_SENDCHANGE = 0x0001 | 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SystemParametersInfo(uint action, uint param, int[] values, uint winIni);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SystemParametersInfo(uint action, uint param, ref int value, uint winIni);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SystemParametersInfo(uint action, uint param, System.IntPtr value, uint winIni);

    public static void Apply()
    {
        if (!SystemParametersInfo(SPI_SETMOUSE, 0, new[] { 0, 0, 0 }, SPIF_UPDATEINIFILE_SENDCHANGE))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        if (!SystemParametersInfo(SPI_SETMOUSESPEED, 0, new System.IntPtr(10), SPIF_UPDATEINIFILE_SENDCHANGE))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    public static int[] ReadAcceleration()
    {
        var values = new int[3];
        SystemParametersInfo(SPI_GETMOUSE, 0, values, 0);
        return values;
    }

    public static int ReadSpeed()
    {
        int speed = 0;
        SystemParametersInfo(SPI_GETMOUSESPEED, 0, ref speed, 0);
        return speed;
    }
}
'@

[PointerSettings]::Apply()
[pscustomobject]@{
    Acceleration = [PointerSettings]::ReadAcceleration() -join ','
    Speed        = [PointerSettings]::ReadSpeed()
}
