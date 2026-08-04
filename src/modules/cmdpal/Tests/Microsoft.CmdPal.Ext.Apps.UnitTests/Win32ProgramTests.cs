// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class Win32ProgramTests
{
    [TestMethod]
    [DataRow(null, 0)]
    [DataRow("%SystemRoot%\\System32\\shell32.dll", 0)]
    [DataRow("%SystemRoot%\\System32\\shell32.dll", 3)]
    [DataRow("%SystemRoot%\\System32\\shell32.dll", -4)]
    [DataRow("C:\\Icons, custom\\APP.ICO", 0)]
    public void Shortcut_UsesIconLocationWithoutChangingLaunchPath(string iconPath, int iconIndex)
    {
        var shortcutPath = Path.Combine(Path.GetTempPath(), $"CmdPal-icon-{Guid.NewGuid():N}.lnk");
        var targetPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        try
        {
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = "/c echo shortcut";
            shortcut.WorkingDirectory = Environment.SystemDirectory;
            shortcut.Description = "Shortcut description";
            if (iconPath is not null)
            {
                shortcut.IconLocation = FormattableString.Invariant($"{iconPath},{iconIndex}");
            }

            shortcut.Save();

            var program = Win32Program.LoadFromPath(shortcutPath, asRunCommand: false);
            Assert.IsNotNull(program);
            Assert.IsTrue(program.Valid);
            Assert.AreEqual("Shortcut description", program.Description);
            var expectedIcon = iconPath is null
                ? targetPath
                : FormattableString.Invariant($"{Environment.ExpandEnvironmentVariables(iconPath)},{iconIndex}");
            Assert.IsTrue(string.Equals(expectedIcon, program.IcoPath, StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(string.Equals(targetPath, program.FullPath, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(shortcutPath, program.LnkFilePath);
            Assert.AreEqual(Environment.SystemDirectory, program.WorkingDirectory);

            var payload = Win32AppPayload.From(program);
            Assert.AreEqual(Environment.SystemDirectory, payload.WorkingDirectory);
            var app = payload.ToAppItem();
            Assert.AreEqual(shortcutPath, app.ExePath);
            Assert.AreEqual("/c echo shortcut", app.Arguments);
            var item = new AppListItem(app, useThumbnails: true);
            Assert.IsTrue(AppIconProtocol.TryParse(item.Icon.Light.Icon, out var candidates, out var jumbo));
            Assert.IsFalse(jumbo);
            CollectionAssert.AreEqual(
                iconPath is null ? new[] { targetPath } : new[] { expectedIcon, targetPath },
                candidates,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shortcut);
            Marshal.FinalReleaseComObject(shell);
            File.Delete(shortcutPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Shortcut_UnreadableIsInvalid(bool createInvalidFile)
    {
        var shortcutPath = Path.Combine(Path.GetTempPath(), $"CmdPal-invalid-{Guid.NewGuid():N}.lnk");
        var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
        try
        {
            if (createInvalidFile)
            {
                File.WriteAllText(shortcutPath, "Invalid shortcut");
            }

            Assert.IsNull(ShellLinkReader.Read(shortcutPath));
            Assert.IsFalse(Win32Program.LoadFromPath(shortcutPath, asRunCommand: false).Valid);
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
            File.Delete(shortcutPath);
        }
    }

    [TestMethod]
    public void DeduplicatePrograms_DifferentArguments_KeepsBothPrograms()
    {
        var first = TestDataHelper.CreateTestWin32Program("Console", @"C:\Tools\console.exe");
        first.Arguments = "--profile first";

        var second = TestDataHelper.CreateTestWin32Program("Console", @"C:\Tools\console.exe");
        second.Arguments = "--profile second";

        var result = Win32Program.DeduplicatePrograms([first, second]);

        Assert.AreEqual(2, result.Count);
    }

    [TestMethod]
    [DataRow(@"C:\Tools\console.exe", "", "")]
    [DataRow(@"C:\Tools\console.exe", "c:/TOOLS/", "")]
    [DataRow(@"%SystemRoot%\System32\cmd.exe", @"%SystemRoot%\System32\", "")]
    [DataRow(@"C:\Tools\console.exe", "c:/Projects/Work/", @"c:\Projects\Work")]
    [DataRow(@"C:\Tools\console.lnk", @"C:\Tools", @"C:\Tools")]
    [DataRow(@"C:\Tools\console.exe", ".", null)]
    public void GetDistinctWorkingDirectory_NormalizesOnlyDefaultExecutableDirectories(string target, string directory, string expected)
    {
        Assert.AreEqual(expected ?? Path.GetFullPath(directory), Win32Program.GetDistinctWorkingDirectory(target, directory));
    }

    [TestMethod]
    public void DeduplicatePrograms_DefaultWorkingDirectory_RemovesDuplicate()
    {
        var first = TestDataHelper.CreateTestWin32Program("Console", @"C:\Tools\console.exe");
        var second = TestDataHelper.CreateTestWin32Program("Console", @"C:\Tools\console.exe");
        second.WorkingDirectory = "c:/TOOLS/";

        Assert.AreEqual(1, Win32Program.DeduplicatePrograms([first, second]).Count);
        Assert.AreEqual("c:/TOOLS/", second.WorkingDirectory, "Identity normalization must retain launch data.");
    }

    [TestMethod]
    public void DeduplicatePrograms_DifferentWorkingDirectories_KeepsBothPrograms()
    {
        var first = TestDataHelper.CreateTestWin32Program("Console", @"C:\Tools\console.exe");
        first.WorkingDirectory = @"C:\Projects\First";
        var second = TestDataHelper.CreateTestWin32Program("Console", @"C:\Tools\console.exe");
        second.WorkingDirectory = @"C:\Projects\Second";

        Assert.AreEqual(2, Win32Program.DeduplicatePrograms([first, second]).Count);
    }

    [TestMethod]
    public void DeduplicatePrograms_CaseOnlyDifference_RemovesDuplicate()
    {
        var first = TestDataHelper.CreateTestWin32Program("Console", @"C:\Tools\console.exe");
        first.Arguments = "--profile default";
        first.WorkingDirectory = @"C:\Projects\Default";

        var second = TestDataHelper.CreateTestWin32Program("CONSOLE", @"c:\tools\CONSOLE.exe");
        second.Arguments = "--profile default";
        second.WorkingDirectory = @"c:\projects\DEFAULT";

        var result = Win32Program.DeduplicatePrograms([first, second]);

        Assert.AreEqual(1, result.Count);
    }

    [TestMethod]
    [DataRow("com.squirrel.GitHubDesktop.GitHubDesktop", @"C:\Apps\GitHubDesktop\app-2.7.2", true)]
    [DataRow("COM.SQUIRREL.GITHUBDESKTOP.GITHUBDESKTOP", "c:/apps/GitHubDesktop/app-3.6.3/", true)]
    [DataRow("", @"C:\Apps\GitHubDesktop\app-2.7.2", false)]
    [DataRow("Contoso.GitHubDesktop", @"C:\Apps\GitHubDesktop\app-2.7.2", false)]
    [DataRow("com.squirrel.Other.GitHubDesktop", @"C:\Apps\GitHubDesktop\app-2.7.2", false)]
    [DataRow("com.squirrel.GitHubDesktop.Other", @"C:\Apps\GitHubDesktop\app-2.7.2", false)]
    [DataRow("com.squirrel.GitHubDesktop.GitHubDesktop", @"C:\Projects\app-2.7.2", false)]
    [DataRow("com.squirrel.GitHubDesktop.GitHubDesktop", @"C:\Apps\GitHubDesktop\app-2.7.2\Work", false)]
    [DataRow("com.squirrel.GitHubDesktop.GitHubDesktop", @"C:\Apps\GitHubDesktop\app-Work", false)]
    public void GetDistinctWorkingDirectory_RecognizesOnlySquirrelInstallerVersionDirectories(string explicitId, string directory, bool isDefault)
    {
        var distinctDirectory = Win32Program.GetDistinctWorkingDirectory(@"C:\Apps\GitHubDesktop\GitHubDesktop.exe", directory, explicitId);
        Assert.AreEqual(isDefault, string.IsNullOrEmpty(distinctDirectory));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DeduplicatePrograms_SquirrelVersionDirectoriesPreserveArguments(bool differentArguments)
    {
        const string target = @"C:\Apps\GitHubDesktop\GitHubDesktop.exe";
        const string explicitId = "com.squirrel.GitHubDesktop.GitHubDesktop";
        var first = TestDataHelper.CreateTestWin32Program("GitHub Desktop", target);
        first.ExplicitAppUserModelId = explicitId;
        first.WorkingDirectory = @"C:\Apps\GitHubDesktop\app-2.7.2";
        var second = TestDataHelper.CreateTestWin32Program("GitHub Desktop", target);
        second.ExplicitAppUserModelId = explicitId;
        second.WorkingDirectory = @"C:\Apps\GitHubDesktop\app-3.6.3";
        second.Arguments = differentArguments ? "--profile Work" : string.Empty;

        Assert.AreEqual(differentArguments ? 2 : 1, Win32Program.DeduplicatePrograms([first, second]).Count);
        Assert.AreEqual(@"C:\Apps\GitHubDesktop\app-2.7.2", first.WorkingDirectory);
        Assert.AreEqual(@"C:\Apps\GitHubDesktop\app-3.6.3", second.WorkingDirectory);
    }
}
