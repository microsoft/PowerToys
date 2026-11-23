// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// Tests of the profile stores against real files. Every test runs in its own empty folder
    /// under the test root that the module initializer set up, never in the user's live
    /// Keyboard Manager folder.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ProfileStoreTests
    {
        private static readonly string LiveSettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "Keyboard Manager");

        private string _directory = string.Empty;

        private EditorSettings _originalEditorSettings = new EditorSettings();

        private string EditorSettingsPath => Path.Combine(_directory, "editorSettings.json");

        [AssemblyCleanup]
        public static void DeleteTestRoot()
        {
            try
            {
                Directory.Delete(UnitTestModuleInitializer.TestRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        [TestInitialize]
        public void SetUp()
        {
            _directory = Path.Combine(UnitTestModuleInitializer.TestRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            KeyboardManagerPaths.RedirectForTests(_directory);
            _originalEditorSettings = SettingsManager.EditorSettings;
        }

        [TestCleanup]
        public void TearDown()
        {
            SettingsManager.EditorSettings = _originalEditorSettings;
            KeyboardManagerPaths.RedirectForTests(UnitTestModuleInitializer.TestRoot);
            try
            {
                foreach (string file in Directory.EnumerateFiles(_directory))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        [TestMethod]
        public void Stores_UseTheRedirectedFolder()
        {
            Assert.IsTrue(KeyboardManagerPaths.IsRedirectedForTests);
            Assert.AreEqual(_directory, KeyboardManagerPaths.SettingsDirectory);
            Assert.AreEqual(_directory, ProfileManager.SettingsDirectory);
            Assert.IsFalse(
                ProfileManager.SettingsDirectory.StartsWith(LiveSettingsDirectory, StringComparison.OrdinalIgnoreCase),
                "Tests must never use the live Keyboard Manager folder.");
        }

        [TestMethod]
        public void RedirectForTests_RefusesTheLiveFolder()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => KeyboardManagerPaths.RedirectForTests(LiveSettingsDirectory));
            Assert.ThrowsExactly<InvalidOperationException>(() => KeyboardManagerPaths.RedirectForTests(LiveSettingsDirectory + Path.DirectorySeparatorChar));
            Assert.ThrowsExactly<InvalidOperationException>(() => KeyboardManagerPaths.RedirectForTests(Path.Combine(LiveSettingsDirectory, "sub")));
            Assert.AreEqual(_directory, KeyboardManagerPaths.SettingsDirectory);
        }

        [TestMethod]
        public void ProfileFiles_AreWrittenToTheRedirectedFolder()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));

            Assert.IsTrue(File.Exists(Path.Combine(_directory, "Work.json")));
            Assert.IsTrue(File.Exists(Path.Combine(_directory, "settings.json")));
            CollectionAssert.Contains(new List<string>(ProfileManager.GetProfiles()), "Work");
        }

        // E5: a second creator must fail instead of truncating a file another editor just created.
        [TestMethod]
        public void TryCreateFileExclusive_DoesNotTouchAnExistingFile()
        {
            string path = Path.Combine(_directory, "Work.json");
            File.WriteAllText(path, "copied mappings");

            Assert.IsFalse(ProfileManager.TryCreateFileExclusive(path, "{}"));
            Assert.AreEqual("copied mappings", File.ReadAllText(path));
        }

        [TestMethod]
        public void TryCreateFileExclusive_CreatesAMissingFile()
        {
            string path = Path.Combine(_directory, "Work.json");

            Assert.IsTrue(ProfileManager.TryCreateFileExclusive(path, "{}"));
            Assert.AreEqual("{}", File.ReadAllText(path));
        }

        // E1: a fresh setup can register "default" without a default.json yet. Switching back to it
        // must work and must create it empty.
        [TestMethod]
        public void SetActiveProfile_SwitchesToDefaultWhoseFileDoesNotExistYet()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));
            string defaultPath = Path.Combine(_directory, "default.json");
            Assert.IsFalse(File.Exists(defaultPath));

            Assert.IsTrue(ProfileManager.SetActiveProfile("default"));
            Assert.AreEqual("default", ProfileManager.GetActiveProfile());
            Assert.IsTrue(File.Exists(defaultPath));
            Assert.AreEqual(File.ReadAllText(Path.Combine(_directory, "Work.json")), File.ReadAllText(defaultPath));
        }

        [TestMethod]
        public void SetActiveProfile_KeepsAnExistingDefaultFile()
        {
            string defaultPath = Path.Combine(_directory, "default.json");
            File.WriteAllText(defaultPath, "default mappings");
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));

            Assert.IsTrue(ProfileManager.SetActiveProfile("default"));
            Assert.AreEqual("default mappings", File.ReadAllText(defaultPath));
        }

        // Only "default" is created on demand; any other profile without a file is still refused and
        // the active profile is left as it was.
        [TestMethod]
        public void SetActiveProfile_RefusesAMissingNonDefaultProfile()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));

            Assert.IsFalse(ProfileManager.SetActiveProfile("Home"));
            Assert.AreEqual("Work", ProfileManager.GetActiveProfile());
            Assert.IsFalse(File.Exists(Path.Combine(_directory, "Home.json")));
        }

        // #6 follow-up: when the membership cleanup cannot run (another editor holds the transaction
        // lock), the delete must fail without having removed anything.
        [TestMethod]
        public void DeleteProfile_FailsAndKeepsEverything_WhenTheTransactionLockIsHeld()
        {
            CreateWorkWithTaggedRows();
            string editorSettingsBefore = File.ReadAllText(EditorSettingsPath);
            string settingsBefore = File.ReadAllText(Path.Combine(_directory, "settings.json"));

            using (new FileStream(Path.Combine(_directory, "editorTransaction.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.IsFalse(ProfileManager.DeleteProfile("Work"));
            }

            Assert.IsTrue(File.Exists(Path.Combine(_directory, "Work.json")));
            Assert.AreEqual(editorSettingsBefore, File.ReadAllText(EditorSettingsPath));
            Assert.AreEqual(settingsBefore, File.ReadAllText(Path.Combine(_directory, "settings.json")));
            AssertNoLeftoverFiles();
        }

        // #6 follow-up: when the membership write fails after the profile file was moved aside, the
        // file is put back and the delete reports failure.
        [TestMethod]
        public void DeleteProfile_RestoresTheProfileFile_WhenTheMembershipWriteFails()
        {
            CreateWorkWithTaggedRows();
            string workContent = File.ReadAllText(Path.Combine(_directory, "Work.json"));
            string editorSettingsBefore = File.ReadAllText(EditorSettingsPath);
            File.SetAttributes(EditorSettingsPath, FileAttributes.ReadOnly);

            Assert.IsFalse(ProfileManager.DeleteProfile("Work"));

            Assert.AreEqual(workContent, File.ReadAllText(Path.Combine(_directory, "Work.json")));
            Assert.AreEqual(editorSettingsBefore, File.ReadAllText(EditorSettingsPath));
            CollectionAssert.Contains(new List<string>(ProfileManager.GetProfiles()), "Work");
            Assert.AreEqual("Work", ProfileManager.GetActiveProfile());
            AssertNoLeftoverFiles();
        }

        // #6 follow-up: when settings.json can't be updated after the membership was removed, both
        // the membership and the file are put back.
        [TestMethod]
        public void DeleteProfile_RestoresMembershipAndFile_WhenSettingsJsonCannotBeWritten()
        {
            CreateWorkWithTaggedRows();
            string workContent = File.ReadAllText(Path.Combine(_directory, "Work.json"));
            string editorSettingsBefore = File.ReadAllText(EditorSettingsPath);
            string settingsPath = Path.Combine(_directory, "settings.json");
            string settingsBefore = File.ReadAllText(settingsPath);
            File.SetAttributes(settingsPath, FileAttributes.ReadOnly);

            Assert.IsFalse(ProfileManager.DeleteProfile("Work"));

            Assert.AreEqual(workContent, File.ReadAllText(Path.Combine(_directory, "Work.json")));
            Assert.AreEqual(settingsBefore, File.ReadAllText(settingsPath));
            Assert.AreEqual(editorSettingsBefore, File.ReadAllText(EditorSettingsPath));
            AssertNoLeftoverFiles();
        }

        [TestMethod]
        public void DeleteProfile_RemovesTheFileMembershipAndRegistration()
        {
            CreateWorkWithTaggedRows();
            DeviceProfileManager.Save(true, new[] { new DeviceAssignment { Device = "keyboard-1", Profile = "Work", Name = "Keyboard" } });
            ProfileManager.RenameProfile("Work", "Office");

            Assert.IsTrue(ProfileManager.DeleteProfile("Work"));

            Assert.IsFalse(File.Exists(Path.Combine(_directory, "Work.json")));
            CollectionAssert.DoesNotContain(new List<string>(ProfileManager.GetProfiles()), "Work");
            Assert.AreEqual("default", ProfileManager.GetActiveProfile());
            Assert.IsTrue(File.Exists(Path.Combine(_directory, "default.json")));
            Assert.IsTrue(SettingsManager.TryReloadSettings());
            Assert.IsFalse(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey("work-only"));
            CollectionAssert.AreEqual(new List<string> { "default" }, SettingsManager.EditorSettings.ShortcutSettingsDictionary["shared"].Profiles);
            Assert.AreEqual(0, DeviceProfileManager.GetSavedAssignments().Count);
            Assert.AreEqual("Work", ProfileManager.GetDisplayName("Work"));
            AssertNoLeftoverFiles();
        }

        // No editorSettings.json yet (nothing was ever saved in the editor): nothing to clean up, so
        // the delete still succeeds.
        [TestMethod]
        public void DeleteProfile_Succeeds_WhenThereIsNoEditorSettingsFile()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));

            Assert.IsTrue(ProfileManager.DeleteProfile("Work"));
            Assert.IsFalse(File.Exists(Path.Combine(_directory, "Work.json")));
            Assert.IsFalse(File.Exists(EditorSettingsPath));
            AssertNoLeftoverFiles();
        }

        // Leftovers of an earlier, partly failed delete (or an older build) must not reappear in a
        // new profile that reuses the id: no turned-off rows, keyboard assignments or display name.
        [TestMethod]
        public void CreateProfile_PurgesLeftoversOfAReusedProfileId()
        {
            WriteEditorSettings();
            DeviceProfileManager.Save(true, new[] { new DeviceAssignment { Device = "keyboard-1", Profile = "Work", Name = "Keyboard" } });
            ProfileMetadataManager.SetDisplayName("Work", "Office");

            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));

            Assert.IsTrue(SettingsManager.TryReloadSettings());
            Assert.IsFalse(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey("work-only"));
            CollectionAssert.AreEqual(new List<string> { "default" }, SettingsManager.EditorSettings.ShortcutSettingsDictionary["shared"].Profiles);
            Assert.AreEqual(0, DeviceProfileManager.GetSavedAssignments().Count);
            Assert.AreEqual("Work", ProfileManager.GetDisplayName("Work"));
        }

        // S1 (New profile): "copy the current profile" copies the profile shown when the dialog
        // opened, even if an auto-switch made another profile active before the user confirmed.
        [TestMethod]
        public void CreateProfile_CopiesTheProfileCapturedWhenTheDialogOpened()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.CreateProfile("Home", copyFromActive: false));
            File.WriteAllText(Path.Combine(_directory, "Work.json"), "work mappings");
            File.WriteAllText(Path.Combine(_directory, "Home.json"), "home mappings");
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));
            string copySource = ProfileManager.GetActiveProfile(); // captured when the dialog opens

            Assert.IsTrue(ProfileManager.SetActiveProfile("Home")); // auto-switch while it is open

            Assert.IsTrue(ProfileManager.CreateProfile("Travel", copySource));
            Assert.AreEqual("work mappings", File.ReadAllText(Path.Combine(_directory, "Travel.json")));
            Assert.AreEqual("Home", ProfileManager.GetActiveProfile());
        }

        // The captured source was deleted before the user confirmed: creating an empty profile
        // instead would silently drop the copy the user asked for.
        [TestMethod]
        public void CreateProfile_Fails_WhenTheCapturedCopySourceNoLongerExists()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));
            Assert.IsTrue(ProfileManager.DeleteProfile("Work"));

            Assert.IsFalse(ProfileManager.CreateProfile("Travel", "Work"));
            Assert.IsFalse(File.Exists(Path.Combine(_directory, "Travel.json")));
            CollectionAssert.DoesNotContain(new List<string>(ProfileManager.GetProfiles()), "Travel");
        }

        // default may legitimately have no file yet (fresh setup); a copy of it is an empty profile.
        [TestMethod]
        public void CreateProfile_CopyOfADefaultWithoutAFile_IsEmpty()
        {
            Assert.IsFalse(File.Exists(Path.Combine(_directory, "default.json")));

            Assert.IsTrue(ProfileManager.CreateProfile("Travel", "default"));
            Assert.IsTrue(File.Exists(Path.Combine(_directory, "Travel.json")));
        }

        [TestMethod]
        public void CreateProfile_DoesNotOverwriteAnExistingProfileFile()
        {
            string path = Path.Combine(_directory, "Work.json");
            File.WriteAllText(path, "copied mappings");

            Assert.IsFalse(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.AreEqual("copied mappings", File.ReadAllText(path));
        }

        // Work exists and is active; editorSettings.json has a turned-off row owned only by Work and
        // a row shared by default and Work.
        private void CreateWorkWithTaggedRows()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));
            WriteEditorSettings();
        }

        private static void WriteEditorSettings()
        {
            var settings = new EditorSettings();
            AddRow(settings, "work-only", "65", isActive: false, "Work");
            AddRow(settings, "shared", "66", isActive: true, "default", "Work");
            Assert.IsTrue(SettingsManager.WriteSettings(settings));
        }

        private static void AddRow(EditorSettings settings, string id, string originalKeys, bool isActive, params string[] profiles)
        {
            var mapping = new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RemapShortcut,
                OriginalKeys = originalKeys,
                TargetKeys = "70",
                TargetText = string.Empty,
            };
            settings.ShortcutSettingsDictionary[id] = new ShortcutSettings { Id = id, Shortcut = mapping, IsActive = isActive, Profiles = new List<string>(profiles) };
            if (!settings.ShortcutsByOperationType.TryGetValue(mapping.OperationType, out List<string> ids))
            {
                ids = new List<string>();
                settings.ShortcutsByOperationType[mapping.OperationType] = ids;
            }

            ids.Add(id);
            foreach (string profile in profiles)
            {
                if (!settings.ProfileDictionary.TryGetValue(profile, out List<string> members))
                {
                    members = new List<string>();
                    settings.ProfileDictionary[profile] = members;
                }

                members.Add(id);
            }
        }

        // Only profile configs and the stores may remain: no moved-aside profile file or temp file.
        private void AssertNoLeftoverFiles()
        {
            foreach (string file in Directory.EnumerateFiles(_directory))
            {
                string name = Path.GetFileName(file);
                Assert.IsFalse(
                    name.Count(c => c == '.') > 1 || name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase),
                    $"Unexpected leftover file '{name}'.");
            }
        }
    }
}
