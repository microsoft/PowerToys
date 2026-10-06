// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Windows.Gaming.Input;
using Properties = MonitorPowerCore;

#pragma warning disable IL2026, IL3050

#pragma warning disable SA1300, SA1307, SA1310, SA1316, SA1649, CA1069, CA1305, CA1863

namespace MonitorPower;

internal enum DISPLAYCONFIG_TOPOLOGY_ID : uint
{
    Zero = 0,
    Internal = 1,
    Clone = 2,
    Extend = 4,
    External = 8,
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_PATH_INFO
{
    public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
    public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
    public uint flags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_PATH_SOURCE_INFO
{
    public LUID adapterId;
    public uint id;
    public uint modeInfoIdx;
    public uint statusFlags;

    // Virtual-mode-aware sub-fields of modeInfoIdx
    public readonly ushort cloneGroupId => (ushort)(modeInfoIdx & 0xFFFF);

    public readonly ushort sourceModeInfoIdx => (ushort)(modeInfoIdx >> 16);
}

[StructLayout(LayoutKind.Sequential)]
internal struct LUID
{
    public uint LowPart { get; set; }

    public int HighPart { get; set; }

    public override readonly string ToString() => $"{HighPart:X8}{LowPart:X8}";
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_PATH_TARGET_INFO
{
    public LUID adapterId;
    public uint id;
    public uint modeInfoIdx;
    public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
    public DISPLAYCONFIG_ROTATION rotation;
    public DISPLAYCONFIG_SCALING scaling;
    public DISPLAYCONFIG_RATIONAL refreshRate;
    public DISPLAYCONFIG_SCANLINE_ORDERING scanlineOrdering;
    public int targetAvailable;
    public uint statusFlags;

    // Virtual-mode-aware sub-fields of modeInfoIdx
    public readonly ushort desktopModeInfoIdx => (ushort)(modeInfoIdx & 0xFFFF);

    public readonly ushort targetModeInfoIdx => (ushort)(modeInfoIdx >> 16);
}

internal enum DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY : uint
{
    Other = unchecked((uint)-1),
    VGA = 0,
    SVideo = 1,
    CompositeVideo = 2,
    ComponentVideo = 3,
    DVI = 4,
    HDMI = 5,
    LVDS = 6,
    D_JPN = 8,
    SDI = 9,
    DisplayPortExternal = 10,
    DisplayPortEmbedded = 11,
    UDI = 12,
    DConnector = 13,
    IndirectWired = 14,
    IndirectVirtual = 15,
    Internal = 0x80000000,
}

internal enum DISPLAYCONFIG_ROTATION : uint
{
    Identity = 1,
    Rotate90 = 2,
    Rotate180 = 3,
    Rotate270 = 4,
}

internal enum DISPLAYCONFIG_SCALING : uint
{
    Identity = 1,
    Centered = 2,
    Stretched = 3,
    AspectRatioCenteredMax = 4,
    Custom = 5,
    Preferred = 128,
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_RATIONAL
{
    public uint Numerator;
    public uint Denominator;
}

internal enum DISPLAYCONFIG_SCANLINE_ORDERING : uint
{
    Unspecified = 0,
    Progressive = 1,
    Interlaced = 2,
    InterlacedUpperFieldFirst = Interlaced,
    InterlacedLowerFieldFirst = 3,
    ForceUInt32 = 0xFFFFFFFF,
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_MODE_INFO
{
    public DISPLAYCONFIG_MODE_INFO_TYPE infoType;
    public uint id;
    public LUID adapterId;
    public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;
}

[StructLayout(LayoutKind.Explicit)]
internal struct DISPLAYCONFIG_MODE_INFO_UNION
{
    [FieldOffset(0)]
    public DISPLAYCONFIG_TARGET_MODE targetMode;

    [FieldOffset(0)]
    public DISPLAYCONFIG_SOURCE_MODE sourceMode;

    [FieldOffset(0)]
    public DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
}

internal enum DISPLAYCONFIG_MODE_INFO_TYPE : uint
{
    Zero = 0,
    Source = 1,
    Target = 2,
    DesktopImage = 3,
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_TARGET_MODE
{
    public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
{
    public long pixelRate;
    public DISPLAYCONFIG_RATIONAL hSyncFreq;
    public DISPLAYCONFIG_RATIONAL vSyncFreq;
    public DISPLAYCONFIG_2DREGION activeSize;
    public DISPLAYCONFIG_2DREGION totalSize;
    public uint videoStandard;
    public DISPLAYCONFIG_SCANLINE_ORDERING scanlineOrdering;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_2DREGION
{
    public uint cx;
    public uint cy;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_SOURCE_MODE
{
    public uint width;
    public uint height;
    public int pixelFormat;
    public DISPLAYCONFIG_POINT position;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_POINT
{
    public int x;
    public int y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
{
    public DISPLAYCONFIG_POINT PathSourceSize;
    public DISPLAYCONFIG_RATIONAL DesktopImageRegion;
    public DISPLAYCONFIG_RATIONAL DesktopImageClip;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_DEVICE_INFO_HEADER
{
    public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
    public uint size;
    public LUID adapterId;
    public uint id;
}

internal enum DISPLAYCONFIG_DEVICE_INFO_TYPE : uint
{
    GetSourceName = 1,
    GetTargetName = 2,
    GetTargetPreferredMode = 3,
    GetAdapterName = 4,
    SetTargetPersistence = 5,
    GetTargetBaseType = 6,
    GetSupportVirtualResolution = 7,
    SetSupportVirtualResolution = 8,
    GetAdvancedColorInfo = 9,
    SetAdvancedColorState = 10,
    GetSdrWhiteLevel = 11,
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct DISPLAYCONFIG_TARGET_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS flags;
    public DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology;
    public ushort edidManufactureId;
    public ushort edidProductCodeId;
    public uint connectorInstance;
    public fixed ushort monitorFriendlyDeviceName[64];
    public fixed ushort monitorDevicePath[128];

    public readonly string GetMonitorFriendlyDeviceName()
    {
        fixed (ushort* ptr = monitorFriendlyDeviceName)
        {
            return new string((char*)ptr);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_TARGET_DEVICE_NAME_FLAGS
{
    public uint value;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
{
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public fixed ushort viewGdiDeviceName[32];

    public readonly string GetViewGdiDeviceName()
    {
        fixed (ushort* ptr = viewGdiDeviceName)
        {
            return new string((char*)ptr);
        }
    }
}

internal static partial class DisplayHelpers
{
    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const uint QDC_ALL_PATHS = 0x00000001;
    private const uint QDC_DATABASE_CURRENT = 0x00000004;
    private const uint QDC_VIRTUAL_MODE_AWARE = 0x00000010;
    private const uint SDC_APPLY = 0x00000080;
    private const uint SDC_SAVE_TO_DATABASE = 0x00000200;
    private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
    private const uint SDC_ALLOW_CHANGES = 0x00000400;
    private const uint SDC_VIRTUAL_MODE_AWARE = 0x00008000;
    private const uint DISPLAYCONFIG_PATH_ACTIVE = 0x00000001;

    private static readonly object StateLock = new();
    private static readonly ushort[] PreviousControllerButtons = new ushort[XinputMaxControllers];
    private static (DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes)? _savedState;
    private static KeyboardHook? _activeHook;
    private static KeyboardHook? _activationHook;
    private static Dictionary<DisplayTargetId, string>? _displayNameCache;
    private static DisplayTargetId[]? _cachedTargetOrder;
    private static Timer? _xboxPollTimer;
    private static ControllerChord _controllerChord = new(0x0420);
    private static bool _controllerChordWasPressed;
    private static int _controllerPollInProgress;
    private static int _slowControllerPollLogged;

    public static event Action? GuideViewComboPressed;

    public static event Action? ActivationShortcutPressed;

    public static event Action<ushort>? ControllerButtonsPressed;

    public static event Action<string>? ControllerInputUnavailable;

    public static event Action? DisplayProfileApplyStarted;

    public static event Action? DisplayProfileApplyCompleted;

    private const ushort XinputGamepadBack = 0x0020;
    private const ushort XinputGamepadGuide = 0x0400;
    private const int XinputMaxControllers = 4;
    private const ushort XinputGamepadDpadUp = 0x0001;
    private const ushort XinputGamepadDpadDown = 0x0002;

    internal const DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY INTERNAL_TECH = (DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY)0x80000000;

    internal readonly record struct DisplayTargetId(LUID AdapterId, uint TargetId) : IComparable<DisplayTargetId>
    {
        public int CompareTo(DisplayTargetId other)
        {
            var c = AdapterId.HighPart.CompareTo(other.AdapterId.HighPart);
            if (c != 0)
            {
                return c;
            }

            c = AdapterId.LowPart.CompareTo(other.AdapterId.LowPart);
            if (c != 0)
            {
                return c;
            }

            return TargetId.CompareTo(other.TargetId);
        }
    }

    [LibraryImport("user32.dll")]
    private static partial int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [LibraryImport("user32.dll")]
    private static unsafe partial int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        DISPLAYCONFIG_PATH_INFO* pathArray,
        ref uint numModeInfoArrayElements,
        DISPLAYCONFIG_MODE_INFO* modeInfoArray,
        nint currentTopologyId);

    [LibraryImport("user32.dll")]
    private static unsafe partial int SetDisplayConfig(
        uint numPathArrayElements,
        DISPLAYCONFIG_PATH_INFO* pathArray,
        uint numModeInfoArrayElements,
        DISPLAYCONFIG_MODE_INFO* modeInfoArray,
        uint flags);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    private static extern int DisplayConfigGetDeviceInfo_IntPtr(IntPtr requestPacket);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint dwFlags, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int EnumDisplaySettings(string lpszDeviceName, uint iModeNum, ref DEVMODE lpDevMode);

    private const uint CDS_UPDATEREGISTRY = 0x01;
    private const uint CDS_GLOBAL = 0x08;
    private const uint CDS_ENABLE = 0x04;
    private const uint CDS_SET_PRIMARY = 0x10;
    private const uint CDS_NORESET = 0x10000000;
    private const uint ENUM_REGISTRY_SETTINGS = 0xFFFFFFFE;
    private const uint ENUM_CURRENT_SETTINGS = 0xFFFFFFFF;
    private const int DISP_CHANGE_SUCCESSFUL = 0;
    private const int DISP_CHANGE_BADPARAM = -5;
    private const int DM_POSITION = 0x00000020;
    private const int DM_DISPLAYORIENTATION = 0x00000080;
    private const int DM_BITSPERPEL = 0x00040000;
    private const int DM_PELSWIDTH = 0x00080000;
    private const int DM_PELSHEIGHT = 0x00100000;
    private const int DM_DISPLAYFREQUENCY = 0x00400000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
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
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
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

    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_PROFILE = 0x00000008;
    private const uint SPDRP_DEVICEDESC = 0x00000000;
    private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    private const uint SPDRP_HARDWAREID = 0x00000001;
    private static readonly Guid GUID_DEVCLASS_MONITOR = new(0x4D36E96E, 0xE325, 0x11CE, 0xBF, 0xC1, 0x08, 0x00, 0x2B, 0xE1, 0x03, 0x18);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint SetupDiGetClassDevs(ref Guid classGuid, nint enumerator, nint hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(nint deviceInfoSet);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(nint deviceInfoSet, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceRegistryProperty(nint deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, uint property, out uint propertyRegDataType, nint propertyBuffer, uint propertyBufferSize, out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInstanceId(nint deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, nint deviceInstanceId, uint deviceInstanceIdSize, out uint requiredSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid classGuid;
        public uint devInst;
        public nint reserved;
    }

    public static List<string> GetMonitorNamesViaSetupApi()
    {
        var names = new List<string>();
        var guid = GUID_DEVCLASS_MONITOR;
        var devInfoSet = SetupDiGetClassDevs(ref guid, nint.Zero, nint.Zero, DIGCF_PRESENT);
        if (devInfoSet == (nint)(-1))
        {
            return names;
        }

        try
        {
            uint index = 0;
            var devInfoData = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            while (SetupDiEnumDeviceInfo(devInfoSet, index, ref devInfoData))
            {
                var name = GetDevicePropertyString(devInfoSet, ref devInfoData, SPDRP_FRIENDLYNAME);
                if (string.IsNullOrEmpty(name))
                {
                    name = GetDevicePropertyString(devInfoSet, ref devInfoData, SPDRP_DEVICEDESC);
                }

                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }

                index++;
                devInfoData = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(devInfoSet);
        }

        return names;
    }

    private static string GetDevicePropertyString(nint devInfoSet, ref SP_DEVINFO_DATA devInfoData, uint property)
    {
        var result = SetupDiGetDeviceRegistryProperty(devInfoSet, ref devInfoData, property, out _, nint.Zero, 0, out var requiredSize);
        if (!result && requiredSize == 0)
        {
            return string.Empty;
        }

        var ptr = Marshal.AllocHGlobal((int)requiredSize);
        try
        {
            result = SetupDiGetDeviceRegistryProperty(devInfoSet, ref devInfoData, property, out _, ptr, requiredSize, out _);
            return result ? Marshal.PtrToStringUni(ptr) ?? string.Empty : string.Empty;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice,
        uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public uint cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;
        public uint StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    public static string? GetDisplayName(DISPLAYCONFIG_PATH_TARGET_INFO targetInfo)
    {
        var targetId = new DisplayTargetId(targetInfo.adapterId, targetInfo.id);

        var nameFromApi = GetDisplayNameFromConfig(targetInfo);
        if (nameFromApi != null)
        {
            return nameFromApi;
        }

        EnsureDisplayNameCache();
        if (_displayNameCache!.TryGetValue(targetId, out var cached))
        {
            return cached;
        }

        return null;
    }

    private static void EnsureDisplayNameCache()
    {
        if (_displayNameCache != null)
        {
            return;
        }

        _displayNameCache = new Dictionary<DisplayTargetId, string>();
        _cachedTargetOrder = [];

        var setupApiNames = GetMonitorNamesViaSetupApi();
        var allPaths = GetAllPaths();
        var uniqueTargets = new List<(DisplayTargetId, DISPLAYCONFIG_PATH_TARGET_INFO)>();
        var seen = new HashSet<DisplayTargetId>();

        foreach (var path in allPaths)
        {
            var id = new DisplayTargetId(path.targetInfo.adapterId, path.targetInfo.id);
            if (seen.Add(id))
            {
                uniqueTargets.Add((id, path.targetInfo));
            }
        }

        _cachedTargetOrder = uniqueTargets.Select(t => t.Item1).ToArray();

        if (uniqueTargets.Count > 0 && setupApiNames.Count > 0)
        {
            int nameCount = Math.Min(setupApiNames.Count, uniqueTargets.Count);
            for (int i = 0; i < nameCount; i++)
            {
                _displayNameCache[uniqueTargets[i].Item1] = setupApiNames[i];
            }
        }

        for (int i = 0; i < uniqueTargets.Count; i++)
        {
            var id = uniqueTargets[i].Item1;
            if (!_displayNameCache.ContainsKey(id))
            {
                _displayNameCache[id] = string.Format(Properties.Resources.display_label, i + 1);
            }
        }
    }

    public static string GetTargetFriendlyName(DISPLAYCONFIG_PATH_TARGET_INFO targetInfo)
    {
        return GetDisplayName(targetInfo) ?? Properties.Resources.unknown_display;
    }

    public static string GetCurrentTopologySummary()
    {
        var (paths, _) = GetActivePaths();
        if (paths.Length == 0)
        {
            return Properties.Resources.no_active_displays;
        }

        var lines = new List<string>();
        for (int i = 0; i < paths.Length; i++)
        {
            var name = GetTargetFriendlyName(paths[i].targetInfo);
            var tech = GetOutputTechnologyName(paths[i].targetInfo.outputTechnology);
            lines.Add($"{i + 1}. {name} ({tech})");
        }

        return string.Join(", ", lines);
    }

    private static unsafe string? GetDisplayNameFromConfig(DISPLAYCONFIG_PATH_TARGET_INFO targetInfo)
    {
        var sz = Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
        var ptr = Marshal.AllocHGlobal(sz);
        try
        {
            var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GetTargetName,
                    size = (uint)sz,
                    adapterId = targetInfo.adapterId,
                    id = targetInfo.id,
                },
            };
            Marshal.StructureToPtr(deviceName, ptr, false);
            var result = DisplayConfigGetDeviceInfo_IntPtr(ptr);
            if (result == 0)
            {
                var resultName = Marshal.PtrToStructure<DISPLAYCONFIG_TARGET_DEVICE_NAME>(ptr);
                return resultName.GetMonitorFriendlyDeviceName();
            }

            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public static string GetOutputTechnologyName(DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY tech) => tech switch
    {
        DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.HDMI => "HDMI",
        DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DisplayPortExternal => "DisplayPort",
        DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DisplayPortEmbedded => "eDP",
        DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DVI => "DVI",
        DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.VGA => "VGA",
        DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.LVDS => "LVDS",
        DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.Internal => "Internal",
        DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.IndirectWired => "IndirectWired",
        DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.IndirectVirtual => "Virtual",
        _ => $"Other({(int)tech})",
    };

    public static unsafe (DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes) GetActivePaths()
    {
        var result = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var numPathArrayElements, out var numModeInfoArrayElements);
        if (result != 0)
        {
            return ([], []);
        }

        var pathArray = new DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
        var modeInfoArray = new DISPLAYCONFIG_MODE_INFO[numModeInfoArrayElements];

        fixed (DISPLAYCONFIG_PATH_INFO* pathsPtr = pathArray)
        {
            fixed (DISPLAYCONFIG_MODE_INFO* modesPtr = modeInfoArray)
            {
                result = QueryDisplayConfig(
                    QDC_ONLY_ACTIVE_PATHS,
                    ref numPathArrayElements,
                    pathsPtr,
                    ref numModeInfoArrayElements,
                    modesPtr,
                    nint.Zero);
            }
        }

        if (result != 0)
        {
            return ([], []);
        }

        Array.Resize(ref pathArray, (int)numPathArrayElements);
        Array.Resize(ref modeInfoArray, (int)numModeInfoArrayElements);
        return (pathArray, modeInfoArray);
    }

    public static unsafe (DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes) GetAllPathsWithModes()
    {
        const int maxAttempts = 3;
        const int errorInsufficientBuffer = 122;
        var flags = QDC_ALL_PATHS | QDC_VIRTUAL_MODE_AWARE;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var result = GetDisplayConfigBufferSizes(flags, out var pathCount, out var modeCount);
            if (result != 0)
            {
                throw new Win32Exception(result, "Could not query the display configuration buffer sizes.");
            }

            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            fixed (DISPLAYCONFIG_PATH_INFO* pathsPtr = paths)
            {
                fixed (DISPLAYCONFIG_MODE_INFO* modesPtr = modes)
                {
                    result = QueryDisplayConfig(flags, ref pathCount, pathsPtr, ref modeCount, modesPtr, nint.Zero);
                }
            }

            if (result == errorInsufficientBuffer && attempt + 1 < maxAttempts)
            {
                continue;
            }

            if (result != 0)
            {
                throw new Win32Exception(result, "Could not query the display topology.");
            }

            Array.Resize(ref paths, (int)pathCount);
            Array.Resize(ref modes, (int)modeCount);
            return (paths, modes);
        }

        throw new InvalidOperationException("The display topology changed repeatedly while it was being queried.");
    }

    // manageState: when true (default), SaveState() is called before the switch and
    // _savedState is cleared on failure. Set to false when called from ActivateDisplays,
    // which already saved state and must preserve it for ESC-restore and fallback steps.
    private static unsafe string SetTopology(DISPLAYCONFIG_TOPOLOGY_ID topology, bool manageState = true)
    {
        if (manageState)
        {
            SaveState();
        }

        LogDiagnostic($"SetTopology: topology={topology} flags=0x{GetTopologyFlags(topology):X8}");
        var err = SetDisplayConfig(0, null, 0, null, GetTopologyFlags(topology));
        LogDiagnostic($"SetTopology: result={err}");

        if (err == 0)
        {
            EnableEscRestore();
        }

        return err switch
        {
            0 => Properties.Resources.switched_display_topology,
            -1 or 87 => string.Format(Properties.Resources.error_format, GetWin32ErrorMessage(err)),
            _ => string.Format(Properties.Resources.error_format, GetWin32ErrorMessage(err)),
        };
    }

    internal static uint GetTopologyFlags(DISPLAYCONFIG_TOPOLOGY_ID topology)
        => SDC_APPLY | SDC_ALLOW_CHANGES | (uint)topology;

    public static string SetExternalOnly()
    {
        return SetTopology(DISPLAYCONFIG_TOPOLOGY_ID.External);
    }

    public static string SetInternalOnly()
    {
        return SetTopology(DISPLAYCONFIG_TOPOLOGY_ID.Internal);
    }

    public static string SetClone()
    {
        return SetTopology(DISPLAYCONFIG_TOPOLOGY_ID.Clone);
    }

    public static string SetExtend()
    {
        return SetTopology(DISPLAYCONFIG_TOPOLOGY_ID.Extend);
    }

    public static string SetPrimaryDisplayOnly(Action<string>? onProgress = null)
    {
        var (paths, modes) = GetAllPathsWithModes();
        var primaryTargets = paths
            .Where(path => (path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0)
            .Where(path =>
            {
                var sourceModeIndex = path.sourceInfo.sourceModeInfoIdx;
                return sourceModeIndex < modes.Length &&
                    modes[sourceModeIndex].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source &&
                    modes[sourceModeIndex].modeInfo.sourceMode.position.x == 0 &&
                    modes[sourceModeIndex].modeInfo.sourceMode.position.y == 0;
            })
            .Select(path => new DisplayTargetId(path.targetInfo.adapterId, path.targetInfo.id))
            .Distinct()
            .ToArray();

        var primaryTarget = ChoosePrimaryTarget(primaryTargets, BuildTargetToDeviceNameMap());
        if (primaryTarget is null)
        {
            LogDiagnostic("SetPrimaryDisplayOnly: no active primary display path found.");
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_primary_display_not_found);
        }

        LogDiagnostic($"SetPrimaryDisplayOnly: selected target={primaryTarget.Value} from {primaryTargets.Length} primary-source target(s).");
        return ApplyExactDisplayTargets([primaryTarget.Value], onProgress);
    }

    internal static DisplayTargetId? ChoosePrimaryTarget(
        IEnumerable<DisplayTargetId> candidates,
        IReadOnlyDictionary<DisplayTargetId, string> deviceNames)
    {
        var distinctCandidates = candidates.Distinct().ToArray();
        if (distinctCandidates.Length == 0)
        {
            return null;
        }

        return distinctCandidates
            .OrderBy(target => deviceNames.ContainsKey(target) ? 0 : 1)
            .ThenBy(target => deviceNames.TryGetValue(target, out var deviceName) ? deviceName : string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(target => target)
            .First();
    }

    internal static (uint Width, uint Height) GetPreviewDimensions(
        uint sourceWidth,
        uint sourceHeight,
        uint signalWidth,
        uint signalHeight,
        DISPLAYCONFIG_ROTATION rotation)
    {
        var width = sourceWidth > 0 ? sourceWidth : signalWidth;
        var height = sourceHeight > 0 ? sourceHeight : signalHeight;
        if ((rotation is DISPLAYCONFIG_ROTATION.Rotate90 or DISPLAYCONFIG_ROTATION.Rotate270) && width >= height)
        {
            return (height, width);
        }

        return (width, height);
    }

    private static string ApplyExactDisplayTargets(
        IReadOnlyCollection<DisplayTargetId> targets,
        Action<string>? onProgress)
    {
        var requested = targets.ToHashSet();
        var allPaths = GetAllPaths(virtualModeAware: false);
        var selectedPaths = allPaths
            .Where(path => requested.Contains(new DisplayTargetId(path.targetInfo.adapterId, path.targetInfo.id)))
            .GroupBy(path => new DisplayTargetId(path.targetInfo.adapterId, path.targetInfo.id))
            .Select(group => group.OrderByDescending(path => (path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0).First())
            .ToArray();

        if (selectedPaths.Length != requested.Count)
        {
            LogDiagnostic($"ApplyExactDisplayTargets: found {selectedPaths.Length} of {requested.Count} requested path(s).");
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_no_matching_paths);
        }

        SaveSnapshot();
        SaveState();
        EnableEscRestore();

        foreach (ref var path in selectedPaths.AsSpan())
        {
            path.sourceInfo.modeInfoIdx = 0xFFFFFFFF;
            path.targetInfo.modeInfoIdx = 0xFFFFFFFF;
        }

        var flags = SDC_APPLY | SDC_SAVE_TO_DATABASE | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES;
        int error;
        unsafe
        {
            fixed (DISPLAYCONFIG_PATH_INFO* selectedPtr = selectedPaths)
            {
                error = SetDisplayConfig((uint)selectedPaths.Length, selectedPtr, 0, null, flags);
            }
        }

        if (error != 0)
        {
            flags = SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES;
            unsafe
            {
                fixed (DISPLAYCONFIG_PATH_INFO* selectedPtr = selectedPaths)
                {
                    error = SetDisplayConfig((uint)selectedPaths.Length, selectedPtr, 0, null, flags);
                }
            }
        }

        if (error != 0)
        {
            var recoveryResult = RestoreState();
            RetainEscRecoveryIfNeeded(recoveryResult);
            LogDiagnostic($"ApplyExactDisplayTargets: SetDisplayConfig failed error={error}; recovery='{recoveryResult}'.");
            return string.Format(
                Properties.Resources.error_format,
                $"{GetWin32ErrorMessage(error)}. Recovery: {recoveryResult}");
        }

        if (!WaitForActiveTargets(targets.ToList(), onProgress: onProgress))
        {
            var recoveryResult = RestoreState();
            RetainEscRecoveryIfNeeded(recoveryResult);
            LogDiagnostic($"ApplyExactDisplayTargets: active targets did not match the requested set; recovery='{recoveryResult}'.");
            return string.Format(
                Properties.Resources.error_format,
                $"{Properties.Resources.error_display_targets_not_applied}. Recovery: {recoveryResult}");
        }

        LogDiagnostic($"ApplyExactDisplayTargets: applied exact target set [{string.Join(",", targets)}].");
        return string.Format(Properties.Resources.activated_displays_format, targets.Count);
    }

    private static void RetainEscRecoveryIfNeeded(string recoveryResult)
    {
        if (!string.Equals(recoveryResult, Properties.Resources.display_state_restored, StringComparison.Ordinal))
        {
            EnableEscRestore();
        }
    }

    public static string GetWin32ErrorMessage(int errorCode)
    {
        return errorCode switch
        {
            0 => "Success",
            -1 => "ERROR_INVALID_PARAMETER: One or more parameters are invalid",
            -2 => "ERROR_NOT_SUPPORTED: The requested operation is not supported",
            -5 => "ERROR_ACCESS_DENIED: Access denied",
            8 => "ERROR_NOT_ENOUGH_MEMORY: Not enough memory",
            87 => "ERROR_INVALID_PARAMETER: The parameter is incorrect",
            123 => "ERROR_INVALID_NAME: The filename or extension is invalid",
            1601 => "ERROR_NO_MORE_ITEMS: No more data is available",
            1460 => "ERROR_TIMEOUT: The operation timed out",
            _ => $"Unknown error code: {errorCode}",
        };
    }

    public static void SaveState()
    {
        var (paths, modes) = GetActivePaths();
        lock (StateLock)
        {
            _savedState = (paths, modes);
        }
    }

    public static bool HasSavedState
    {
        get
        {
            lock (StateLock)
            {
                return _savedState.HasValue;
            }
        }
    }

    public static unsafe string RestoreState()
    {
        (DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes) snapshot;
        bool hasSavedState;
        lock (StateLock)
        {
            hasSavedState = _savedState.HasValue;
            if (hasSavedState)
            {
                snapshot = _savedState!.Value;
            }
            else
            {
                snapshot = ([], []);
            }
        }

        if (!hasSavedState)
        {
            return RestoreBaseTopology();
        }

        if (snapshot.paths.Length == 0)
        {
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_saved_state_no_paths);
        }

        var flags = SDC_APPLY | SDC_SAVE_TO_DATABASE | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES;
        int err;

        fixed (DISPLAYCONFIG_PATH_INFO* pathsPtr = snapshot.paths)
        {
            fixed (DISPLAYCONFIG_MODE_INFO* modesPtr = snapshot.modes)
            {
                err = SetDisplayConfig(
                    (uint)snapshot.paths.Length,
                    pathsPtr,
                    (uint)snapshot.modes.Length,
                    modesPtr,
                    flags);
            }
        }

        if (err == 0)
        {
            lock (StateLock)
            {
                _savedState = null;
            }

            CancelEscRestore();
        }

        return err switch
        {
            0 => Properties.Resources.display_state_restored,
            -1 or 87 => string.Format(Properties.Resources.restore_failed_format, GetWin32ErrorMessage(err)),
            _ => string.Format(Properties.Resources.restore_failed_format, GetWin32ErrorMessage(err)),
        };
    }

    public static void EnableEscRestore()
    {
        lock (StateLock)
        {
            if (_activeHook != null)
            {
                _activeHook.Register(0x1B, () =>
                {
                    var msg = RestoreState();
                    System.Diagnostics.Debug.WriteLine($"ESC restore: {msg}");
                });
                return;
            }

            _activeHook = KeyboardHook.CreateWithEsc(() =>
            {
                var msg = RestoreState();
                System.Diagnostics.Debug.WriteLine($"ESC restore: {msg}");
            });
        }
    }

    public static void CancelEscRestore()
    {
        lock (StateLock)
        {
            if (_activeHook != null)
            {
                _activeHook.Dispose();
                _activeHook = null;
            }
        }
    }

    public static bool RegisterActivationShortcut(string shortcutText)
    {
        if (!KeyboardActivationShortcut.TryParse(shortcutText, out var shortcut, out _))
        {
            return false;
        }

        lock (StateLock)
        {
            _activationHook?.Dispose();
            _activationHook = new KeyboardHook();
            _activationHook.RegisterConditional(shortcut!.VirtualKey, () =>
            {
                if ((shortcut.Win && !IsKeyDown(0x5B) && !IsKeyDown(0x5C)) ||
                    (shortcut.Control && !IsKeyDown(0x11)) ||
                    (shortcut.Alt && !IsKeyDown(0x12)) ||
                    (shortcut.Shift && !IsKeyDown(0x10)))
                {
                    return false;
                }

                ActivationShortcutPressed?.Invoke();
                return true;
            });
        }

        return true;
    }

    public static void UnregisterActivationShortcut()
    {
        lock (StateLock)
        {
            _activationHook?.Dispose();
            _activationHook = null;
        }
    }

    private static bool IsKeyDown(int virtualKey) => GetAsyncKeyState((short)virtualKey) < 0;

    public static void EnableXboxGuideViewCombo()
        => EnableControllerChord("Guide + View");

    public static bool EnableControllerChord(string chord)
    {
        if (!ControllerChord.TryParse(chord, out var parsedChord))
        {
            LogDiagnostic($"Controller listener was not started: unsupported chord '{chord}'.");
            return false;
        }

        lock (StateLock)
        {
            _controllerChord = parsedChord;
            if (_xboxPollTimer != null)
            {
                LogDiagnostic($"Controller chord updated to '{chord}'; existing 100 ms poll timer remains active.");
                return true;
            }

            _controllerChordWasPressed = false;
            _slowControllerPollLogged = 0;
            _xboxPollTimer = new Timer(
                _ => PollXboxGuide(),
                null,
                0,
                100);
        }

        LogDiagnostic($"Controller listener started for chord '{chord}' with a 100 ms poll interval.");
        return true;
    }

    public static void DisableXboxGuideViewCombo()
    {
        bool wasRunning;
        lock (StateLock)
        {
            wasRunning = _xboxPollTimer != null;
            _xboxPollTimer?.Dispose();
            _xboxPollTimer = null;
        }

        if (wasRunning)
        {
            LogDiagnostic("Controller listener stopped.");
        }
    }

    private static void PollXboxGuide()
    {
        if (Interlocked.Exchange(ref _controllerPollInProgress, 1) != 0)
        {
            return;
        }

        var pollStarted = Stopwatch.GetTimestamp();
        try
        {
            var chordDown = false;
            for (int userIndex = 0; userIndex < XinputMaxControllers; userIndex++)
            {
                var state = default(XINPUT_STATE);
                ushort buttons = 0;
                if (XInputGetState(userIndex, ref state) == 0)
                {
                    buttons = state.Gamepad.wButtons;
                }

                var pressedButtons = (ushort)(buttons & ~PreviousControllerButtons[userIndex]);
                PreviousControllerButtons[userIndex] = buttons;
                if (pressedButtons != 0)
                {
                    ControllerButtonsPressed?.Invoke(pressedButtons);
                }

                chordDown |= _controllerChord.IsPressed(buttons);
            }

            if (chordDown && !_controllerChordWasPressed)
            {
                GuideViewComboPressed?.Invoke();
            }

            _controllerChordWasPressed = chordDown;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            LogDiagnostic($"Controller polling stopped because XInput is unavailable: {ex.Message}");
            DisableXboxGuideViewCombo();
            ControllerInputUnavailable?.Invoke(ex.Message);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(pollStarted);
            if (elapsed >= TimeSpan.FromMilliseconds(100) &&
                Interlocked.Exchange(ref _slowControllerPollLogged, 1) == 0)
            {
                LogDiagnostic($"Controller polling took {elapsed.TotalMilliseconds:F0} ms; overlapping timer callbacks are suppressed.");
            }

            Volatile.Write(ref _controllerPollInProgress, 0);
        }
    }

    internal static List<(int Index, ushort Buttons)> GetConnectedControllerInputs()
    {
        var connected = new List<(int Index, ushort Buttons)>();
        for (int userIndex = 0; userIndex < XinputMaxControllers; userIndex++)
        {
            var state = default(XINPUT_STATE);
            if (XInputGetState(userIndex, ref state) == 0)
            {
                connected.Add((userIndex + 1, state.Gamepad.wButtons));
            }
        }

        return connected;
    }

    private static bool IsViewButtonPressed()
    {
        for (int i = 0; i < XinputMaxControllers; i++)
        {
            var state = default(XINPUT_STATE);
            if (XInputGetState(i, ref state) == 0 && (state.Gamepad.wButtons & XinputGamepadBack) != 0)
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryActivateDisplays(List<DisplayTargetId> targets, out string message)
    {
        message = null!;

        if (targets.Count == 0)
        {
            message = string.Format(Properties.Resources.error_format, Properties.Resources.error_no_displays_selected);
            return false;
        }

        var allPaths = GetAllPaths();
        var allUniqueTargets = allPaths
            .Select(p => new DisplayTargetId(p.targetInfo.adapterId, p.targetInfo.id))
            .Distinct()
            .ToList();

        // Safety check: at least one selected target must exist in hardware
        var validTargets = targets.Where(t =>
            allUniqueTargets.Any(u => u.AdapterId.Equals(t.AdapterId) && u.TargetId == t.TargetId)).ToList();

        if (validTargets.Count == 0)
        {
            message = string.Format(Properties.Resources.error_format, Properties.Resources.error_no_displays_connected);
            return false;
        }

        // Safety check: at least one target must already be actively displaying,
        // otherwise applying the profile risks turning off every monitor
        // (e.g., when the chosen targets are physically disconnected or asleep).
        var (activePaths, _) = GetActivePaths();
        var activeTargetIds = activePaths
            .Select(p => new DisplayTargetId(p.targetInfo.adapterId, p.targetInfo.id))
            .ToHashSet();

        if (!validTargets.Any(t => activeTargetIds.Contains(t)))
        {
            message = string.Format(Properties.Resources.error_format, Properties.Resources.error_no_active_monitors_remaining);
            return false;
        }

        // Proceed with activation
        message = ActivateDisplays(validTargets);
        return true;
    }

    public static unsafe DISPLAYCONFIG_PATH_INFO[] GetAllPaths(bool virtualModeAware = true)
    {
        uint flags = QDC_ALL_PATHS;
        if (virtualModeAware)
        {
            flags |= QDC_VIRTUAL_MODE_AWARE;
        }

        var result = GetDisplayConfigBufferSizes(flags, out var numPathArrayElements, out var numModeInfoArrayElements);
        if (result != 0)
        {
            return FallbackToActivePaths();
        }

        var pathArray = new DISPLAYCONFIG_PATH_INFO[numPathArrayElements];
        var modeInfoArray = new DISPLAYCONFIG_MODE_INFO[numModeInfoArrayElements];

        fixed (DISPLAYCONFIG_PATH_INFO* pathsPtr = pathArray)
        {
            fixed (DISPLAYCONFIG_MODE_INFO* modesPtr = modeInfoArray)
            {
                result = QueryDisplayConfig(
                    flags,
                    ref numPathArrayElements,
                    pathsPtr,
                    ref numModeInfoArrayElements,
                    modesPtr,
                    nint.Zero);
            }
        }

        if (result != 0)
        {
            return FallbackToActivePaths();
        }

        Array.Resize(ref pathArray, (int)numPathArrayElements);
        return pathArray;
    }

    private static unsafe DISPLAYCONFIG_PATH_INFO[] FallbackToActivePaths()
    {
        var (activePaths, _) = GetActivePaths();
        return activePaths;
    }

    internal static bool IsInternalTechnology(DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY tech)
        => tech == DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.Internal || ((uint)tech & 0x80000000) != 0;

    private static DISPLAYCONFIG_TOPOLOGY_ID ClassifyTopology(List<DisplayTargetId> targets)
    {
        // Use all paths (not just active) so profiles targeting inactive displays
        // are classified correctly â€” active-only misses disconnected/off monitors.
        var techByTarget = GetAllPaths()
            .GroupBy(p => new DisplayTargetId(p.targetInfo.adapterId, p.targetInfo.id))
            .ToDictionary(g => g.Key, g => g.First().targetInfo.outputTechnology);
        return ClassifyTopology(targets, techByTarget);
    }

    internal static DISPLAYCONFIG_TOPOLOGY_ID ClassifyTopology(
        List<DisplayTargetId> targets,
        IReadOnlyDictionary<DisplayTargetId, DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY> techByTarget)
    {
        var selectedInternal = 0;
        var selectedExternal = 0;
        foreach (var t in targets)
        {
            if (techByTarget.TryGetValue(t, out var tech))
            {
                if (IsInternalTechnology(tech))
                {
                    selectedInternal++;
                }
                else
                {
                    selectedExternal++;
                }
            }
        }

        DISPLAYCONFIG_TOPOLOGY_ID result;
        if (selectedExternal == 1 && selectedInternal == 0)
        {
            result = DISPLAYCONFIG_TOPOLOGY_ID.External;
        }
        else if (selectedInternal == 1 && selectedExternal == 0)
        {
            result = DISPLAYCONFIG_TOPOLOGY_ID.Internal;
        }
        else
        {
            result = DISPLAYCONFIG_TOPOLOGY_ID.Extend;
        }

        LogDiagnostic($"ClassifyTopology: internal={selectedInternal} external={selectedExternal} → {result}");
        return result;
    }

    public static string ActivateDisplays(
        List<DisplayTargetId> targets,
        Action<string>? onProgress = null,
        bool saveRecoverySnapshot = true)
    {
        SaveState();
        if (saveRecoverySnapshot)
        {
            SaveSnapshot();
        }

        // Resolve targets to unique device names. Ghost targets (Intel GPU) that
        // share a GDI device name are skipped — they can't be activated individually.
        var deviceNameMap = BuildTargetToDeviceNameMap();
        var realTargets = targets.Where(t => deviceNameMap.ContainsKey(t)).ToList();

        LogDiagnostic($"ActivateDisplays: {targets.Count} requested, {realTargets.Count} real (device name map has {deviceNameMap.Count} entries)");

        if (realTargets.Count == 0)
        {
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_no_displays_selected);
        }

        if (realTargets.Count != targets.Count)
        {
            LogDiagnostic($"ActivateDisplays: filtered {targets.Count - realTargets.Count} ghost/unmappable target(s): {string.Join(",", targets.Except(realTargets))}");
            targets = realTargets;
        }

        var topology = ClassifyTopology(targets);
        LogDiagnostic($"ActivateDisplays: topology={topology} targets=[{string.Join(",", targets)}]");

        // manageState:false — SaveState() already called above; must not null _savedState on failure.
        var result = SetTopology(topology, manageState: false);
        var activeBeforeFallback = GetActivePaths().paths.Length;
        LogDiagnostic($"ActivateDisplays: SetTopology result='{result}' activePaths={activeBeforeFallback}");
        if (!result.StartsWith(Properties.Resources.error_prefix, StringComparison.OrdinalIgnoreCase) &&
            WaitForActiveTargets(targets, onProgress: onProgress))
        {
            LogDiagnostic($"ActivateDisplays: topology switch succeeded for {targets.Count} target(s)");
            return string.Format(Properties.Resources.activated_displays_format, targets.Count);
        }

        LogDiagnostic($"ActivateDisplays: topology switch failed, trying path array fallback");

        // Topology fallback failed — try with path array as last resort.
        // QDC_ALL_PATHS returns multiple paths per target (one per possible source),
        // so deduplicate by target and prefer active paths to avoid ERROR_INVALID_PARAMETER.
        var allPaths = GetAllPaths(virtualModeAware: false);
        var selectedPaths = allPaths
            .Where(p => targets.Any(t =>
                t.AdapterId.Equals(p.targetInfo.adapterId) &&
                t.TargetId == p.targetInfo.id))
            .GroupBy(p => new DisplayTargetId(p.targetInfo.adapterId, p.targetInfo.id))
            .Select(g => g.OrderByDescending(p => (p.flags & 1u) != 0 ? 1 : 0).First())
            .ToArray();

        if (selectedPaths.Length == 0)
        {
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_no_matching_paths);
        }

        for (int i = 0; i < selectedPaths.Length; i++)
        {
            selectedPaths[i].sourceInfo.modeInfoIdx = 0xFFFFFFFF;
            selectedPaths[i].targetInfo.modeInfoIdx = 0xFFFFFFFF;
        }

        var flags = SDC_APPLY | SDC_SAVE_TO_DATABASE | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES;
        int err;

        unsafe
        {
            fixed (DISPLAYCONFIG_PATH_INFO* selectedPtr = selectedPaths)
            {
                err = SetDisplayConfig((uint)selectedPaths.Length, selectedPtr, 0, null, flags);
            }
        }

        if (err != 0)
        {
            flags = SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES;
            unsafe
            {
                fixed (DISPLAYCONFIG_PATH_INFO* selectedPtr = selectedPaths)
                {
                    err = SetDisplayConfig((uint)selectedPaths.Length, selectedPtr, 0, null, flags);
                }
            }
        }

        if (err == 0)
        {
            LogDiagnostic($"ActivateDisplays: path array succeeded (flags=0x{flags:X8})");
            if (WaitForActiveTargets(targets, onProgress: onProgress))
            {
                EnableEscRestore();
                return string.Format(Properties.Resources.activated_displays_format, targets.Count);
            }
        }
        else
        {
            LogDiagnostic($"ActivateDisplays: path array failed err={err}");
        }

        // CDSE fallback for builds where SetDisplayConfig returns ERROR_INVALID_PARAMETER
        LogDiagnostic($"ActivateDisplays: trying CDSE fallback");
        int cdseResult = TryActivateViaCDSE(targets);
        LogDiagnostic($"ActivateDisplays: CDSE result={cdseResult}");
        if (cdseResult == DISP_CHANGE_SUCCESSFUL && WaitForActiveTargets(targets, onProgress: onProgress))
        {
            EnableEscRestore();
            return string.Format(Properties.Resources.activated_displays_format, targets.Count);
        }

        LogDiagnostic($"ActivateDisplays: ALL METHODS FAILED. lastErr={err} cdseResult={cdseResult}");
        return string.Format(Properties.Resources.error_format, GetWin32ErrorMessage(err));
    }

    private static bool WaitForActiveTargets(List<DisplayTargetId> targets, int maxWaitMs = 15000, Action<string>? onProgress = null)
    {
        var requested = targets.ToHashSet();
        const int pollMs = 500;
        int elapsed = 0;
        while (elapsed < maxWaitMs)
        {
            var active = GetActivePaths().paths
                .Select(path => new DisplayTargetId(path.targetInfo.adapterId, path.targetInfo.id))
                .ToHashSet();

            if (active.SetEquals(requested))
            {
                return true;
            }

            if (onProgress != null)
            {
                int remainingSecs = (maxWaitMs - elapsed + 999) / 1000;
                onProgress.Invoke($"Verifica monitor attivi in corso ({remainingSecs}s)...");
            }

            Thread.Sleep(pollMs);
            elapsed += pollMs;
        }

        var activeAfterTimeout = GetActivePaths().paths
            .Select(path => new DisplayTargetId(path.targetInfo.adapterId, path.targetInfo.id))
            .ToList();
        LogDiagnostic($"WaitForActiveTargets: TIMEOUT after {maxWaitMs}ms. Requested=[{string.Join(",", requested)}] Active=[{string.Join(",", activeAfterTimeout)}]");
        return false;
    }

    private static readonly string ProfilesDir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MonitorPower",
        "profiles");

    private static MonitorPowerProfilePersistence ProfileStore => new(ProfilesDir);

    private static readonly string SnapshotDir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MonitorPower");

    private static readonly string SnapshotPath = System.IO.Path.Combine(SnapshotDir, "snapshot.json");

    private static readonly string BaseTopologyPath = System.IO.Path.Combine(SnapshotDir, "base-topology.json");
    private static readonly string DiagnosticsPath = System.IO.Path.Combine(SnapshotDir, "diagnostics.log");

    private static void LogDiagnostic(string message)
    {
        try
        {
            Directory.CreateDirectory(SnapshotDir);
            File.AppendAllText(DiagnosticsPath, $"{DateTime.Now:O} {message}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Monitor Power diagnostic write failed: {ex.Message}");
        }
    }

    public static string ReadDiagnostics()
    {
        if (!File.Exists(DiagnosticsPath))
        {
            return "No Monitor Power diagnostics have been recorded.";
        }

        return File.ReadAllText(DiagnosticsPath);
    }

    internal static void SaveSnapshot()
        => SaveTopologySnapshot(SnapshotPath);

    public static void SaveBaseTopology()
        => SaveTopologySnapshot(BaseTopologyPath);

    private static void SaveTopologySnapshot(string path)
    {
        var (paths, _) = GetActivePaths();
        var targets = CaptureLayout(paths.Select(path =>
            new DisplayTargetId(path.targetInfo.adapterId, path.targetInfo.id)));
        if (targets.Count == 0)
        {
            throw new InvalidOperationException("The current display topology could not be captured.");
        }

        Directory.CreateDirectory(SnapshotDir);
        File.WriteAllText(path, JsonSerializer.Serialize(new { targets }));
    }

    public static string RestoreBaseTopology()
    {
        if (!File.Exists(BaseTopologyPath))
        {
            return "No base display topology has been saved.";
        }

        var saved = JsonSerializer.Deserialize<SnapshotDocument>(
            File.ReadAllText(BaseTopologyPath));
        if (saved?.Targets is not { Count: > 0 })
        {
            return "The saved base topology is empty or invalid.";
        }

        var currentTargets = BuildTargetToDeviceNameMap();
        var remappedLayout = new List<SnapshotTarget>();
        foreach (var entry in saved.Targets)
        {
            var savedTarget = new DisplayTargetId(
                new LUID { LowPart = entry.LowPart, HighPart = entry.HighPart },
                entry.TargetId);
            var candidates = currentTargets.Keys
                .Where(target => target.Equals(savedTarget) || target.TargetId == savedTarget.TargetId)
                .Take(2)
                .ToArray();
            if (candidates.Length != 1)
            {
                continue;
            }

            entry.LowPart = candidates[0].AdapterId.LowPart;
            entry.HighPart = candidates[0].AdapterId.HighPart;
            entry.DeviceName = currentTargets[candidates[0]];
            remappedLayout.Add(entry);
        }

        if (remappedLayout.Count == 0)
        {
            return "None of the displays from the saved base topology are available.";
        }

        var targets = remappedLayout
            .Select(entry => new DisplayTargetId(
                new LUID { LowPart = entry.LowPart, HighPart = entry.HighPart },
                entry.TargetId))
            .ToList();
        var activationResult = ActivateDisplays(targets, saveRecoverySnapshot: false);
        if (activationResult.StartsWith(Properties.Resources.error_prefix, StringComparison.OrdinalIgnoreCase))
        {
            return activationResult;
        }

        if (remappedLayout.Count > 1 && !TryValidateLayout(remappedLayout, targets, out var validationError))
        {
            return $"The saved base topology is invalid: {validationError}";
        }

        var layoutResult = ApplyLayout(remappedLayout);
        return layoutResult == DISP_CHANGE_SUCCESSFUL
            ? "Base display topology restored."
            : $"Could not restore the saved display layout: {GetWin32ErrorMessage(layoutResult)}";
    }

    private sealed class SnapshotDocument
    {
        [JsonPropertyName("targets")]
        public List<SnapshotTarget> Targets { get; set; } = [];
    }

    internal sealed class SnapshotTarget
    {
        public uint LowPart { get; set; }

        public int HighPart { get; set; }

        public uint TargetId { get; set; }

        public string DeviceName { get; set; } = string.Empty;

        public int Width { get; set; }

        public int Height { get; set; }

        public int Frequency { get; set; }

        public int PositionX { get; set; }

        public int PositionY { get; set; }

        public int Orientation { get; set; }

        public int BitsPerPel { get; set; }

        public bool IsPrimary { get; set; }
    }

    internal readonly record struct DisplayNameCandidate(
        DisplayTargetId Target,
        string DeviceName,
        bool IsActive,
        uint SourceId);

    internal static Dictionary<DisplayTargetId, string> AssignUniqueDeviceNames(
        IEnumerable<DisplayNameCandidate> candidates)
    {
        var candidateGroups = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.DeviceName))
            .GroupBy(candidate => candidate.Target)
            .Select(group => new
            {
                Target = group.Key,
                Candidates = group
                    .GroupBy(candidate => candidate.DeviceName, StringComparer.OrdinalIgnoreCase)
                    .Select(deviceGroup => deviceGroup
                        .OrderByDescending(candidate => candidate.IsActive)
                        .ThenBy(candidate => candidate.SourceId)
                        .First())
                    .OrderByDescending(candidate => candidate.IsActive)
                    .ThenBy(candidate => candidate.SourceId)
                    .ToList(),
            })
            .OrderByDescending(group => group.Candidates.Any(candidate => candidate.IsActive))
            .ThenBy(group => group.Candidates.Count)
            .ThenBy(group => group.Target)
            .ToList();

        var map = new Dictionary<DisplayTargetId, string>();
        var assignedDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in candidateGroups)
        {
            var candidate = group.Candidates.FirstOrDefault(
                item => !assignedDeviceNames.Contains(item.DeviceName));
            if (string.IsNullOrEmpty(candidate.DeviceName))
            {
                continue;
            }

            map[group.Target] = candidate.DeviceName;
            assignedDeviceNames.Add(candidate.DeviceName);
        }

        return map;
    }

    // Build a one-to-one map from DisplayTargetId to GDI device name. QDC_ALL_PATHS
    // exposes every possible target/source pairing, so selecting the first path can
    // incorrectly map several targets to the same \\.\DISPLAY device.
    internal static Dictionary<DisplayTargetId, string> BuildTargetToDeviceNameMap()
    {
        uint qflags = QDC_ALL_PATHS | QDC_VIRTUAL_MODE_AWARE;
        int hr = GetDisplayConfigBufferSizes(qflags, out var np, out var nm);
        if (hr != 0)
        {
            return [];
        }

        var pa = new DISPLAYCONFIG_PATH_INFO[np];
        var ma = new DISPLAYCONFIG_MODE_INFO[nm];
        unsafe
        {
            fixed (DISPLAYCONFIG_PATH_INFO* pp = pa)
            {
                fixed (DISPLAYCONFIG_MODE_INFO* mm = ma)
                {
                    hr = QueryDisplayConfig(qflags, ref np, pp, ref nm, mm, nint.Zero);
                    if (hr != 0)
                    {
                        return [];
                    }
                }
            }
        }

        Array.Resize(ref pa, (int)np);
        var candidates = new List<DisplayNameCandidate>();

        // Some Windows builds report targetAvailable == 0 even for an active path.
        // Keep active paths in the map so the profile page does not discard the
        // display that is currently in use.
        foreach (var p in pa.Where(path =>
                     path.targetInfo.targetAvailable != 0 ||
                     (path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0))
        {
            var targetId = new DisplayTargetId(p.targetInfo.adapterId, p.targetInfo.id);

            // GetSourceName requires the source id, not the target id.
            unsafe
            {
                var srcName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GetSourceName,
                        size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME),
                        adapterId = p.sourceInfo.adapterId,
                        id = p.sourceInfo.id,
                    },
                };
                DISPLAYCONFIG_SOURCE_DEVICE_NAME* ptr = &srcName;
                int nameHr = DisplayConfigGetDeviceInfo_IntPtr((nint)ptr);
                if (nameHr == 0)
                {
                    var gdiName = srcName.GetViewGdiDeviceName();
                    if (!string.IsNullOrEmpty(gdiName))
                    {
                        candidates.Add(new DisplayNameCandidate(
                            targetId,
                            gdiName,
                            (p.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0,
                            p.sourceInfo.id));
                    }
                }
            }
        }

        return AssignUniqueDeviceNames(candidates);
    }

    private static unsafe int TryActivateViaCDSE(List<DisplayTargetId>? targetsToActivate = null)
    {
        // Build target → GDI device name map using correct source IDs
        var targetToDevice = BuildTargetToDeviceNameMap();
        if (targetToDevice.Count == 0)
        {
            return -1;
        }

        // Filter: keep only targets that map to a unique device name (skip ghost targets)
        var activateSet = targetsToActivate != null
            ? new HashSet<DisplayTargetId>(targetsToActivate.Where(t => targetToDevice.ContainsKey(t)))
            : null;

        var removedGhosts = targetsToActivate?.Count - activateSet?.Count;
        if (removedGhosts > 0)
        {
            LogDiagnostic($"CDSE filtered {removedGhosts} ghost target(s) from activate set");
        }

        var changeResult = DISP_CHANGE_SUCCESSFUL;
        LogDiagnostic($"CDSE start targets={string.Join(",", activateSet ?? [])} map={string.Join(",", targetToDevice.Select(p => $"{p.Key}={p.Value}"))}");

        // First, detach monitors not in the requested set from the desktop.
        if (activateSet != null)
        {
            foreach (var kvp in targetToDevice)
            {
                if (activateSet.Contains(kvp.Key))
                {
                    continue;
                }

                var deviceName = kvp.Value;
                if (!deviceName.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var disabledMode = new DEVMODE
                {
                    dmSize = (short)Marshal.SizeOf<DEVMODE>(),
                    dmFields = DM_PELSWIDTH | DM_PELSHEIGHT,
                    dmPelsWidth = 0,
                    dmPelsHeight = 0,
                };
                var disabledModePtr = Marshal.AllocHGlobal(Marshal.SizeOf<DEVMODE>());
                try
                {
                    Marshal.StructureToPtr(disabledMode, disabledModePtr, false);
                    var result = ChangeDisplaySettingsEx(
                        deviceName,
                        disabledModePtr,
                        nint.Zero,
                        CDS_UPDATEREGISTRY | CDS_NORESET,
                        nint.Zero);
                    LogDiagnostic($"CDSE detach {kvp.Key} {deviceName} result={result}");
                    if (result != DISP_CHANGE_SUCCESSFUL)
                    {
                        changeResult = result;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(disabledModePtr);
                }
            }
        }

        // Then activate each requested target
        foreach (var target in targetToDevice)
        {
            if (activateSet != null && !activateSet.Contains(target.Key))
            {
                continue;
            }

            var deviceName = target.Value;
            if (!deviceName.StartsWith(@"\\.\DISPLAY", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var dm = default(DEVMODE);
            dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
            int regOk = EnumDisplaySettings(deviceName, ENUM_REGISTRY_SETTINGS, ref dm);
            LogDiagnostic($"CDSE activate {target.Key} {deviceName} enumRegistry={regOk} mode={dm.dmPelsWidth}x{dm.dmPelsHeight} fields=0x{dm.dmFields:X}");
            if (regOk == 0 || dm.dmPelsWidth == 0 || dm.dmPelsHeight == 0)
            {
                changeResult = -1;
                continue;
            }

            // Apply the registry settings with NORESET so we can commit all at once
            int size = Marshal.SizeOf<DEVMODE>();
            nint dmPtr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(dm, dmPtr, false);
                int result = ChangeDisplaySettingsEx(
                    deviceName, dmPtr, nint.Zero, CDS_UPDATEREGISTRY | CDS_NORESET, nint.Zero);
                if (result != DISP_CHANGE_SUCCESSFUL)
                {
                    result = ChangeDisplaySettingsEx(deviceName, dmPtr, nint.Zero, CDS_GLOBAL | CDS_NORESET, nint.Zero);
                }

                LogDiagnostic($"CDSE activate apply {target.Key} {deviceName} result={result}");
                if (result != DISP_CHANGE_SUCCESSFUL)
                {
                    changeResult = result;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(dmPtr);
            }
        }

        // Commit all deferred changes at once
        var commitResult = ChangeDisplaySettingsEx(null!, nint.Zero, nint.Zero, 0, nint.Zero);
        LogDiagnostic($"CDSE commit result={commitResult}");
        if (commitResult != DISP_CHANGE_SUCCESSFUL)
        {
            changeResult = commitResult;
        }

        return changeResult;
    }

    private static List<SnapshotTarget> CaptureLayout(IEnumerable<DisplayTargetId> targets)
    {
        var requested = targets.ToHashSet();
        var deviceNameMap = BuildTargetToDeviceNameMap();
        var layout = new List<SnapshotTarget>();
        foreach (var id in requested)
        {
            if (!deviceNameMap.TryGetValue(id, out var deviceName))
            {
                continue;
            }

            var dm = default(DEVMODE);
            dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
            if (EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref dm) == 0 ||
                dm.dmPelsWidth == 0 ||
                dm.dmPelsHeight == 0)
            {
                continue;
            }

            layout.Add(new SnapshotTarget
            {
                LowPart = id.AdapterId.LowPart,
                HighPart = id.AdapterId.HighPart,
                TargetId = id.TargetId,
                DeviceName = deviceName,
                Width = dm.dmPelsWidth,
                Height = dm.dmPelsHeight,
                Frequency = dm.dmDisplayFrequency,
                PositionX = dm.dmPositionX,
                PositionY = dm.dmPositionY,
                Orientation = dm.dmDisplayOrientation,
                BitsPerPel = dm.dmBitsPerPel,
                IsPrimary = dm.dmPositionX == 0 && dm.dmPositionY == 0,
            });
        }

        return layout;
    }

    internal static bool TryValidateLayout(
        IReadOnlyList<SnapshotTarget> layout,
        IReadOnlyCollection<DisplayTargetId>? expectedTargets,
        out string validationError)
    {
        validationError = string.Empty;
        if (layout.Count == 0)
        {
            validationError = "The display layout is empty.";
            return false;
        }

        if (expectedTargets != null && layout.Count != expectedTargets.Count)
        {
            validationError = "The layout does not contain every selected display.";
            return false;
        }

        var targetIds = layout
            .Select(entry => new DisplayTargetId(
                new LUID { LowPart = entry.LowPart, HighPart = entry.HighPart },
                entry.TargetId))
            .ToList();
        if (targetIds.Distinct().Count() != targetIds.Count ||
            (expectedTargets != null && !targetIds.ToHashSet().SetEquals(expectedTargets)))
        {
            validationError = "The layout contains duplicate or unexpected display targets.";
            return false;
        }

        if (layout.Any(entry =>
            string.IsNullOrWhiteSpace(entry.DeviceName) ||
            entry.Width <= 0 ||
            entry.Height <= 0))
        {
            validationError = "The layout contains a display without a valid device name or mode.";
            return false;
        }

        if (layout.Select(entry => entry.DeviceName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != layout.Count)
        {
            validationError = "Multiple display targets are mapped to the same device.";
            return false;
        }

        if (layout.Count > 1 && layout.Count(entry => entry.IsPrimary) != 1)
        {
            validationError = "The layout must contain exactly one primary display.";
            return false;
        }

        for (int i = 0; i < layout.Count; i++)
        {
            var first = layout[i];
            long firstRight = (long)first.PositionX + first.Width;
            long firstBottom = (long)first.PositionY + first.Height;
            for (int j = i + 1; j < layout.Count; j++)
            {
                var second = layout[j];
                long secondRight = (long)second.PositionX + second.Width;
                long secondBottom = (long)second.PositionY + second.Height;
                bool overlaps =
                    first.PositionX < secondRight &&
                    firstRight > second.PositionX &&
                    first.PositionY < secondBottom &&
                    firstBottom > second.PositionY;
                if (overlaps)
                {
                    validationError = $"Displays {first.DeviceName} and {second.DeviceName} overlap.";
                    return false;
                }
            }
        }

        return true;
    }

    private static int ApplyLayout(IReadOnlyList<SnapshotTarget> layout)
    {
        if (!TryValidateLayout(layout, null, out var validationError))
        {
            LogDiagnostic($"CDSE layout rejected: {validationError}");
            return DISP_CHANGE_BADPARAM;
        }

        // Resolve current device names — saved names may be stale after replug/reboot
        var currentDeviceMap = BuildTargetToDeviceNameMap();
        var resolved = new List<(SnapshotTarget Entry, string DeviceName)>();
        foreach (var entry in layout)
        {
            var id = new DisplayTargetId(
                new LUID { LowPart = entry.LowPart, HighPart = entry.HighPart },
                entry.TargetId);
            if (currentDeviceMap.TryGetValue(id, out var dn))
            {
                resolved.Add((entry, dn));
            }
            else
            {
                LogDiagnostic($"ApplyLayout: target {id} has no current device name, skipping");
            }
        }

        if (resolved.Count == 0)
        {
            LogDiagnostic("ApplyLayout: no resolvable targets");
            return DISP_CHANGE_BADPARAM;
        }

        // Pass 1: Apply orientation and resolution/frequency settings
        var result = DISP_CHANGE_SUCCESSFUL;
        foreach (var (entry, deviceName) in resolved)
        {
            if (entry.Width <= 0 || entry.Height <= 0)
            {
                continue;
            }

            var dm = default(DEVMODE);
            dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
            _ = EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref dm);
            dm.dmFields = DM_DISPLAYORIENTATION |
                          DM_PELSWIDTH |
                          DM_PELSHEIGHT |
                          DM_DISPLAYFREQUENCY |
                          DM_BITSPERPEL;
            dm.dmDisplayOrientation = entry.Orientation;
            dm.dmPelsWidth = entry.Width;
            dm.dmPelsHeight = entry.Height;
            dm.dmDisplayFrequency = entry.Frequency;
            dm.dmBitsPerPel = entry.BitsPerPel;

            var dmPtr = Marshal.AllocHGlobal(Marshal.SizeOf<DEVMODE>());
            try
            {
                Marshal.StructureToPtr(dm, dmPtr, false);
                var flags = CDS_UPDATEREGISTRY | CDS_GLOBAL | CDS_NORESET;
                var applyResult = ChangeDisplaySettingsEx(deviceName, dmPtr, nint.Zero, flags, nint.Zero);
                LogDiagnostic($"CDSE mode {deviceName} {entry.Width}x{entry.Height}@{entry.Frequency} orientation={entry.Orientation} result={applyResult}");

                if (applyResult != DISP_CHANGE_SUCCESSFUL)
                {
                    result = applyResult;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(dmPtr);
            }
        }

        _ = ChangeDisplaySettingsEx(null!, nint.Zero, nint.Zero, 0, nint.Zero);
        Thread.Sleep(500); // Give the system a brief moment to apply the rotation

        // Pass 2: Apply positions
        foreach (var (entry, deviceName) in resolved)
        {
            if (entry.Width <= 0 || entry.Height <= 0)
            {
                result = -1;
                continue;
            }

            var dm = default(DEVMODE);
            dm.dmSize = (short)Marshal.SizeOf<DEVMODE>();
            _ = EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref dm);
            dm.dmFields = DM_POSITION;
            dm.dmPositionX = entry.PositionX;
            dm.dmPositionY = entry.PositionY;

            var dmPtr = Marshal.AllocHGlobal(Marshal.SizeOf<DEVMODE>());
            try
            {
                Marshal.StructureToPtr(dm, dmPtr, false);
                var flags = CDS_UPDATEREGISTRY | CDS_GLOBAL | CDS_NORESET;
                if (entry.IsPrimary)
                {
                    flags |= CDS_SET_PRIMARY;
                }

                var applyResult = ChangeDisplaySettingsEx(deviceName, dmPtr, nint.Zero, flags, nint.Zero);
                LogDiagnostic($"CDSE position {deviceName} pos=({entry.PositionX},{entry.PositionY}) result={applyResult}");
                if (applyResult != DISP_CHANGE_SUCCESSFUL)
                {
                    result = applyResult;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(dmPtr);
            }
        }

        var commitResult = ChangeDisplaySettingsEx(null!, nint.Zero, nint.Zero, 0, nint.Zero);
        LogDiagnostic($"CDSE position commit result={commitResult}");
        return commitResult == DISP_CHANGE_SUCCESSFUL ? result : commitResult;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).ToArray());
    }

    public static string? ResolveSavedProfileFileName(string profileReference, bool referenceIsFileName)
    {
        if (string.IsNullOrWhiteSpace(profileReference))
        {
            return null;
        }

        if (referenceIsFileName)
        {
            // Reject any input that contains path separators to prevent directory traversal
            if (profileReference.Contains(System.IO.Path.DirectorySeparatorChar) ||
                profileReference.Contains(System.IO.Path.AltDirectorySeparatorChar))
            {
                return null;
            }

            var name = System.IO.Path.GetFileNameWithoutExtension(profileReference);
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return $"{name}.json";
        }

        var profiles = GetSavedProfiles();
        foreach (var (fileName, pName) in profiles)
        {
            if (string.Equals(pName, profileReference, StringComparison.OrdinalIgnoreCase))
            {
                return fileName;
            }
        }

        return null;
    }

    public static string ApplySavedProfileReference(string profileReference, bool referenceIsFileName)
    {
        var fileName = ResolveSavedProfileFileName(profileReference, referenceIsFileName);
        if (fileName == null)
        {
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_profile_not_found);
        }

        DisplayProfileApplyStarted?.Invoke();
        try
        {
            return ApplyNamedProfile(fileName);
        }
        finally
        {
            DisplayProfileApplyCompleted?.Invoke();
        }
    }

    public static string SaveNamedProfile(string name, List<DisplayTargetId> targets, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Format(Properties.Resources.error_format, "A profile name is required.");
        }

        if (targets.Count == 0)
        {
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_no_displays_selected);
        }

        var safeName = SanitizeFileName(name);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            return string.Format(Properties.Resources.error_format, "The profile name does not contain any valid filename characters.");
        }

        var fileName = $"{safeName}.json";
        if (File.Exists(ProfileStore.GetPath(fileName)) && !overwrite)
        {
            return string.Format(Properties.Resources.error_format, "A profile with this name already exists.");
        }

        var data = new MonitorPowerProfile
        {
            Name = name,
            Targets = targets,
            Layout = CaptureLayout(targets),
        };
        if (!TryValidateLayout(data.Layout, targets, out var validationError))
        {
            LogDiagnostic($"SaveNamedProfile rejected: {validationError}");
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_profile_layout_invalid);
        }

        ProfileStore.Save(fileName, data, overwrite);
        return Properties.Resources.profile_saved;
    }

    public static List<(string FileName, string Name)> GetSavedProfiles()
        => ProfileStore.List().ToList();

    public static string ApplyNamedProfile(string fileName, Action<string>? onProgress = null)
    {
        var profile = ProfileStore.Load(fileName);
        if (profile == null || profile.Targets.Count == 0)
        {
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_profile_empty);
        }

        LogDiagnostic($"ApplyNamedProfile: fileName={fileName} name='{profile.Name}' targets=[{string.Join(",", profile.Targets)}] layoutEntries={profile.Layout?.Count ?? 0}");

        // Adapter LUIDs are not stable across driver updates and reboots. Remap
        // saved targets to the current adapter by target ID, and omit displays
        // that are not currently exposed by Windows (for example, a powered-off TV).
        var currentTargets = BuildTargetToDeviceNameMap().Keys.ToList();
        var targetRemap = new Dictionary<DisplayTargetId, DisplayTargetId>();
        foreach (var savedTarget in profile.Targets)
        {
            var currentTarget = currentTargets.Contains(savedTarget)
                ? savedTarget
                : currentTargets.Where(target => target.TargetId == savedTarget.TargetId).SingleOrDefault();
            if (!currentTarget.Equals(default(DisplayTargetId)))
            {
                targetRemap[savedTarget] = currentTarget;
            }
        }

        profile.Targets = targetRemap.Values.Distinct().ToList();
        if (profile.Targets.Count == 0)
        {
            LogDiagnostic("ApplyNamedProfile: no saved targets are currently available");
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_no_displays_connected);
        }

        if (profile.Layout != null)
        {
            profile.Layout.RemoveAll(entry =>
            {
                var savedTarget = new DisplayTargetId(
                    new LUID { LowPart = entry.LowPart, HighPart = entry.HighPart },
                    entry.TargetId);
                if (!targetRemap.TryGetValue(savedTarget, out var currentTarget))
                {
                    return true;
                }

                entry.LowPart = currentTarget.AdapterId.LowPart;
                entry.HighPart = currentTarget.AdapterId.HighPart;
                return false;
            });
        }

        LogDiagnostic($"ApplyNamedProfile: resolved targets=[{string.Join(",", profile.Targets)}] layoutEntries={profile.Layout?.Count ?? 0}");

        if (profile.Layout != null &&
            profile.Layout.Count > 0 &&
            !TryValidateLayout(profile.Layout, profile.Targets, out var validationError))
        {
            LogDiagnostic($"ApplyNamedProfile rejected: {validationError}");
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_profile_layout_invalid);
        }

        var activateResult = ActivateDisplays(profile.Targets, onProgress);

        // Always attempt ApplyLayout when a layout is available.
        // When switching from a single-display topology (e.g. TV-only) back to
        // multi-monitor, the topology switch may succeed but WaitForActiveTargets
        // can time out before physical monitors finish waking. ApplyLayout sends
        // ChangeDisplaySettingsEx to each monitor and gives Windows another chance
        // to bring them fully online.
        bool activateError = activateResult.StartsWith(
            Properties.Resources.error_prefix, StringComparison.OrdinalIgnoreCase);

        var activePaths = GetActivePaths().paths;
        bool enablingMonitors = activePaths.Length < profile.Targets.Count;

        if (profile.Layout == null || profile.Layout.Count == 0)
        {
            return activateResult;
        }

        if (enablingMonitors)
        {
            // Give DP monitors sufficient time to wake up, complete link training
            // and expose display modes before applying the layout (resolution/orientation).
            for (int i = 3; i > 0; i--)
            {
                onProgress?.Invoke($"Attivazione profilo in corso... Attendi l'accensione dei monitor ({i}s)...");
                Thread.Sleep(1000);
            }
        }
        else if (activateError)
        {
            for (int i = 2; i > 0; i--)
            {
                onProgress?.Invoke($"Attivazione profilo in corso... Attendi ({i}s)...");
                Thread.Sleep(750);
            }
        }

        var layoutResult = ApplyLayout(profile.Layout);
        var activeAfterLayout = GetActivePaths().paths.Length;
        LogDiagnostic($"ApplyNamedProfile layout result={layoutResult} activateError={activateError} activePaths={activeAfterLayout}");

        if (layoutResult == DISP_CHANGE_SUCCESSFUL && WaitForActiveTargets(profile.Targets))
        {
            LogDiagnostic($"ApplyNamedProfile: SUCCESS after layout");
            return string.Format(Properties.Resources.activated_displays_format, profile.Targets.Count);
        }

        var finalResult = activateError
            ? activateResult
            : string.Format(Properties.Resources.error_format, GetWin32ErrorMessage(layoutResult));
        LogDiagnostic($"ApplyNamedProfile: FINAL result='{finalResult}'");
        return finalResult;
    }

    public static void DeleteSavedProfile(string fileName)
        => ProfileStore.Delete(fileName);

    public static string RenameSavedProfile(string fileName, string newName)
    {
        var sourcePath = GetProfilePath(fileName);
        if (!File.Exists(sourcePath))
        {
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_profile_not_found);
        }

        var safeName = SanitizeFileName(newName);
        if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(safeName))
        {
            return string.Format(Properties.Resources.error_format, "A valid profile name is required.");
        }

        var targetPath = System.IO.Path.Combine(ProfilesDir, $"{safeName}.json");
        if (!string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase) && File.Exists(targetPath))
        {
            return string.Format(Properties.Resources.error_format, "A profile with this name already exists.");
        }

        var profile = ProfileStore.Load(Path.GetFileName(sourcePath));
        if (profile == null)
        {
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_profile_empty);
        }

        profile.Name = newName.Trim();
        ProfileStore.Save(Path.GetFileName(targetPath), profile, overwrite: true);
        if (!string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(sourcePath);
        }

        return "Profile renamed.";
    }

    public static string DuplicateSavedProfile(string fileName, string newName)
    {
        var sourcePath = GetProfilePath(fileName);
        if (!File.Exists(sourcePath))
        {
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_profile_not_found);
        }

        var safeName = SanitizeFileName(newName);
        if (string.IsNullOrWhiteSpace(newName) || string.IsNullOrWhiteSpace(safeName))
        {
            return string.Format(Properties.Resources.error_format, "A valid profile name is required.");
        }

        var targetPath = System.IO.Path.Combine(ProfilesDir, $"{safeName}.json");
        if (File.Exists(targetPath))
        {
            return string.Format(Properties.Resources.error_format, "A profile with this name already exists.");
        }

        var profile = ProfileStore.Load(Path.GetFileName(sourcePath));
        if (profile == null)
        {
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_profile_empty);
        }

        profile.Name = newName.Trim();
        ProfileStore.Save(Path.GetFileName(targetPath), profile, overwrite: false);
        return "Profile duplicated.";
    }

    private static string GetProfilePath(string fileName)
    {
        var resolvedFileName = ResolveSavedProfileFileName(fileName, referenceIsFileName: true);
        return ProfileStore.GetPath(resolvedFileName ?? string.Empty);
    }

    public static (string Name, List<DisplayTargetId> Targets)? LoadNamedProfile(string fileName)
    {
        var profile = ProfileStore.Load(fileName);
        if (profile == null || string.IsNullOrEmpty(profile.Name) || profile.Targets.Count == 0)
        {
            return null;
        }

        return (profile.Name, profile.Targets);
    }

    public static string OverwriteNamedProfile(string fileName, string name, List<DisplayTargetId> targets)
    {
        if (targets.Count == 0)
        {
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_no_displays_selected);
        }

        var safeName = SanitizeFileName(name);
        var path = ProfileStore.GetPath($"{safeName}.json");
        var data = new MonitorPowerProfile
        {
            Name = name,
            Targets = targets,
            Layout = CaptureLayout(targets),
        };
        if (!TryValidateLayout(data.Layout, targets, out var validationError))
        {
            LogDiagnostic($"OverwriteNamedProfile rejected: {validationError}");
            return string.Format(Properties.Resources.error_format, Properties.Resources.error_profile_layout_invalid);
        }

        ProfileStore.Save(Path.GetFileName(path), data, overwrite: true);
        if (!string.Equals(path, GetProfilePath(fileName), StringComparison.OrdinalIgnoreCase))
        {
            ProfileStore.Delete(fileName);
        }

        return Properties.Resources.profile_saved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState(int dwUserIndex, ref XINPUT_STATE pState);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(short vKey);
}
