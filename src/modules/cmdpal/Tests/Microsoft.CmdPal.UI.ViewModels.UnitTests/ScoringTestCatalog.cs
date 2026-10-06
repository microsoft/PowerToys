// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

internal static partial class ScoringTestCatalog
{
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

    internal static IPrecomputedFuzzyMatcher CreateMatcher()
    {
        return new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());
    }

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

    internal static AppListItem[] BuildAppCatalog(int count)
    {
        var programs = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return BuildCatalog(count, "app").Select((template, i) =>
        {
            var packaged = i % 3 == 0;
            var shortcut = Path.Combine(programs, "Contoso", $"{template.Title}.lnk");
            var directory = Path.Combine(programFiles, packaged ? "WindowsApps" : "Contoso", $"Contoso.App{i}");
            return new AppListItem(
                new AppItem
                {
                    Name = template.Title,
                    Subtitle = template.Subtitle,
                    LaunchTarget = packaged ? string.Empty : shortcut,
                    ResolvedTarget = Path.Combine(directory, $"app{i}.exe"),
                    DirectoryPath = directory,
                    IsPackaged = packaged,
                    AppUserModelId = packaged ? $"Contoso.App{i}_publisher!App" : string.Empty,
                    PackageFamilyName = packaged ? $"Contoso.App{i}_publisher" : string.Empty,
                    MatchTerms = [$"alias{i}", $"Profile{i % 8}", shortcut],
                },
                useThumbnails: false);
        }).ToArray();
    }

    internal static RecentCommandsManager SeedHistory(IReadOnlyList<IListItem> apps, int seedCount)
    {
        var history = new RecentCommandsManager();
        var n = Math.Min(seedCount, apps.Count);
        for (var i = 0; i < n; i++)
        {
            var idx = (i * 7) % apps.Count;
            history = history.WithHistoryItem(apps[idx].Command!.Id);
        }

        return history;
    }

    internal sealed partial class CatalogItem : ListItem, IPrecomputedListItem
    {
        private FuzzyTargetCache _titleCache;
        private FuzzyTargetCache _subtitleCache;

        internal string Id { get; }

        internal CatalogItem(string title, string subtitle, string id)
            : base(new NoOpCommand() { Id = id })
        {
            Title = title;
            Subtitle = subtitle;
            Id = id;
        }

        public FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher)
        {
            return _titleCache.GetOrUpdate(matcher, Title);
        }

        public FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher)
        {
            return _subtitleCache.GetOrUpdate(matcher, Subtitle);
        }
    }
}
