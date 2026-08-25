// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed partial class IndexerFallbackResultSource : FallbackResultSource
{
    private const string CommandId = "com.microsoft.cmdpal.builtin.indexer.fallback";
    private Func<string, bool>? _suppressCallback;

    internal IndexerFallbackResultSource()
        : base(Resources.Indexer_Find_Path_fallback_display_title, CommandId)
    {
        Title = Resources.IndexerCommandsProvider_DisplayName;
        SuggestedQueryDelayMilliseconds = 100;
        SuggestedMinQueryLength = 1;
        Icon = Icons.FileExplorerIcon;
    }

    protected override async Task<IFallbackCommandResult> QueryAsync(
        IFallbackQueryArgs args,
        CancellationToken cancellationToken,
        IProgress<IFallbackCommandResult> progress)
    {
        if (string.IsNullOrWhiteSpace(args.Query) || _suppressCallback?.Invoke(args.Query) == true)
        {
            return new FallbackQueryResult(args.Query, args.QueryId, []);
        }

        if (Path.Exists(args.Query))
        {
            var directItem = new IndexerListItem(new IndexerItem(fullPath: args.Query), IncludeBrowseCommand.AsDefault);
            SetStableCommandId(directItem);
            return new FallbackQueryResult(args.Query, args.QueryId, [directItem]);
        }

        var session = new IndexerFallbackSearchSession(args);
        try
        {
            return await session.QueryAsync(cancellationToken, progress).ConfigureAwait(false);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    internal void SuppressFallbackWhen(Func<string, bool> callback)
    {
        _suppressCallback = callback;
    }

    internal static void SetStableCommandId(IListItem item)
    {
        if (item is IndexerListItem indexerItem && item.Command is Command command)
        {
            command.Id = $"com.microsoft.cmdpal.builtin.indexer.open:{indexerItem.FilePath}";
        }
    }
}
