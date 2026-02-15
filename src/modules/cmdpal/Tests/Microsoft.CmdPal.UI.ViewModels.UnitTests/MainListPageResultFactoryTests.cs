// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class MainListPageResultFactoryTests
{
    private static readonly Separator _resultsSeparator = new("Results");
    private static readonly Separator _fallbacksSeparator = new("Fallbacks");

    private sealed partial class MockListItem : IListItem
    {
        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public ICommand Command => new NoOpCommand();

        public IDetails? Details => null;

        public IIconInfo? Icon => null;

        public string Section => throw new NotImplementedException();

        public ITag[] Tags => throw new NotImplementedException();

        public string TextToSuggest => throw new NotImplementedException();

        public IContextItem[] MoreCommands => throw new NotImplementedException();

#pragma warning disable CS0067 // The event is never used
        public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;
#pragma warning restore CS0067 // The event is never used

        public override string ToString() => Title;
    }

    private static RoScored<IListItem> S(string title, int score)
    {
        return new RoScored<IListItem>(score: score, item: new MockListItem { Title = title });
    }

    [TestMethod]
    public void Merge_PrioritizesListsCorrectly()
    {
        var filtered = new List<RoScored<IListItem>>
        {
            S("F1", 100),
            S("F2", 50),
        };

        var scoredFallback = new List<RoScored<IListItem>>
        {
            S("SF1", 100),
            S("SF2", 60),
        };

        var apps = new List<RoScored<IListItem>>
        {
            S("A1", 100),
            S("A2", 55),
        };

        // Fallbacks are not scored.
        var fallbacks = new List<RoScored<IListItem>>
        {
            S("FB1", 0),
            S("FB2", 0),
        };

        var result = MainListPageResultFactory.Create(
            filtered,
            scoredFallback,
            apps,
            fallbacks,
            _resultsSeparator,
            _fallbacksSeparator,
            appResultLimit: 10);

        // Expected order:
        // "Results" section header
        // 100: F1, SF1, A1
        // 60: SF2
        // 55: A2
        // 50: F2
        // "Fallbacks" section header
        // Then fallbacks in original order: FB1, FB2
        var titles = result.Select(r => r.Title).ToArray();
#pragma warning disable CA1861 // Avoid constant arrays as arguments
        CollectionAssert.AreEqual(
            new[] { "Results", "F1", "SF1", "A1", "SF2", "A2", "F2", "Fallbacks", "FB1", "FB2" },
            titles);
#pragma warning restore CA1861 // Avoid constant arrays as arguments
    }

    [TestMethod]
    public void Merge_AppliesAppLimit()
    {
        var apps = new List<RoScored<IListItem>>
        {
            S("A1", 100),
            S("A2", 90),
            S("A3", 80),
        };

        var result = MainListPageResultFactory.Create(
            null,
            null,
            apps,
            null,
            _resultsSeparator,
            _fallbacksSeparator,
            2);

        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("Results", result[0].Title);
        Assert.AreEqual("A1", result[1].Title);
        Assert.AreEqual("A2", result[2].Title);
    }

    [TestMethod]
    public void Merge_FiltersEmptyFallbacks()
    {
        var fallbacks = new List<RoScored<IListItem>>
        {
            S("FB1", 0),
            S("FB3", 0),
        };

        var result = MainListPageResultFactory.Create(
            null,
            null,
            null,
            fallbacks,
            _resultsSeparator,
            _fallbacksSeparator,
            appResultLimit: 10);

        Assert.AreEqual(3, result.Length);
        Assert.AreEqual("Fallbacks", result[0].Title);
        Assert.AreEqual("FB1", result[1].Title);
        Assert.AreEqual("FB3", result[2].Title);
    }

    [TestMethod]
    public void Merge_HandlesNullLists()
    {
        var result = MainListPageResultFactory.Create(
            null,
            null,
            null,
            null,
            _resultsSeparator,
            _fallbacksSeparator,
            appResultLimit: 10);

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Length);
    }
}
