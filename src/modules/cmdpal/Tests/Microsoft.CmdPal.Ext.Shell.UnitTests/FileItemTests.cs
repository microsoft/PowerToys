// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using Microsoft.CmdPal.Ext.Run;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Shell.UnitTests;

[TestClass]
public class FileItemTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Icon_RequestsTheShellIconOfThePath(bool commandLine)
    {
        var path = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var item = commandLine
            ? (FileItem)new RunCommandLineItem(path, "cmd /c echo test", null)
            : new RunExeItem("cmd", "/c echo test", path, null);

        Assert.AreEqual(path, ShellItemPath(item));
    }

    [TestMethod]
    public void Icon_AddsTheCurrentDriveToARootedPath()
    {
        var item = new RunExeItem("notepad", string.Empty, "\\Windows\\notepad.exe", null);

        var itemPath = ShellItemPath(item);
        Assert.AreEqual(Path.GetPathRoot(Environment.CurrentDirectory), Path.GetPathRoot(itemPath));
        Assert.IsTrue(itemPath.EndsWith("\\Windows\\notepad.exe", StringComparison.Ordinal), itemPath);
    }

    [TestMethod]
    public void Icon_ResolvesARelativePathAgainstTheCurrentDirectory()
    {
        var item = new RunExeItem("README.md", string.Empty, "README.md", null);

        // A file check resolves the same relative path against the current directory, not the drive root.
        Assert.AreEqual(Path.GetFullPath("README.md"), ShellItemPath(item));
    }

    [TestMethod]
    public void Icon_KeepsShortNamesAsTheyAre()
    {
        const string ShortPath = "C:\\PROGRA~1\\cmdpal\\tool.exe";
        var item = new RunExeItem("tool", string.Empty, ShortPath, null);

        // Expanding PROGRA~1 to its long name looks it up on disk, which can stall on a network share.
        Assert.AreEqual(ShortPath, ShellItemPath(item));
    }

    [TestMethod]
    [DataRow("C:hosts")]
    [DataRow("Z:")]
    public void Icon_IsTheRunIconForADriveRelativePath(string path)
    {
        var item = new RunExeItem(path, string.Empty, path, null);

        // Resolving one checks that drive's saved directory, which can stall on a mapped drive.
        Assert.AreSame(Icons.RunGeneric, item.Icon);
    }

    [TestMethod]
    public void Icon_KeepsAVerbatimPathAsItIs()
    {
        const string VerbatimPath = "\\\\?\\C:\\Windows\\System32\\drivers\\etc\\hosts.";
        var item = new RunExeItem("hosts.", string.Empty, VerbatimPath, null);

        // Under \\?\ a trailing dot is part of the name, so normalizing would name a different file.
        Assert.AreEqual(VerbatimPath, ShellItemPath(item));
    }

    [TestMethod]
    public void Icon_IsTheRunIconForAnEmptyPath()
    {
        var item = new RunExeItem(string.Empty, string.Empty, string.Empty, null);

        Assert.AreSame(Icons.RunGeneric, item.Icon);
    }

    private static string ShellItemPath(FileItem item)
    {
        Assert.IsTrue(ShellItemIconProtocol.TryParse(item.Icon?.Light?.Icon, out var itemPath, out var jumbo), item.Icon?.Light?.Icon);
        Assert.IsFalse(jumbo);
        return itemPath;
    }
}
