// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.Editor.UnitTests;

/// <summary>
/// Ignoring case, the editor's display, duplicate and reachability rules have to agree with the engine
/// about letters outside the Basic Multilingual Plane, which are surrogate pairs.
/// </summary>
[TestClass]
public sealed class TriggerCasingTests
{
    // ADLAM SMALL LETTER ALIF and ADLAM CAPITAL LETTER ALIF.
    private const string Lower = "!\U0001E922";
    private const string Upper = "!\U0001E900";

    [TestMethod]
    public void EveryFormTheRowShowsExpandsTheTrigger()
    {
        var row = new TextExpanderSnippetViewModel(0, Lower, "adlam") { CaseSensitive = false };
        string[] forms = row.TriggerDisplay.Split(" / ");
        Assert.IsTrue(row.ShowsCaseVariants);
        CollectionAssert.Contains(forms, Upper);

        TriggerIndex runtime = TriggerIndex.Build([Lower], caseSensitive: false);
        foreach (string form in forms)
        {
            Assert.AreEqual(Lower, runtime.MatchLongestSuffix(form), form);
        }
    }

    [TestMethod]
    public void TheFormsAreOneTriggerToDuplicateDetectionAndTheEngine()
    {
        SnippetFile file = SnippetFile.Read(SnippetJson.Bytes(Lower + "=first", Upper + "=second"));
        int second = file.Snippets[1].Id;
        Assert.AreEqual(Lower, file.FindDuplicate(Upper, second, caseSensitive: false)?.Trigger);
        Assert.IsNull(file.FindDuplicate(Upper, second, caseSensitive: true));

        var triggers = new TextExpanderTriggerSet(file.Snippets.Select(entry => entry.Trigger), caseSensitive: false);
        string kept = triggers.Triggers.Single();
        Assert.AreEqual(kept, triggers.Index.MatchLongestSuffix(Lower));
        Assert.AreEqual(kept, triggers.Index.MatchLongestSuffix(Upper));
    }

    [TestMethod]
    public void ReachabilityReportsTheOtherFormFiringFirst()
    {
        string longer = Upper + ".x";
        var triggers = new TextExpanderTriggerSet([Lower, longer], caseSensitive: false);
        (string Trigger, char Next)? expected = (Upper, '.');
        Assert.AreEqual(expected, TextExpanderTriggerReach.FindBlocker(longer, triggers, wordMode: true));
        Assert.AreEqual(expected, TextExpanderTriggerReach.FindBlocker(longer, triggers, wordMode: false));

        Assert.IsNull(TextExpanderTriggerReach.FindBlocker(longer, new TextExpanderTriggerSet([Lower, longer]), wordMode: false));
    }
}
