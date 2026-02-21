// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PowerToys.Interop;

namespace RunnerV2.Helpers
{
    internal static class AutoStartHelper
    {
        internal static void SetAutoStartState(bool enabled)
        {
            bool isActive = AutoStart.IsAutoStartTaskActiveForThisUser();
            if (isActive && enabled)
            {
                return;
            }

            if (!isActive && !enabled)
            {
                return;
            }

            if (isActive && !enabled)
            {
                AutoStart.DeleteAutoStartTaskForThisUser();
            }
            else if (!isActive && enabled)
            {
                AutoStart.CreateAutoStartTaskForThisUser(ElevationHelper.IsProcessElevated());
            }
        }
    }
}
