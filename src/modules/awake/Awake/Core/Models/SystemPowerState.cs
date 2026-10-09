// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Awake.Core.Models
{
    /// <summary>
    /// Represents the system power state.
    /// </summary>
    /// <remarks>
    /// See <see href="https://learn.microsoft.com/windows/win32/power/system-power-states">System power states</see>.
    /// </remarks>
    public enum SystemPowerState
    {
        PowerSystemUnspecified = 0,
        PowerSystemWorking = 1,
        PowerSystemSleeping1 = 2,
        PowerSystemSleeping2 = 3,
        PowerSystemSleeping3 = 4,
        PowerSystemHibernate = 5,
        PowerSystemShutdown = 6,
        PowerSystemMaximum = 7,
    }
}
