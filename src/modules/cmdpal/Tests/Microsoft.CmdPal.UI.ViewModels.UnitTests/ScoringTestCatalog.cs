// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

internal static partial class ScoringTestCatalog
{
    internal sealed partial class CatalogItem : ListItem, IPrecomputedListItem
    {
        private FuzzyTargetCache _titleCache;
        private FuzzyTargetCache _subtitleCache;

        internal CatalogItem(string title, string subtitle, string id)
            : base(new NoOpCommand() { Id = id })
        {
            Title = title;
            Subtitle = subtitle;
            Id = id;
        }

        internal string Id { get; }

        public FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher) => _titleCache.GetOrUpdate(matcher, Title);

        public FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher) => _subtitleCache.GetOrUpdate(matcher, Subtitle);
    }

    private static readonly string[] Nouns =
    [
        "Calculator", "Calendar", "Camera", "Canvas", "Command", "Control", "Cloud", "Cast",
        "Visual", "Studio", "Code", "Terminal", "Task", "Notepad", "Paint", "Photos", "Player",
        "Panel", "Prompt", "Settings", "Store", "System", "Manager", "Monitor", "Editor", "Browser",
        "Mail", "Maps", "Music", "Movies", "Network", "Office", "Onenote", "Outlook", "People",
    ];

    private static readonly string[] Qualifiers =
    [
        string.Empty, "Pro", "2022", "3D", "Preview", "X", "Lite", "Plus", "Home", "Enterprise",
        "for Windows", "Insider", "Legacy", "New", "Classic",
    ];

    private static readonly string[] SubtitleWords =
    [
        "Perform calculations and conversions", "View and manage your schedule", "Edit and refine images",
        "Modern terminal for command-line tools", "Full-featured integrated development environment",
        "Browse the web quickly and securely", "Adjust your computer settings", "Monitor apps and processes",
        "A simple and fast text editor", "Play and organize your media library",
    ];

    internal static IPrecomputedFuzzyMatcher CreateMatcher() => new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());

    internal static CatalogItem[] BuildCatalog(int count, string idPrefix)
    {
        var items = new CatalogItem[count];
        for (var i = 0; i < count; i++)
        {
            var noun = Nouns[i % Nouns.Length];
            var qualifier = Qualifiers[(i / Nouns.Length) % Qualifiers.Length];
            var title = string.IsNullOrEmpty(qualifier) ? noun : $"{noun} {qualifier}";

            if (i >= Nouns.Length * Qualifiers.Length)
            {
                title = $"{title} {i}";
            }

            var subtitle = SubtitleWords[i % SubtitleWords.Length];
            items[i] = new CatalogItem(title, subtitle, $"{idPrefix}.{i}");
        }

        return items;
    }

    internal static RecentCommandsManager SeedHistory(CatalogItem[] apps, int seedCount)
    {
        var history = new RecentCommandsManager();
        var n = Math.Min(seedCount, apps.Length);
        for (var i = 0; i < n; i++)
        {
            var idx = (i * 7) % apps.Length;
            history = history.WithHistoryItem(apps[idx].Id);
        }

        return history;
    }
}
