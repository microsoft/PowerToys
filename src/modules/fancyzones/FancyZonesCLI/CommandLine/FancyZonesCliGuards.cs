// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace FancyZonesCLI.CommandLine;

internal static class FancyZonesCliGuards
{
    public static bool IsFancyZonesRunning()
    {
        try
        {
            return Process.GetProcessesByName("PowerToys.FancyZones").Length != 0;
        }
        catch
        {
            return false;
        }
    }
}
