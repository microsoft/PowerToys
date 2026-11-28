// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests
{
    [TestClass]
    public sealed class TextExpanderBackupTests
    {
        private const string SnippetsKey = "\\TextExpander\\PowerToysTextExpanderSnippets.json";
        private const string SettingsFile = "settings.json";
        private const string SavedSnippets = """
            {"snippets":[{"trigger":"!same","replacement":"first"},{"trigger":"!same","replacement":"second"}]}
            """;

        private const string ReorderedSnippets = """
            {"snippets":[{"trigger":"!same","replacement":"second"},{"trigger":"!same","replacement":"first"}]}
            """;

        private const string BackupConfiguration = """
            {
                "IncludeFiles": ["*settings.json", "*TextExpander\\PowerToysTextExpanderSnippets.json"],
                "IgnoreFiles": [],
                "CustomRestoreSettings": {
                    "\\TextExpander\\PowerToysTextExpanderSnippets.json": {"overwrite": true}
                }
            }
            """;

        [TestMethod]
        [DataRow("[]", 0)]
        [DataRow("[{\"trigger\":\"!x\",\"replacement\":\"text\",},]", 1)]
        public void ExportAcceptsTheSnippetLibrarysSupportedJson(string snippets, int expectedCount)
        {
            WithFile("{/* user's comment */\"version\":1,\"snippets\":" + snippets + ",}", path =>
            {
                string exported = SettingsBackupAndRestoreUtils.GetExportVersion(new JsonObject(), SnippetsKey, path);
                using JsonDocument document = JsonDocument.Parse(exported);
                Assert.AreEqual(expectedCount, document.RootElement.GetProperty("snippets").GetArrayLength());
                Assert.AreEqual(1, document.RootElement.GetProperty("version").GetInt32());
            });
        }

        [TestMethod]
        public void OtherSettingsKeepTheirExistingStrictJsonRules()
        {
            WithFile("{/* comment */\"value\":1,}", path =>
                Assert.Throws<JsonException>(() => SettingsBackupAndRestoreUtils.GetExportVersion(new JsonObject(), "\\Other\\settings.json", path)));
        }

        [TestMethod]
        public void MalformedSnippetJsonIsNotExported()
        {
            WithFile("{\"snippets\":[}", path =>
                Assert.Throws<JsonException>(() => SettingsBackupAndRestoreUtils.GetExportVersion(new JsonObject(), SnippetsKey, path)));
        }

        [TestMethod]
        [DataRow("{")]
        [DataRow("{}")]
        public void BackupCarriesForwardLastGoodSnippetsWhenTheCurrentLibraryIsInvalid(string corruptLibrary)
        {
            using var folders = new BackupFolders();
            folders.WriteBackup("{\"value\":1}", SavedSnippets);
            folders.WriteCurrent(SettingsFile, "{\"value\":2}");
            folders.WriteCurrent(SnippetsKey[1..], corruptLibrary);

            var result = folders.Service.BackupSettings(folders.Current, folders.Backups, dryRun: false);

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(result.LastBackupExists);
            using ZipArchive newest = folders.OpenLatestBackup();
            using JsonDocument settings = ReadArchiveEntry(newest, SettingsFile);
            using JsonDocument snippets = ReadArchiveEntry(newest, SnippetsKey);
            Assert.AreEqual(2, settings.RootElement.GetProperty("value").GetInt32());
            Assert.AreEqual("second", snippets.RootElement.GetProperty("snippets")[1].GetProperty("replacement").GetString());
            Assert.AreEqual(corruptLibrary, File.ReadAllText(Path.Combine(folders.Current, SnippetsKey[1..])));
        }

        [TestMethod]
        public void FirstBackupStillSavesSettingsWhenTheSnippetLibraryIsInvalid()
        {
            using var folders = new BackupFolders();
            folders.WriteCurrent(SettingsFile, "{\"value\":2}");
            folders.WriteCurrent(SnippetsKey[1..], "{");

            var result = folders.Service.BackupSettings(folders.Current, folders.Backups, dryRun: false);

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsFalse(result.LastBackupExists);
            using ZipArchive backup = folders.OpenLatestBackup();
            using JsonDocument settings = ReadArchiveEntry(backup, SettingsFile);
            Assert.AreEqual(2, settings.RootElement.GetProperty("value").GetInt32());
            Assert.IsNull(backup.GetEntry(SnippetsKey[1..].Replace('\\', '/')));
        }

        [TestMethod]
        public void RestoreReplacesAInvalidLiveLibraryAndMergesOtherSettings()
        {
            using var folders = new BackupFolders();
            folders.WriteBackup("{\"value\":2}", SavedSnippets);
            folders.WriteCurrent(SettingsFile, "{\"value\":1,\"keep\":true}");
            string liveLibrary = folders.WriteCurrent(SnippetsKey[1..], "{");

            var result = folders.Service.RestoreSettings(folders.Current, folders.Backups);

            Assert.IsTrue(result.Success, result.Message);
            using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(folders.Current, SettingsFile)));
            using JsonDocument snippets = JsonDocument.Parse(File.ReadAllText(liveLibrary));
            Assert.AreEqual(2, settings.RootElement.GetProperty("value").GetInt32());
            Assert.IsTrue(settings.RootElement.GetProperty("keep").GetBoolean());
            Assert.AreEqual("second", snippets.RootElement.GetProperty("snippets")[1].GetProperty("replacement").GetString());
        }

        [TestMethod]
        public void RestoreSkipsAnInvalidArchivedLibraryAndKeepsTheLiveLibrary()
        {
            using var folders = new BackupFolders();
            folders.WriteBackup("{\"value\":2}", "{");
            folders.WriteCurrent(SettingsFile, "{\"value\":1}");
            string liveLibrary = folders.WriteCurrent(SnippetsKey[1..], SavedSnippets);

            var result = folders.Service.RestoreSettings(folders.Current, folders.Backups);

            Assert.IsTrue(result.Success, result.Message);
            using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(folders.Current, SettingsFile)));
            Assert.AreEqual(2, settings.RootElement.GetProperty("value").GetInt32());
            Assert.AreEqual(SavedSnippets, File.ReadAllText(liveLibrary));
        }

        [TestMethod]
        public void BackupAndRestoreDetectDuplicateTriggerReordering()
        {
            using var folders = new BackupFolders();
            folders.WriteBackup("{\"value\":[1,2]}", SavedSnippets);
            string currentSettings = folders.WriteCurrent(SettingsFile, "{\"value\":[2,1]}");
            string liveLibrary = folders.WriteCurrent(SnippetsKey[1..], ReorderedSnippets);

            var backup = folders.Service.BackupSettings(folders.Current, folders.Backups, dryRun: true);
            var restore = folders.Service.RestoreSettings(folders.Current, folders.Backups);

            Assert.IsTrue(backup.Success, backup.Message);
            Assert.IsTrue(restore.Success, restore.Message);
            using JsonDocument restored = JsonDocument.Parse(File.ReadAllText(liveLibrary));
            Assert.AreEqual("second", restored.RootElement.GetProperty("snippets")[1].GetProperty("replacement").GetString());
            Assert.AreEqual("{\"value\":[2,1]}", File.ReadAllText(currentSettings), "Other modules still compare arrays without regard to order.");
        }

        private static JsonDocument ReadArchiveEntry(ZipArchive archive, string relativePath)
        {
            ZipArchiveEntry entry = archive.GetEntry(relativePath.TrimStart('\\').Replace('\\', '/'));
            Assert.IsNotNull(entry);
            using Stream contents = entry.Open();
            return JsonDocument.Parse(contents);
        }

        private static void WithFile(string contents, Action<string> test)
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, contents);
                test(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private sealed class BackupFolders : IDisposable
        {
            private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("PowerToys-TextExpanderBackup-");

            public BackupFolders()
            {
                Current = _root.CreateSubdirectory("live").FullName;
                Backups = _root.CreateSubdirectory("backups").FullName;
                string staging = _root.CreateSubdirectory("staging").FullName;

                // Use the production workflow with an isolated configuration and temporary folder.
                ConstructorInfo create = typeof(SettingsBackupAndRestoreUtils).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: [typeof(Func<string>), typeof(Func<string>)],
                    modifiers: null);
                Assert.IsNotNull(create);
                Func<string> getTemporaryPath = () => staging;
                Func<string> readBackupConfiguration = () => BackupConfiguration;
                Service = (SettingsBackupAndRestoreUtils)create.Invoke([getTemporaryPath, readBackupConfiguration]);
            }

            public string Current { get; }

            public string Backups { get; }

            public SettingsBackupAndRestoreUtils Service { get; }

            public string WriteCurrent(string relativePath, string json)
            {
                string path = Path.Combine(Current, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json);
                return path;
            }

            public void WriteBackup(string settings, string snippets)
            {
                string backupId = DateTime.UtcNow.AddDays(-1).ToFileTimeUtc().ToString(CultureInfo.InvariantCulture);
                using ZipArchive archive = ZipFile.Open(Path.Combine(Backups, $"settings_{backupId}.ptb"), ZipArchiveMode.Create);
                WriteEntry(archive, SettingsFile, settings);
                WriteEntry(archive, SnippetsKey[1..].Replace('\\', '/'), snippets);
                WriteEntry(archive, "manifest.json", "{}");
            }

            public ZipArchive OpenLatestBackup()
            {
                string latest = Directory.GetFiles(Backups, "*.ptb").OrderByDescending(path => path, StringComparer.Ordinal).First();
                return ZipFile.OpenRead(latest);
            }

            public void Dispose() => _root.Delete(recursive: true);

            private static void WriteEntry(ZipArchive archive, string relativePath, string contents)
            {
                using var writer = new StreamWriter(archive.CreateEntry(relativePath).Open());
                writer.Write(contents);
            }
        }
    }
}
