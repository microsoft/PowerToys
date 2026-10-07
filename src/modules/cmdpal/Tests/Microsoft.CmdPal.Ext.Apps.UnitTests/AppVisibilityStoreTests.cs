// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppVisibilityStoreTests
{
    [TestMethod]
    public void VisibilityStore_PersistsHiddenIdentityWithoutRewritingPreferences()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            const string identity = "win32:stable-identity";
            const string preferences = "{\"futureSetting\":\"preserved\"}";
            File.WriteAllText(settingsPath, preferences);
            var store = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(settingsPath));
            Assert.IsTrue(store.SetSnapshot(new HashSet<string> { identity }));
            store.Persist();

            Assert.AreEqual(preferences, File.ReadAllText(settingsPath));
            Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(settingsPath)!, $"{Path.GetFileName(store.FilePath)}.*.tmp").Length);
            var reloaded = new AppVisibilityStore(store.FilePath);
            Assert.IsTrue(reloaded.GetSnapshot().Contains(identity));
            Assert.IsTrue(reloaded.SetSnapshot(FrozenSet<string>.Empty));
            reloaded.Persist();
            Assert.AreEqual(0, new AppVisibilityStore(store.FilePath).GetSnapshot().Count);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void VisibilityStore_CopiesInputsAndPreservesEarlierSnapshots()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            var store = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(settingsPath));
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "win32:first" };
            Assert.IsTrue(store.SetSnapshot(identities));
            var first = store.GetSnapshot();
            identities.Clear();

            Assert.IsTrue(first.Contains("WIN32:FIRST"));
            Assert.IsFalse(store.SetSnapshot(new HashSet<string> { "WIN32:FIRST" }));
            Assert.AreSame(first, store.GetSnapshot());
            Assert.IsTrue(store.SetSnapshot(new HashSet<string> { "win32:second" }));
            Assert.IsTrue(first.Contains("win32:first"));
            Assert.IsFalse(first.Contains("win32:second"));
            Assert.IsFalse(File.Exists(store.FilePath), "Replacing in-memory data must not persist it implicitly.");
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void VisibilityStore_LoadsItsOwnFileWithoutLoadingPreferences()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            const string preferences = "invalid preferences";
            File.WriteAllText(settingsPath, preferences);
            TestDataHelper.WriteHiddenIdentities(settingsPath, "win32:hidden");
            var store = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(settingsPath));

            Assert.IsTrue(store.GetSnapshot().Contains("win32:hidden"));
            Assert.AreEqual(preferences, File.ReadAllText(settingsPath));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    public void VisibilityStore_RestartLoadsTheUpdatedHiddenIdentitySet()
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            TestDataHelper.WriteHiddenIdentities(settingsPath, "win32:first");
            var store = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(settingsPath));
            Assert.IsTrue(store.GetSnapshot().Contains("win32:first"));

            TestDataHelper.WriteHiddenIdentities(settingsPath, "win32:second");
            var restarted = new AppVisibilityStore(store.FilePath);
            Assert.IsFalse(restarted.GetSnapshot().Contains("win32:first"));
            Assert.IsTrue(restarted.GetSnapshot().Contains("win32:second"));
            Assert.IsTrue(store.GetSnapshot().Contains("win32:first"));
            Assert.IsFalse(File.Exists(settingsPath));
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("{\"HiddenAppIdentities\":null}")]
    [DataRow("{\"HiddenAppIdentities\":[42]}")]
    [DataRow("{\"HiddenAppIdentities\":[null]}")]
    [DataRow("{\"HiddenAppIdentities\":[\" \"]}")]
    public void VisibilityStore_UnreadableContentIsPreservedUntilRestart(string content)
    {
        var settingsPath = TemporarySettingsPath();
        try
        {
            File.WriteAllText(TestDataHelper.GetVisibilityPath(settingsPath), content);
            var store = new AppVisibilityStore(TestDataHelper.GetVisibilityPath(settingsPath));
            Assert.ThrowsExactly<InvalidOperationException>(() => store.SetSnapshot(new HashSet<string> { "win32:hidden" }));
            Assert.AreEqual(0, store.GetSnapshot().Count);
            Assert.ThrowsExactly<InvalidOperationException>(store.Persist);
            Assert.AreEqual(content, File.ReadAllText(store.FilePath));

            TestDataHelper.WriteHiddenIdentities(settingsPath, "win32:hidden");
            var restarted = new AppVisibilityStore(store.FilePath);
            Assert.IsTrue(restarted.GetSnapshot().Contains("win32:hidden"));
            Assert.IsTrue(restarted.SetSnapshot(FrozenSet<string>.Empty));
            restarted.Persist();
            Assert.AreEqual(0, JsonNode.Parse(File.ReadAllText(store.FilePath))!["HiddenAppIdentities"]!.AsArray().Count);
        }
        finally
        {
            TestDataHelper.DeleteSettingsFiles(settingsPath);
        }
    }

    private static string TemporarySettingsPath()
    {
        return Path.Combine(Path.GetTempPath(), $"cmdpal-apps-visibility-{Guid.NewGuid():N}.json");
    }
}
