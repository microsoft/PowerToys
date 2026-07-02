// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power.Classes;
using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static class PowerModeDisplayHelper
{
    internal static string GetUserModeLabel(UserPowerMode mode) =>
        PowerModeCatalog.GetDefinition(mode).Label;

    internal static string GetUserModeShortLabel(UserPowerMode mode) =>
        PowerModeCatalog.GetDefinition(mode).ShortLabel;

    internal static string GetStatusSubtitle(PowerModeSnapshot snapshot)
    {
        if (!snapshot.CanReadUserMode)
        {
            return Resources.power_mode_not_supported;
        }

        return GetUserModeLabel(snapshot.UserMode);
    }

    internal static ITag[] GetModeItemTags(UserPowerMode mode, PowerModeSnapshot snapshot)
    {
        if (snapshot.CanReadUserMode && snapshot.UserMode == mode)
        {
            return [new Tag(Resources.power_list_current)];
        }

        return [];
    }
}
