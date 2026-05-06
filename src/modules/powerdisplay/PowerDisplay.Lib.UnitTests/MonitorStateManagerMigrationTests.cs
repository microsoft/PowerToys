// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MonitorStateManagerMigrationTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"powerdisplay-monitorstate-test-{System.Guid.NewGuid():N}.json");

    [TestMethod]
    public void MigrateMonitorIds_RewritesKeys()
    {
        var path = TempPath();
        try
        {
            using var manager = new MonitorStateManager(path);
            manager.UpdateMonitorParameter("DDC_DELD1A8_1", "Brightness", 42);

            var rewrites = new Dictionary<string, string>
            {
                { "DDC_DELD1A8_1", @"\\?\DISPLAY#DELD1A8#5&abc&0&UID1" },
            };

            int n = manager.MigrateMonitorIds(rewrites);

            Assert.AreEqual(1, n);
            Assert.IsNull(manager.GetMonitorParameters("DDC_DELD1A8_1"));
            var migrated = manager.GetMonitorParameters(@"\\?\DISPLAY#DELD1A8#5&abc&0&UID1");
            Assert.IsNotNull(migrated);
            Assert.AreEqual(42, migrated.Value.Brightness);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [TestMethod]
    public void MigrateMonitorIds_NoMatchingEntries_ReturnsZero()
    {
        var path = TempPath();
        try
        {
            using var manager = new MonitorStateManager(path);
            manager.UpdateMonitorParameter("WMI_BOE0900_1", "Brightness", 50);

            var rewrites = new Dictionary<string, string>
            {
                { "DDC_DELD1A8_1", "irrelevant" },
            };

            int n = manager.MigrateMonitorIds(rewrites);

            Assert.AreEqual(0, n);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
