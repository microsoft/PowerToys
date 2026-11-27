// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using KeyboardManagerEditorUI.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    /// <summary>
    /// The keyboard assignment dialog can stay open for minutes, and the Settings page and the
    /// runner can each start an editor. Saving applies only what this dialog changed to the file
    /// as it is now, so a dialog opened earlier cannot overwrite a newer save. Every test runs in
    /// its own folder under the test root, never in the live Keyboard Manager folder.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class DeviceAssignmentSaveTests
    {
        private string _directory = string.Empty;

        private string DeviceProfilesPath => Path.Combine(_directory, "deviceProfiles.json");

        [TestInitialize]
        public void SetUp()
        {
            _directory = Path.Combine(UnitTestModuleInitializer.TestRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            KeyboardManagerPaths.RedirectForTests(_directory);
            Assert.IsTrue(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.IsTrue(ProfileManager.CreateProfile("Home", copyFromActive: false));
        }

        [TestCleanup]
        public void TearDown()
        {
            KeyboardManagerPaths.RedirectForTests(UnitTestModuleInitializer.TestRoot);
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        // Scenario: two dialogs open on the same file. A assigns a new keyboard and
        // saves; B changes only the auto-switch toggle and saves. A's assignment must survive.
        [TestMethod]
        public void SecondDialog_ChangingOnlyTheToggle_KeepsTheFirstDialogsNewAssignment()
        {
            WriteFile(autoSwitch: false, ("k1", "Work"));
            DeviceProfileManager.DeviceAssignmentsBaseline baseline = DeviceProfileManager.LoadBaseline();

            Assert.AreEqual(
                DeviceProfileManager.DeviceAssignmentsSaveResult.Saved,
                DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: false, Rows(("k1", "Work"), ("k2", "Home"))));
            Assert.AreEqual(
                DeviceProfileManager.DeviceAssignmentsSaveResult.Saved,
                DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: true, Rows(("k1", "Work"))));

            CollectionAssert.AreEquivalent(new List<string> { "k1=Work", "k2=Home" }, SavedMap());
            Assert.IsTrue(DeviceProfileManager.GetAutoSwitchEnabled());
        }

        // A turned auto-switch on; B, opened before that, changes only an assignment. B must not
        // turn auto-switch back off.
        [TestMethod]
        public void SecondDialog_ChangingOnlyAnAssignment_KeepsTheFirstDialogsToggle()
        {
            WriteFile(autoSwitch: false, ("k1", "Work"));
            DeviceProfileManager.DeviceAssignmentsBaseline baseline = DeviceProfileManager.LoadBaseline();

            DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: true, Rows(("k1", "Work")));
            DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: false, Rows(("k1", "Home")));

            Assert.IsTrue(DeviceProfileManager.GetAutoSwitchEnabled());
            CollectionAssert.AreEquivalent(new List<string> { "k1=Home" }, SavedMap());
        }

        // Clearing a keyboard's assignment removes only that keyboard; another dialog's change to a different
        // keyboard stays.
        [TestMethod]
        public void ClearingAKeyboard_RemovesOnlyThatKeyboard()
        {
            WriteFile(autoSwitch: true, ("k1", "Work"), ("k2", "Home"));
            DeviceProfileManager.DeviceAssignmentsBaseline baseline = DeviceProfileManager.LoadBaseline();

            DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: true, Rows(("k1", "Work"), ("k2", "Work")));
            DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: true, Rows(("k1", string.Empty), ("k2", "Home")));

            CollectionAssert.AreEquivalent(new List<string> { "k2=Work" }, SavedMap());
        }

        // A profile deleted while the dialog was open: its assignment is not written back, neither
        // the one the dialog shows nor one left in the file.
        [TestMethod]
        public void AssignmentsToProfilesThatNoLongerExist_AreDropped()
        {
            WriteFile(autoSwitch: true, ("k1", "Work"), ("k3", "Gone"));
            DeviceProfileManager.DeviceAssignmentsBaseline baseline = DeviceProfileManager.LoadBaseline();
            Assert.IsTrue(ProfileManager.DeleteProfile("Home"));

            DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: true, Rows(("k1", "Work"), ("k2", "Home")));

            CollectionAssert.AreEquivalent(new List<string> { "k1=Work" }, SavedMap());
        }

        // The user assigned a keyboard to a profile that another window deleted meanwhile: the save
        // reports it and names the profile, so the dialog can tell the user. A stale entry that was
        // already in the file is dropped silently.
        [TestMethod]
        public void AssignmentTheUserMadeToADeletedProfile_IsReported()
        {
            WriteFile(autoSwitch: true, ("k1", "Work"), ("k3", "Gone"));
            DeviceProfileManager.DeviceAssignmentsBaseline baseline = DeviceProfileManager.LoadBaseline();
            Assert.IsTrue(ProfileManager.DeleteProfile("Home"));

            Assert.AreEqual(
                DeviceProfileManager.DeviceAssignmentsSaveResult.SavedWithDroppedAssignments,
                DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: true, Rows(("k1", "Work"), ("k2", "Home"), ("k4", "Home")), out IReadOnlyList<string> dropped));

            CollectionAssert.AreEqual(new List<string> { "Home" }, dropped.ToList());
            CollectionAssert.AreEquivalent(new List<string> { "k1=Work" }, SavedMap());
        }

        // The keyboard already had a valid assignment (Work) and the user changed it to a profile
        // (Home) that another window deleted meanwhile: the change is rejected and reported, and
        // the Work assignment stays.
        [TestMethod]
        public void ChangingAnAssignmentToADeletedProfile_KeepsTheExistingAssignment()
        {
            WriteFile(autoSwitch: true, ("k1", "Work"));
            DeviceProfileManager.DeviceAssignmentsBaseline baseline = DeviceProfileManager.LoadBaseline();
            Assert.IsTrue(ProfileManager.DeleteProfile("Home"));

            Assert.AreEqual(
                DeviceProfileManager.DeviceAssignmentsSaveResult.SavedWithDroppedAssignments,
                DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: true, Rows(("k1", "Home")), out IReadOnlyList<string> dropped));

            CollectionAssert.AreEqual(new List<string> { "Home" }, dropped.ToList());
            CollectionAssert.AreEquivalent(new List<string> { "k1=Work" }, SavedMap());
        }

        // Same, without files: the rejected keyboard's entry keeps its place and its neighbours.
        [TestMethod]
        public void MergeAssignments_RejectedReplacement_LeavesTheCurrentEntryInPlace()
        {
            var current = new List<DeviceAssignment>
            {
                new() { Device = "k1", Profile = "Work", Name = "One" },
                new() { Device = "k2", Profile = "Office", Name = "Two" },
            };
            var edited = new List<DeviceAssignment>
            {
                new() { Device = "k1", Profile = "Gone", Name = "One" },
                new() { Device = "k2", Profile = "Office", Name = "Two" },
            };

            List<DeviceAssignment> merged = DeviceProfileManager.MergeAssignments(
                current, current, edited, profile => profile != "Gone", out List<string> dropped);

            CollectionAssert.AreEqual(new List<string> { "Gone" }, dropped);
            CollectionAssert.AreEqual(
                new List<string> { "k1=Work", "k2=Office" },
                merged.Select(a => a.Device + "=" + a.Profile).ToList());
        }

        // The file names the profile in another letter case than the dialog shows ("work" / "Work"):
        // that is the same profile, so the row is unchanged, and its refreshed keyboard name is kept.
        [TestMethod]
        public void MergeAssignments_ProfileInAnotherLetterCase_IsUnchanged_AndTheNameIsRefreshed()
        {
            var current = new List<DeviceAssignment> { new() { Device = "k1", Profile = "work", Name = "Old name" } };
            var edited = new List<DeviceAssignment> { new() { Device = "k1", Profile = "Work", Name = "New name" } };

            List<DeviceAssignment> merged = DeviceProfileManager.MergeAssignments(
                current, current, edited, profile => true, out List<string> dropped);

            Assert.AreEqual(0, dropped.Count);
            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual("work", merged[0].Profile);
            Assert.AreEqual("New name", merged[0].Name);
        }

        // If the keyboard's old profile is gone too, nothing valid is left to keep.
        [TestMethod]
        public void ChangingAnAssignmentToADeletedProfile_WhenTheOldProfileIsAlsoGone_LeavesTheKeyboardUnassigned()
        {
            WriteFile(autoSwitch: true, ("k1", "Work"));
            DeviceProfileManager.DeviceAssignmentsBaseline baseline = DeviceProfileManager.LoadBaseline();
            Assert.IsTrue(ProfileManager.DeleteProfile("Home"));
            Assert.IsTrue(ProfileManager.DeleteProfile("Work"));

            Assert.AreEqual(
                DeviceProfileManager.DeviceAssignmentsSaveResult.SavedWithDroppedAssignments,
                DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: true, Rows(("k1", "Home")), out IReadOnlyList<string> dropped));

            CollectionAssert.AreEqual(new List<string> { "Home" }, dropped.ToList());
            Assert.AreEqual(0, SavedMap().Count);
        }

        [TestMethod]
        public void SaveWithoutDroppedAssignments_ReportsSaved_EvenWhenTheFileHadStaleEntries()
        {
            WriteFile(autoSwitch: true, ("k1", "Work"), ("k3", "Gone"));
            DeviceProfileManager.DeviceAssignmentsBaseline baseline = DeviceProfileManager.LoadBaseline();

            Assert.AreEqual(
                DeviceProfileManager.DeviceAssignmentsSaveResult.Saved,
                DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: true, Rows(("k1", "Work"), ("k2", "Home")), out IReadOnlyList<string> dropped));

            Assert.AreEqual(0, dropped.Count);
            CollectionAssert.AreEquivalent(new List<string> { "k1=Work", "k2=Home" }, SavedMap());
        }

        // deviceProfiles.json is replaced while a reader has it open without delete sharing (as the
        // engine's ifstream does) for a moment: the save waits for it instead of failing.
        [TestMethod]
        public void Save_WaitsForAReaderThatHasTheFileOpenForAMoment()
        {
            WriteFile(autoSwitch: false, ("k1", "Work"));
            DeviceProfileManager.DeviceAssignmentsBaseline baseline = DeviceProfileManager.LoadBaseline();

            using (ProfileStoreTests.HoldOpenBriefly(DeviceProfilesPath, millisecondsHeld: 150))
            {
                Assert.AreEqual(
                    DeviceProfileManager.DeviceAssignmentsSaveResult.Saved,
                    DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: false, Rows(("k1", "Home"))));
            }

            CollectionAssert.AreEquivalent(new List<string> { "k1=Home" }, SavedMap());
        }

        [TestMethod]
        public void UnreadableFile_IsNotOverwritten()
        {
            File.WriteAllText(DeviceProfilesPath, "{ \"map\": [ ");
            var baseline = new DeviceProfileManager.DeviceAssignmentsBaseline(false, Array.Empty<DeviceAssignment>());

            Assert.AreEqual(
                DeviceProfileManager.DeviceAssignmentsSaveResult.Unreadable,
                DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: true, Rows(("k1", "Work"))));
            Assert.AreEqual("{ \"map\": [ ", File.ReadAllText(DeviceProfilesPath));
        }

        // Saved under the editor transaction lock, which profile delete and create hold while they
        // prune assignments.
        [TestMethod]
        public void Save_WaitsForTheEditorTransactionLock_AndWritesNothingWithoutIt()
        {
            WriteFile(autoSwitch: false, ("k1", "Work"));
            string before = File.ReadAllText(DeviceProfilesPath);
            DeviceProfileManager.DeviceAssignmentsBaseline baseline = DeviceProfileManager.LoadBaseline();

            using (FileStream held = SettingsManager.TryAcquireMappingTransactionLock())
            {
                Assert.IsNotNull(held);
                Assert.AreEqual(
                    DeviceProfileManager.DeviceAssignmentsSaveResult.Busy,
                    DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: true, Rows(("k1", "Home")), lockTimeoutMS: 0));
            }

            Assert.AreEqual(before, File.ReadAllText(DeviceProfilesPath));
        }

        // The engine's cycle hotkey is kept, and no temporary file is left behind.
        [TestMethod]
        public void Save_KeepsTheCycleHotkey_AndLeavesNoTemporaryFile()
        {
            File.WriteAllText(
                DeviceProfilesPath,
                "{ \"autoSwitchEnabled\": false, \"map\": [], \"cycleHotkey\": { \"key\": 112, \"ctrl\": true } }");
            DeviceProfileManager.DeviceAssignmentsBaseline baseline = DeviceProfileManager.LoadBaseline();

            Assert.AreEqual(
                DeviceProfileManager.DeviceAssignmentsSaveResult.Saved,
                DeviceProfileManager.SaveChanges(baseline, autoSwitchEnabled: false, Rows(("k1", "Work"))));

            JsonNode root = JsonNode.Parse(File.ReadAllText(DeviceProfilesPath));
            Assert.AreEqual(112, root?["cycleHotkey"]?["key"]?.GetValue<int>());
            Assert.AreEqual(1, Directory.EnumerateFiles(_directory, "deviceProfiles*").Count());
        }

        // Deleting a profile prunes the keyboards assigned to it. The engine opens "<profile>.json"
        // on a file system that ignores case, so an assignment that differs only in case points at
        // the same profile; left behind, a profile created later under that name would inherit it.
        [TestMethod]
        public void RemoveAssignmentsForProfile_AlsoRemovesAnAssignmentThatDiffersOnlyInCase()
        {
            WriteFile(autoSwitch: true, ("k1", "work"), ("k2", "Home"));

            Assert.IsTrue(DeviceProfileManager.RemoveAssignmentsForProfile("Work"));

            CollectionAssert.AreEquivalent(new List<string> { "k2=Home" }, SavedMap());
        }

        // A file that exists but cannot be read (held open by another process) is not "nothing to
        // remove": the prune reports failure and the file is left as it is.
        [TestMethod]
        public void RemoveAssignmentsForProfile_WhenTheFileCannotBeRead_FailsAndLeavesTheFile()
        {
            WriteFile(autoSwitch: true, ("k1", "Work"));
            string before = File.ReadAllText(DeviceProfilesPath);

            using (new FileStream(DeviceProfilesPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.IsFalse(DeviceProfileManager.RemoveAssignmentsForProfile("Work"));
            }

            Assert.AreEqual(before, File.ReadAllText(DeviceProfilesPath));
        }

        // Creating a profile whose id an earlier profile left assignments for: if those cannot be
        // checked, creation fails instead of letting the new profile inherit them later.
        [TestMethod]
        public void CreateProfile_WhenLeftoverAssignmentsCannotBeChecked_Fails()
        {
            WriteFile(autoSwitch: true, ("k1", "Office"));

            using (new FileStream(DeviceProfilesPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.IsFalse(ProfileManager.CreateProfile("Office", copyFromActive: false));
            }

            Assert.IsFalse(ProfileManager.GetProfiles().Contains("Office", StringComparer.OrdinalIgnoreCase));
            CollectionAssert.AreEquivalent(new List<string> { "k1=Office" }, SavedMap());
        }

        // A hand edit left deviceProfiles.json unparsable (or "null", or empty). It cannot name a new
        // profile, so creating one still works; the file is left as it is. The name only inside a
        // longer string (a keyboard named "Office Keyboard") does not count.
        [DataTestMethod]
        [DataRow("{ \"map\": [], \"cycleHotkey\": { \"key\": 112, }, ")]
        [DataRow("{ \"map\": [ { \"device\": \"k1\", \"name\": \"Office Keyboard\", \"profile\": \"Home\" } ], \"cycleHotkey\": { \"key\": 112, }, ")]
        [DataRow("null")]
        [DataRow("")]
        public void CreateProfile_WhenTheFileCannotBeParsedAndDoesNotNameTheProfile_Works(string content)
        {
            File.WriteAllText(DeviceProfilesPath, content);

            Assert.IsTrue(ProfileManager.CreateProfile("Office", copyFromActive: false));

            Assert.AreEqual(content, File.ReadAllText(DeviceProfilesPath));
        }

        // The unparsable file may still hold an assignment to that id, in any letter case or with the
        // name escaped: creation fails rather than letting the new profile inherit it later.
        [DataTestMethod]
        [DataRow("Office", "{ \"map\": [ { \"device\": \"k1\", \"profile\": \"office\" }, ")]
        [DataRow("仕事", "{ \"map\": [ { \"device\": \"k1\", \"profile\": \"\\u4ED5\\u4E8B\" }, ")]
        public void CreateProfile_WhenTheFileCannotBeParsedAndMayNameTheProfile_Fails(string profile, string content)
        {
            File.WriteAllText(DeviceProfilesPath, content);

            Assert.IsFalse(ProfileManager.CreateProfile(profile, copyFromActive: false));

            Assert.IsFalse(ProfileManager.GetProfiles().Contains(profile, StringComparer.OrdinalIgnoreCase));

            // It was the file: without it the same name is created.
            File.Delete(DeviceProfilesPath);
            Assert.IsTrue(ProfileManager.CreateProfile(profile, copyFromActive: false));
        }

        // A hand edit left "map": null or a null entry. Both parse; the assignments read as empty (or
        // without the null entry) instead of throwing, so Auto-switch can still be opened to fix them.
        [DataTestMethod]
        [DataRow("{ \"map\": null }", "")]
        [DataRow("{ \"map\": [ null, { \"device\": \"k1\", \"profile\": \"Office\" } ] }", "k1=Office")]
        public void GetSavedAssignments_WithANullMapOrEntry_DoesNotThrow(string content, string expected)
        {
            File.WriteAllText(DeviceProfilesPath, content);

            Assert.AreEqual(expected, string.Join(",", SavedMap()));
        }

        // The same files on the update path: deleting another profile's assignments still works, keeps
        // k1 = Office and drops only the null entry.
        [DataTestMethod]
        [DataRow("{ \"map\": null }", "")]
        [DataRow("{ \"map\": [ null, { \"device\": \"k1\", \"profile\": \"Office\" }, { \"device\": \"k2\", \"profile\": \"Home\" } ] }", "k1=Office")]
        public void RemoveAssignmentsForProfile_WithANullMapOrEntry_Works(string content, string expected)
        {
            File.WriteAllText(DeviceProfilesPath, content);

            Assert.IsTrue(DeviceProfileManager.RemoveAssignmentsForProfile("Home"));

            Assert.AreEqual(expected, string.Join(",", SavedMap()));
        }

        private static List<DeviceAssignment> Rows(params (string Device, string Profile)[] rows) =>
            rows.Select(r => new DeviceAssignment { Device = r.Device, Profile = r.Profile, Name = r.Device }).ToList();

        private static List<string> SavedMap() =>
            DeviceProfileManager.GetSavedAssignments().Select(a => $"{a.Device}={a.Profile}").ToList();

        private void WriteFile(bool autoSwitch, params (string Device, string Profile)[] map)
        {
            var root = new JsonObject
            {
                ["autoSwitchEnabled"] = autoSwitch,
                ["map"] = new JsonArray(map.Select(m => (JsonNode)new JsonObject { ["device"] = m.Device, ["profile"] = m.Profile, ["name"] = m.Device }).ToArray()),
            };
            File.WriteAllText(DeviceProfilesPath, root.ToJsonString());
        }
    }
}
