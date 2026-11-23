// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
            ProfileManager.AfterCreateExistenceCheckForTests = null;
            ProfileManager.BeforeDeleteCommitForTests = null;
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
                Assert.AreEqual(ProfileManager.ProfileDeleteResult.Kept, ProfileManager.DeleteProfileWithResult("Work", lockTimeoutMS: 100));
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
            WriteDeviceProfiles("{ \"autoSwitchEnabled\": true, \"map\": [ { \"device\": \"keyboard-1\", \"profile\": \"Work\", \"name\": \"Keyboard\" } ] }");
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
            WriteDeviceProfiles("{ \"autoSwitchEnabled\": true, \"map\": [ { \"device\": \"keyboard-1\", \"profile\": \"Work\", \"name\": \"Keyboard\" } ] }");
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

        // Another editor creates the same profile between this call's existence check and its create.
        // This call must fail without overwriting that file (an exclusive create, not a plain write)
        // and without purging the membership the other editor's profile already has (the id is
        // reserved by the create before anything is purged).
        [TestMethod]
        public void CreateProfile_LeavesAnEmptyProfileCreatedMeanwhileByAnotherEditorUntouched()
        {
            string path = Path.Combine(_directory, "Work.json");
            ProfileManager.AfterCreateExistenceCheckForTests = () =>
            {
                File.WriteAllText(path, "other editor's mappings");
                WriteEditorSettings();
            };

            Assert.IsFalse(ProfileManager.CreateProfile("Work", copyFromActive: false));

            Assert.AreEqual("other editor's mappings", File.ReadAllText(path));
            Assert.IsTrue(SettingsManager.TryReloadSettings());
            Assert.IsTrue(
                SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey("work-only"),
                "The other editor's Work row must not be purged.");
        }

        // Same race for the copy branch (File.Copy without overwrite).
        [TestMethod]
        public void CreateProfile_LeavesACopiedProfileCreatedMeanwhileByAnotherEditorUntouched()
        {
            File.WriteAllText(Path.Combine(_directory, "default.json"), "default mappings");
            string path = Path.Combine(_directory, "Work.json");
            ProfileManager.AfterCreateExistenceCheckForTests = () =>
            {
                File.WriteAllText(path, "other editor's mappings");
                WriteEditorSettings();
            };

            Assert.IsFalse(ProfileManager.CreateProfile("Work", "default"));

            Assert.AreEqual("other editor's mappings", File.ReadAllText(path));
            Assert.IsTrue(SettingsManager.TryReloadSettings());
            Assert.IsTrue(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey("work-only"));
        }

        // "default" is the built-in profile; creating it (in any case) must not purge its rows or
        // register a second, differently cased default.
        [TestMethod]
        public void CreateProfile_RejectsDefaultInAnyCase()
        {
            var settings = new EditorSettings();
            AddRow(settings, "default-off", "65", isActive: false, "default");
            Assert.IsTrue(SettingsManager.WriteSettings(settings));

            foreach (string name in new[] { "default", "Default", "DEFAULT" })
            {
                Assert.IsFalse(ProfileManager.CreateProfile(name, copyFromActive: false), name);
            }

            Assert.AreEqual(0, Directory.GetFiles(_directory, "default.json").Length);
            Assert.IsTrue(SettingsManager.TryReloadSettings());
            Assert.IsTrue(SettingsManager.EditorSettings.ShortcutSettingsDictionary.ContainsKey("default-off"));
            Assert.AreEqual(1, ProfileManager.GetProfiles().Count(p => p.Equals("default", StringComparison.OrdinalIgnoreCase)));
        }

        // A profile that is registered but whose file is gone must not get a second registration that
        // differs only in case.
        [TestMethod]
        public void CreateProfile_RejectsACaseVariantOfARegisteredProfile()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Home", copyFromActive: false));
            File.Delete(Path.Combine(_directory, "Home.json"));

            Assert.IsFalse(ProfileManager.CreateProfile("home", copyFromActive: false));

            Assert.AreEqual(1, ProfileManager.GetProfiles().Count(p => p.Equals("home", StringComparison.OrdinalIgnoreCase)));
        }

        // The engine may have reloaded while the active profile's file was moved aside, and then runs
        // with an empty remap table. After putting the file back, the delete must ask for a reload.
        [TestMethod]
        public void DeleteProfile_AsksTheEngineToReload_WhenItRollsBackTheActiveProfile()
        {
            CreateWorkWithTaggedRows();
            File.SetAttributes(Path.Combine(_directory, "settings.json"), FileAttributes.ReadOnly);
            int before = ProfileManager.SuppressedEngineReloadsForTests;

            Assert.AreEqual(ProfileManager.ProfileDeleteResult.Kept, ProfileManager.DeleteProfileWithResult("Work"));

            Assert.AreEqual(before + 1, ProfileManager.SuppressedEngineReloadsForTests);
        }

        [TestMethod]
        public void DeleteProfile_DoesNotAskForAReload_WhenTheRolledBackProfileWasNotActive()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            WriteEditorSettings();
            File.SetAttributes(Path.Combine(_directory, "settings.json"), FileAttributes.ReadOnly);
            int before = ProfileManager.SuppressedEngineReloadsForTests;

            Assert.AreEqual(ProfileManager.ProfileDeleteResult.Kept, ProfileManager.DeleteProfileWithResult("Work"));

            Assert.AreEqual(before, ProfileManager.SuppressedEngineReloadsForTests);
        }

        // The rollback itself fails (the membership can't be written back): the delete must not
        // claim the profile was kept as it was.
        [TestMethod]
        public void DeleteProfile_ReportsAnIncompleteUndo_WhenTheMembershipCannotBeRestored()
        {
            CreateWorkWithTaggedRows();
            string settingsPath = Path.Combine(_directory, "settings.json");
            ProfileManager.BeforeDeleteCommitForTests = () =>
            {
                File.SetAttributes(settingsPath, FileAttributes.ReadOnly);
                File.SetAttributes(EditorSettingsPath, FileAttributes.ReadOnly);
            };

            Assert.AreEqual(ProfileManager.ProfileDeleteResult.NotFullyRestored, ProfileManager.DeleteProfileWithResult("Work"));

            Assert.IsTrue(File.Exists(Path.Combine(_directory, "Work.json")), "The file is still put back.");
            CollectionAssert.Contains(new List<string>(ProfileManager.GetProfiles()), "Work");
        }

        // The rollback itself fails (the file can't be moved back because a Work.json appeared): the
        // delete reports it and the original file is kept aside, not lost.
        [TestMethod]
        public void DeleteProfile_ReportsAnIncompleteUndo_WhenTheProfileFileCannotBeMovedBack()
        {
            CreateWorkWithTaggedRows();
            string settingsPath = Path.Combine(_directory, "settings.json");
            ProfileManager.BeforeDeleteCommitForTests = () =>
            {
                File.SetAttributes(settingsPath, FileAttributes.ReadOnly);
                File.WriteAllText(Path.Combine(_directory, "Work.json"), "created meanwhile");
            };

            Assert.AreEqual(ProfileManager.ProfileDeleteResult.NotFullyRestored, ProfileManager.DeleteProfileWithResult("Work"));

            Assert.AreEqual(1, Directory.GetFiles(_directory, "Work.deleting-*.json").Length);
        }

        // A delete that died between moving the file aside and committing leaves the profile registered
        // with only its moved-aside file: put it back.
        [TestMethod]
        public void GetProfiles_MovesBackTheSetAsideFileOfAStillRegisteredProfile()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            string path = Path.Combine(_directory, "Work.json");
            File.WriteAllText(path, "work mappings");
            File.Move(path, Path.Combine(_directory, $"Work.deleting-{Guid.NewGuid():N}.json"));

            CollectionAssert.Contains(new List<string>(ProfileManager.GetProfiles()), "Work");

            Assert.AreEqual("work mappings", File.ReadAllText(path));
            AssertNoLeftoverFiles();
        }

        // A committed delete whose moved-aside file could not be removed: delete it.
        [TestMethod]
        public void GetProfiles_DeletesTheSetAsideFileOfAnUnregisteredProfile()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            File.WriteAllText(Path.Combine(_directory, $"Old.deleting-{Guid.NewGuid():N}.json"), "old mappings");

            CollectionAssert.DoesNotContain(new List<string>(ProfileManager.GetProfiles()), "Old");

            Assert.IsFalse(File.Exists(Path.Combine(_directory, "Old.json")));
            AssertNoLeftoverFiles();
        }

        // A profile that was never registered (a "{name}.json" found by scanning the folder) leaves no
        // trace of a committed delete in settings.json. Its moved-aside file is put back rather than
        // removed: deleting it again is possible, getting it back would not be.
        [TestMethod]
        public void GetProfiles_MovesBackTheSetAsideFileOfAProfileThatWasUnregisteredWhenDeleted()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            File.WriteAllText(Path.Combine(_directory, $"Travel.deleting-unregistered-{Guid.NewGuid():N}.json"), "travel mappings");

            CollectionAssert.Contains(new List<string>(ProfileManager.GetProfiles()), "Travel");

            Assert.AreEqual("travel mappings", File.ReadAllText(Path.Combine(_directory, "Travel.json")));
            AssertNoLeftoverFiles();
        }

        // The rollback of an unregistered profile's delete cannot move its file back (another editor
        // created the same id meanwhile). Once that file is gone, the cleanup restores the original
        // instead of removing it.
        [TestMethod]
        public void UnregisteredProfile_WhoseDeleteCouldNotBeUndone_IsRestoredByTheCleanup()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            string path = Path.Combine(_directory, "Travel.json");
            File.WriteAllText(path, "travel mappings");
            CollectionAssert.Contains(new List<string>(ProfileManager.GetProfiles()), "Travel");
            string settingsPath = Path.Combine(_directory, "settings.json");
            ProfileManager.BeforeDeleteCommitForTests = () =>
            {
                File.SetAttributes(settingsPath, FileAttributes.ReadOnly);
                File.WriteAllText(path, "created meanwhile");
            };

            Assert.AreEqual(ProfileManager.ProfileDeleteResult.NotFullyRestored, ProfileManager.DeleteProfileWithResult("Travel"));
            Assert.AreEqual(1, Directory.GetFiles(_directory, "Travel.deleting-*.json").Length);

            ProfileManager.BeforeDeleteCommitForTests = null;
            File.SetAttributes(settingsPath, FileAttributes.Normal);
            File.Delete(path);
            CollectionAssert.Contains(new List<string>(ProfileManager.GetProfiles()), "Travel");

            Assert.AreEqual("travel mappings", File.ReadAllText(path));
            AssertNoLeftoverFiles();
        }

        // Only "{id}.deleting-<32 hex>.json" without an "{id}.json" is cleaned up, and only while no
        // other editor holds the transaction lock (a delete may be in progress).
        [TestMethod]
        public void GetProfiles_LeavesOtherFilesAndInProgressDeletesAlone()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.CreateProfile("Home", copyFromActive: false));
            File.Delete(Path.Combine(_directory, "Home.json"));
            string[] untouched =
            {
                Path.Combine(_directory, "default.backup-1.json"),
                Path.Combine(_directory, "Old.deleting-not-a-guid.json"),
                Path.Combine(_directory, $"Work.deleting-{Guid.NewGuid():N}.json"), // Work.json exists
            };
            foreach (string file in untouched)
            {
                File.WriteAllText(file, "x");
            }

            string homeAside = Path.Combine(_directory, $"Home.deleting-{Guid.NewGuid():N}.json");
            File.WriteAllText(homeAside, "home mappings");
            using (new FileStream(Path.Combine(_directory, "editorTransaction.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                ProfileManager.GetProfiles();
            }

            foreach (string file in untouched)
            {
                Assert.IsTrue(File.Exists(file), file);
            }

            Assert.IsTrue(File.Exists(homeAside), "Must not be moved back while another editor may be deleting Home.");
            Assert.IsFalse(File.Exists(Path.Combine(_directory, "Home.json")));
        }

        // The live folder must also be refused when given in its 8.3 short form.
        [TestMethod]
        public void RedirectForTests_RefusesTheLiveFolderInShortForm()
        {
            string shortForm = ToShortPath(LiveSettingsDirectory);

            Assert.ThrowsExactly<InvalidOperationException>(() => KeyboardManagerPaths.RedirectForTests(shortForm));
            Assert.ThrowsExactly<InvalidOperationException>(() => KeyboardManagerPaths.RedirectForTests(Path.Combine(shortForm, "sub")));
            Assert.AreEqual(_directory, KeyboardManagerPaths.SettingsDirectory);
        }

        // Settings restores an old backup (a version-0 editorSettings.json) while
        // Work is active, and the editor reconciles it on reload (ReloadIfChangedOnDisk). The legacy
        // rows go to default, not to Work, and the file is marked as migrated.
        [TestMethod]
        public void RestoredVersion0EditorSettings_ReconciledWhileWorkIsActive_GoesToDefault()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));
            Assert.IsTrue(SettingsManager.WriteSettings(new EditorSettings { ActiveProfile = "Work", SchemaVersion = EditorSettings.CurrentSchemaVersion }));
            Assert.IsTrue(SettingsManager.TryReloadSettings());

            File.WriteAllText(EditorSettingsPath, LegacyMembershipMigrationTests.V0100DisabledRowJson); // the restore
            Assert.IsTrue(SettingsManager.HasSettingsChangedOnDisk());
            Assert.IsTrue(SettingsManager.TryReloadSettings());

            Assert.IsTrue(SettingsManager.ReconcileLoadedSettings(new List<ShortcutKeyMapping>(), ProfileManager.GetActiveProfile()));

            EditorSettings onDisk = ReadEditorSettings();
            CollectionAssert.AreEqual(new List<string> { "default" }, onDisk.ShortcutSettingsDictionary[LegacyMembershipMigrationTests.RowId].Profiles);
            Assert.AreEqual(EditorSettings.CurrentSchemaVersion, onDisk.SchemaVersion);
            Assert.AreEqual("Work", onDisk.ActiveProfile);
            Assert.IsFalse(SettingsManager.IsMappingInActiveProfile(onDisk.ShortcutSettingsDictionary[LegacyMembershipMigrationTests.RowId], "Work"));
        }

        // A version-0 file without tagged rows goes to default even when it records another profile
        // that exists in the real profile store.
        [TestMethod]
        public void RestoredVersion0EditorSettings_GoesToDefault_EvenWhenItRecordsAnExistingProfile()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Home", copyFromActive: false));
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));
            File.WriteAllText(
                EditorSettingsPath,
                LegacyMembershipMigrationTests.V0100DisabledRowJson.Replace("\"ActiveProfile\": \"\"", "\"ActiveProfile\": \"Home\"", StringComparison.Ordinal));
            Assert.IsTrue(SettingsManager.TryReloadSettings());

            Assert.IsTrue(SettingsManager.ReconcileLoadedSettings(new List<ShortcutKeyMapping>(), "Work"));

            CollectionAssert.AreEqual(new List<string> { "default" }, ReadEditorSettings().ShortcutSettingsDictionary[LegacyMembershipMigrationTests.RowId].Profiles);
        }

        // A version-0 file from a pre-release profile-aware build (rows tagged with other profiles, a recorded profile
        // other than default): its untagged row goes to every profile of the real store.
        [TestMethod]
        public void Version0DevBuildEditorSettings_TagsUntaggedRowsWithEveryExistingProfile()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Apple Magic Keyboard", copyFromActive: false));
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));
            File.WriteAllText(EditorSettingsPath, LegacyMembershipMigrationTests.DevBuildJson);
            Assert.IsTrue(SettingsManager.TryReloadSettings());

            Assert.IsTrue(SettingsManager.ReconcileLoadedSettings(new List<ShortcutKeyMapping>(), "Work"));

            EditorSettings onDisk = ReadEditorSettings();
            CollectionAssert.AreEquivalent(
                new List<string> { "default", "Apple Magic Keyboard", "Work" },
                onDisk.ShortcutSettingsDictionary[LegacyMembershipMigrationTests.RowId].Profiles);
            Assert.AreEqual(EditorSettings.CurrentSchemaVersion, onDisk.SchemaVersion);
        }

        // An edit saved before any reconcile has migrated a released version-0 file must not tag
        // a row first: that would make the file look like one from a profile-aware build. The
        // write migrates the file, so the untagged rows go to default and Work cannot claim them.
        [TestMethod]
        public void WriteToAVersion0File_MigratesFirst_SoWorkCannotClaimDefaultsRows()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));
            File.WriteAllText(EditorSettingsPath, LegacyMembershipMigrationTests.V0100DisabledRowJson);
            Assert.IsTrue(SettingsManager.TryReloadSettings());

            Assert.IsFalse(SettingsManager.TrySetShortcutKeyMappingActiveState(LegacyMembershipMigrationTests.RowId, true, "Work"));
            var mapping = new ShortcutKeyMapping { OperationType = ShortcutOperationType.RemapShortcut, OriginalKeys = "80", TargetKeys = "81", TargetText = string.Empty };
            Assert.IsTrue(SettingsManager.TryCommitShortcutKeyMapping(mapping, null, "Work"));

            EditorSettings onDisk = ReadEditorSettings();
            Assert.AreEqual(EditorSettings.CurrentSchemaVersion, onDisk.SchemaVersion);
            CollectionAssert.AreEqual(new List<string> { "default" }, onDisk.ShortcutSettingsDictionary[LegacyMembershipMigrationTests.RowId].Profiles);
            Assert.IsFalse(onDisk.ShortcutSettingsDictionary[LegacyMembershipMigrationTests.RowId].IsActive);

            // A later reconcile does not move it either.
            Assert.IsTrue(SettingsManager.TryReloadSettings());
            SettingsManager.ReconcileLoadedSettings(new List<ShortcutKeyMapping> { mapping }, "Work");
            CollectionAssert.AreEqual(new List<string> { "default" }, ReadEditorSettings().ShortcutSettingsDictionary[LegacyMembershipMigrationTests.RowId].Profiles);
        }

        // settings.json is replaced while a reader (the engine's ifstream shares read and write, not
        // delete) has it open for a moment. Windows reports access denied for that; the switch waits
        // for the reader instead of failing.
        [TestMethod]
        public void SetActiveProfile_WaitsForAReaderThatHasSettingsJsonOpenForAMoment()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));

            using (HoldOpenBriefly(Path.Combine(_directory, "settings.json"), millisecondsHeld: 150))
            {
                Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));
            }

            Assert.AreEqual("Work", ProfileManager.GetActiveProfile());
            AssertNoLeftoverFiles();
        }

        [TestMethod]
        public void ReplaceFile_GivesUp_WhenTheTargetStaysOpen()
        {
            string target = Path.Combine(_directory, "target.json");
            string temporary = Path.Combine(_directory, "target.tmp");
            File.WriteAllText(target, "old");
            File.WriteAllText(temporary, "new");

            using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                Assert.ThrowsExactly<UnauthorizedAccessException>(() => SettingsFileReplacer.Replace(temporary, target));
            }

            Assert.AreEqual("old", File.ReadAllText(target));
        }

        // A read-only target will not change by waiting, so it fails at once (ten attempts would take
        // about half a second).
        [TestMethod]
        public void ReplaceFile_FailsAtOnce_WhenTheTargetIsReadOnly()
        {
            string target = Path.Combine(_directory, "target.json");
            string temporary = Path.Combine(_directory, "target.tmp");
            File.WriteAllText(target, "old");
            File.WriteAllText(temporary, "new");
            File.SetAttributes(target, FileAttributes.ReadOnly);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Assert.ThrowsExactly<UnauthorizedAccessException>(() => SettingsFileReplacer.Replace(temporary, target));
            stopwatch.Stop();

            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 250, $"Took {stopwatch.ElapsedMilliseconds} ms.");
            Assert.AreEqual("old", File.ReadAllText(target));
        }

        // Opens path for reading without delete sharing, as a native reader does, and closes it on
        // another thread after millisecondsHeld. Disposing waits for that.
        internal static IDisposable HoldOpenBriefly(string path, int millisecondsHeld)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var release = System.Threading.Tasks.Task.Run(() =>
            {
                System.Threading.Thread.Sleep(millisecondsHeld);
                stream.Dispose();
            });
            return new HeldFile(release);
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetShortPathNameW(string longPath, StringBuilder shortPath, uint bufferLength);

        // The 8.3 form of the deepest existing part of path, with the rest appended unchanged.
        private static string ToShortPath(string path)
        {
            string existing = path;
            string rest = string.Empty;
            while (!Directory.Exists(existing) && Path.GetDirectoryName(existing) is string parent)
            {
                rest = Path.Combine(Path.GetFileName(existing), rest);
                existing = parent;
            }

            var buffer = new StringBuilder(1024);
            uint length = GetShortPathNameW(existing, buffer, (uint)buffer.Capacity);
            string shortExisting = length > 0 && length < buffer.Capacity ? buffer.ToString() : existing;
            return Path.TrimEndingDirectorySeparator(Path.Combine(shortExisting, rest));
        }

        // Work exists and is active; editorSettings.json has a turned-off row owned only by Work and
        // a row shared by default and Work.
        private void CreateWorkWithTaggedRows()
        {
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.SetActiveProfile("Work"));
            WriteEditorSettings();
        }

        private void WriteDeviceProfiles(string json) => File.WriteAllText(Path.Combine(_directory, "deviceProfiles.json"), json);

        private EditorSettings ReadEditorSettings() =>
            System.Text.Json.JsonSerializer.Deserialize<EditorSettings>(File.ReadAllText(EditorSettingsPath)) ?? new EditorSettings();

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

        private sealed class HeldFile(System.Threading.Tasks.Task release) : IDisposable
        {
            public void Dispose() => release.Wait();
        }
    }
}
