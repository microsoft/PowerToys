// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.Helpers;
using ShortcutGuide.Models;

namespace ShortcutGuide.UnitTests.ManifestInterpreterTests;

[TestClass]
public sealed class ForegroundMatchTests
{
    private static readonly IndexFile.IndexItem[] Index =
    [
        new() { WindowFilter = "*", BackgroundProcess = true, Apps = ["+WindowsNT.Shell"] },
        new() { WindowFilter = "chrome.exe", Apps = ["Google.Chrome"] },
        new() { WindowFilter = "chrome.exe", AppUserModelId = "Chrome._crx_youtube*", Apps = ["+Web.YouTube"] },
        new() { WindowFilter = "chrome.exe;msedge.exe", AppUserModelId = "Chrome._crx_music;MSEdge._crx_music", Apps = ["+Web.YouTubeMusic"] },
        new() { WindowFilter = "notepad.exe", Apps = ["+WindowsNT.Notepad"] },
    ];

    [TestMethod]
    [DataRow("chrome.exe", "Chrome", "Google.Chrome")]
    [DataRow("chrome.exe", null, "Google.Chrome")]
    [DataRow("chrome.exe", "Chrome._crx_otherapp", "Google.Chrome")]
    [DataRow("chrome.exe", "Chrome._crx_youtube", "+Web.YouTube")]
    [DataRow("chrome.exe", "Chrome._crx_youtube.Profile1", "+Web.YouTube")]
    [DataRow("CHROME.EXE", "chrome._CRX_YOUTUBE", "+Web.YouTube")]
    [DataRow("chrome.exe", "Chrome._crx_music", "+Web.YouTubeMusic")]
    [DataRow("msedge.exe", "MSEdge._crx_music", "+Web.YouTubeMusic")]
    [DataRow("chrome.exe", "Chrome._crx_music.Profile1", "Google.Chrome")]
    public void FindForegroundMatch_PrefersMatchingAppUserModelIdOverProcessName(string processName, string? appUserModelId, string expectedApp)
    {
        IndexFile.IndexItem? match = ManifestInterpreter.FindForegroundMatch(Index, processName, () => appUserModelId);

        Assert.IsNotNull(match);
        Assert.AreEqual(1, match.Value.Apps.Length);
        Assert.AreEqual(expectedApp, match.Value.Apps[0]);
    }

    [TestMethod]
    public void FindForegroundMatch_AppUserModelIdOnlyEntryWithoutMatchingWindow_ReturnsNull()
    {
        // MSEdge has no process-only entry, so a regular Edge window must not show YouTube Music shortcuts.
        IndexFile.IndexItem? match = ManifestInterpreter.FindForegroundMatch(Index, "msedge.exe", () => "MSEdge");

        Assert.IsNull(match);
    }

    [TestMethod]
    public void FindForegroundMatch_WithoutAppUserModelIdEntries_DoesNotReadAppUserModelId()
    {
        int calls = 0;

        IndexFile.IndexItem? match = ManifestInterpreter.FindForegroundMatch(Index, "notepad.exe", () =>
        {
            calls++;
            return null;
        });

        Assert.IsNotNull(match);
        Assert.AreEqual(1, match.Value.Apps.Length);
        Assert.AreEqual("+WindowsNT.Notepad", match.Value.Apps[0]);
        Assert.AreEqual(0, calls, "The window's AppUserModelID should only be read when an entry needs it.");
    }

    [TestMethod]
    public void FindForegroundMatch_ReadsAppUserModelIdAtMostOnce()
    {
        int calls = 0;

        _ = ManifestInterpreter.FindForegroundMatch(Index, "chrome.exe", () =>
        {
            calls++;
            return "Chrome";
        });

        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void FindForegroundMatch_IgnoresBackgroundEntries()
    {
        IndexFile.IndexItem? match = ManifestInterpreter.FindForegroundMatch(Index, "unknown.exe", () => null);

        Assert.IsNull(match);
    }
}
