// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>Finds script actions without executing them while the user searches.</summary>
internal sealed partial class PowerScriptsFallback : FallbackResultsItem
{
    internal const string FallbackId = "com.microsoft.cmdpal.builtin.powerscripts.fallback";

    private readonly PowerScriptsCatalogSource _source;

    internal PowerScriptsFallback(PowerScriptsCatalogSource source, PowerScriptsListPage page)
        : base(Resources.PowerScripts, FallbackId)
    {
        _source = source;
        Title = Resources.PowerScripts;
        Icon = page.Icon;

        // Older hosts can still open the full script list.
        Command = page;
    }

    protected override async Task<IFallbackCommandResult> QueryAsync(
        FallbackQueryArgs args,
        CancellationToken cancellationToken,
        IProgress<IFallbackCommandResult> progress)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (args.RequestedItemCount == 0 || string.IsNullOrWhiteSpace(args.Query))
        {
            return new FallbackQueryResult(args.Query, args.QueryId, []);
        }

        var catalog = await _source.GetCatalogAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!catalog.IsEnabled)
        {
            return new FallbackQueryResult(args.Query, args.QueryId, []);
        }

        var requestedCount = (int)Math.Min(args.RequestedItemCount, (uint)catalog.Scripts.Count);
        return CreateResult(args, catalog, requestedCount, cancellationToken);
    }

    private FallbackQueryResult CreateResult(
        FallbackQueryArgs args,
        PowerScriptsCatalog catalog,
        int requestedCount,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // One lookahead item establishes whether another page exists without
        // creating pages and run sessions for every matching script.
        var maximumItems = requestedCount == int.MaxValue ? int.MaxValue : requestedCount + 1;
        var matches = _source.GetItems(catalog, args.Query, maximumItems);
        var visibleCount = Math.Min(requestedCount, matches.Length);
        var hasMore = matches.Length > visibleCount;
        cancellationToken.ThrowIfCancellationRequested();

        Task<IFallbackCommandResult> LoadMoreAsync(
            uint additionalCount,
            CancellationToken token,
            IProgress<IFallbackCommandResult> unusedProgress)
        {
            // Continue over the same catalogue snapshot, regardless of later
            // refreshes or other searches. Each result includes the earlier items.
            var totalCount = (int)Math.Min((ulong)visibleCount + additionalCount, (ulong)catalog.Scripts.Count);
            return Task.FromResult<IFallbackCommandResult>(CreateResult(args, catalog, totalCount, token));
        }

        return new FallbackQueryResult(
            args.Query,
            args.QueryId,
            matches[..visibleCount],
            hasMore,
            hasMore ? LoadMoreAsync : null);
    }
}
