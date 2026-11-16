// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace CoreWidgetProvider.Helpers;

/// <summary>
/// Reads WDDM adapter temperature through the graphics kernel's performance data API.
/// No vendor SDK or additional kernel driver is required.
/// </summary>
internal static partial class GpuTemperature
{
    // KMTQUERYADAPTERINFOTYPE in the Windows SDK's d3dkmthk.h (WDDM 2.4+).
    private const int AdapterPerfDataType = 62;

    internal static unsafe float? ReadCelsius(long luidKey)
    {
        var open = new OpenAdapterFromLuid
        {
            LowPart = unchecked((uint)luidKey),
            HighPart = unchecked((int)(luidKey >> 32)),
        };

        if (D3DKMTOpenAdapterFromLuid(ref open) < 0)
        {
            return null;
        }

        try
        {
            // GPUStats has one entry per LUID. For linked display adapters this
            // reads the first physical adapter, rather than mixing temperatures.
            AdapterPerfData data = default;
            var query = new QueryAdapterInfo
            {
                AdapterHandle = open.AdapterHandle,
                Type = AdapterPerfDataType,
                PrivateDriverData = &data,
                PrivateDriverDataSize = (uint)sizeof(AdapterPerfData),
            };

            return D3DKMTQueryAdapterInfo(ref query) < 0 ? null : ToCelsius(data.Temperature);
        }
        finally
        {
            // Reopen on each sample so device removal/driver resets cannot leave
            // a cached stale handle. D3DKMT handles must not use CloseHandle.
            var close = new CloseAdapter { AdapterHandle = open.AdapterHandle };
            _ = D3DKMTCloseAdapter(ref close);
        }
    }

    internal static float? ToCelsius(uint temperature)
    {
        // Some drivers succeed but leave unsupported sensor fields zero. Reject
        // those and implausible/sentinel values; the API uses tenths of Celsius.
        return temperature is > 0 and <= 2000 ? temperature / 10f : null;
    }

    // Blittable SDK layouts used with source-generated P/Invoke for Native AOT.
    // D3DKMT_HANDLE is a 32-bit value, even in a 64-bit process.
    [StructLayout(LayoutKind.Sequential)]
    internal struct OpenAdapterFromLuid
    {
        internal uint LowPart;
        internal int HighPart;
        internal uint AdapterHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct QueryAdapterInfo
    {
        internal uint AdapterHandle;
        internal int Type;
        internal void* PrivateDriverData;
        internal uint PrivateDriverDataSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    internal struct AdapterPerfData
    {
        internal uint PhysicalAdapterIndex;
        internal ulong MemoryFrequency;
        internal ulong MaxMemoryFrequency;
        internal ulong MaxMemoryFrequencyOC;
        internal ulong MemoryBandwidth;
        internal ulong PCIEBandwidth;
        internal uint FanRPM;
        internal uint Power;
        internal uint Temperature;
        internal byte PowerStateOverride;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CloseAdapter
    {
        internal uint AdapterHandle;
    }

    [LibraryImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int D3DKMTOpenAdapterFromLuid(ref OpenAdapterFromLuid data);

    [LibraryImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int D3DKMTQueryAdapterInfo(ref QueryAdapterInfo data);

    [LibraryImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int D3DKMTCloseAdapter(ref CloseAdapter data);
}
