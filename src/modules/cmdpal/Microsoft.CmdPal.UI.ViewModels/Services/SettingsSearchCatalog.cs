// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.Messages;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public static class SettingsSearchCatalog
{
    // Reuse the labels shown on the pages without constructing pages or loading extension settings.
    // Keywords is a localized resource key suffix: "Shortcuts" resolves to "SettingsSearch_Keywords_Shortcuts".
    private static readonly Dictionary<string, (string Title, string? Description, string IconGlyph, string? Keywords, string? Section)> EntryMetadata = new()
    {
        [SettingsLinkIds.General.Page] = ("Settings_PageTitles_GeneralPage", null, Glyphs.Home, null, null),
        [SettingsLinkIds.General.Activation] = ("Settings_GeneralPage_ActivationSettingsHeader.Text", null, Glyphs.KeyboardShortcut, "Shortcuts", null),
        [SettingsLinkIds.General.ActivationKey] = ("Settings_GeneralPage_ActivationKey_SettingsExpander.Header", null, Glyphs.KeyboardShortcut, "Shortcuts", SettingsLinkIds.General.Activation),
        [SettingsLinkIds.General.AutoGoHome] = ("Settings_GeneralPage_AutoGoHome_SettingsCard.Header", null, Glyphs.Home, "GoHome", SettingsLinkIds.General.Activation),
        [SettingsLinkIds.General.KeepPreviousQuery] = ("Settings_GeneralPage_KeepPreviousQuery_SettingsCard.Header", null, Glyphs.History, "PreviousQuery", SettingsLinkIds.General.Activation),
        [SettingsLinkIds.General.HighlightSearch] = ("Settings_GeneralPage_HighlightSearch_SettingsCard.Header", null, Glyphs.Highlight, "HighlightSearch", SettingsLinkIds.General.Activation),
        [SettingsLinkIds.General.AppBehavior] = ("Settings_GeneralPage_AppBehaviorSettingsHeader.Text", null, Glyphs.Settings, null, null),
        [SettingsLinkIds.General.SystemTrayIcon] = ("Settings_GeneralPage_ShowSystemTrayIcon_SettingsCard.Header", null, Glyphs.SystemTray, "TrayIcon", SettingsLinkIds.General.AppBehavior),
        [SettingsLinkIds.General.AltF4] = ("Settings_GeneralPage_AllowAltF4_SettingsCard.Header", null, Glyphs.Power, "Quit", SettingsLinkIds.General.AppBehavior),
        [SettingsLinkIds.General.Language] = ("Settings_GeneralPage_Language_SettingsCard.Header", "Settings_GeneralPage_Language_SettingsCard.Description", Glyphs.Language, "Language", SettingsLinkIds.General.AppBehavior),
        [SettingsLinkIds.General.ExternalLinks] = ("ExternalLinksSettingsHeader.Text", null, Glyphs.Link, "Links", null),
        [SettingsLinkIds.General.ExternalCommandLinks] = ("Settings_GeneralPage_ExternalCommandLinks_SettingsExpander.Header", "Settings_GeneralPage_ExternalCommandLinks_SettingsExpander.Description", Glyphs.Link, "Links", SettingsLinkIds.General.ExternalLinks),
        [SettingsLinkIds.General.AboutSection] = ("Settings_GeneralPage_AboutSettingsHeader.Text", null, Glyphs.Info, null, null),
        [SettingsLinkIds.General.About] = ("Settings_GeneralPage_About_SettingsExpander.Header", null, Glyphs.Info, "About", SettingsLinkIds.General.AboutSection),
        [SettingsLinkIds.General.SendFeedback] = ("Settings_GeneralPage_SendFeedback_Hyperlink.Content", null, Glyphs.Feedback, "Feedback", SettingsLinkIds.General.AboutSection),
        [SettingsLinkIds.Appearance.Page] = ("Settings_PageTitles_AppearancePage", null, Glyphs.Personalization, "Appearance", null),
        [SettingsLinkIds.Appearance.Visuals] = ("Settings_AppearancePage_AppearanceSettingsHeader.Text", null, Glyphs.Personalization, "Appearance", null),
        [SettingsLinkIds.Appearance.Theme] = ("Settings_GeneralPage_AppTheme_SettingsCard.Header", "Settings_GeneralPage_AppTheme_SettingsCard.Description", Glyphs.Theme, "Theme", SettingsLinkIds.Appearance.Visuals),
        [SettingsLinkIds.Appearance.Backdrop] = ("Settings_GeneralPage_BackdropStyle_SettingsCard.Header", "Settings_GeneralPage_BackdropStyle_SettingsCard.Description", Glyphs.Backdrop, "Backdrop", SettingsLinkIds.Appearance.Visuals),
        [SettingsLinkIds.Appearance.Background] = ("Settings_GeneralPage_Background_SettingsExpander.Header", "Settings_GeneralPage_Background_SettingsExpander.Description", Glyphs.Personalization, "Background", SettingsLinkIds.Appearance.Visuals),
        [SettingsLinkIds.Appearance.DisableAnimations] = ("Settings_GeneralPage_DisableAnimations_SettingsCard.Header", null, Glyphs.Animations, "Animations", SettingsLinkIds.Appearance.Visuals),
        [SettingsLinkIds.Appearance.Layout] = ("Settings_AppearancePage_LayoutSettingsHeader.Text", null, Glyphs.Layout, null, null),
        [SettingsLinkIds.Appearance.CompactMode] = ("Settings_GeneralPage_CompactMode_SettingsCard.Header", null, Glyphs.CompactMode, "CompactMode", SettingsLinkIds.Appearance.Layout),
        [SettingsLinkIds.Appearance.HomeRecentCommands] = ("Settings_GeneralPage_HomeRecentCommands_SettingsCard.Header", "Settings_GeneralPage_HomeRecentCommands_SettingsCard.Description", Glyphs.History, "History", SettingsLinkIds.Appearance.Layout),
        [SettingsLinkIds.Appearance.RecentCommandsDisplayLimit] = ("Settings_GeneralPage_RecentCommandsDisplayLimit_SettingsCard.Header", "Settings_GeneralPage_RecentCommandsDisplayLimit_SettingsCard.Description", Glyphs.History, "History", SettingsLinkIds.Appearance.Layout),
        [SettingsLinkIds.Appearance.ClearRecentCommands] = ("Settings_GeneralPage_ClearRecentCommands_SettingsCard.Header", "Settings_GeneralPage_ClearRecentCommands_SettingsCard.Description", Glyphs.Delete, "ClearHistory", SettingsLinkIds.Appearance.Layout),
        [SettingsLinkIds.Appearance.LaunchPosition] = ("Run_PositionHeader.Header", null, Glyphs.Position, "LaunchPosition", SettingsLinkIds.Appearance.Layout),
        [SettingsLinkIds.Appearance.ToastPosition] = ("Settings_GeneralPage_ToastPosition_SettingsCard.Header", null, Glyphs.Notifications, "Toast", SettingsLinkIds.Appearance.Layout),
        [SettingsLinkIds.Appearance.Interaction] = ("Settings_AppearancePage_InteractionSettingsHeader.Text", null, Glyphs.Interaction, null, null),
        [SettingsLinkIds.Appearance.SingleClickActivation] = ("Settings_GeneralPage_SingleClickActivation_SettingsCard.Header", null, Glyphs.Interaction, "SingleClick", SettingsLinkIds.Appearance.Interaction),
        [SettingsLinkIds.Appearance.ShowAppDetails] = ("Settings_GeneralPage_ShowAppDetails_SettingsCard.Header", null, Glyphs.Layout, "AppDetails", SettingsLinkIds.Appearance.Interaction),
        [SettingsLinkIds.Appearance.BackspaceGoesBack] = ("Settings_GeneralPage_BackspaceGoesBack_SettingsCard.Header", null, Glyphs.Back, "GoBack", SettingsLinkIds.Appearance.Interaction),
        [SettingsLinkIds.Appearance.EscapeKeyBehavior] = ("Settings_GeneralPage_EscapeKeyBehavior_SettingsCard.Header", null, Glyphs.Escape, "Escape", SettingsLinkIds.Appearance.Interaction),
        [SettingsLinkIds.Extensions.Page] = ("Settings_PageTitles_ExtensionsPage", null, Glyphs.Extensions, "Extensions", null),
        [SettingsLinkIds.Extensions.FallbackOrder] = ("ManageFallbackRank.Text", "ManageFallbackOrderDialogDescription.Text", Glyphs.Reorder, "FallbackOrder", null),
        [SettingsLinkIds.Dock.Page] = ("Settings_PageTitles_DockPage", null, Glyphs.Dock, null, null),
        [SettingsLinkIds.Dock.Enabled] = ("Settings_GeneralPage_EnableDock_SettingsCard.Header", "Settings_GeneralPage_EnableDock_SettingsCard.Description", Glyphs.Dock, null, null),
        [SettingsLinkIds.Dock.FocusShortcut] = ("Settings_DockPage_FocusShortcut_SettingsCard.Header", "Settings_DockPage_FocusShortcut_SettingsCard.Description", Glyphs.Focus, "Shortcuts", null),
        [SettingsLinkIds.Dock.AppearanceSection] = ("DockAppearanceSettingsHeader.Text", null, Glyphs.Personalization, "Appearance", null),
        [SettingsLinkIds.Dock.Position] = ("DockAppearance_DockPosition_SettingsCard.Header", null, Glyphs.Position, "DockPosition", SettingsLinkIds.Dock.AppearanceSection),
        [SettingsLinkIds.Dock.Size] = ("DockAppearance_DockSize_SettingsCard.Header", "DockAppearance_DockSize_SettingsCard.Description", Glyphs.Size, "DockSize", SettingsLinkIds.Dock.AppearanceSection),
        [SettingsLinkIds.Dock.Theme] = ("DockAppearance_AppTheme_SettingsCard.Header", "DockAppearance_AppTheme_SettingsCard.Description", Glyphs.Theme, "Theme", SettingsLinkIds.Dock.AppearanceSection),
        [SettingsLinkIds.Dock.Backdrop] = ("DockAppearance_Backdrop_SettingsCard.Header", "DockAppearance_Backdrop_SettingsCard.Description", Glyphs.Backdrop, "DockBackdrop", SettingsLinkIds.Dock.AppearanceSection),
        [SettingsLinkIds.Dock.Background] = ("DockAppearance_Background_SettingsExpander.Header", "DockAppearance_Background_SettingsExpander.Description", Glyphs.Personalization, "DockBackground", SettingsLinkIds.Dock.AppearanceSection),
        [SettingsLinkIds.Dock.BehaviorSection] = ("BehaviorSettingsHeader.Text", null, Glyphs.Settings, null, null),
        [SettingsLinkIds.Dock.AlwaysOnTop] = ("DockBehavior_AlwaysOnTop_SettingsCard.Header", "DockBehavior_AlwaysOnTop_SettingsCard.Description", Glyphs.AlwaysOnTop, "AlwaysOnTop", SettingsLinkIds.Dock.BehaviorSection),
        [SettingsLinkIds.Dock.AutoHide] = ("DockBehavior_AutoHide_SettingsCard.Header", "DockBehavior_AutoHide_SettingsCard.Description", Glyphs.AutoHide, "AutoHide", SettingsLinkIds.Dock.BehaviorSection),
        [SettingsLinkIds.Dock.Monitors] = ("DockMonitors_Header.Text", null, Glyphs.Monitors, "Monitors", null),
    };

    public static SettingsSearchResult[] CreateEntries(Func<string, string> getString)
    {
        var entries = new List<SettingsSearchResult>();
        var settingsTitle = getString("SettingsWindowTitle");
        var pagesTitle = getString("SettingsWindow_SearchPagesGroup");
        var pageCategory = getString("SettingsWindow_SearchCategoryPage");
        var settingsCategory = getString("SettingsWindow_SearchCategorySettings");
        foreach (var destination in SettingsLinkResolver.Destinations)
        {
            if (destination.RequiresExtensionProvider || !EntryMetadata.TryGetValue(destination.LinkId, out var metadata))
            {
                continue;
            }

            var pageTitle = getString($"Settings_PageTitles_{destination.PageTag}Page");
            var breadcrumb = metadata.Section is null
                ? pageTitle
                : string.Format(CultureInfo.CurrentCulture, getString("SettingsWindow_SearchBreadcrumb"), pageTitle, getString(EntryMetadata[metadata.Section].Title));
            var navigation = new OpenSettingsMessage(SettingsLinkId: destination.LinkId);
            entries.Add(new(
                getString(metadata.Title),
                destination.ElementId is null ? settingsTitle : breadcrumb,
                navigation,
                metadata.Description is null ? string.Empty : getString(metadata.Description),
                metadata.Keywords is null ? string.Empty : getString($"SettingsSearch_Keywords_{metadata.Keywords}"),
                metadata.IconGlyph,
                GroupName: destination.ElementId is null ? pagesTitle : pageTitle,
                Category: destination.ElementId is null ? pageCategory : settingsCategory));
        }

        entries.Add(new(
            getString("Settings_PageTitles_GalleryPage"),
            settingsTitle,
            new OpenSettingsMessage(SettingsPageTag: SettingsPageTags.Gallery),
            Keywords: getString("SettingsSearch_Keywords_Gallery"),
            IconGlyph: Glyphs.Gallery,
            GroupName: pagesTitle,
            Category: pageCategory));
        return [.. entries];
    }

    public static SettingsSearchResultGroup[] GroupResults(IEnumerable<SettingsSearchResult> results)
    {
        return
        [
            .. results
                .GroupBy(result => result.GroupName)
                .Select(group => new SettingsSearchResultGroup(group.Key, [.. group]))
        ];
    }

    public static SettingsSearchResult[] CreateSuggestions(SettingsSearchResult[] results, string showAllResultsFormat, string noResultsText)
    {
        return results.Length == 0
            ? [new SettingsSearchResult(noResultsText, string.Empty, null)]
            :
            [
                .. results.Take(5),
                new SettingsSearchResult(
                    string.Format(CultureInfo.CurrentCulture, showAllResultsFormat, results.Length), string.Empty, null, IsShowAllResults: true)
            ];
    }

    public static SettingsSearchResult[] Search(string query, IEnumerable<SettingsSearchResult> entries, IPrecomputedFuzzyMatcher matcher)
    {
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            return [];
        }

        var text = query.Trim();
        var hotkeyQuery = NormalizeHotkey(text);
        var fuzzyQueries = terms.Select(matcher.PrecomputeQuery).ToArray();
        return
        [
            .. entries
                .Where(entry => entry.Destination is not null)
                .Select(entry =>
                {
                    var keywords = entry.Keywords.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    var hotkeyMatch = !string.IsNullOrEmpty(entry.AssignedHotkey) &&
                                      hotkeyQuery.Equals(NormalizeHotkey(entry.AssignedHotkey), StringComparison.Ordinal);
                    var literalMatch = hotkeyMatch || terms.All(term => MatchesLiteralTerm(entry, term));
                    var rank = !literalMatch ? 5
                        : hotkeyMatch ? 0
                        : entry.Title.Equals(text, StringComparison.OrdinalIgnoreCase) ? 0
                        : entry.Title.StartsWith(text, StringComparison.OrdinalIgnoreCase) ? 1
                        : terms.All(term => entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) ? 2
                        : terms.Any(MatchesTitleOrAlias) && terms.All(term =>
                            MatchesTitleOrAlias(term) ||
                            entry.Breadcrumb.Contains(term, StringComparison.OrdinalIgnoreCase)) ? 3 : 4;
                    var score = literalMatch ? 0 : GetFuzzyScore(entry, terms, fuzzyQueries, matcher);
                    return (Entry: entry, Rank: rank, Score: score);

                    bool MatchesTitleOrAlias(string term)
                    {
                        return entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                               keywords.Contains(term, StringComparer.OrdinalIgnoreCase);
                    }
                })
                .Where(match => match.Rank < 5 || match.Score > 0)
                .OrderBy(match => match.Rank)
                .ThenByDescending(match => match.Score)
                .ThenBy(match => match.Entry.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(match => match.Entry.Breadcrumb, StringComparer.CurrentCultureIgnoreCase)
                .Select(match => match.Entry)
        ];
    }

    private static bool MatchesLiteralTerm(SettingsSearchResult entry, string term)
    {
        return entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               entry.Breadcrumb.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               entry.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
               entry.Keywords.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeHotkey(string text)
    {
        return string.Join("+", text.Replace('+', ' ').Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(key => key.ToUpperInvariant() switch
            {
                "CONTROL" => "CTRL",
                "WINDOWS" => "WIN",
                var name => name,
            })
            .Order(StringComparer.Ordinal));
    }

    private static int GetFuzzyScore(SettingsSearchResult entry, string[] terms, FuzzyQuery[] queries, IPrecomputedFuzzyMatcher matcher)
    {
        var targets = entry.Keywords.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Prepend(entry.Breadcrumb)
            .Prepend(entry.Title)
            .Select(matcher.PrecomputeTarget)
            .ToArray();
        var score = 0;
        for (var i = 0; i < terms.Length; i++)
        {
            var bestScore = 0;
            foreach (var target in targets)
            {
                bestScore = Math.Max(bestScore, matcher.Score(queries[i], target));
            }

            if (bestScore == 0 && !MatchesLiteralTerm(entry, terms[i]))
            {
                return 0;
            }

            score += bestScore;
        }

        return score;
    }

    private static class Glyphs
    {
        public const string AlwaysOnTop = "\uE840";
        public const string Animations = "\uE945";
        public const string AutoHide = "\uE70A";
        public const string Back = "\uE750";
        public const string Backdrop = "\uF5EF";
        public const string CompactMode = "\uE73F";
        public const string Delete = "\uE74D";
        public const string Dock = "\uF596";
        public const string Escape = "\uE845";
        public const string Extensions = "\uEA86";
        public const string Feedback = "\uE939";
        public const string Focus = "\uE765";
        public const string Gallery = "\uE719";
        public const string Highlight = "\uE933";
        public const string History = "\uE81C";
        public const string Home = "\uE80F";
        public const string Info = "\uE946";
        public const string Interaction = "\uE962";
        public const string KeyboardShortcut = "\uEDA7";
        public const string Language = "\uF2B7";
        public const string Layout = "\uE8A0";
        public const string Link = "\uE71B";
        public const string Monitors = "\uE7F4";
        public const string Notifications = "\uE75A";
        public const string Personalization = "\uE790";
        public const string Position = "\uE78B";
        public const string Power = "\uE7E8";
        public const string Reorder = "\uE8CB";
        public const string Settings = "\uE713";
        public const string Size = "\uE799";
        public const string SystemTray = "\uE75B";
        public const string Theme = "\uE793";
    }
}
