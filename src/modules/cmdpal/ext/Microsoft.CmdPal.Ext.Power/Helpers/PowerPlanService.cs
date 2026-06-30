// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CmdPal.Ext.Power.Classes;
using Microsoft.CmdPal.Ext.Power.Properties;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;

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
        if (PInvoke.PowerSetActiveScheme(null, schemeGuid) == WIN32_ERROR.NO_ERROR)
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
        var result = PInvoke.PowerEnumerate(
            null,
            null,
            null,
            POWER_DATA_ACCESSOR.ACCESS_SCHEME,
            index,
            default,
            ref bufferSize);

        if (result == WIN32_ERROR.ERROR_NO_MORE_ITEMS)
        {
            return false;
        }

        if (result != WIN32_ERROR.NO_ERROR && result != WIN32_ERROR.ERROR_MORE_DATA)
        {
            return false;
        }

        if (bufferSize == 0)
        {
            return false;
        }

        var buffer = new byte[bufferSize];
        result = PInvoke.PowerEnumerate(
            null,
            null,
            null,
            POWER_DATA_ACCESSOR.ACCESS_SCHEME,
            index,
            buffer,
            ref bufferSize);

        if (result != WIN32_ERROR.NO_ERROR)
        {
            return false;
        }

        schemeGuid = new Guid(buffer);
        return true;
    }

    private static bool TryGetActivePlanGuid(out Guid schemeGuid)
    {
        schemeGuid = Guid.Empty;
        unsafe
        {
            if (PInvoke.PowerGetActiveScheme(null, out Guid* activePolicyGuid) != WIN32_ERROR.NO_ERROR)
            {
                return false;
            }

            try
            {
                schemeGuid = *activePolicyGuid;
                return true;
            }
            finally
            {
                _ = PInvoke.LocalFree((HLOCAL)activePolicyGuid);
            }
        }
    }

    private static bool TryReadFriendlyName(Guid schemeGuid, out string friendlyName)
    {
        friendlyName = string.Empty;
        var bufferSize = 0u;
        var result = PInvoke.PowerReadFriendlyName(
            null,
            schemeGuid,
            null,
            null,
            default,
            ref bufferSize);

        if (result != WIN32_ERROR.NO_ERROR && result != WIN32_ERROR.ERROR_MORE_DATA)
        {
            return false;
        }

        if (bufferSize == 0)
        {
            return false;
        }

        var buffer = new byte[bufferSize];
        result = PInvoke.PowerReadFriendlyName(
            null,
            schemeGuid,
            null,
            null,
            buffer,
            ref bufferSize);

        if (result != WIN32_ERROR.NO_ERROR)
        {
            return false;
        }

        friendlyName = Encoding.Unicode.GetString(buffer.AsSpan(0, (int)bufferSize)).TrimEnd('\0');
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
