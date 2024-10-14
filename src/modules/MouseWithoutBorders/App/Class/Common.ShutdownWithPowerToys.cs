// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using ManagedCommon;

using Logger = MouseWithoutBorders.Core.Logger;

namespace MouseWithoutBorders
{
    internal class ShutdownWithPowerToys
    {
        public static void WaitForPowerToysRunner()
        {
            try
            {
                RunnerHelper.WaitForPowerToysRunnerExitFallback(() =>
                    {
                        Common.MainForm.Quit(true, false);
                    });
            }
            catch (Exception e)
            {
                Logger.Log(e);
            }
        }
    }
}
