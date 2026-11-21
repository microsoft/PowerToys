// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Helpers;

/// <summary>
/// Returns a module command when the query fuzzy-matches its title or subtitle.
/// </summary>
internal sealed partial class PowerToysFallbackResultSource : FallbackResultsItem
{
    private readonly ICommand _command;

    public PowerToysFallbackResultSource(ICommand command, string title, string subtitle, IIconInfo? icon, IContextItem[]? moreCommands)
        : base(title, $"{command.Id}.fallback")
    {
        _command = command;
        Title = title ?? string.Empty;
        Subtitle = subtitle ?? string.Empty;
        Icon = icon;
        MoreCommands = moreCommands ?? Array.Empty<IContextItem>();
    }

    protected override Task<IFallbackCommandResult> QueryAsync(
        FallbackQueryArgs args,
        CancellationToken cancellationToken,
        IProgress<IFallbackCommandResult> progress)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (args.RequestedItemCount == 0 || string.IsNullOrWhiteSpace(args.Query))
        {
            return Task.FromResult<IFallbackCommandResult>(new FallbackQueryResult(args.Query, args.QueryId, []));
        }

        var titleMatch = Common.Search.FuzzSearch.StringMatcher.FuzzyMatch(args.Query, Title);
        var subtitleMatch = Common.Search.FuzzSearch.StringMatcher.FuzzyMatch(args.Query, Subtitle);
        var matches = (titleMatch.Success && titleMatch.Score > 0) || (subtitleMatch.Success && subtitleMatch.Score > 0);
        cancellationToken.ThrowIfCancellationRequested();
        IListItem[] items = matches
            ? [
                new ListItem(_command)
                {
                    Title = Title,
                    Subtitle = Subtitle,
                    Icon = Icon,
                    MoreCommands = MoreCommands,
                },
            ]
            : [];
        return Task.FromResult<IFallbackCommandResult>(new FallbackQueryResult(args.Query, args.QueryId, items));
    }
}
