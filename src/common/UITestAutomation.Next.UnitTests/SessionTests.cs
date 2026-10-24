// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class SessionTests
{
    [TestMethod]
    public void SlugInspectionReturnsOnlySelectedRootIncludingUnnamedElements()
    {
        using var document = JsonDocument.Parse("""
            {"windows":[{"elements":[{
              "selector":"grp-1234","type":"Group","className":"SettingsCard",
              "x":-10,"y":20,"width":300,"height":40,
              "children":[{"selector":"lbl-value-2345","type":"Text","name":"value"}]
            }]}]}
            """);

        var matches = Session.ParseSearchResult(document.RootElement, fromInspection: true);

        Assert.HasCount(1, matches);
        Assert.AreEqual(new Session.SearchHit("grp-1234", string.Empty, "Group", "SettingsCard", -10, 20, 300, 40), matches[0]);
    }

    [TestMethod]
    public void SlugInspectionPreservesEmptyBoundsForOffscreenElements()
    {
        using var document = JsonDocument.Parse("""
            {"windows":[{"elements":[{
              "selector":"grp-offscreen-1234","type":"Group","className":"SettingsCard",
              "isOffscreen":true,"x":0,"y":0,"width":0,"height":0
            }]}]}
            """);

        var matches = Session.ParseSearchResult(document.RootElement, fromInspection: true);

        Assert.HasCount(1, matches);
        Assert.AreEqual(new Session.SearchHit("grp-offscreen-1234", string.Empty, "Group", "SettingsCard", 0, 0, 0, 0), matches[0]);
    }

    [TestMethod]
    public void TextSearchPreservesAllMatchesAndMetadata()
    {
        using var document = JsonDocument.Parse("""
            {"matches":[
              {"selector":"btn-one-1234","name":"One","type":"Button","className":"Button","x":1,"y":2,"width":3,"height":4},
              {"selector":"btn-two-5678","name":"Two","type":"Button","className":"ToggleSwitch"}
            ]}
            """);

        var matches = Session.ParseSearchResult(document.RootElement, fromInspection: false);

        Assert.HasCount(2, matches);
        Assert.AreEqual(new Session.SearchHit("btn-one-1234", "One", "Button", "Button", 1, 2, 3, 4), matches[0]);
        Assert.AreEqual(new Session.SearchHit("btn-two-5678", "Two", "Button", "ToggleSwitch", 0, 0, 0, 0), matches[1]);
    }

    [TestMethod]
    public void InspectionAcceptsKnownMatchesEnvelope()
    {
        using var document = JsonDocument.Parse("""
            {"matches":[{"selector":"btn-one-1234","name":"One","type":"Button","className":"Button","width":32,"height":24}]}
            """);

        var matches = Session.ParseSearchResult(document.RootElement, fromInspection: true);

        Assert.HasCount(1, matches);
        Assert.AreEqual(new Session.SearchHit("btn-one-1234", "One", "Button", "Button", 0, 0, 32, 24), matches[0]);
    }

    [TestMethod]
    public void InspectionPrefersSelectedRootsOverSearchMatches()
    {
        using var document = JsonDocument.Parse("""
            {
              "windows":[{"elements":[{"selector":"grp-selected-1234","type":"Group"}]}],
              "matches":[{"selector":"btn-unrelated-5678","type":"Button"}]
            }
            """);

        var matches = Session.ParseSearchResult(document.RootElement, fromInspection: true);

        Assert.HasCount(1, matches);
        Assert.AreEqual("grp-selected-1234", matches[0].Selector);
    }

    [TestMethod]
    [DataRow("""{"windows":[]}""", true)]
    [DataRow("""{"windows":[{}]}""", true)]
    [DataRow("""{"matches":[]}""", false)]
    public void EmptyResultsRemainEmpty(string json, bool fromInspection)
    {
        using var document = JsonDocument.Parse(json);

        Assert.HasCount(0, Session.ParseSearchResult(document.RootElement, fromInspection));
    }
}
