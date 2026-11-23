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
    }
}
