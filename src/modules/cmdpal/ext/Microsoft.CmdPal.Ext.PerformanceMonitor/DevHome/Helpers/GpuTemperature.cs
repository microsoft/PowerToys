// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Wdk.Graphics.Direct3D;
using Windows.Win32.Foundation;
using WdkPInvoke = Windows.Wdk.PInvoke;

namespace CoreWidgetProvider.Helpers;

/// <summary>
/// Reads GPU temperatures the way Task Manager does: from the adapter performance data that display
/// drivers report through D3DKMT (WDDM 2.4 and later). The "GPU Engine" performance counters that
/// <see cref="GPUStats"/> reads for utilization don't include temperature.
/// </summary>
internal static class GpuTemperature
{
    /// <summary>
    /// Returns the temperature of the adapter with the given LUID key in degrees Celsius, or 0 if it
    /// isn't available, such as for the Microsoft Basic Render Driver or a driver that doesn't report one.
    /// </summary>
    internal static unsafe float Read(long luidKey)
    {
        var open = new D3DKMT_OPENADAPTERFROMLUID { AdapterLuid = ToLuid(luidKey) };
        if (WdkPInvoke.D3DKMTOpenAdapterFromLuid(ref open).Value < 0)
        {
            return 0;
        }

        try
        {
            // Adapters linked together (SLI or CrossFire) share a LUID and are told apart by their
            // physical adapter index. This reads the first, which is the only one on unlinked GPUs.
            var perfData = new D3DKMT_ADAPTER_PERFDATA { PhysicalAdapterIndex = 0 };
            var query = new D3DKMT_QUERYADAPTERINFO
            {
                hAdapter = open.hAdapter,
                Type = KMTQUERYADAPTERINFOTYPE.KMTQAITYPE_ADAPTERPERFDATA,
                pPrivateDriverData = &perfData,
                PrivateDriverDataSize = (uint)sizeof(D3DKMT_ADAPTER_PERFDATA),
            };

            return WdkPInvoke.D3DKMTQueryAdapterInfo(ref query).Value < 0 ? 0 : FromDeciCelsius(perfData.Temperature);
        }
        finally
        {
            _ = WdkPInvoke.D3DKMTCloseAdapter(new D3DKMT_CLOSEADAPTER { hAdapter = open.hAdapter });
        }
    }

    /// <summary>Converts the driver's temperature, in tenths of a degree Celsius, to degrees.</summary>
    internal static float FromDeciCelsius(uint deciCelsius) => deciCelsius / 10f;

    /// <summary>
    /// The inverse of the LUID key that <see cref="GpuAdapterNames"/> builds from DXGI and
    /// <see cref="GPUStats"/> parses from counter names: <c>(HighPart &lt;&lt; 32) | LowPart</c>.
    /// </summary>
    internal static LUID ToLuid(long luidKey) => new() { LowPart = (uint)luidKey, HighPart = (int)(luidKey >> 32) };
}
