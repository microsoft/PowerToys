# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Arranges the guest's virtual monitors into a named layout and paints each monitor a solid color.

.DESCRIPTION
Runs inside the guest in the standard user's interactive session, under PowerShell 7, launched by a
limited interactive scheduled task. Display settings belong to the interactive session, so a
PowerShell Direct session cannot apply them.

Layout uses the documented multi-monitor ChangeDisplaySettingsEx sequence: every change is first
checked with CDS_TEST, then written with CDS_UPDATEREGISTRY | CDS_NORESET, and one final call with
no device applies them together. If a write or the apply fails, the script writes back and applies
the layout it started from, so no pending mode stays in the registry. A monitor is detached with a
0x0 mode. The Hyper-V synthetic
display stays primary at (0,0), because VMConnect and Get-VmConsoleImage.ps1 show only that display.

Per-monitor scale has no documented API. The script uses the DisplayConfigSetDeviceInfo request the
Settings app uses (type -4), reads the result back with GetDpiForMonitor, and fails when Windows
applied a different scale. It also fails when the attached monitors differ from the ones the preset
attaches, or when the primary is not at (0,0) at 1920x1080. Windows may move a non-primary monitor
to close a gap, so only the primary's position and size are checked.

The presets target the three monitors of the unmodified Windows-driver-samples IndirectDisplay
driver. They are told apart by the EDID manufacturer in the monitor's device interface path: the
sample's 2560x1440 monitor reports DEL, its 3840x2160 monitor reports LEN, and the third has no EDID
and offers 1920x1080.

Every position and size is in physical pixels, so the script's thread must be per-monitor DPI aware.
The script sets that on its thread with Enable-PerMonitorDpiAwareness.ps1, because a pwsh process can
start with its awareness already fixed (see references\multimon.md), and fails when Windows refuses
it.

.PARAMETER Preset
SideBySide: one 1920x1080 monitor left and one right of the primary.
Stacked: one above the primary, one below.
Offset: right neighbor 540 px lower, left neighbor 300 px higher, so the shared edges are partial.
MixedDpi: a 2560x1440 monitor at 150% to the right and a 3840x2160 monitor at 200% above.
Single: every virtual monitor detached, so the primary is the only monitor.

.PARAMETER WorkRoot
Guest folder for generated wallpapers.

.PARAMETER WallpaperPath
Optional guest folder of images, used in file-name order. When it is empty, the seven 75% color-bar
colors (gray, yellow, cyan, green, magenta, red, blue) are generated and picked per monitor count so
neighboring monitors contrast.

.PARAMETER AdapterPattern
Regular expression matching the virtual adapter's device string.

.PARAMETER QhdMonitorPattern
Regular expression matching the device interface path of the 2560x1440-capable monitor.

.PARAMETER UhdMonitorPattern
Regular expression matching the device interface path of the 3840x2160-capable monitor.

.PARAMETER ReportPath
Optional JSON file that receives what was applied and the scale Windows reports for each monitor.

.EXAMPLE
pwsh -NoProfile -File C:\PowerToysUiTestRun\Multimon\Set-MonitorLayout.ps1 -Preset SideBySide -ReportPath C:\PowerToysUiTestRun\Multimon\out\layout-applied.json
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('SideBySide', 'Stacked', 'Offset', 'MixedDpi', 'Single')]
    [string]$Preset,
    [ValidateNotNullOrEmpty()]
    [string]$WorkRoot = 'C:\PowerToysUiTestRun\Multimon',
    [string]$WallpaperPath = '',
    [ValidateNotNullOrEmpty()]
    [string]$AdapterPattern = 'IddSampleDriver',
    [ValidateNotNullOrEmpty()]
    [string]$QhdMonitorPattern = '#DEL[0-9A-F]{4}#',
    [ValidateNotNullOrEmpty()]
    [string]$UhdMonitorPattern = '#LEN[0-9A-F]{4}#',
    [string]$ReportPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PowerToysUiTestVm
{
    public sealed class DisplaySource
    {
        public string DeviceName = "";
        public string AdapterString = "";
        public bool Attached;
        public bool Primary;
        public string MonitorId = "";
        public string MonitorString = "";
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static class MonitorLayout
    {
        public const int CDS_UPDATEREGISTRY = 0x00000001;
        public const int CDS_TEST = 0x00000002;
        public const int CDS_NORESET = 0x10000000;

        private const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;
        private const int DISPLAY_DEVICE_PRIMARY_DEVICE = 0x4;
        private const int DISPLAY_DEVICE_MIRRORING_DRIVER = 0x8;
        private const uint EDD_GET_DEVICE_INTERFACE_NAME = 0x1;
        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int ENUM_REGISTRY_SETTINGS = -2;
        private const int DM_POSITION = 0x00000020;
        private const int DM_PELSWIDTH = 0x00080000;
        private const int DM_PELSHEIGHT = 0x00100000;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const uint QDC_ONLY_ACTIVE_PATHS = 0x2;
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;

        // Undocumented request types the Settings app uses for per-source scale.
        private const int DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE = -3;
        private const int DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE = -4;

        // The scale steps Settings offers; the scale requests index into this list relative to the
        // recommended step.
        private static readonly int[] ScaleSteps = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
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

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_RATIONAL
        {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public int outputTechnology;
            public int rotation;
            public int scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate;
            public int scanLineOrdering;
            public int targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO
        {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
        {
            public int type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_SOURCE_DPI_SCALE_GET
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public int minScaleRel;
            public int curScaleRel;
            public int maxScaleRel;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_SOURCE_DPI_SCALE_SET
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public int scaleRel;
        }

        // DISPLAYCONFIG_MODE_INFO is read by nobody here; QueryDisplayConfig still needs room for it.
        private const int DisplayConfigModeInfoSize = 64;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplayDevicesW(string device, uint index, ref DISPLAY_DEVICE displayDevice, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettingsW(string device, int modeNumber, ref DEVMODE devMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsExW(string device, ref DEVMODE devMode, IntPtr hwnd, int flags, IntPtr parameter);

        [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW")]
        private static extern int ChangeDisplaySettingsExApply(IntPtr device, IntPtr devMode, IntPtr hwnd, int flags, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT point, uint flags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(
            uint flags, ref uint pathCount, [Out] DISPLAYCONFIG_PATH_INFO[] paths, ref uint modeCount, IntPtr modes,
            IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME request);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DPI_SCALE_GET request);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigSetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DPI_SCALE_SET request);

        public static List<DisplaySource> GetSources()
        {
            List<DisplaySource> sources = new List<DisplaySource>();
            for (uint index = 0; ; index++)
            {
                DISPLAY_DEVICE adapter = NewDisplayDevice();
                if (!EnumDisplayDevicesW(null, index, ref adapter, 0))
                {
                    break;
                }

                if ((adapter.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER) != 0)
                {
                    continue;
                }

                DisplaySource source = new DisplaySource();
                source.DeviceName = adapter.DeviceName ?? "";
                source.AdapterString = adapter.DeviceString ?? "";
                source.Attached = (adapter.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0;
                source.Primary = (adapter.StateFlags & DISPLAY_DEVICE_PRIMARY_DEVICE) != 0;

                DISPLAY_DEVICE monitor = NewDisplayDevice();
                if (EnumDisplayDevicesW(adapter.DeviceName, 0, ref monitor, EDD_GET_DEVICE_INTERFACE_NAME))
                {
                    source.MonitorId = monitor.DeviceID ?? "";
                    source.MonitorString = monitor.DeviceString ?? "";
                }

                DEVMODE mode = NewDevMode();
                if (source.Attached && EnumDisplaySettingsW(adapter.DeviceName, ENUM_CURRENT_SETTINGS, ref mode))
                {
                    source.X = mode.dmPositionX;
                    source.Y = mode.dmPositionY;
                    source.Width = mode.dmPelsWidth;
                    source.Height = mode.dmPelsHeight;
                }

                sources.Add(source);
            }

            return sources;
        }

        // Writes or tests one source's position and size. A 0x0 size detaches the source. Returns the
        // DISP_CHANGE code.
        public static int Change(string device, int x, int y, int width, int height, int flags)
        {
            DEVMODE mode = NewDevMode();
            if (!EnumDisplaySettingsW(device, ENUM_CURRENT_SETTINGS, ref mode) &&
                !EnumDisplaySettingsW(device, ENUM_REGISTRY_SETTINGS, ref mode))
            {
                mode = NewDevMode();
            }

            mode.dmPositionX = x;
            mode.dmPositionY = y;
            mode.dmPelsWidth = width;
            mode.dmPelsHeight = height;
            mode.dmFields = DM_POSITION | DM_PELSWIDTH | DM_PELSHEIGHT;
            return ChangeDisplaySettingsExW(device, ref mode, IntPtr.Zero, flags, IntPtr.Zero);
        }

        // Applies every change written with CDS_NORESET in one mode set.
        public static int ApplyPending()
        {
            return ChangeDisplaySettingsExApply(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
        }

        public static uint GetEffectiveDpi(int x, int y)
        {
            POINT point = new POINT();
            point.X = x;
            point.Y = y;
            IntPtr monitor = MonitorFromPoint(point, 0);
            if (monitor == IntPtr.Zero)
            {
                return 0;
            }

            uint dpiX;
            uint dpiY;
            int result = GetDpiForMonitor(monitor, 0, out dpiX, out dpiY);
            if (result != 0)
            {
                Marshal.ThrowExceptionForHR(result);
            }

            return dpiX;
        }

        public static void SetScale(string device, int percent)
        {
            int targetIndex = Array.IndexOf(ScaleSteps, percent);
            if (targetIndex < 0)
            {
                throw new ArgumentException(percent + "% is not a Windows scale step.", "percent");
            }

            DISPLAYCONFIG_PATH_SOURCE_INFO source = FindActiveSource(device);
            DISPLAYCONFIG_SOURCE_DPI_SCALE_GET current = new DISPLAYCONFIG_SOURCE_DPI_SCALE_GET();
            current.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE;
            current.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DPI_SCALE_GET));
            current.header.adapterId = source.adapterId;
            current.header.id = source.id;
            int result = DisplayConfigGetDeviceInfo(ref current);
            if (result != 0)
            {
                throw new Win32Exception(result, "Reading the scale of " + device + " failed");
            }

            // The smallest step is 100%, so the distance from the minimum to zero is the recommended index.
            int recommendedIndex = -current.minScaleRel;
            int relative = targetIndex - recommendedIndex;
            if (relative < current.minScaleRel || relative > current.maxScaleRel)
            {
                throw new InvalidOperationException(string.Format(
                    "{0} accepts {1}% to {2}%, not {3}%.",
                    device,
                    ScaleSteps[Math.Max(0, recommendedIndex + current.minScaleRel)],
                    ScaleSteps[Math.Min(ScaleSteps.Length - 1, recommendedIndex + current.maxScaleRel)],
                    percent));
            }

            if (relative == current.curScaleRel)
            {
                return;
            }

            DISPLAYCONFIG_SOURCE_DPI_SCALE_SET request = new DISPLAYCONFIG_SOURCE_DPI_SCALE_SET();
            request.header.type = DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE;
            request.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DPI_SCALE_SET));
            request.header.adapterId = source.adapterId;
            request.header.id = source.id;
            request.scaleRel = relative;
            result = DisplayConfigSetDeviceInfo(ref request);
            if (result != 0)
            {
                throw new Win32Exception(result, "Setting " + percent + "% on " + device + " failed");
            }
        }

        private static DISPLAYCONFIG_PATH_SOURCE_INFO FindActiveSource(string device)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                uint pathCount;
                uint modeCount;
                int result = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount);
                if (result != 0)
                {
                    throw new Win32Exception(result, "GetDisplayConfigBufferSizes failed");
                }

                DISPLAYCONFIG_PATH_INFO[] paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                IntPtr modes = Marshal.AllocHGlobal((int)Math.Max(1, modeCount) * DisplayConfigModeInfoSize);
                try
                {
                    result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeHGlobal(modes);
                }

                if (result == ERROR_INSUFFICIENT_BUFFER)
                {
                    continue;
                }

                if (result != 0)
                {
                    throw new Win32Exception(result, "QueryDisplayConfig failed");
                }

                for (int index = 0; index < pathCount; index++)
                {
                    DISPLAYCONFIG_SOURCE_DEVICE_NAME name = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    name.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    name.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME));
                    name.header.adapterId = paths[index].sourceInfo.adapterId;
                    name.header.id = paths[index].sourceInfo.id;
                    if (DisplayConfigGetDeviceInfo(ref name) == 0 &&
                        string.Equals(name.viewGdiDeviceName, device, StringComparison.OrdinalIgnoreCase))
                    {
                        return paths[index].sourceInfo;
                    }
                }

                throw new InvalidOperationException("No active display path drives " + device + ".");
            }

            throw new InvalidOperationException("The display configuration kept changing while it was read.");
        }

        private static DISPLAY_DEVICE NewDisplayDevice()
        {
            DISPLAY_DEVICE device = new DISPLAY_DEVICE();
            device.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE));
            return device;
        }

        private static DEVMODE NewDevMode()
        {
            DEVMODE mode = new DEVMODE();
            mode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            return mode;
        }
    }

    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorId);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(int position);
        int GetPosition();
    }

    [ComImport, Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    public class DesktopWallpaperClass
    {
    }

    public sealed class WallpaperMonitor
    {
        public string MonitorId;
        public RECT Rect;
    }

    // PowerShell reaches COM only through IDispatch, and IDesktopWallpaper is IUnknown-only, so every
    // call on it happens here.
    public static class DesktopWallpaper
    {
        public static WallpaperMonitor[] GetMonitors()
        {
            IDesktopWallpaper wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
            uint count = wallpaper.GetMonitorDevicePathCount();
            WallpaperMonitor[] monitors = new WallpaperMonitor[count];
            for (uint index = 0; index < count; index++)
            {
                string monitorId = wallpaper.GetMonitorDevicePathAt(index);
                RECT rect = new RECT();
                try
                {
                    rect = wallpaper.GetMonitorRECT(monitorId);
                }
                catch (COMException)
                {
                    // A monitor that is listed but detached has no rectangle; the empty one marks it.
                }
                monitors[index] = new WallpaperMonitor { MonitorId = monitorId, Rect = rect };
            }
            return monitors;
        }

        public static void Apply(string[] monitorIds, string[] imagePaths, int position)
        {
            IDesktopWallpaper wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
            for (int index = 0; index < monitorIds.Length; index++)
            {
                wallpaper.SetWallpaper(monitorIds[index], imagePaths[index]);
            }
            wallpaper.SetPosition(position);
        }
    }
}
'@

$dispChangeNames = @{
    0  = 'DISP_CHANGE_SUCCESSFUL'
    1  = 'DISP_CHANGE_RESTART'
    -1 = 'DISP_CHANGE_FAILED'
    -2 = 'DISP_CHANGE_BADMODE'
    -3 = 'DISP_CHANGE_NOTUPDATED'
    -4 = 'DISP_CHANGE_BADFLAGS'
    -5 = 'DISP_CHANGE_BADPARAM'
    -6 = 'DISP_CHANGE_BADDUALVIEW'
}

function Format-Source {
    param([Parameter(Mandatory)][object[]]$Source)

    ($Source | ForEach-Object {
            "$($_.DeviceName) adapter='$($_.AdapterString)' attached=$($_.Attached) monitor='$($_.MonitorId)'"
        }) -join '; '
}

function Invoke-DisplayChange {
    param(
        [Parameter(Mandatory)][string]$Device,
        [Parameter(Mandatory)][int[]]$Bounds,
        [Parameter(Mandatory)][int]$Flags,
        [Parameter(Mandatory)][string]$Purpose
    )

    $result = [PowerToysUiTestVm.MonitorLayout]::Change($Device, $Bounds[0], $Bounds[1], $Bounds[2], $Bounds[3], $Flags)
    if ($result -ne 0) {
        throw "$Purpose for $Device at $($Bounds -join ',') returned $($dispChangeNames[$result] ?? $result)."
    }
}

function Export-SolidWallpaperSet {
    param(
        [Parameter(Mandatory)][string]$Folder
    )

    # The seven 75% color bars in their standard left-to-right order of descending luminance: white
    # (75%, so gray), yellow, cyan, green, magenta, red, blue. ITU-R BT.471-1 (1986), Fig. 1, sets
    # each primary of a bar either to 75% of white or to the minimum. The minimum here is 0, the
    # 75/0/75/0 form, because full-range RGB has no 7.5% analog setup. 75% of 8-bit full range is
    # 0.75 x 255 = 191.25, stored as 191.
    $bars = @(
        @{ Name = 'Gray'; R = 191; G = 191; B = 191 }
        @{ Name = 'Yellow'; R = 191; G = 191; B = 0 }
        @{ Name = 'Cyan'; R = 0; G = 191; B = 191 }
        @{ Name = 'Green'; R = 0; G = 191; B = 0 }
        @{ Name = 'Magenta'; R = 191; G = 0; B = 191 }
        @{ Name = 'Red'; R = 191; G = 0; B = 0 }
        @{ Name = 'Blue'; R = 0; G = 0; B = 191 }
    )
    New-Item -ItemType Directory -Path $Folder -Force | Out-Null
    # The folder holds only generated images, and a stale one would join the file-name order.
    Get-ChildItem -LiteralPath $Folder -Filter '*.png' -File | Remove-Item -Force
    for ($index = 0; $index -lt $bars.Count; $index++) {
        $bar = $bars[$index]
        $path = Join-Path $Folder ('{0:D2}_{1}.png' -f ($index + 1), $bar.Name)
        $bitmap = [System.Drawing.Bitmap]::new(64, 64)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.Clear([System.Drawing.Color]::FromArgb(255, $bar.R, $bar.G, $bar.B))
            }
            finally {
                $graphics.Dispose()
            }
            $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $bitmap.Dispose()
        }
    }
}

# Bar indexes (Export-SolidWallpaperSet order) per monitor count, in left-then-top monitor order:
# for each count, the order maximizing the smallest CIEDE2000 distance between neighbors, found offline.
$barsByMonitorCount = @(
    , @(0)
    , @(3, 4)
    , @(1, 4, 3)
    , @(3, 4, 1, 6)
    , @(6, 1, 4, 3, 5)
    , @(2, 5, 3, 4, 1, 6)
    , @(0, 6, 1, 4, 3, 5, 2)
)

$awareness = & (Join-Path $PSScriptRoot 'Enable-PerMonitorDpiAwareness.ps1')

# A detached source can lose its monitor, which leaves nothing to identify it by. Attaching every
# virtual source first, in a row right of the attached desktop, makes every monitor identifiable.
$sources = @([PowerToysUiTestVm.MonitorLayout]::GetSources())
$primary = $sources | Where-Object { $_.Primary } | Select-Object -First 1
$virtual = @($sources | Where-Object { $_.AdapterString -match $AdapterPattern })
$startingSources = $sources
if ($null -eq $primary -or $primary.AdapterString -match $AdapterPattern) {
    throw "The primary display must be the synthetic display, not a virtual one. Sources: $(Format-Source -Source $sources)"
}
# One role per monitor of the unmodified sample.
$roleNames = @('Qhd', 'Uhd', 'Fhd')
if ($virtual.Count -lt $roleNames.Count) {
    throw "Expected $($roleNames.Count) sources matching '$AdapterPattern'; found $($virtual.Count). Sources: $(Format-Source -Source $sources)"
}

$writeFlags = [PowerToysUiTestVm.MonitorLayout]::CDS_UPDATEREGISTRY -bor [PowerToysUiTestVm.MonitorLayout]::CDS_NORESET

# Writes every source this script may change back to its state before the first write and applies
# it, so a failure leaves no pending registry mode behind. Returns a sentence for the error message.
function Restore-StartingLayout {
    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($source in $startingSources | Where-Object { $_.Primary -or $_.AdapterString -match $AdapterPattern }) {
        $bounds = $source.Attached ? @($source.X, $source.Y, $source.Width, $source.Height) : @(0, 0, 0, 0)
        $result = [PowerToysUiTestVm.MonitorLayout]::Change($source.DeviceName, $bounds[0], $bounds[1], $bounds[2], $bounds[3], $writeFlags)
        if ($result -ne 0) {
            $failures.Add("writing $($source.DeviceName) returned $($dispChangeNames[$result] ?? $result)")
        }
    }
    $applied = [PowerToysUiTestVm.MonitorLayout]::ApplyPending()
    if ($applied -ne 0) {
        $failures.Add("applying returned $($dispChangeNames[$applied] ?? $applied)")
    }
    if ($failures.Count -eq 0) {
        return 'The starting layout was restored.'
    }
    "Restoring the starting layout also failed: $($failures -join '; ')."
}

$writesStarted = $false
try {
    $detached = @($virtual | Where-Object { -not $_.Attached })
    if ($detached.Count -gt 0) {
        $rightEdges = $sources | Where-Object { $_.Attached } | ForEach-Object { $_.X + $_.Width }
        $nextX = [int]($rightEdges | Measure-Object -Maximum).Maximum
        $writesStarted = $true
        foreach ($source in $detached) {
            Invoke-DisplayChange -Device $source.DeviceName -Bounds @($nextX, 0, 1920, 1080) `
                -Flags $writeFlags -Purpose 'Attaching'
            $nextX += 1920
        }
        $applied = [PowerToysUiTestVm.MonitorLayout]::ApplyPending()
        if ($applied -ne 0) {
            throw "Attaching the virtual sources returned $($dispChangeNames[$applied] ?? $applied)."
        }
        Start-Sleep -Seconds 2
        $sources = @([PowerToysUiTestVm.MonitorLayout]::GetSources())
        $virtual = @($sources | Where-Object { $_.AdapterString -match $AdapterPattern })
    }

    $roles = @{
        Qhd = @($virtual | Where-Object { $_.MonitorId -match $QhdMonitorPattern })
        Uhd = @($virtual | Where-Object { $_.MonitorId -match $UhdMonitorPattern })
        Fhd = @($virtual | Where-Object {
                $_.MonitorId -ne '' -and $_.MonitorId -notmatch $QhdMonitorPattern -and $_.MonitorId -notmatch $UhdMonitorPattern
            })
    }
    foreach ($role in @($roles.Keys)) {
        if ($roles[$role].Count -ne 1) {
            throw "Monitor role $role matched $($roles[$role].Count) sources instead of 1. Sources: $(Format-Source -Source $virtual)"
        }
        $roles[$role] = $roles[$role][0]
    }

    # Each entry is X, Y, width, height, scale percent. $null detaches the monitor.
    $plan = switch ($Preset) {
        'SideBySide' { @{ Qhd = @(1920, 0, 1920, 1080, 100); Fhd = @(-1920, 0, 1920, 1080, 100); Uhd = $null } }
        'Stacked' { @{ Qhd = @(0, -1080, 1920, 1080, 100); Fhd = @(0, 1080, 1920, 1080, 100); Uhd = $null } }
        'Offset' { @{ Qhd = @(1920, 540, 1920, 1080, 100); Fhd = @(-1920, -300, 1920, 1080, 100); Uhd = $null } }
        'MixedDpi' { @{ Qhd = @(1920, 0, 2560, 1440, 150); Uhd = @(0, -2160, 3840, 2160, 200); Fhd = $null } }
        'Single' { @{ Qhd = $null; Uhd = $null; Fhd = $null } }
    }

    $changes = [System.Collections.Generic.List[object]]::new()
    $changes.Add([pscustomobject]@{ Role = 'Primary'; Device = $primary.DeviceName; Bounds = @(0, 0, 1920, 1080); Scale = 100 })
    foreach ($role in $roleNames) {
        $entry = $plan[$role]
        $changes.Add([pscustomobject]@{
                Role   = $role
                Device = $roles[$role].DeviceName
                Bounds = ($null -eq $entry) ? @(0, 0, 0, 0) : @($entry[0], $entry[1], $entry[2], $entry[3])
                Scale  = ($null -eq $entry) ? $null : $entry[4]
            })
    }

    # A detach entry is tested too, so no write starts before every entry passed.
    foreach ($change in $changes) {
        $testFlags = [PowerToysUiTestVm.MonitorLayout]::CDS_TEST
        Invoke-DisplayChange -Device $change.Device -Bounds $change.Bounds -Flags $testFlags -Purpose "Testing $($change.Role)"
    }
    $writesStarted = $true
    foreach ($change in $changes) {
        Invoke-DisplayChange -Device $change.Device -Bounds $change.Bounds -Flags $writeFlags -Purpose "Writing $($change.Role)"
    }
    $applyResult = [PowerToysUiTestVm.MonitorLayout]::ApplyPending()
    if ($applyResult -ne 0) {
        throw "Applying the $Preset layout returned $($dispChangeNames[$applyResult] ?? $applyResult)."
    }
}
catch {
    $layoutError = $_.Exception.Message
    $restoreState = ''
    if ($writesStarted) {
        try {
            $restoreState = Restore-StartingLayout
        }
        catch {
            $restoreState = "Restoring the starting layout also failed: $($_.Exception.Message)"
        }
    }
    throw "$layoutError $restoreState"
}
Start-Sleep -Seconds 2

foreach ($change in $changes | Where-Object { $null -ne $_.Scale }) {
    [PowerToysUiTestVm.MonitorLayout]::SetScale($change.Device, $change.Scale)
}

# The scale change lands asynchronously, so the read-back retries until it matches or times out.
$deadline = (Get-Date).AddSeconds(10)
$readBack = @()
do {
    Start-Sleep -Milliseconds 500
    $current = @([PowerToysUiTestVm.MonitorLayout]::GetSources())
    $readBack = @($changes | Where-Object { $null -ne $_.Scale } | ForEach-Object {
            $change = $_
            $source = $current | Where-Object { $_.DeviceName -eq $change.Device } | Select-Object -First 1
            $dpi = 0
            if ($null -ne $source -and $source.Attached) {
                $centerX = $source.X + [int]($source.Width / 2)
                $centerY = $source.Y + [int]($source.Height / 2)
                $dpi = [PowerToysUiTestVm.MonitorLayout]::GetEffectiveDpi($centerX, $centerY)
            }
            [pscustomobject]@{
                Role          = $change.Role
                Device        = $change.Device
                Bounds        = ($null -ne $source) ? "$($source.X),$($source.Y) $($source.Width)x$($source.Height)" : ''
                RequestedScale = $change.Scale
                AppliedScale  = [int][Math]::Round($dpi / 96.0 * 100)
            }
        })
    $matched = @($readBack | Where-Object { $_.AppliedScale -ne $_.RequestedScale }).Count -eq 0
} while (-not $matched -and (Get-Date) -lt $deadline)

if (-not $matched) {
    throw "Windows applied a different scale than requested: $(($readBack | ForEach-Object { "$($_.Role) $($_.Device) requested $($_.RequestedScale)% got $($_.AppliedScale)%" }) -join '; ')"
}

# Windows may move a non-primary monitor to close a gap, so only the attached set and the primary's
# bounds are compared with the plan.
$plannedAttached = @($changes | Where-Object { $_.Bounds[2] -gt 0 } | ForEach-Object { $_.Device } | Sort-Object)
$attached = @($current | Where-Object { $_.Attached } | ForEach-Object { $_.DeviceName } | Sort-Object)
if (($attached -join ' ') -ne ($plannedAttached -join ' ')) {
    throw "The $Preset layout attaches $($plannedAttached -join ', '), but Windows reports $($attached -join ', ') attached. Sources: $(Format-Source -Source $current)"
}
$currentPrimary = $current | Where-Object { $_.Primary } | Select-Object -First 1
$primaryBounds = ($null -ne $currentPrimary) ? "$($currentPrimary.DeviceName) at $($currentPrimary.X),$($currentPrimary.Y) $($currentPrimary.Width)x$($currentPrimary.Height)" : 'none'
if ($primaryBounds -ne "$($primary.DeviceName) at 0,0 1920x1080") {
    throw "The primary display is $primaryBounds, not $($primary.DeviceName) at 0,0 1920x1080."
}

$generated = [string]::IsNullOrWhiteSpace($WallpaperPath)
$wallpaperFolder = $WallpaperPath
if ($generated) {
    $wallpaperFolder = Join-Path $WorkRoot 'wallpapers'
    Export-SolidWallpaperSet -Folder $wallpaperFolder
}
$colors = @(Get-ChildItem -LiteralPath $wallpaperFolder -File |
        Where-Object { $_.Extension -in '.png', '.jpg', '.jpeg', '.bmp' } |
        Sort-Object -Property Name)
if ($colors.Count -eq 0) {
    throw "No images in $wallpaperFolder."
}

$painted = [System.Collections.Generic.List[object]]::new()
foreach ($monitor in [PowerToysUiTestVm.DesktopWallpaper]::GetMonitors()) {
    $rect = $monitor.Rect
    # A detached monitor stays in the list with an empty rectangle.
    if ($rect.Right -gt $rect.Left -and $rect.Bottom -gt $rect.Top) {
        $painted.Add([pscustomobject]@{ MonitorId = $monitor.MonitorId; Left = $rect.Left; Top = $rect.Top; Wallpaper = '' })
    }
}
$ordered = @($painted | Sort-Object -Property Left, Top)

# Generated bars follow the per-count lookup; a folder of the caller's own images keeps file-name order.
$assignment = @(if ($generated -and $ordered.Count -le $barsByMonitorCount.Count) {
    $barsByMonitorCount[$ordered.Count - 1]
}
else {
    @(for ($index = 0; $index -lt $ordered.Count; $index++) { $index % $colors.Count })
})
$imagePaths = @(for ($index = 0; $index -lt $ordered.Count; $index++) {
        $color = $colors[$assignment[$index]]
        $ordered[$index].Wallpaper = $color.BaseName
        $color.FullName
    })
# 4 is DWPOS_FILL.
[PowerToysUiTestVm.DesktopWallpaper]::Apply([string[]]@($ordered.MonitorId), $imagePaths, 4)

$result = [pscustomobject]@{
    Preset        = $Preset
    DpiAwareness  = $awareness
    Monitors      = $readBack
    Wallpapers    = @($ordered | ForEach-Object { "$($_.Left),$($_.Top) $($_.Wallpaper)" })
}
if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    New-Item -ItemType Directory -Path (Split-Path -Path $ReportPath -Parent) -Force | Out-Null
    $result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding utf8
}
$result
