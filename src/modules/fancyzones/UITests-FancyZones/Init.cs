// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class Init
    {
        private static Process? appDriver;

        [AssemblyInitialize]
        public static void SetupAll(TestContext context)
        {
            string winAppDriverPath = "C:\\Program Files (x86)\\Windows Application Driver\\WinAppDriver.exe";
            context.WriteLine($"Attempting to launch WinAppDriver at: {winAppDriverPath}");
            appDriver = Process.Start(winAppDriverPath);
        }

        [AssemblyCleanup]
        public static void CleanupAll()
        {
            try
            {
                appDriver?.Kill();
            }
            catch
            {
            }
        }
    }
}
