// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

#pragma warning disable SA1649 // File name should match first type name - Multiple related P/Invoke structures

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Physical monitor structure for DDC/CI
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct PhysicalMonitor
    {
        /// <summary>
        /// Physical monitor handle
        /// </summary>
        public IntPtr HPhysicalMonitor;

        /// <summary>
        /// Physical monitor description string - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort SzPhysicalMonitorDescription[128];

        /// <summary>
        /// Helper method to get description as string
        /// </summary>
        public readonly string GetDescription()
        {
            fixed (ushort* ptr = SzPhysicalMonitorDescription)
            {
                return new string((char*)ptr);
            }
        }
    }

    /// <summary>
    /// Rectangle structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;

        public int Height => Bottom - Top;

        public Rect(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
    }

    /// <summary>
    /// Monitor information extended structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct MonitorInfoEx
    {
        /// <summary>
        /// Structure size
        /// </summary>
        public uint CbSize;

        /// <summary>
        /// Monitor rectangle area
        /// </summary>
        public Rect RcMonitor;

        /// <summary>
        /// Work area rectangle
        /// </summary>
        public Rect RcWork;

        /// <summary>
        /// Flags
        /// </summary>
        public uint DwFlags;

        /// <summary>
        /// Device name (e.g., "\\.\DISPLAY1") - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort SzDevice[32];

        /// <summary>
        /// Helper property to get device name as string
        /// </summary>
        public readonly string GetDeviceName()
        {
            fixed (ushort* ptr = SzDevice)
            {
                return new string((char*)ptr);
            }
        }
    }

    /// <summary>
    /// Display device structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct DisplayDevice
    {
        /// <summary>
        /// Structure size
        /// </summary>
        public uint Cb;

        /// <summary>
        /// Device name (e.g., "\\.\DISPLAY1\Monitor0") - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort DeviceName[32];

        /// <summary>
        /// Device description string - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort DeviceString[128];

        /// <summary>
        /// Status flags
        /// </summary>
        public uint StateFlags;

        /// <summary>
        /// Device ID - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort DeviceID[128];

        /// <summary>
        /// Registry device key - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort DeviceKey[128];

        /// <summary>
        /// Helper method to get device name as string
        /// </summary>
        public readonly string GetDeviceName()
        {
            fixed (ushort* ptr = DeviceName)
            {
                return new string((char*)ptr);
            }
        }
    }

    /// <summary>
    /// LUID (Locally Unique Identifier) structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Luid
    {
        public uint LowPart;
        public int HighPart;

        public override string ToString()
        {
            return $"{HighPart:X8}:{LowPart:X8}";
        }
    }

    /// <summary>
    /// Display configuration path information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO SourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO TargetInfo;
        public uint Flags;
    }

    /// <summary>
    /// Display configuration path source information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    /// <summary>
    /// Display configuration path target information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public DISPLAYCONFIG_RATIONAL RefreshRate;
        public uint ScanLineOrdering;
        public bool TargetAvailable;
        public uint StatusFlags;
    }

    /// <summary>
    /// Display configuration rational number
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    /// <summary>
    /// Display configuration mode information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_MODE_INFO
    {
        public uint InfoType;
        public uint Id;
        public Luid AdapterId;
        public DISPLAYCONFIG_MODE_INFO_UNION ModeInfo;
    }

    /// <summary>
    /// Display configuration mode information union
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct DISPLAYCONFIG_MODE_INFO_UNION
    {
        [FieldOffset(0)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Native API structure field")]
        public DISPLAYCONFIG_TARGET_MODE targetMode;

        [FieldOffset(0)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1307:Accessible fields should begin with upper-case letter", Justification = "Native API structure field")]
        public DISPLAYCONFIG_SOURCE_MODE sourceMode;
    }

    /// <summary>
    /// Display configuration target mode
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_TARGET_MODE
    {
        public DISPLAYCONFIG_VIDEO_SIGNAL_INFO TargetVideoSignalInfo;
    }

    /// <summary>
    /// Display configuration source mode
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_SOURCE_MODE
    {
        public uint Width;
        public uint Height;
        public uint PixelFormat;
        public DISPLAYCONFIG_POINT Position;
    }

    /// <summary>
    /// Display configuration point
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// Display configuration video signal information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
    {
        public ulong PixelRate;
        public DISPLAYCONFIG_RATIONAL HSyncFreq;
        public DISPLAYCONFIG_RATIONAL VSyncFreq;
        public DISPLAYCONFIG_2DREGION ActiveSize;
        public DISPLAYCONFIG_2DREGION TotalSize;
        public uint VideoStandard;
        public uint ScanLineOrdering;
    }

    /// <summary>
    /// Display configuration 2D region
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_2DREGION
    {
        public uint Cx;
        public uint Cy;
    }

    /// <summary>
    /// Display configuration device information header
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    /// <summary>
    /// Display configuration source device name - contains GDI device name (e.g., "\\.\DISPLAY1")
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER Header;

        /// <summary>
        /// GDI device name - fixed buffer for 32 wide characters (CCHDEVICENAME)
        /// </summary>
        public fixed ushort ViewGdiDeviceName[32];

        /// <summary>
        /// Helper method to get GDI device name as string
        /// </summary>
        public readonly string GetViewGdiDeviceName()
        {
            fixed (ushort* ptr = ViewGdiDeviceName)
            {
                return new string((char*)ptr);
            }
        }
    }

    /// <summary>
    /// Display configuration target device name
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;

        /// <summary>
        /// Monitor friendly name - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort MonitorFriendlyDeviceName[64];

        /// <summary>
        /// Monitor device path - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort MonitorDevicePath[128];

        /// <summary>
        /// Helper method to get monitor friendly name as string
        /// </summary>
        public readonly string GetMonitorFriendlyDeviceName()
        {
            fixed (ushort* ptr = MonitorFriendlyDeviceName)
            {
                return new string((char*)ptr);
            }
        }

        /// <summary>
        /// Helper method to get monitor device path as string
        /// </summary>
        public readonly string GetMonitorDevicePath()
        {
            fixed (ushort* ptr = MonitorDevicePath)
            {
                return new string((char*)ptr);
            }
        }
    }

    /// <summary>
    /// Display configuration SDR white level
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_SDR_WHITE_LEVEL
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER Header;
        public uint SDRWhiteLevel;
    }

    /// <summary>
    /// Display configuration advanced color information
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER Header;
        public uint AdvancedColorSupported;
        public uint AdvancedColorEnabled;
        public uint BitsPerColorChannel;
        public uint ColorEncoding;
        public uint FormatSupport;
    }

    /// <summary>
    /// Custom structure for setting SDR white level
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DISPLAYCONFIG_SET_SDR_WHITE_LEVEL
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER Header;
        public uint SDRWhiteLevel;
        public byte FinalValue;
    }

    /// <summary>
    /// Point structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    /// <summary>
    /// The DEVMODE structure contains information about the initialization and environment of a printer or a display device.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public unsafe struct DevMode
    {
        /// <summary>
        /// Device name - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort DmDeviceName[32];

        public short DmSpecVersion;
        public short DmDriverVersion;
        public short DmSize;
        public short DmDriverExtra;
        public int DmFields;
        public int DmPositionX;
        public int DmPositionY;
        public int DmDisplayOrientation;
        public int DmDisplayFixedOutput;
        public short DmColor;
        public short DmDuplex;
        public short DmYResolution;
        public short DmTTOption;
        public short DmCollate;

        /// <summary>
        /// Form name - fixed buffer for LibraryImport compatibility
        /// </summary>
        public fixed ushort DmFormName[32];

        public short DmLogPixels;
        public int DmBitsPerPel;
        public int DmPelsWidth;
        public int DmPelsHeight;
        public int DmDisplayFlags;
        public int DmDisplayFrequency;
        public int DmICMMethod;
        public int DmICMIntent;
        public int DmMediaType;
        public int DmDitherType;
        public int DmReserved1;
        public int DmReserved2;
        public int DmPanningWidth;
        public int DmPanningHeight;
    }
}
