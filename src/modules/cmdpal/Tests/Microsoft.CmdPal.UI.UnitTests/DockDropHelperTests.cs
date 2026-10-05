// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class DockDropHelperTests
{
    [TestMethod]
    [DataRow("Disney.37853FC22B2CE_6rarf9sa4v8jt!App", "Disney+")]
    [DataRow("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", "Calculator")]
    [DataRow("Microsoft.Office.POWERPNT.EXE.15", "PowerPoint")]
    [DataRow("Chrome", "Google Chrome")]
    [DataRow("MSEdge", "Microsoft Edge")]
    [DataRow("Microsoft.Windows.Explorer", "File Explorer")]
    [DataRow("zoom.us.Zoom Video Meetings", "Zoom")]
    public void GetBookmark_App_UsesAppsFolderAndDisplayName(string appUserModelId, string displayName)
    {
        var bookmark = DockDropHelper.GetBookmark(appUserModelId, id =>
        {
            Assert.AreEqual(appUserModelId, id);
            return displayName;
        });

        Assert.AreEqual(displayName, bookmark.Name);
        Assert.AreEqual("shell:AppsFolder\\" + appUserModelId, bookmark.Target);
        Assert.IsFalse(bookmark.ShowSubtitles);
    }

    [TestMethod]
    [DataRow("shell:AppsFolder\\", "Disney.37853FC22B2CE_6rarf9sa4v8jt!App")]
    [DataRow("SHELL:APPSFOLDER\\", "Disney.37853FC22B2CE_6rarf9sa4v8jt!App")]
    [DataRow("shell:AppsFolder\\", "Microsoft.Office.POWERPNT.EXE.15")]
    [DataRow("SHELL:APPSFOLDER\\", "Microsoft.Office.POWERPNT.EXE.15")]
    public void GetBookmark_QualifiedApp_DoesNotDuplicatePrefix(string prefix, string appUserModelId)
    {
        var bookmark = DockDropHelper.GetBookmark(prefix + appUserModelId, id =>
        {
            Assert.AreEqual(appUserModelId, id);
            return "Application";
        });

        Assert.AreEqual("Application", bookmark.Name);
        Assert.AreEqual("shell:AppsFolder\\" + appUserModelId, bookmark.Target);
        Assert.IsFalse(bookmark.ShowSubtitles);
    }

    [TestMethod]
    [DataRow(@"C:\Apps\Example.exe", "Example")]
    [DataRow(@"C:\Shortcuts\Example.lnk", "Example")]
    [DataRow(@"C:\Documents\Report.txt", "Report")]
    [DataRow(@"C:\Documents\Projects", "Projects")]
    [DataRow(@"\\server\share\Report.txt", "Report")]
    [DataRow(@"C:\", "")]
    [DataRow(@"C:\Documents\Disney.37853FC22B2CE_6rarf9sa4v8jt!App", "Disney")]
    public void GetBookmark_FileSystemItem_PreservesTargetWithoutAppLookup(string path, string expectedName)
    {
        var bookmark = DockDropHelper.GetBookmark(path, static _ => throw new AssertFailedException("Filesystem items must not be resolved as apps."));

        Assert.AreEqual(expectedName, bookmark.Name);
        Assert.AreEqual(path, bookmark.Target);
        Assert.IsFalse(bookmark.ShowSubtitles);
    }

    [TestMethod]
    [DataRow("Unknown.Package_1234567890123!App")]
    [DataRow("Unknown.Desktop.Application.15")]
    [DataRow("shell:Downloads")]
    [DataRow("example.txt")]
    public void GetBookmark_UnrecognizedItem_PreservesTarget(string path)
    {
        var bookmark = DockDropHelper.GetBookmark(path, static _ => null);

        Assert.AreEqual(Path.GetFileNameWithoutExtension(path), bookmark.Name);
        Assert.AreEqual(path, bookmark.Target);
        Assert.IsFalse(bookmark.ShowSubtitles);
    }

    [TestMethod]
    [DataRow("https://www.reddit.com/r/PowerToys/", "www.reddit.com")]
    [DataRow("http://example.com/report?version=2#summary", "example.com")]
    [DataRow("file:///C:/Documents/Report.txt", "")]
    [DataRow("ms-settings:display", "")]
    public void GetBookmark_Uri_PreservesHostAndTargetAndHidesSubtitle(string target, string expectedName)
    {
        var bookmark = DockDropHelper.GetBookmark(new Uri(target));

        Assert.AreEqual(expectedName, bookmark.Name);
        Assert.AreEqual(target, bookmark.Target);
        Assert.IsFalse(bookmark.ShowSubtitles);
    }

    [TestMethod]
    public void GetBookmark_AppWithoutDisplayName_StillUsesAppsFolder()
    {
        const string appUserModelId = "Disney.37853FC22B2CE_6rarf9sa4v8jt!App";
        var bookmark = DockDropHelper.GetBookmark(appUserModelId, static _ => string.Empty);

        Assert.AreEqual(appUserModelId, bookmark.Name);
        Assert.AreEqual("shell:AppsFolder\\" + appUserModelId, bookmark.Target);
        Assert.IsFalse(bookmark.ShowSubtitles);
    }

    [TestMethod]
    public void GetBookmark_FileExplorer_ResolvesDesktopAppThroughShell()
    {
        const string appUserModelId = "Microsoft.Windows.Explorer";

        var bookmark = DockDropHelper.GetBookmark(appUserModelId);

        Assert.AreEqual("shell:AppsFolder\\" + appUserModelId, bookmark.Target);
        Assert.IsFalse(string.IsNullOrWhiteSpace(bookmark.Name));
        Assert.AreNotEqual(appUserModelId, bookmark.Name);
        Assert.IsFalse(bookmark.ShowSubtitles);
    }

    [TestMethod]
    public void GetBookmark_UnknownAppInShell_PreservesTarget()
    {
        var path = "CmdPal.Nonexistent.App." + Guid.NewGuid().ToString("N");

        var bookmark = DockDropHelper.GetBookmark(path);

        Assert.AreEqual(Path.GetFileNameWithoutExtension(path), bookmark.Name);
        Assert.AreEqual(path, bookmark.Target);
        Assert.IsFalse(bookmark.ShowSubtitles);
    }
}
