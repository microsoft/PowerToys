// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
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
            CollectionAssert.Contains(new System.Collections.Generic.List<string>(ProfileManager.GetProfiles()), "Work");
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

        [TestMethod]
        public void CreateProfile_DoesNotOverwriteAnExistingProfileFile()
        {
            string path = Path.Combine(_directory, "Work.json");
            File.WriteAllText(path, "copied mappings");

            Assert.IsFalse(ProfileManager.CreateProfile("Work", copyFromActive: false));
            Assert.AreEqual("copied mappings", File.ReadAllText(path));
        }
    }
}
