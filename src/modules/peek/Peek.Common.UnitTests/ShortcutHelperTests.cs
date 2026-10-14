// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.Common.Helpers;

namespace Peek.Common.UnitTests
{
    [TestClass]
    public class ShortcutHelperTests
    {
        private string testDirectory = string.Empty;

        [TestInitialize]
        public void Setup()
        {
            testDirectory = Directory.CreateTempSubdirectory("PeekShortcutHelperTests").FullName;
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, true);
                }
            }
            catch (IOException)
            {
                // The shell may still hold a shortcut open. Leaving the temporary folder behind
                // does not affect the test result.
            }
            catch (UnauthorizedAccessException)
            {
                // Same as above: cleanup is best effort.
            }
        }

        /// <summary>
        /// Product code: ShortcutHelper.IsShortcut(string)
        /// What: Verifies the shortcut file extension is recognized regardless of casing
        /// Why: Explorer writes .lnk files, but the stored casing is not guaranteed
        /// </summary>
        [TestMethod]
        public void IsShortcut_ShortcutFileExtension_ShouldReturnTrue()
        {
            Assert.IsTrue(ShortcutHelper.IsShortcut(@"C:\Temp\My Shortcut.LnK"));
        }

        /// <summary>
        /// Product code: ShortcutHelper.IsShortcut(string)
        /// What: Verifies that other files and empty values are not treated as shortcuts
        /// Why: Only .lnk files can be resolved to a target
        /// </summary>
        [TestMethod]
        public void IsShortcut_OtherValues_ShouldReturnFalse()
        {
            Assert.IsFalse(ShortcutHelper.IsShortcut(@"C:\Temp\image.png"));
            Assert.IsFalse(ShortcutHelper.IsShortcut(string.Empty));
            Assert.IsFalse(ShortcutHelper.IsShortcut(null));
        }

        /// <summary>
        /// Product code: ShortcutHelper.TryGetTargetPath(string)
        /// What: Verifies the target of a shortcut to a file is resolved
        /// Why: Peek shows the target path of a shortcut and offers to peek that file
        /// </summary>
        [TestMethod]
        public void TryGetTargetPath_ShortcutToFile_ShouldReturnTargetPath()
        {
            string targetPath = Path.Combine(testDirectory, "target-file.txt");
            File.WriteAllText(targetPath, "target");

            string shortcutPath = CreateShortcut("shortcut-to-file.lnk", targetPath);

            var targetResult = ShortcutHelper.TryGetTargetPath(shortcutPath);

            Assert.IsNotNull(targetResult);
            Assert.AreEqual(targetPath, targetResult, true, CultureInfo.InvariantCulture);
            Assert.IsTrue(ShortcutHelper.TargetExists(targetResult));
        }

        /// <summary>
        /// Product code: ShortcutHelper.TryGetTargetPath(string)
        /// What: Verifies the target of a shortcut to a folder is resolved
        /// Why: Folder targets are previewed by the folder previewer
        /// </summary>
        [TestMethod]
        public void TryGetTargetPath_ShortcutToFolder_ShouldReturnTargetPath()
        {
            string targetPath = Path.Combine(testDirectory, "target-folder");
            Directory.CreateDirectory(targetPath);

            string shortcutPath = CreateShortcut("shortcut-to-folder.lnk", targetPath);

            var targetResult = ShortcutHelper.TryGetTargetPath(shortcutPath);

            Assert.IsNotNull(targetResult);
            Assert.AreEqual(targetPath, targetResult, true, CultureInfo.InvariantCulture);
            Assert.IsTrue(ShortcutHelper.TargetExists(targetResult));
        }

        /// <summary>
        /// Product code: ShortcutHelper.TryGetTargetPath(string)
        /// What: Verifies that a shortcut whose target no longer exists still reports that target
        /// Why: The shortcut card shows the stored target, but only offers to peek it when it exists
        /// </summary>
        [TestMethod]
        public void TryGetTargetPath_ShortcutWithMissingTarget_ShouldReturnPathThatDoesNotExist()
        {
            string targetPath = Path.Combine(testDirectory, "deleted-target.txt");
            File.WriteAllText(targetPath, "target");

            string shortcutPath = CreateShortcut("shortcut-to-deleted-file.lnk", targetPath);

            File.Delete(targetPath);

            var targetResult = ShortcutHelper.TryGetTargetPath(shortcutPath);

            Assert.IsNotNull(targetResult);
            Assert.AreEqual(targetPath, targetResult, true, CultureInfo.InvariantCulture);
            Assert.IsFalse(ShortcutHelper.TargetExists(targetResult));
        }

        /// <summary>
        /// Product code: ShortcutHelper.TryGetTargetPath(string)
        /// What: Verifies that files which are not shortcuts are not resolved
        /// Why: Regular files have to be previewed as they are
        /// </summary>
        [TestMethod]
        public void TryGetTargetPath_NotAShortcut_ShouldReturnNull()
        {
            string path = Path.Combine(testDirectory, "not-a-shortcut.txt");
            File.WriteAllText(path, "content");

            Assert.IsNull(ShortcutHelper.TryGetTargetPath(path));
        }

        /// <summary>
        /// Creates a shortcut with the Windows Script Host COM object, which is present on every
        /// Windows installation.
        /// </summary>
        /// <param name="shortcutFileName">The file name of the shortcut to create.</param>
        /// <param name="targetPath">The target the shortcut should point to.</param>
        /// <returns>The full path of the created shortcut.</returns>
        private string CreateShortcut(string shortcutFileName, string targetPath)
        {
            string shortcutPath = Path.Combine(testDirectory, shortcutFileName);

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            Assert.IsNotNull(shellType, "The Windows Script Host COM server is required to create shortcuts for this test.");

            dynamic shell = Activator.CreateInstance(shellType);
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.Save();

            return shortcutPath;
        }
    }
}
