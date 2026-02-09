// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Text;

namespace Microsoft.CmdPal.Common.UnitTests.Text;

[TestClass]
public sealed class PrecomputedFuzzyMatcherSecondaryInputTests
{
    private readonly PrecomputedFuzzyMatcher _matcher = new();
    private readonly StringFolder _folder = new();
    private readonly BloomFilter _bloom = new();

    [TestMethod]
    public void Score_PrimaryQueryMatchesSecondaryTarget_ShouldMatch()
    {
        // Scenario: Searching for "calc" should match a file "calculator.exe" where primary is filename, secondary is path
        var query = CreateQuery("calc");
        var target = CreateTarget(primary: "important.txt", secondary: "C:\\Programs\\Calculator\\");

        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected primary query to match secondary target");
    }

    [TestMethod]
    public void Score_SecondaryQueryMatchesPrimaryTarget_ShouldMatch()
    {
        // Scenario: User types "documents\\report" and we want to match against filename
        var query = CreateQuery(primary: "documents", secondary: "report");
        var target = CreateTarget(primary: "report.docx");

        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected secondary query to match primary target");
    }

    [TestMethod]
    public void Score_SecondaryQueryMatchesSecondaryTarget_ShouldMatch()
    {
        // Scenario: Both query and target have secondary info that matches
        var query = CreateQuery(primary: "test", secondary: "documents");
        var target = CreateTarget(primary: "something.txt", secondary: "C:\\Users\\Documents\\");

        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected secondary query to match secondary target");
    }

    [TestMethod]
    public void Score_PrimaryQueryMatchesBothTargets_ShouldReturnBestScore()
    {
        // The same query matches both primary and secondary of target
        var query = CreateQuery("test");
        var target = CreateTarget(primary: "test.txt", secondary: "test_folder\\");

        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected query to match when it appears in both primary and secondary");
    }

    [TestMethod]
    public void Score_NoSecondaryInQuery_MatchesSecondaryTarget()
    {
        // Query without secondary can still match target's secondary
        var query = CreateQuery("downloads");
        var target = CreateTarget(primary: "file.txt", secondary: "C:\\Downloads\\");

        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected primary query to match secondary target");
    }

    [TestMethod]
    public void Score_NoSecondaryInTarget_SecondaryQueryShouldNotMatch()
    {
        // Query with secondary but target without secondary - secondary query shouldn't interfere
        var query = CreateQuery(primary: "test", secondary: "extra");
        var target = CreateTarget(primary: "test.txt");

        var score = _matcher.Score(query, target);

        // Primary should still match, secondary query just doesn't contribute
        Assert.IsTrue(score > 0, "Expected primary query to match primary target");
    }

    [TestMethod]
    public void Score_SecondaryQueryNoMatch_PrimaryCanStillMatch()
    {
        // Secondary doesn't match anything, but primary does
        var query = CreateQuery(primary: "file", secondary: "nomatch");
        var target = CreateTarget(primary: "myfile.txt", secondary: "C:\\Documents\\");

        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected primary query to match even when secondary doesn't");
    }

    [TestMethod]
    public void Score_OnlySecondaryMatches_ShouldReturnScore()
    {
        // Only the secondary parts match, primary doesn't
        var query = CreateQuery(primary: "xyz", secondary: "documents");
        var target = CreateTarget(primary: "abc.txt", secondary: "C:\\Users\\Documents\\");

        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected match when only secondary parts match");
    }

    [TestMethod]
    public void Score_BothQueriesMatchDifferentTargets_ShouldReturnBestScore()
    {
        // Primary query matches secondary target, secondary query matches primary target
        var query = CreateQuery(primary: "docs", secondary: "report");
        var target = CreateTarget(primary: "report.pdf", secondary: "C:\\Documents\\");

        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected match when queries cross-match with targets");
    }

    [TestMethod]
    public void Score_CompletelyDifferent_ShouldNotMatch()
    {
        var query = CreateQuery(primary: "xyz", secondary: "abc");
        var target = CreateTarget(primary: "hello", secondary: "world");

        var score = _matcher.Score(query, target);

        Assert.AreEqual(0, score, "Expected no match when nothing matches");
    }

    [TestMethod]
    public void Score_EmptySecondaryInputs_ShouldMatchOnPrimary()
    {
        var query = CreateQuery(primary: "test", secondary: string.Empty);
        var target = CreateTarget(primary: "test.txt", secondary: string.Empty);

        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected match on primary when secondaries are empty");
    }

    [TestMethod]
    public void Score_WordSeparatorMatching_AcrossSecondary()
    {
        // Test that "Power Point" matches "PowerPoint" using secondary
        var query = CreateQuery(primary: "power", secondary: "point");
        var target = CreateTarget(primary: "PowerPoint.exe");

        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected 'power' + 'point' to match 'PowerPoint'");
    }

    private FuzzyQuery CreateQuery(string primary, string? secondary = null)
    {
        var primaryFolded = _folder.Fold(primary, removeDiacritics: true);
        var primaryBloom = _bloom.Compute(primaryFolded);
        var primaryEffectiveLength = primaryFolded.Length;
        var primaryIsAllLowercase = IsAllLowercaseAsciiOrNonLetter(primary);

        string? secondaryFolded = null;
        ulong secondaryBloom = 0;
        var secondaryEffectiveLength = 0;
        var secondaryIsAllLowercase = true;

        if (!string.IsNullOrEmpty(secondary))
        {
            secondaryFolded = _folder.Fold(secondary, removeDiacritics: true);
            secondaryBloom = _bloom.Compute(secondaryFolded);
            secondaryEffectiveLength = secondaryFolded.Length;
            secondaryIsAllLowercase = IsAllLowercaseAsciiOrNonLetter(secondary);
        }

        return new FuzzyQuery(
            original: primary,
            folded: primaryFolded,
            bloom: primaryBloom,
            effectiveLength: primaryEffectiveLength,
            isAllLowercaseAsciiOrNonLetter: primaryIsAllLowercase,
            secondaryOriginal: secondary,
            secondaryFolded: secondaryFolded,
            secondaryBloom: secondaryBloom,
            secondaryEffectiveLength: secondaryEffectiveLength,
            secondaryIsAllLowercaseAsciiOrNonLetter: secondaryIsAllLowercase);
    }

    private FuzzyTarget CreateTarget(string primary, string? secondary = null)
    {
        var primaryFolded = _folder.Fold(primary, removeDiacritics: true);
        var primaryBloom = _bloom.Compute(primaryFolded);

        string? secondaryFolded = null;
        ulong secondaryBloom = 0;

        if (!string.IsNullOrEmpty(secondary))
        {
            secondaryFolded = _folder.Fold(secondary, removeDiacritics: true);
            secondaryBloom = _bloom.Compute(secondaryFolded);
        }

        return new FuzzyTarget(
            original: primary,
            folded: primaryFolded,
            bloom: primaryBloom,
            secondaryOriginal: secondary,
            secondaryFolded: secondaryFolded,
            secondaryBloom: secondaryBloom);
    }

    private static bool IsAllLowercaseAsciiOrNonLetter(string s)
    {
        foreach (var c in s)
        {
            if ((uint)(c - 'A') <= ('Z' - 'A'))
            {
                return false;
            }
        }

        return true;
    }
}
