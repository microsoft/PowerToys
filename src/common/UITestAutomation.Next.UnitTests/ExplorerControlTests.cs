// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class ExplorerControlTests
{
    [TestMethod]
    [DataRow("C:\\folder with spaces\\Cafe\u0301")]
    [DataRow("shell:MyComputerFolder")]
    public void LaunchPreservesExplorerFolderGrammar(string folder)
    {
        var startInfo = ExplorerControl.CreateStartInfo(folder);

        Assert.AreEqual("explorer.exe", startInfo.FileName);
        Assert.AreEqual($"/n,\"{folder}\"", startInfo.Arguments);
        Assert.IsTrue(startInfo.UseShellExecute);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("folder\" /unexpected")]
    public void LaunchRejectsInvalidParsingNames(string folder) =>
        Assert.Throws<ArgumentException>(() => ExplorerControl.CreateStartInfo(folder));

    [TestMethod]
    public void FreshTaskbarRequiresANewExplorerPidAndVisibleBounds()
    {
        var previousIds = new HashSet<int> { 10, 11 };
        var fresh = Window(20, "taskbar", 12, ExplorerControl.TaskbarWindowClassName);

        Assert.IsTrue(ExplorerControl.IsFreshTaskbar(fresh, previousIds));
        Assert.IsFalse(ExplorerControl.IsFreshTaskbar(fresh with { ProcessId = 10 }, previousIds));
        Assert.IsFalse(ExplorerControl.IsFreshTaskbar(fresh with { ProcessName = "other" }, previousIds));
        Assert.IsFalse(ExplorerControl.IsFreshTaskbar(fresh with { ClassName = ExplorerControl.FileWindowClassName }, previousIds));
        Assert.IsFalse(ExplorerControl.IsFreshTaskbar(fresh with { Width = 0 }, previousIds));
        Assert.IsFalse(ExplorerControl.IsFreshTaskbar(fresh with { Hwnd = 0 }, previousIds));
    }

    [TestMethod]
    public void ReplacementExcludesPreviousWindowAndPrefersMatchingForeground()
    {
        var previous = Window(1, "Output", 10);
        var background = Window(2, "Output", 10);
        var foreground = Window(3, "Output", 10);
        var otherFolder = Window(4, "Other folder", 10);
        var taskbar = Window(5, "Output", 10, ExplorerControl.TaskbarWindowClassName);

        var selected = ExplorerControl.SelectReplacementWindow(
            [previous, background, otherFolder, taskbar, foreground],
            previousHandle: 1,
            foregroundHandle: 3,
            folderPath: @"C:\fixtures\Output");

        Assert.AreSame(foreground, selected);
    }

    [TestMethod]
    public void ReplacementCanTargetShellNamespacesWithoutAFilesystemTitleFilter()
    {
        var previous = Window(1, "This PC", 10);
        var replacement = Window(2, "This PC", 10);

        Assert.AreSame(
            replacement,
            ExplorerControl.SelectReplacementWindow([previous, replacement], 1, 2, folderPath: null));
    }

    [TestMethod]
    public void ReplacementDoesNotFallBackToAnUnrelatedFolder()
    {
        Assert.IsNull(ExplorerControl.SelectReplacementWindow(
            [Window(1, "Output", 10), Window(2, "Other", 10)],
            1,
            2,
            @"C:\fixtures\Output"));
    }

    [TestMethod]
    public void SessionBindingPreservesWindowIdentity()
    {
        var window = Window(123, "Output", 42);
        var session = ExplorerControl.CreateSession(window, PowerToysModule.PowerRename);

        Assert.AreEqual(123L, session.WindowHandle);
        Assert.AreEqual(42, session.ProcessId);
        Assert.AreEqual(PowerToysModule.PowerRename, session.InitScope);
        Assert.AreEqual("Output", session.WindowTitle);
    }

    private static WindowsFinder.WindowInfo Window(
        long handle,
        string title,
        int processId,
        string className = ExplorerControl.FileWindowClassName) =>
        new(handle, title, ExplorerControl.ProcessName, processId, className, 100, 100);
}
