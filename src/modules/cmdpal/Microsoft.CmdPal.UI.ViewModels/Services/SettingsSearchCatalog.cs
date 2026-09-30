// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.Messages;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public static class SettingsSearchCatalog
{
    // Reuse the labels shown on the pages without constructing pages or loading extension settings.
    private static readonly Dictionary<string, (string Title, string? Description, string IconGlyph, string? Keywords)> EntryMetadata = new()
    {
        [SettingsLinkIds.General.Page] = ("Settings_PageTitles_GeneralPage", null, "\uE80F", null),
        [SettingsLinkIds.General.Activation] = ("Settings_GeneralPage_ActivationSettingsHeader.Text", null, "\uEDA7", "Shortcuts"),
        [SettingsLinkIds.General.ActivationKey] = ("Settings_GeneralPage_ActivationKey_SettingsExpander.Header", null, "\uEDA7", "Shortcuts"),
        [SettingsLinkIds.General.AutoGoHome] = ("Settings_GeneralPage_AutoGoHome_SettingsCard.Header", null, "\uE80F", "GoHome"),
        [SettingsLinkIds.General.KeepPreviousQuery] = ("Settings_GeneralPage_KeepPreviousQuery_SettingsCard.Header", null, "\uE81C", "PreviousQuery"),
        [SettingsLinkIds.General.HighlightSearch] = ("Settings_GeneralPage_HighlightSearch_SettingsCard.Header", null, "\uE933", "HighlightSearch"),
        [SettingsLinkIds.General.AppBehavior] = ("Settings_GeneralPage_AppBehaviorSettingsHeader.Text", null, "\uE713", null),
        [SettingsLinkIds.General.SystemTrayIcon] = ("Settings_GeneralPage_ShowSystemTrayIcon_SettingsCard.Header", null, "\uE75B", "TrayIcon"),
        [SettingsLinkIds.General.AltF4] = ("Settings_GeneralPage_AllowAltF4_SettingsCard.Header", null, "\uE7E8", "Quit"),
        [SettingsLinkIds.General.Language] = ("Settings_GeneralPage_Language_SettingsCard.Header", "Settings_GeneralPage_Language_SettingsCard.Description", "\uF2B7", "Language"),
        [SettingsLinkIds.General.ExternalLinks] = ("ExternalLinksSettingsHeader.Text", null, "\uE71B", "Links"),
        [SettingsLinkIds.General.ExternalCommandLinks] = ("Settings_GeneralPage_ExternalCommandLinks_SettingsExpander.Header", "Settings_GeneralPage_ExternalCommandLinks_SettingsExpander.Description", "\uE71B", "Links"),
        [SettingsLinkIds.General.AboutSection] = ("Settings_GeneralPage_AboutSettingsHeader.Text", null, "\uE946", null),
        [SettingsLinkIds.General.About] = ("Settings_GeneralPage_About_SettingsExpander.Header", null, "\uE946", "About"),
        [SettingsLinkIds.General.SendFeedback] = ("Settings_GeneralPage_SendFeedback_Hyperlink.Content", null, "\uE939", "Feedback"),
        [SettingsLinkIds.Appearance.Page] = ("Settings_PageTitles_AppearancePage", null, "\uE790", "Appearance"),
        [SettingsLinkIds.Appearance.Visuals] = ("Settings_AppearancePage_AppearanceSettingsHeader.Text", null, "\uE790", "Appearance"),
        [SettingsLinkIds.Appearance.Theme] = ("Settings_GeneralPage_AppTheme_SettingsCard.Header", "Settings_GeneralPage_AppTheme_SettingsCard.Description", "\uE793", "Theme"),
        [SettingsLinkIds.Appearance.Backdrop] = ("Settings_GeneralPage_BackdropStyle_SettingsCard.Header", "Settings_GeneralPage_BackdropStyle_SettingsCard.Description", "\uF5EF", "Backdrop"),
        [SettingsLinkIds.Appearance.Background] = ("Settings_GeneralPage_Background_SettingsExpander.Header", "Settings_GeneralPage_Background_SettingsExpander.Description", "\uE790", "Background"),
        [SettingsLinkIds.Appearance.DisableAnimations] = ("Settings_GeneralPage_DisableAnimations_SettingsCard.Header", null, "\uE945", "Animations"),
        [SettingsLinkIds.Appearance.Layout] = ("Settings_AppearancePage_LayoutSettingsHeader.Text", null, "\uE8A0", null),
        [SettingsLinkIds.Appearance.CompactMode] = ("Settings_GeneralPage_CompactMode_SettingsCard.Header", null, "\uE73F", "CompactMode"),
        [SettingsLinkIds.Appearance.HomeRecentCommands] = ("Settings_GeneralPage_HomeRecentCommands_SettingsCard.Header", "Settings_GeneralPage_HomeRecentCommands_SettingsCard.Description", "\uE81C", "History"),
        [SettingsLinkIds.Appearance.RecentCommandsDisplayLimit] = ("Settings_GeneralPage_RecentCommandsDisplayLimit_SettingsCard.Header", "Settings_GeneralPage_RecentCommandsDisplayLimit_SettingsCard.Description", "\uE81C", "History"),
        [SettingsLinkIds.Appearance.ClearRecentCommands] = ("Settings_GeneralPage_ClearRecentCommands_SettingsCard.Header", "Settings_GeneralPage_ClearRecentCommands_SettingsCard.Description", "\uE74D", "ClearHistory"),
        [SettingsLinkIds.Appearance.LaunchPosition] = ("Run_PositionHeader.Header", null, "\uE78B", "LaunchPosition"),
        [SettingsLinkIds.Appearance.ToastPosition] = ("Settings_GeneralPage_ToastPosition_SettingsCard.Header", null, "\uE75A", "Toast"),
        [SettingsLinkIds.Appearance.Interaction] = ("Settings_AppearancePage_InteractionSettingsHeader.Text", null, "\uE962", null),
        [SettingsLinkIds.Appearance.SingleClickActivation] = ("Settings_GeneralPage_SingleClickActivation_SettingsCard.Header", null, "\uE962", "SingleClick"),
        [SettingsLinkIds.Appearance.ShowAppDetails] = ("Settings_GeneralPage_ShowAppDetails_SettingsCard.Header", null, "\uE8A0", "AppDetails"),
        [SettingsLinkIds.Appearance.BackspaceGoesBack] = ("Settings_GeneralPage_BackspaceGoesBack_SettingsCard.Header", null, "\uE750", "GoBack"),
        [SettingsLinkIds.Appearance.EscapeKeyBehavior] = ("Settings_GeneralPage_EscapeKeyBehavior_SettingsCard.Header", null, "\uE845", "Escape"),
        [SettingsLinkIds.Extensions.Page] = ("Settings_PageTitles_ExtensionsPage", null, "\uEA86", "Extensions"),
        [SettingsLinkIds.Extensions.FallbackOrder] = ("ManageFallbackRank.Text", "ManageFallbackOrderDialogDescription.Text", "\uE8CB", "FallbackOrder"),
        [SettingsLinkIds.Dock.Page] = ("Settings_PageTitles_DockPage", null, "\uF596", null),
        [SettingsLinkIds.Dock.Enabled] = ("Settings_GeneralPage_EnableDock_SettingsCard.Header", "Settings_GeneralPage_EnableDock_SettingsCard.Description", "\uF596", null),
        [SettingsLinkIds.Dock.FocusShortcut] = ("Settings_DockPage_FocusShortcut_SettingsCard.Header", "Settings_DockPage_FocusShortcut_SettingsCard.Description", "\uE765", "Shortcuts"),
        [SettingsLinkIds.Dock.AppearanceSection] = ("DockAppearanceSettingsHeader.Text", null, "\uE790", "Appearance"),
        [SettingsLinkIds.Dock.Position] = ("DockAppearance_DockPosition_SettingsCard.Header", null, "\uE78B", "DockPosition"),
        [SettingsLinkIds.Dock.Size] = ("DockAppearance_DockSize_SettingsCard.Header", "DockAppearance_DockSize_SettingsCard.Description", "\uE799", "DockSize"),
        [SettingsLinkIds.Dock.Theme] = ("DockAppearance_AppTheme_SettingsCard.Header", "DockAppearance_AppTheme_SettingsCard.Description", "\uE793", "Theme"),
        [SettingsLinkIds.Dock.Backdrop] = ("DockAppearance_Backdrop_SettingsCard.Header", "DockAppearance_Backdrop_SettingsCard.Description", "\uF5EF", "DockBackdrop"),
        [SettingsLinkIds.Dock.Background] = ("DockAppearance_Background_SettingsExpander.Header", "DockAppearance_Background_SettingsExpander.Description", "\uE790", "DockBackground"),
        [SettingsLinkIds.Dock.BehaviorSection] = ("BehaviorSettingsHeader.Text", null, "\uE713", null),
        [SettingsLinkIds.Dock.AlwaysOnTop] = ("DockBehavior_AlwaysOnTop_SettingsCard.Header", "DockBehavior_AlwaysOnTop_SettingsCard.Description", "\uE840", "AlwaysOnTop"),
        [SettingsLinkIds.Dock.AutoHide] = ("DockBehavior_AutoHide_SettingsCard.Header", "DockBehavior_AutoHide_SettingsCard.Description", "\uE70A", "AutoHide"),
        [SettingsLinkIds.Dock.Monitors] = ("DockMonitors_Header.Text", null, "\uE7F4", "Monitors"),
    };

    public static SettingsSearchResult[] CreateEntries(Func<string, string> getString)
    {
        var entries = new List<SettingsSearchResult>();
        var settingsTitle = getString("SettingsWindowTitle");
        foreach (var destination in SettingsLinkResolver.Destinations)
        {
            if (destination.RequiresExtensionProvider || !EntryMetadata.TryGetValue(destination.LinkId, out var metadata))
            {
                continue;
            }

            var pageTitle = getString($"Settings_PageTitles_{destination.PageTag}Page");
            var navigation = new OpenSettingsMessage(SettingsLinkId: destination.LinkId);
            entries.Add(new(
                getString(metadata.Title),
                destination.ElementId is null ? settingsTitle : pageTitle,
                navigation,
                metadata.Description is null ? string.Empty : getString(metadata.Description),
                metadata.Keywords is null ? string.Empty : getString($"SettingsSearch_Keywords_{metadata.Keywords}"),
                metadata.IconGlyph));
        }

        entries.Add(new(
            getString("Settings_PageTitles_GalleryPage"),
            settingsTitle,
            new OpenSettingsMessage(SettingsPageTag: SettingsPageTags.Gallery),
            Keywords: getString("SettingsSearch_Keywords_Gallery"),
            IconGlyph: "\uE719"));
        return entries.ToArray();
    }

    public static SettingsSearchResult[] CreateSuggestions(SettingsSearchResult[] results, string showAllResultsText, string noResultsText)
    {
        return results.Length == 0
            ? [new SettingsSearchResult(noResultsText, string.Empty, null)]
            : results.Take(5).Append(new SettingsSearchResult(showAllResultsText, string.Empty, null, IsShowAllResults: true)).ToArray();
    }

    public static SettingsSearchResult[] Search(string query, IEnumerable<SettingsSearchResult> entries, IPrecomputedFuzzyMatcher matcher)
    {
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
        {
            return [];
        }

        var text = query.Trim();
        var fuzzyQueries = terms.Select(matcher.PrecomputeQuery).ToArray();
        return entries
            .Where(entry => entry.Destination is not null)
            .Select(entry =>
            {
                var literalMatch = terms.All(term => MatchesLiteralTerm(entry, term));
                var rank = !literalMatch ? 4
                    : entry.Title.Equals(text, StringComparison.OrdinalIgnoreCase) ? 0
                    : entry.Title.StartsWith(text, StringComparison.OrdinalIgnoreCase) ? 1
                    : terms.All(term => entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) ? 2 : 3;
                var score = literalMatch ? 0 : GetFuzzyScore(entry, terms, fuzzyQueries, matcher);
                return (Entry: entry, Rank: rank, Score: score);
            })
            .Where(match => match.Rank < 4 || match.Score > 0)
            .OrderBy(match => match.Rank)
            .ThenByDescending(match => match.Score)
            .ThenBy(match => match.Entry.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(match => match.Entry.Breadcrumb, StringComparer.CurrentCultureIgnoreCase)
            .Select(match => match.Entry)
            .ToArray();
    }

    private static bool MatchesLiteralTerm(SettingsSearchResult entry, string term) =>
        entry.Title.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        entry.Breadcrumb.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        entry.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
        entry.Keywords.Contains(term, StringComparison.OrdinalIgnoreCase);

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
}
