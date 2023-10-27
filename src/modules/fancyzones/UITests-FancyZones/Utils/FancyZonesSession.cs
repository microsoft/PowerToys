// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.FancyZones.UnitTests.Utils
{
    public class FancyZonesSession
    {
        private const string FancyZonesPath = @"\..\..\..\PowerToys.FancyZones.exe";
        private const string FancyZonesProcessName = "PowerToys.FancyZones";

        private bool stopFancyZones = true;

        public Process? FancyZonesProcess { get; }

        public FancyZonesSession(TestContext testContext)
        {
            try
            {
                // Check if FancyZones is already running
                Process[] runningFZ = Process.GetProcessesByName(FancyZonesProcessName);
                if (runningFZ.Length > 0)
                {
                    FancyZonesProcess = runningFZ[0];
                    stopFancyZones = false;
                }
                else
                {
                    // Launch FancyZones
                    string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    path += FancyZonesPath;

                    ProcessStartInfo info = new ProcessStartInfo(path);
                    FancyZonesProcess = Process.Start(info);
                }
            }
            catch (Exception ex)
            {
                testContext.WriteLine(ex.Message);
            }

            Assert.IsNotNull(FancyZonesProcess, "FancyZones process not started");
        }

        public void Close()
        {
            // Close the application
            if (FancyZonesProcess != null)
            {
                if (stopFancyZones)
                {
                    FancyZonesProcess.Kill();
                }

                FancyZonesProcess.Close();
                FancyZonesProcess.Dispose();
            }
        }
    }
}
