// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Win32;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

public partial class Win32AppSourceTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    [DoNotParallelize]
    public void ShortcutFolderDepth_AppliesToFullScansAndIncrementalUpdates(bool scanAllSubfolders, bool cloudFolders)
    {
        var root = CreateTemporaryDirectory("cmdpal-shortcut-depth");
        var settingsPath = Path.Combine(root, "settings.json");
        var originalFile = Win32FileEnumerator.FileWrapper;
        try
        {
            var directories = new List<string> { root };
            for (var depth = 1; depth <= 4; depth++)
            {
                directories.Add(Path.Combine(directories[^1], $"Level{depth}"));
                Directory.CreateDirectory(directories[^1]);
            }

            var rootShortcut = Path.Combine(root, "Root.lnk");
            var boundaryShortcut = Path.Combine(directories[3], "Boundary.lnk");
            var deepShortcut = Path.Combine(directories[4], "Deep.lnk");
            foreach (var path in new[] { rootShortcut, boundaryShortcut, deepShortcut })
            {
                File.WriteAllText(path, string.Empty);
            }

            if (cloudFolders)
            {
                // Cloud folders are reparse points without a directory-link target.
                var file = new Mock<IFile>();
                file.Setup(value => value.GetAttributes(It.IsAny<string>())).Returns((string path) =>
                {
                    var attributes = File.GetAttributes(path);
                    return (attributes & FileAttributes.Directory) != 0 ? attributes | FileAttributes.ReparsePoint : attributes;
                });
                Win32FileEnumerator.FileWrapper = file.Object;
            }

            var settings = new AllAppsSettings(settingsPath);
            settings.Settings.Update(new JsonObject
            {
                ["apps.CustomShortcutFolders"] = new JsonArray(new JsonObject { ["value"] = root }),
                ["apps.ScanAllCustomShortcutSubfolders"] = scanAllSubfolders ? "true" : "false",
            }.ToJsonString());
            var source = Win32AppSource.CreateProgramSources(settings)
                .Single(origin => origin.Id.StartsWith("custom-shortcut:", StringComparison.Ordinal));
            var expected = scanAllSubfolders
                ? new[] { rootShortcut, boundaryShortcut, deepShortcut }
                : new[] { rootShortcut, boundaryShortcut };

            CollectionAssert.AreEquivalent(expected, source.GetPaths().ToArray());
            Assert.AreEqual(scanAllSubfolders, source.IsRelevantPath(deepShortcut));
            CollectionAssert.AreEquivalent(expected.Skip(1).ToArray(), source.GetPathsForChange(directories[2]).ToArray());
            CollectionAssert.AreEquivalent(scanAllSubfolders ? new[] { deepShortcut } : [], source.GetPathsForChange(deepShortcut).ToArray());
        }
        finally
        {
            Win32FileEnumerator.FileWrapper = originalFile;
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("apps.ScanAllDesktopSubfolders", "win32:desktop")]
    [DataRow("apps.ScanAllCustomShortcutSubfolders", "win32:custom-shortcut:")]
    public void ScanAllSubfolders_ReplacesOnlyItsAffectedSourceAndChangesCacheKey(string settingKey, string sourceIdPrefix)
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"cmdpal-shortcut-depth-settings-{Guid.NewGuid():N}.json");
        var allSources = new HashSet<IAppSource>();
        try
        {
            var settingsJson = """
                {
                  "apps.CustomShortcutFolders": [{ "value": "C:\\Shortcuts" }],
                  "apps.PortableAppFolders": [{ "value": "D:\\PortableApps" }]
                }
                """;
            File.WriteAllText(settingsPath, settingsJson);
            var settings = new AllAppsSettings(settingsPath);
            using var provider = new SettingsAppSourceProvider(settings, new EmptyAppSource());
            var initial = provider.GetSources();
            AddSources(allSources, initial);
            var affected = FindSourceByPrefix(initial, sourceIdPrefix);
            Assert.IsNotNull(affected);
            var cacheKey = affected.CacheKey;
            var changeCount = 0;
            provider.Changed += (_, _) => changeCount++;
            var form = (SettingsForm)settings.Settings.ToContent()[0];

            form.SubmitForm(new JsonObject { [settingKey] = "true" }.ToJsonString(), string.Empty);

            var unlimited = provider.GetSources();
            AddSources(allSources, unlimited);
            var unlimitedSource = FindSource(unlimited, affected.Id);
            Assert.IsNotNull(unlimitedSource);
            Assert.AreNotSame(affected, unlimitedSource);
            Assert.AreNotEqual(cacheKey, unlimitedSource.CacheKey);
            foreach (var source in initial.Where(source => source != affected))
            {
                Assert.AreSame(source, FindSource(unlimited, source.Id), source.Id);
            }

            form.SubmitForm(new JsonObject { [settingKey] = "false" }.ToJsonString(), string.Empty);

            var bounded = provider.GetSources();
            AddSources(allSources, bounded);
            Assert.AreNotSame(unlimitedSource, FindSource(bounded, affected.Id));
            foreach (var source in initial.Where(source => source != affected))
            {
                Assert.AreSame(source, FindSource(bounded, source.Id), source.Id);
            }

            Assert.AreEqual(2, changeCount);
        }
        finally
        {
            foreach (var source in allSources)
            {
                source.Dispose();
            }

            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }
}
