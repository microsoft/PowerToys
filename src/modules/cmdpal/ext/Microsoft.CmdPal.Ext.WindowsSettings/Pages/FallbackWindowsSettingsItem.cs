// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowsSettings.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Pages;

internal sealed partial class FallbackWindowsSettingsItem : FallbackResultsItem
{
    private const string _id = "com.microsoft.cmdpal.builtin.windows.settings.fallback";

    private readonly Classes.WindowsSettings _windowsSettings;

    public FallbackWindowsSettingsItem(Classes.WindowsSettings windowsSettings)
        : base(Resources.settings_title, _id)
    {
        Title = Resources.settings_title;
        Icon = Icons.WindowsSettingsIcon;
        _windowsSettings = windowsSettings;
    }

    protected override Task<IFallbackCommandResult> QueryAsync(
        FallbackQueryArgs args,
        CancellationToken cancellationToken,
        IProgress<IFallbackCommandResult> progress)
    {
        return Task.Run<IFallbackCommandResult>(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (args.RequestedItemCount == 0 || string.IsNullOrWhiteSpace(args.Query))
                {
                    return new FallbackQueryResult(args.Query, args.QueryId, []);
                }

                var items = new WindowsSettingsListPage(_windowsSettings).Query(args.Query).ToArray<IListItem>();
                cancellationToken.ThrowIfCancellationRequested();
                var count = (int)Math.Min(args.RequestedItemCount, (uint)items.Length);
                return CreateResult(args.Query, args.QueryId, items, count);
            },
            cancellationToken);
    }

    private static FallbackQueryResult CreateResult(string query, string queryId, IListItem[] items, int count)
    {
        return new FallbackQueryResult(
            query,
            queryId,
            items.Take(count).ToArray(),
            count < items.Length,
            (requestedItemCount, cancellationToken, progress) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var additionalCount = (int)Math.Min(requestedItemCount, (uint)(items.Length - count));
                return Task.FromResult<IFallbackCommandResult>(CreateResult(query, queryId, items, count + additionalCount));
            });
    }
}
