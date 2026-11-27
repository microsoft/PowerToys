// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using KeyboardManagerEditorUI.Helpers;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// A live reload (the engine switched profiles, a Settings restore rewrote the files) must not
    /// destroy anything: rows the user turned off, and other profiles' rows, exist only in
    /// editorSettings.json. When that file cannot be read right now the reload changes nothing and
    /// is tried again; only a file that is not there is created from the active mappings. Every
    /// test runs in its own folder under the test root, never in the live Keyboard Manager folder.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class EditorSettingsLiveReloadTests
    {
        private const string TurnedOffId = "00000000-0000-0000-0000-0000000000aa";

        private string _directory = string.Empty;
        private EditorSettings _originalEditorSettings = new();

        private string EditorSettingsPath => Path.Combine(_directory, "editorSettings.json");

        private static List<ShortcutKeyMapping> NoActiveMappings => new();

        [TestInitialize]
        public void SetUp()
        {
            _directory = Path.Combine(UnitTestModuleInitializer.TestRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            KeyboardManagerPaths.RedirectForTests(_directory);
            _originalEditorSettings = SettingsManager.EditorSettings;

            // A file with one row that is turned off: it is in no native profile file, so it cannot
            // be rebuilt from the active mappings.
            var settings = new EditorSettings
            {
                ShortcutSettingsDictionary = new Dictionary<string, ShortcutSettings>
                {
                    [TurnedOffId] = new ShortcutSettings
                    {
                        Id = TurnedOffId,
                        IsActive = false,
                        Shortcut = new ShortcutKeyMapping { OperationType = ShortcutOperationType.RemapShortcut, OriginalKeys = "112", TargetKeys = "113" },
                    },
                },
            };
            SettingsManager.NormalizeSettings(settings);
            Assert.IsTrue(SettingsManager.WriteSettings(settings));
            Assert.IsTrue(SettingsManager.TryReloadSettings());
            Assert.IsTrue(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey(TurnedOffId));
        }

        [TestCleanup]
        public void TearDown()
        {
            SettingsManager.EditorSettings = _originalEditorSettings;
            KeyboardManagerPaths.RedirectForTests(UnitTestModuleInitializer.TestRoot);
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        [TestMethod]
        public void TryReloadSettings_HalfWrittenFile_ReportsUnreadable_AndKeepsTheLoadedSettings()
        {
            const string halfWritten = "{ \"ShortcutSettingsDictionary\": ";
            File.WriteAllText(EditorSettingsPath, halfWritten);

            Assert.IsFalse(SettingsManager.TryReloadSettings(out SettingsManager.SettingsReadResult result));

            Assert.AreEqual(SettingsManager.SettingsReadResult.Unreadable, result);
            Assert.IsTrue(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey(TurnedOffId));
            Assert.AreEqual(halfWritten, File.ReadAllText(EditorSettingsPath));
        }

        [TestMethod]
        public void TryReloadSettings_FileHeldByAWriter_ReportsUnreadable_AndKeepsTheLoadedSettings()
        {
            using (new FileStream(EditorSettingsPath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                Assert.IsFalse(SettingsManager.TryReloadSettings(out SettingsManager.SettingsReadResult result));
                Assert.AreEqual(SettingsManager.SettingsReadResult.Unreadable, result);
            }

            Assert.IsTrue(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey(TurnedOffId));
        }

        [TestMethod]
        public void TryReloadSettings_NoFile_ReportsMissing()
        {
            File.Delete(EditorSettingsPath);

            Assert.IsFalse(SettingsManager.TryReloadSettings(out SettingsManager.SettingsReadResult result));

            Assert.AreEqual(SettingsManager.SettingsReadResult.Missing, result);
        }

        // The rule itself: a live reload (false) never rebuilds an unreadable file.
        [TestMethod]
        public void ShouldRegenerateSettings_OnlyForAMissingFile_UnlessTheCallerAllowsUnreadable()
        {
            Assert.IsTrue(SettingsManager.ShouldRegenerateSettings(SettingsManager.SettingsReadResult.Missing, regenerateUnreadableSettings: false));
            Assert.IsTrue(SettingsManager.ShouldRegenerateSettings(SettingsManager.SettingsReadResult.Missing, regenerateUnreadableSettings: true));
            Assert.IsFalse(SettingsManager.ShouldRegenerateSettings(SettingsManager.SettingsReadResult.Unreadable, regenerateUnreadableSettings: false));
            Assert.IsTrue(SettingsManager.ShouldRegenerateSettings(SettingsManager.SettingsReadResult.Unreadable, regenerateUnreadableSettings: true));
            Assert.IsFalse(SettingsManager.ShouldRegenerateSettings(SettingsManager.SettingsReadResult.Loaded, regenerateUnreadableSettings: true));
        }

        // The case: the file is held by another writer (a Settings restore) at the moment of
        // the live reload. Nothing is rewritten and the turned-off row is still there afterwards.
        [TestMethod]
        public void LiveReconcile_WhileTheFileIsHeldByAWriter_ChangesNothing()
        {
            byte[] before = File.ReadAllBytes(EditorSettingsPath);

            using (new FileStream(EditorSettingsPath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                Assert.IsFalse(SettingsManager.ReconcileFromDisk(NoActiveMappings, "Work", regenerateUnreadableSettings: false));
            }

            CollectionAssert.AreEqual(before, File.ReadAllBytes(EditorSettingsPath));
            Assert.IsTrue(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey(TurnedOffId));
        }

        [TestMethod]
        public void LiveReconcile_WithAHalfWrittenFile_DoesNotRewriteIt()
        {
            const string halfWritten = "{ \"ShortcutSettingsDictionary\": ";
            File.WriteAllText(EditorSettingsPath, halfWritten);

            Assert.IsFalse(SettingsManager.ReconcileFromDisk(NoActiveMappings, "Work", regenerateUnreadableSettings: false));

            Assert.AreEqual(halfWritten, File.ReadAllText(EditorSettingsPath));
            Assert.IsTrue(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey(TurnedOffId));
        }

        // The retry: once the file can be read again, the same call succeeds and the row is kept.
        [TestMethod]
        public void LiveReconcile_OnceTheFileIsReadableAgain_Succeeds_AndKeepsTheTurnedOffRow()
        {
            string valid = File.ReadAllText(EditorSettingsPath);
            File.WriteAllText(EditorSettingsPath, "{ \"ShortcutSettingsDictionary\": ");
            Assert.IsFalse(SettingsManager.ReconcileFromDisk(NoActiveMappings, "Work", regenerateUnreadableSettings: false));

            File.WriteAllText(EditorSettingsPath, valid);
            Assert.IsTrue(SettingsManager.ReconcileFromDisk(NoActiveMappings, "Work", regenerateUnreadableSettings: false));

            Assert.IsTrue(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey(TurnedOffId));
            Assert.IsFalse(SettingsManager.EditorSettings.ShortcutSettingsDictionary[TurnedOffId].IsActive);
            Assert.IsTrue(SettingsManager.TryReloadSettings());
            Assert.IsTrue(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey(TurnedOffId));
        }

        // A file that is not there has nothing to lose: it is created from the active mappings.
        [TestMethod]
        public void LiveReconcile_WithNoFile_CreatesItFromTheActiveMappings()
        {
            File.Delete(EditorSettingsPath);
            var active = new List<ShortcutKeyMapping>
            {
                new() { OperationType = ShortcutOperationType.RemapShortcut, OriginalKeys = "65", TargetKeys = "66" },
            };

            Assert.IsTrue(SettingsManager.ReconcileFromDisk(active, "Work", regenerateUnreadableSettings: false));

            Assert.IsTrue(File.Exists(EditorSettingsPath));
            Assert.AreEqual(1, SettingsManager.EditorSettings.ShortcutSettingsDictionary.Count);
        }

        // Startup (after its retries) may still rebuild a file that stays unreadable, as before.
        [TestMethod]
        public void StartupReconcile_WithAFileThatStaysUnreadable_RebuildsIt()
        {
            File.WriteAllText(EditorSettingsPath, "{ \"ShortcutSettingsDictionary\": ");

            Assert.IsTrue(SettingsManager.ReconcileFromDisk(NoActiveMappings, "Work", regenerateUnreadableSettings: true));

            Assert.IsTrue(SettingsManager.TryReloadSettings());
            Assert.IsFalse(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey(TurnedOffId));
        }

        // An unreadable settings.json is not "default is active".
        [TestMethod]
        public void TryGetActiveProfile_UnreadableSettings_ReturnsFalse()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));
            Assert.IsTrue(ProfileManager.TryGetActiveProfile(out string active));
            Assert.AreEqual("Work", active);

            File.WriteAllText(Path.Combine(_directory, "settings.json"), "{ \"properties\": ");

            Assert.IsFalse(ProfileManager.TryGetActiveProfile(out _));
        }

        [TestMethod]
        public void TryGetActiveProfile_NoSettingsFile_IsDefault()
        {
            Assert.IsTrue(ProfileManager.TryGetActiveProfile(out string active));
            Assert.AreEqual("default", active);
        }

        // The editor follows settings.json with this watcher. The engine and the editor save that file
        // by renaming a temporary file over it, which raises no Changed event for it.
        [TestMethod]
        public void FileReplacementWatcher_NoticesAReplacementByRename()
        {
            string target = Path.Combine(_directory, "settings.json");
            File.WriteAllText(target, "{ \"old\": true }");
            string temporary = Path.Combine(_directory, "settings.test.tmp");
            File.WriteAllText(temporary, "{ \"new\": true }");

            using var noticed = new ManualResetEventSlim();
            using (new FileReplacementWatcher(_directory, "settings.json", noticed.Set))
            {
                // The replacement only: no time stamp is touched afterwards.
                SettingsFileReplacer.Replace(temporary, target);

                Assert.IsTrue(noticed.Wait(TimeSpan.FromSeconds(5)), "The watcher did not notice the replacement.");
            }

            Assert.AreEqual("{ \"new\": true }", File.ReadAllText(target));
        }

        [TestMethod]
        public void FileReplacementWatcher_NoticesAnInPlaceWrite()
        {
            string target = Path.Combine(_directory, "settings.json");
            File.WriteAllText(target, "{ \"old\": true }");

            using var noticed = new ManualResetEventSlim();
            using (new FileReplacementWatcher(_directory, "settings.json", noticed.Set))
            {
                File.WriteAllText(target, "{ \"new\": true, \"longer\": true }");

                Assert.IsTrue(noticed.Wait(TimeSpan.FromSeconds(5)), "The watcher did not notice the write.");
            }
        }
    }
}
