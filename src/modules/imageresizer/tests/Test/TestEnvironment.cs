// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using ImageResizer.Helpers;
using ImageResizer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Test
{
    [TestClass]
    public sealed class TestEnvironment
    {
        private static TestDirectory _settingsDirectory;

        [AssemblyInitialize]
        public static void Initialize(TestContext context)
        {
            // CLI validation tests may initialize Settings.Default. Keep all test
            // settings and its watcher away from the running user's configuration.
            _settingsDirectory = new TestDirectory();
            Settings.SettingsPath = Path.Combine(_settingsDirectory, "settings.json");
            ResourceLoaderInstance.GetString = key => key;
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            _settingsDirectory?.Dispose();
        }
    }
}
