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
    public async Task GetBookmarkAsync_App_ResolvesOutsideCallingContext()
    {
        var originalContext = SynchronizationContext.Current;
        var callingContext = new SynchronizationContext();
        Task<(string Name, string Target)> lookup;
        SynchronizationContext.SetSynchronizationContext(callingContext);
        try
        {
            lookup = DockDropHelper.GetBookmarkAsync(
                "Test.App",
                _ =>
                {
                    Assert.AreNotSame(callingContext, SynchronizationContext.Current);
                    Assert.IsTrue(Thread.CurrentThread.IsThreadPoolThread);
                    return "Test application";
                },
                CancellationToken.None);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }

        var bookmark = await lookup.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual("Test application", bookmark.Name);
        Assert.AreEqual("shell:AppsFolder\\Test.App", bookmark.Target);
    }

    [TestMethod]
    public async Task GetBookmarkAsync_AlreadyCanceled_DoesNotStartLookup()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var lookupCalled = false;

        var lookup = DockDropHelper.GetBookmarkAsync(
            "Test.App",
            _ =>
            {
                lookupCalled = true;
                return "Test application";
            },
            cancellation.Token);

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () => await lookup);
        Assert.IsFalse(lookupCalled);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task GetBookmarkAsync_CanceledDuringLookup_DoesNotWaitForLateCompletion(bool failAfterCancellation)
    {
        using var cancellation = new CancellationTokenSource();
        using var releaseLookup = new ManualResetEventSlim();
        var lookupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookupFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookup = DockDropHelper.GetBookmarkAsync(
            "Test.App",
            _ =>
            {
                lookupStarted.SetResult();
                try
                {
                    Assert.IsTrue(releaseLookup.Wait(TimeSpan.FromSeconds(5)));
                    if (failAfterCancellation)
                    {
                        throw new InvalidOperationException("Late Shell lookup failure");
                    }

                    return "Test application";
                }
                finally
                {
                    lookupFinished.SetResult();
                }
            },
            cancellation.Token);

        try
        {
            await lookupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(lookup.IsCompleted);
            cancellation.Cancel();

            await Assert.ThrowsExactlyAsync<TaskCanceledException>(
                async () => await lookup.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            releaseLookup.Set();
            await lookupFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.IsTrue(lookup.IsCanceled);
    }

    [TestMethod]
    public async Task GetBookmarkAsync_LookupFailure_PropagatesException()
    {
        var lookup = DockDropHelper.GetBookmarkAsync(
            "Test.App",
            static _ => throw new InvalidOperationException("Shell lookup failure"),
            CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await lookup.WaitAsync(TimeSpan.FromSeconds(5)));
    }

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
    }

    [TestMethod]
    [DataRow(@"C:\Apps\Example.exe", "Example")]
    [DataRow(@"C:\Shortcuts\Example.lnk", "Example")]
    [DataRow(@"C:\Documents\Report.txt", "Report")]
    [DataRow(@"C:\Documents\Projects", "Projects")]
    [DataRow(@"\\server\share\Report.txt", "Report")]
    [DataRow(@"C:\Documents\Disney.37853FC22B2CE_6rarf9sa4v8jt!App", "Disney")]
    public void GetBookmark_FileSystemItem_PreservesTargetWithoutAppLookup(string path, string expectedName)
    {
        var bookmark = DockDropHelper.GetBookmark(path, static _ => throw new AssertFailedException("Filesystem items must not be resolved as apps."));

        Assert.AreEqual(expectedName, bookmark.Name);
        Assert.AreEqual(path, bookmark.Target);
    }

    [TestMethod]
    [DataRow("Unknown.Package_1234567890123!App")]
    [DataRow("Unknown.Desktop.Application.15")]
    [DataRow("Microsoft.Windows.Explorer")]
    [DataRow("shell:Downloads")]
    [DataRow("example.txt")]
    public void GetBookmark_UnresolvedItem_PreservesTarget(string path)
    {
        var bookmark = DockDropHelper.GetBookmark(path, static _ => null);

        Assert.AreEqual(Path.GetFileNameWithoutExtension(path), bookmark.Name);
        Assert.AreEqual(path, bookmark.Target);
    }

    [TestMethod]
    public void GetBookmark_AppWithoutDisplayName_StillUsesAppsFolder()
    {
        const string appUserModelId = "Disney.37853FC22B2CE_6rarf9sa4v8jt!App";
        var bookmark = DockDropHelper.GetBookmark(appUserModelId, static _ => string.Empty);

        Assert.AreEqual(appUserModelId, bookmark.Name);
        Assert.AreEqual("shell:AppsFolder\\" + appUserModelId, bookmark.Target);
    }

    [TestMethod]
    public async Task GetBookmarkAsync_UnknownAppInShell_PreservesTarget()
    {
        var path = "CmdPal.Nonexistent.App." + Guid.NewGuid().ToString("N");

        var bookmark = await DockDropHelper.GetBookmarkAsync(path, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.AreEqual(Path.GetFileNameWithoutExtension(path), bookmark.Name);
        Assert.AreEqual(path, bookmark.Target);
    }
}
