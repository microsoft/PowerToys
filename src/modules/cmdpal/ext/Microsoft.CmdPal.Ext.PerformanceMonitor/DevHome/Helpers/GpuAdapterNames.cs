// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Common;
using Windows.Win32;
using Windows.Win32.Graphics.Dxgi;

namespace CoreWidgetProvider.Helpers;

/// <summary>
/// Resolves friendly GPU adapter names (and whether an adapter is a software
/// renderer) keyed by adapter LUID, using DXGI.
///
/// The "GPU Engine" performance counters identify each physical adapter by its
/// LUID, but not by name, so we enumerate DXGI adapters once and match them up.
/// We can't use WMI for this (it isn't AOT-compatible), but DXGI via CsWin32 is.
/// </summary>
internal static class GpuAdapterNames
{
    internal readonly record struct AdapterInfo(string Description, bool IsSoftware);

    /// <summary>
    /// Enumerates the system's DXGI adapters and returns their descriptions keyed
    /// by LUID. The key matches <see cref="GPUStats"/>'s LUID parsing:
    /// <c>(HighPart &lt;&lt; 32) | LowPart</c>. Returns an empty map on any failure;
    /// callers fall back to generic names.
    /// </summary>
    internal static unsafe Dictionary<long, AdapterInfo> GetByLuid()
    {
        var adapters = new Dictionary<long, AdapterInfo>();

        IDXGIFactory1* factory = null;

        try
        {
            if (PInvoke.CreateDXGIFactory1(IDXGIFactory1.IID_Guid, out var factoryPtr).Failed || factoryPtr is null)
            {
                return adapters;
            }

            factory = (IDXGIFactory1*)factoryPtr;

            for (uint index = 0; ; index++)
            {
                IDXGIAdapter1* adapter = null;

                // EnumAdapters1 returns DXGI_ERROR_NOT_FOUND once we walk past the
                // last adapter, which surfaces here as a failed HRESULT.
                if (factory->EnumAdapters1(index, &adapter).Failed || adapter is null)
                {
                    break;
                }

                try
                {
                    DXGI_ADAPTER_DESC1 desc = default;
                    if (adapter->GetDesc1(&desc).Failed)
                    {
                        continue;
                    }

                    var luidKey = ((long)(uint)desc.AdapterLuid.HighPart << 32) | desc.AdapterLuid.LowPart;
                    var isSoftware = (desc.Flags & DXGI_ADAPTER_FLAG.DXGI_ADAPTER_FLAG_SOFTWARE) != 0;

                    // __char_128.ToString() is generated as
                    // AsReadOnlySpan().SliceAtNull().ToString(), so it already
                    // stops at the null terminator and yields the friendly name.
                    var description = desc.Description.ToString();

                    adapters[luidKey] = new AdapterInfo(description, isSoftware);
                }
                finally
                {
                    adapter->Release();
                }
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to enumerate DXGI adapters for GPU names.", ex);
        }
        finally
        {
            if (factory is not null)
            {
                factory->Release();
            }
        }

        return adapters;
    }
}
