// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WindowWalker.UnitTests;

[TestClass]
public class WindowMatcherTests
{
    [DataTestMethod]
    [DataRow("excel", "Book1 - Excel", "EXCEL.EXE", (int)WindowMatcher.MatchKind.ProcessName)]
    [DataRow("EXC", "Book1 - Excel", "EXCEL.EXE", (int)WindowMatcher.MatchKind.ProcessName)]
    [DataRow("book1", "Book1 - Excel", "EXCEL.EXE", (int)WindowMatcher.MatchKind.Title)]
    [DataRow("visual studio code", "Program.cs - PowerToys - Visual Studio Code", "Code.exe", (int)WindowMatcher.MatchKind.Title)]
    [DataRow("  excel  ", "Book1 - Excel", "EXCEL.EXE", (int)WindowMatcher.MatchKind.ProcessName)]
    [DataRow("exe", "Notepad", "notepad.exe", (int)WindowMatcher.MatchKind.None)]
    [DataRow("e", "Book1 - Excel", "EXCEL.EXE", (int)WindowMatcher.MatchKind.None)]
    [DataRow("ntpd", "Untitled - Notepad", "notepad.exe", (int)WindowMatcher.MatchKind.None)]
    [DataRow("word", "Book1 - Excel", "EXCEL.EXE", (int)WindowMatcher.MatchKind.None)]
    [DataRow("excel", "", null, (int)WindowMatcher.MatchKind.None)]
    public void GetMatchKind(string query, string title, string processName, int expected)
    {
        Assert.AreEqual((WindowMatcher.MatchKind)expected, WindowMatcher.GetMatchKind(query, title, processName));
    }

    [TestMethod]
    public void FindBestMatchPrefersProcessNameOverEarlierTitleMatch()
    {
        (string Title, string Process)[] windows =
        [
            ("Excel tips - Microsoft Edge", "msedge.exe"),
            ("Book1 - Excel", "EXCEL.EXE"),
        ];

        Assert.AreEqual(1, WindowMatcher.FindBestMatch(windows, "excel", static w => w.Title, static w => w.Process));
    }

    [TestMethod]
    public void FindBestMatchPrefersMostRecentWindowAmongEqualMatches()
    {
        (string Title, string Process)[] windows =
        [
            ("Untitled - Notepad", "explorer.exe"),
            ("Book2 - Excel", "EXCEL.EXE"),
            ("Book1 - Excel", "EXCEL.EXE"),
        ];

        Assert.AreEqual(1, WindowMatcher.FindBestMatch(windows, "excel", static w => w.Title, static w => w.Process));
    }

    [TestMethod]
    public void FindBestMatchReturnsMinusOneWhenNothingMatches()
    {
        (string Title, string Process)[] windows =
        [
            ("Book1 - Excel", "EXCEL.EXE"),
        ];

        Assert.AreEqual(-1, WindowMatcher.FindBestMatch(windows, "outlook", static w => w.Title, static w => w.Process));
    }
}
