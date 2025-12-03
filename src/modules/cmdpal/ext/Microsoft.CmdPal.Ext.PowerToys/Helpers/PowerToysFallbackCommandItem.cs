// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Common.Search.FuzzSearch;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Helpers;

/// <summary>
/// A fallback item that filters itself based on the landing-page query.
/// It hides itself (empty Title) when the query doesn't fuzzy-match the title or subtitle.
/// </summary>
internal sealed partial class PowerToysFallbackCommandItem : FallbackCommandItem, IFallbackHandler
{
    private readonly string _baseTitle;
    private readonly string _baseSubtitle;
    private readonly string _baseName;
    private readonly Command? _mutableCommand;

    public PowerToysFallbackCommandItem(ICommand command, string title, string subtitle, IIconInfo? icon, IContextItem[]? moreCommands)
        : base(command, title)
    {
        _baseTitle = title ?? string.Empty;
        _baseSubtitle = subtitle ?? string.Empty;
        _baseName = command?.Name ?? string.Empty;
        _mutableCommand = command as Command;

        // Start hidden; we only surface when the query matches
        Title = string.Empty;
        Subtitle = string.Empty;
        if (_mutableCommand is not null)
        {
            _mutableCommand.Name = string.Empty;
        }

        if (icon != null)
        {
            Icon = icon;
        }

        MoreCommands = moreCommands ?? Array.Empty<IContextItem>();

        // Ensure fallback updates route to this instance
        FallbackHandler = this;
    }

    public override void UpdateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            if (_mutableCommand is not null)
            {
                _mutableCommand.Name = string.Empty;
            }

            return;
        }

        // Simple fuzzy match against title/subtitle; hide if neither match
        var titleMatch = StringMatcher.FuzzyMatch(query, _baseTitle);
        var subtitleMatch = StringMatcher.FuzzyMatch(query, _baseSubtitle);
        var matches = (titleMatch.Success && titleMatch.Score > 0) || (subtitleMatch.Success && subtitleMatch.Score > 0);

        if (matches)
        {
            Title = _baseTitle;
            Subtitle = _baseSubtitle;
            if (_mutableCommand is not null)
            {
                _mutableCommand.Name = _baseName;
            }
        }
        else
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            if (_mutableCommand is not null)
            {
                _mutableCommand.Name = string.Empty;
            }
        }
    }
}
