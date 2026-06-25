// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static partial class PowerModeNative
{
    internal const uint ErrorSuccess = 0;

    [DllImport("powrprof.dll", ExactSpelling = true)]
    internal static extern uint PowerGetUserConfiguredACPowerMode(out Guid powerModeGuid);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    internal static extern uint PowerGetUserConfiguredDCPowerMode(out Guid powerModeGuid);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    internal static extern uint PowerSetUserConfiguredACPowerMode(ref Guid powerModeGuid);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    internal static extern uint PowerSetUserConfiguredDCPowerMode(ref Guid powerModeGuid);

    [DllImport("powrprof.dll", EntryPoint = "PowerGetActualOverlayScheme")]
    internal static extern uint PowerGetActualOverlayScheme(out Guid actualOverlayGuid);

    [DllImport("powrprof.dll", EntryPoint = "PowerGetEffectiveOverlayScheme")]
    internal static extern uint PowerGetEffectiveOverlayScheme(out Guid effectiveOverlayGuid);

    [DllImport("powrprof.dll", EntryPoint = "PowerSetActiveOverlayScheme")]
    internal static extern uint PowerSetActiveOverlayScheme(ref Guid overlaySchemeGuid);

    internal const uint AccessScheme = 16;

    internal const uint ErrorMoreData = 234;

    internal const uint ErrorNoMoreItems = 259;

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    internal static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    internal static extern uint PowerSetActiveScheme(IntPtr userRootPowerKey, ref Guid schemeGuid);

    [DllImport("powrprof.dll", SetLastError = true)]
    internal static extern uint PowerEnumerate(
        IntPtr rootPowerKey,
        IntPtr schemeGuid,
        IntPtr subGroupOfPowerSettingsGuid,
        uint accessFlags,
        uint index,
        byte[]? buffer,
        ref uint bufferSize);

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint PowerReadFriendlyName(
        IntPtr rootPowerKey,
        ref Guid schemeGuid,
        IntPtr subGroupOfPowerSettingsGuid,
        IntPtr powerSettingGuid,
        StringBuilder? buffer,
        ref uint bufferSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern IntPtr LocalFree(IntPtr hMem);
}
