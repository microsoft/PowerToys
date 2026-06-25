// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CmdPal.Ext.Power.Properties;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal sealed partial class PowerPlanService
{
    internal PowerPlanSnapshot GetSnapshot()
    {
        if (!TryEnumeratePlans(out var plans))
        {
            return new PowerPlanSnapshot(
                null,
                Array.Empty<PowerPlanInfo>(),
                CanReadPlans: false,
                CanSetPlans: false);
        }

        PowerPlanInfo? activePlan = null;
        if (TryGetActivePlanGuid(out var activeGuid))
        {
            foreach (var plan in plans)
            {
                if (plan.SchemeGuid == activeGuid)
                {
                    activePlan = plan;
                    break;
                }
            }

            if (activePlan is null && TryReadFriendlyName(activeGuid, out var activeName))
            {
                activePlan = CreatePlanInfo(activeGuid, activeName);
            }
        }

        return new PowerPlanSnapshot(
            activePlan,
            plans,
            CanReadPlans: true,
            CanSetPlans: true);
    }

    internal bool TrySetActivePlan(Guid schemeGuid, out string? errorMessage)
    {
        errorMessage = null;
        var result = PowerModeNative.PowerSetActiveScheme(IntPtr.Zero, ref schemeGuid);
        if (result == PowerModeNative.ErrorSuccess)
        {
            return true;
        }

        errorMessage = Resources.power_plan_set_failed;
        return false;
    }

    private static bool TryEnumeratePlans(out List<PowerPlanInfo> plans)
    {
        plans = [];
        for (uint index = 0; ; index++)
        {
            if (!TryEnumerateSchemeGuid(index, out var schemeGuid))
            {
                break;
            }

            if (!TryReadFriendlyName(schemeGuid, out var displayName))
            {
                displayName = schemeGuid.ToString("B");
            }

            plans.Add(CreatePlanInfo(schemeGuid, displayName));
        }

        PowerPlanCatalog.SortBySpeed(plans);

        return plans.Count > 0;
    }

    private static bool TryEnumerateSchemeGuid(uint index, out Guid schemeGuid)
    {
        schemeGuid = Guid.Empty;
        var bufferSize = 0u;
        var result = PowerModeNative.PowerEnumerate(
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            PowerModeNative.AccessScheme,
            index,
            null,
            ref bufferSize);

        if (result == PowerModeNative.ErrorNoMoreItems)
        {
            return false;
        }

        if (result != PowerModeNative.ErrorSuccess && result != PowerModeNative.ErrorMoreData)
        {
            return false;
        }

        if (bufferSize == 0)
        {
            return false;
        }

        var buffer = new byte[bufferSize];
        result = PowerModeNative.PowerEnumerate(
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            PowerModeNative.AccessScheme,
            index,
            buffer,
            ref bufferSize);

        if (result != PowerModeNative.ErrorSuccess)
        {
            return false;
        }

        schemeGuid = new Guid(buffer);
        return true;
    }

    private static bool TryGetActivePlanGuid(out Guid schemeGuid)
    {
        schemeGuid = Guid.Empty;
        var result = PowerModeNative.PowerGetActiveScheme(IntPtr.Zero, out var activePolicyGuid);
        if (result != PowerModeNative.ErrorSuccess || activePolicyGuid == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            schemeGuid = Marshal.PtrToStructure<Guid>(activePolicyGuid);
            return true;
        }
        finally
        {
            PowerModeNative.LocalFree(activePolicyGuid);
        }
    }

    private static bool TryReadFriendlyName(Guid schemeGuid, out string friendlyName)
    {
        friendlyName = string.Empty;
        var scheme = schemeGuid;
        var bufferSize = 0u;
        var result = PowerModeNative.PowerReadFriendlyName(
            IntPtr.Zero,
            ref scheme,
            IntPtr.Zero,
            IntPtr.Zero,
            null,
            ref bufferSize);

        if (result != PowerModeNative.ErrorSuccess && result != PowerModeNative.ErrorMoreData)
        {
            return false;
        }

        if (bufferSize == 0)
        {
            return false;
        }

        var builder = new StringBuilder((int)(bufferSize / sizeof(char)));
        result = PowerModeNative.PowerReadFriendlyName(
            IntPtr.Zero,
            ref scheme,
            IntPtr.Zero,
            IntPtr.Zero,
            builder,
            ref bufferSize);

        if (result != PowerModeNative.ErrorSuccess)
        {
            return false;
        }

        friendlyName = builder.ToString();
        return friendlyName.Length > 0;
    }

    private static PowerPlanInfo CreatePlanInfo(Guid schemeGuid, string displayName)
    {
        var description = PowerPlanCatalog.TryGetKnownDescription(schemeGuid, out var knownDescription)
            ? knownDescription
            : string.Empty;

        return new PowerPlanInfo(schemeGuid, displayName, description);
    }
}
