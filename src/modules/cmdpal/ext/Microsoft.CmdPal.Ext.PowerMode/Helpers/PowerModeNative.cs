// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.PowerMode.Helpers;

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
}
