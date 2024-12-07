﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using MouseWithoutBorders.Class;

using Logger = MouseWithoutBorders.Core.Logger;

namespace MouseWithoutBorders
{
    internal class ShutdownWithPowerToys
    {
        public static void WaitForPowerToysRunner(ETWTrace etwTrace)
        {
            try
            {
                RunnerHelper.WaitForPowerToysRunnerExitFallback(() =>
                    {
                        etwTrace?.Dispose();
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
