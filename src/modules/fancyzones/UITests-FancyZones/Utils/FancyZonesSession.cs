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
    public class FancyZonesSession : IDisposable
    {
        private const string FancyZonesPath = @"\..\..\..\PowerToys.FancyZones.exe";

        public Process? FancyZonesProcess { get; }

        public FancyZonesSession(TestContext testContext)
        {
            try
            {
                // Launch FancyZones
                string? path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                path += FancyZonesPath;

                ProcessStartInfo info = new ProcessStartInfo(path);
                FancyZonesProcess = Process.Start(info);
            }
            catch (Exception ex)
            {
                testContext.WriteLine(ex.Message);
            }

            Assert.IsNotNull(FancyZonesProcess);
        }

        public void Dispose()
        {
            // Close the application
            if (FancyZonesProcess != null)
            {
                FancyZonesProcess.Kill();
                FancyZonesProcess.Close();
                FancyZonesProcess.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }
}
