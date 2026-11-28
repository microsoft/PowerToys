// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander.Pages;

/// <summary>What the snippets page shows when it lists nothing, and what can be done about it.</summary>
internal readonly record struct EmptyState(string Title, string Subtitle, bool OffersSettings)
{
    /// <summary>
    /// Says why the list is empty: Text Expander is off (by policy, the user can't change it), the snippets file has no
    /// snippets, isn't there or can't be read, or nothing matches the search.
    /// </summary>
    public static EmptyState For(SnippetSourceState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var library = state.Library;
        return state.Availability switch
        {
            TextExpanderAvailability.OffByPolicy => new(Resources.empty_off_title, Resources.empty_policy_subtitle, false),
            TextExpanderAvailability.Off => new(Resources.empty_off_title, Resources.empty_off_subtitle, true),
            _ => library.Status switch
            {
                SnippetLibraryStatus.NoSnippetsYet => new(Resources.empty_no_snippets_title, Resources.empty_no_snippets_subtitle, true),
                SnippetLibraryStatus.FileNotFound => new(Resources.empty_not_found_title, Format(Resources.empty_not_found_subtitle, library.FilePath), true),
                SnippetLibraryStatus.Unreadable => new(Resources.empty_unreadable_title, Format(Resources.empty_unreadable_subtitle, library.FilePath), false),
                SnippetLibraryStatus.NotSnippetsFile => new(Resources.empty_unreadable_title, NotSnippetsFileSubtitle(library), true),
                _ => new(Resources.empty_no_matches_title, Resources.empty_no_matches_subtitle, false),
            },
        };
    }

    public ICommandItem ToCommandItem() => new CommandItem(OffersSettings ? new OpenTextExpanderSettingsCommand() : new NoOpCommand())
    {
        Title = Title,
        Subtitle = Subtitle,
        Icon = Icons.TextExpander,
    };

    private static string NotSnippetsFileSubtitle(SnippetLibrarySnapshot library) => library.ErrorLine is long line
        ? Format(Resources.empty_not_snippets_line_subtitle, line, library.FilePath)
        : Format(Resources.empty_not_snippets_subtitle, library.FilePath);

    private static string Format(string format, params object[] args) => string.Format(CultureInfo.CurrentCulture, format, args);
}
