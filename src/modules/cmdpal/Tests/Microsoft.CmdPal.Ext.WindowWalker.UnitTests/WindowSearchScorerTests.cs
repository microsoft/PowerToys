// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WindowWalker.UnitTests;

[TestClass]
public class WindowSearchScorerTests
{
    // A Microsoft Word window holding "budget.docx", as in issue #40910.
    private const string WordTitle = "budget.docx - Word";
    private const string WordProcess = "WINWORD";

    /// <summary>
    /// The behavior before multi-word support: score the whole query against each field and
    /// take the better of the two. Used to prove single-word queries are unaffected.
    /// </summary>
    private static int LegacyScore(string query, string title, string processName)
        => Math.Max(
            FuzzyStringMatcher.ScoreFuzzy(query, title),
            FuzzyStringMatcher.ScoreFuzzy(query, processName ?? string.Empty));

    [DataTestMethod]
    [DataRow("word budget")]
    [DataRow("budget word")]
    [DataRow("winword budget")]
    [DataRow("budget.docx word")]
    public void Score_ProcessAndTitleWordsInAnyOrder_Matches(string query)
    {
        var score = WindowSearchScorer.Score(query, WordTitle, WordProcess);

        Assert.IsTrue(score > 0, $"Expected '{query}' to match the Word window, but scored {score}.");
    }

    [TestMethod]
    public void Score_CompoundQuery_DistinguishesBetweenWindowsOfTheSameProcess()
    {
        var budget = WindowSearchScorer.Score("word budget", WordTitle, WordProcess);
        var resume = WindowSearchScorer.Score("word budget", "resume.docx - Word", WordProcess);

        Assert.IsTrue(budget > 0, "The budget document should match.");
        Assert.AreEqual(0, resume, "A different document of the same process should not match.");
    }

    [TestMethod]
    public void Score_BrowserWindows_MatchProcessPlusTabTitle()
    {
        const string process = "chrome";
        const string powerToysTab = "microsoft/PowerToys: Windows system utilities - Google Chrome";
        const string gmailTab = "Inbox (12) - Gmail - Google Chrome";

        Assert.IsTrue(WindowSearchScorer.Score("chrome powertoys", powerToysTab, process) > 0);
        Assert.AreEqual(0, WindowSearchScorer.Score("chrome powertoys", gmailTab, process));
        Assert.IsTrue(WindowSearchScorer.Score("chrome gmail", gmailTab, process) > 0);
    }

    [TestMethod]
    public void Score_MoreThanTwoWords_Matches()
    {
        var score = WindowSearchScorer.Score("chrome inbox gmail", "Inbox (12) - Gmail - Google Chrome", "chrome");

        Assert.IsTrue(score > 0, $"Expected a three-word query to match, but scored {score}.");
    }

    [TestMethod]
    public void Score_BareTitle_IsFoundByProcessPlusTitle()
    {
        // Explorer windows are titled with just the folder name, so the process name is the
        // only way to say "the Explorer window showing Downloads".
        Assert.IsTrue(WindowSearchScorer.Score("explorer downloads", "Downloads", "explorer") > 0);
        Assert.AreEqual(0, WindowSearchScorer.Score("explorer documents", "Downloads", "explorer"));
    }

    [DataTestMethod]
    [DataRow("word spreadsheet")]
    [DataRow("budget excel")]
    [DataRow("word budget nonexistentword")]
    public void Score_WordMatchingNeitherField_DoesNotMatch(string query)
    {
        var score = WindowSearchScorer.Score(query, WordTitle, WordProcess);

        Assert.AreEqual(0, score, $"Expected '{query}' not to match, but scored {score}.");
    }

    [DataTestMethod]
    [DataRow("word")]
    [DataRow("budget")]
    [DataRow("winword")]
    [DataRow("docx")]
    [DataRow("zzz")]
    public void Score_SingleWordQuery_IsUnchangedFromLegacyBehavior(string query)
    {
        var score = WindowSearchScorer.Score(query, WordTitle, WordProcess);

        Assert.AreEqual(LegacyScore(query, WordTitle, WordProcess), score);
    }

    [TestMethod]
    public void Score_IsNeverLowerThanLegacyScore()
    {
        // A multi-word query that the whole-query path already matched, because the title
        // contains it literally.
        const string title = "Quarterly budget review - Word";
        const string query = "budget review";

        var score = WindowSearchScorer.Score(query, title, WordProcess);

        Assert.IsTrue(
            score >= LegacyScore(query, title, WordProcess),
            "Multi-word support must never score lower than the previous whole-query behavior.");
    }

    [TestMethod]
    public void Score_SurroundingAndRepeatedWhitespace_IsIgnored()
    {
        var expected = WindowSearchScorer.Score("word budget", WordTitle, WordProcess);

        Assert.AreEqual(expected, WindowSearchScorer.Score("  word   budget  ", WordTitle, WordProcess));
        Assert.AreEqual(expected, WindowSearchScorer.Score("word\tbudget", WordTitle, WordProcess));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Score_EmptyQuery_ReturnsZero(string query)
    {
        Assert.AreEqual(0, WindowSearchScorer.Score(query, WordTitle, WordProcess));
    }

    [TestMethod]
    public void Score_NullOrEmptyFields_DoesNotThrow()
    {
        Assert.AreEqual(0, WindowSearchScorer.Score("word budget", null, null));
        Assert.AreEqual(0, WindowSearchScorer.Score("word budget", string.Empty, string.Empty));

        // A window with no title can still be found by its process name alone.
        Assert.IsTrue(WindowSearchScorer.Score("winword", null, WordProcess) > 0);
    }
}
