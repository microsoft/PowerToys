// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Settings.UI.UnitTests.SettingsBackupAndSyncUtils
{
    [TestClass]
    public class AllTest
    {
        [TestMethod]
        [DataRow("\\settings.json", "C:\\Users\\jeff\\AppData\\Local\\Microsoft\\PowerToys\\settings.json")]
        public void GetExportVerion(string settingFileKey, string settingsFileName)
        {
            var results = Settings.UI.Library.SettingsBackupAndSyncUtils.GetExportVerion(settingFileKey, settingsFileName);

            var settngsDoc = JObject.Parse(results);

            // Assert.IsTrue(results.success, $"Failed, restore failed: {results.message}.");
        }

        [TestMethod]
        [DataRow("__fakeBackupDir__", "__fakeSettingsDir__")]
        [DataRow(@"C:\Users\jeff\AppData\Local\Microsoft\PowerToys", @"C:\Temp\PowerToysBackup")]
        public void BackupSettings(string appBasePath, string settingsBackupAndSyncDir)
        {
            var results = Settings.UI.Library.SettingsBackupAndSyncUtils.BackupSettings(appBasePath, settingsBackupAndSyncDir);

            if (appBasePath == "__fakeBackupDir__")
            {
                Assert.IsTrue(!results.success, "Failed to detect bad folders.");
                return;
            }

            if (settingsBackupAndSyncDir == "__fakeSettingsDir__")
            {
                Assert.IsTrue(!results.success, "Failed to detect bad folders.");
                return;
            }

            Assert.IsTrue(results.success, $"Failed, backup failed: {results.message}.");
        }

        [TestMethod]
        [DataRow("__fakeBackupDir__", "__fakeSettingsDir__")]
        [DataRow(@"C:\Users\jeff\AppData\Local\Microsoft\PowerToys", @"C:\Temp\PowerToysBackup")]
        public void RestoreSettings(string appBasePath, string settingsBackupAndSyncDir)
        {
            var results = Settings.UI.Library.SettingsBackupAndSyncUtils.RestoreSettings(appBasePath, settingsBackupAndSyncDir);

            if (appBasePath == "__fakeBackupDir__")
            {
                Assert.IsTrue(!results.success, "Failed to detect bad folders.");
                return;
            }

            if (settingsBackupAndSyncDir == "__fakeSettingsDir__")
            {
                Assert.IsTrue(!results.success, "Failed to detect bad folders.");
                return;
            }

            Assert.IsTrue(results.success, $"Failed, restore failed: {results.message}.");
        }
    }
}
