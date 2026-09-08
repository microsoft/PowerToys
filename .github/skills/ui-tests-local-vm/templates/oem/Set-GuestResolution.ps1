# Sets the interactive desktop resolution for the UI-test guest.
# Runs inside the guest as the standard user via an interactive scheduled task: display settings
# belong to the interactive session, so a PowerShell Direct session (session 0) cannot change them.

[CmdletBinding()]
param(
    [int]$Width = 1920,
    [int]$Height = 1080
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

public static class DisplayConfiguration
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);
}
'@

Add-Type -AssemblyName System.Windows.Forms
$before = $null
$result = -1
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
for ($attempt = 1; $attempt -le 10; $attempt++) {
    $mode = New-Object DisplayConfiguration+DEVMODE
    # Pass the instance: Marshal::SizeOf binds its object overload, and a Type argument is rejected.
    $mode.dmSize = [int16][Runtime.InteropServices.Marshal]::SizeOf($mode)

    # [NullString]::Value marshals as a real NULL; PowerShell would turn $null into an empty string,
    # and EnumDisplaySettings needs NULL to mean "the current display device".
    if ([DisplayConfiguration]::EnumDisplaySettings([NullString]::Value, -1, [ref]$mode) -eq 0) {
        throw 'EnumDisplaySettings failed.'
    }

    if ($null -eq $before) {
        $before = "$($mode.dmPelsWidth)x$($mode.dmPelsHeight)"
    }
    $mode.dmPelsWidth = $Width
    $mode.dmPelsHeight = $Height
    # DM_PELSWIDTH | DM_PELSHEIGHT
    $mode.dmFields = 0x00080000 -bor 0x00100000

    # CDS_UPDATEREGISTRY makes the change persist for later logons. The synthetic display can reject
    # the first request while it is still initializing immediately after logon, so retry boundedly.
    $result = [DisplayConfiguration]::ChangeDisplaySettings([ref]$mode, 0x00000001)
    Start-Sleep -Milliseconds 500
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    if ($result -eq 0 -and $bounds.Width -eq $Width -and $bounds.Height -eq $Height) {
        break
    }

    Start-Sleep -Seconds 1
}

[ordered]@{
    Before = $before
    Requested = "${Width}x${Height}"
    ChangeDisplaySettingsResult = $result
    After = "$($bounds.Width)x$($bounds.Height)"
    User = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    SessionId = (Get-Process -Id $PID).SessionId
} | ConvertTo-Json | Set-Content C:\PowerToysUiTestRun\set-resolution.json -Encoding utf8
