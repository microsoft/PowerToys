// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Mxc.Sdk;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.Worker;

internal static class BackendDiscovery
{
    public static BackendAvailability Get()
    {
        var support = MxcSandbox.GetPlatformSupport();
        var available = MxcSandbox.GetAvailableBackends();
        var windows = support.AvailableMethods.Contains(ContainmentBackend.ProcessContainer);
        var linux = support.AvailableMethods.Contains(ContainmentBackend.Wslc);
        var tier = available.FirstOrDefault(backend => backend.Backend == ContainmentBackend.ProcessContainer)?.Tier;
        return new BackendAvailability(
            windows,
            linux,
            windows ? $"Windows · MXC ProcessContainer (host capability: {tier}; actual policy support is checked at launch)." : support.Reason ?? "Windows ProcessContainer is unavailable.",
            linux ? "Linux · MXC WSLC. Requires a prepared image containing your application's runtime." : "Linux WSLC is unavailable. Build with MxcWithWslc=true and install a compatible WSL runtime. No ordinary WSL execution will be substituted.",
            MxcSandbox.NativeVersion);
    }
}
