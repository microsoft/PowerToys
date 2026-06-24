// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class PathHelpersTests
{
    [TestMethod]
    public void IsPathInsideDirectory_System32Child_ReturnsTrue()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system32 = Path.Combine(systemRoot, "System32");
        var child = Path.Combine(system32, "cmd.exe");

        Assert.IsTrue(PathHelpers.IsPathInsideDirectory(child, system32));
    }

    [TestMethod]
    public void IsPathInsideDirectory_SimilarPrefixNotChild_ReturnsFalse()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system32 = Path.Combine(systemRoot, "System32");

        // Construct a path that contains the sequence but is not under System32
        var notChild = Path.Combine(systemRoot, "System32Apps", "MyApp.exe");

        Assert.IsFalse(PathHelpers.IsPathInsideDirectory(notChild, system32));
    }

    [TestMethod]
    public void IsPathInsideDirectory_TrailingSeparatorHandling_ReturnsTrue()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system32 = Path.Combine(systemRoot, "System32") + Path.DirectorySeparatorChar;
        var child = Path.Combine(systemRoot, "System32", "notepad.exe");

        Assert.IsTrue(PathHelpers.IsPathInsideDirectory(child, system32));
    }

    [TestMethod]
    public void IsPathInsideDirectory_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(PathHelpers.IsPathInsideDirectory(null, @"C:\Windows"));
        Assert.IsFalse(PathHelpers.IsPathInsideDirectory(string.Empty, @"C:\Windows"));
        Assert.IsFalse(PathHelpers.IsPathInsideDirectory(@"C:\Windows\cmd.exe", null));
        Assert.IsFalse(PathHelpers.IsPathInsideDirectory(@"C:\Windows\cmd.exe", string.Empty));
    }

    [TestMethod]
    public void IsPathInsideDirectory_CaseInsensitive_ReturnsTrue()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var path = Path.Combine(systemRoot.ToLowerInvariant(), "system32", "notepad.exe");

        Assert.IsTrue(PathHelpers.IsPathInsideDirectory(path, systemRoot));
    }

    [TestMethod]
    public void IsSystemRootPath_SystemExecutable_ReturnsTrue()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var path = Path.Combine(systemRoot, "regedit.exe");

        Assert.IsTrue(PathHelpers.IsSystemRootPath(path));
    }

    [TestMethod]
    public void IsSystemRootPath_System32Executable_ReturnsTrue()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var path = Path.Combine(systemRoot, "System32", "notepad.exe");

        Assert.IsTrue(PathHelpers.IsSystemRootPath(path));
    }

    [TestMethod]
    public void IsSystemRootPath_ProgramFilesApp_ReturnsFalse()
    {
        Assert.IsFalse(PathHelpers.IsSystemRootPath(@"C:\Program Files\MyApp\app.exe"));
    }

    [TestMethod]
    public void IsSystemRootPath_FakeWindowsPrefix_ReturnsFalse()
    {
        Assert.IsFalse(PathHelpers.IsSystemRootPath(@"C:\WindowsFake\System32\notepad.exe"));
    }

    [TestMethod]
    public void IsSystemRootPath_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(PathHelpers.IsSystemRootPath(null));
        Assert.IsFalse(PathHelpers.IsSystemRootPath(string.Empty));
    }

    [TestMethod]
    public void IsShortcutFile_LnkExtension_ReturnsTrue()
    {
        Assert.IsTrue(PathHelpers.IsShortcutFile(@"C:\Users\test\Desktop\target.lnk"));
    }

    [TestMethod]
    public void IsShortcutFile_LnkExtensionUpperCase_ReturnsTrue()
    {
        Assert.IsTrue(PathHelpers.IsShortcutFile(@"C:\Users\test\Desktop\target.LNK"));
    }

    [TestMethod]
    public void IsShortcutFile_ExeExtension_ReturnsFalse()
    {
        Assert.IsFalse(PathHelpers.IsShortcutFile(@"C:\Program Files\App\app.exe"));
    }

    [TestMethod]
    public void IsShortcutFile_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(PathHelpers.IsShortcutFile(null));
        Assert.IsFalse(PathHelpers.IsShortcutFile(string.Empty));
    }
}
