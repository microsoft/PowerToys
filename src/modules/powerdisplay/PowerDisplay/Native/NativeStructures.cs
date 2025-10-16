// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PowerDisplay.Native
{
    /// <summary>
    /// Physical monitor structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PhysicalMonitor
    {
        /// <summary>
        /// Physical monitor handle
        /// </summary>
        public IntPtr HPhysicalMonitor;

        /// <summary>
        /// Physical monitor description (128 characters)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string SzPhysicalMonitorDescription;
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
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MonitorInfoEx
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
        /// Device name (e.g., "\\.\DISPLAY1")
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string SzDevice;
    }

    /// <summary>
    /// Display device structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DisplayDevice
    {
        /// <summary>
        /// Structure size
        /// </summary>
        public uint Cb;

        /// <summary>
        /// Device name (e.g., "\\.\DISPLAY1\Monitor0")
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        /// <summary>
        /// Device description string
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        /// <summary>
        /// Status flags
        /// </summary>
        public uint StateFlags;

        /// <summary>
        /// Device ID
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        /// <summary>
        /// Registry device key
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
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
        public DISPLAYCONFIG_TARGET_MODE targetMode;

        [FieldOffset(0)]
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
    /// Display configuration target device name
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string MonitorFriendlyDeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string MonitorDevicePath;
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
}