// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleResultFallback : FallbackResultSource
{
    private const int TotalItemCount = 6;

    internal SampleResultFallback()
        : base("Sample results", "com.microsoft.cmdpal.sample.fallback.results")
    {
        Title = "Sample results";
        SuggestedQueryDelayMilliseconds = 75;
        SuggestedMinQueryLength = 1;
        Icon = new IconInfo("\uE8B7");
    }

    protected override async Task<IFallbackCommandResult> QueryAsync(
        IFallbackQueryArgs args,
        CancellationToken cancellationToken,
        IProgress<IFallbackCommandResult> progress)
    {
        await Task.Delay(75, cancellationToken);
        progress.Report(CreateResult(args, 1));

        var initialCount = Math.Min((int)args.RequestedItemCount, 2);
        return CreateResult(args, initialCount);
    }

    private static FallbackQueryResult CreateResult(IFallbackQueryArgs args, int itemCount)
    {
        var items = new IListItem[itemCount];
        for (var index = 0; index < itemCount; index++)
        {
            var itemNumber = index + 1;
            items[index] = new ListItem(new ShowToastCommand($"{args.Query}: {itemNumber}")
            {
                Id = $"com.microsoft.cmdpal.sample.fallback.results.{itemNumber}",
                Name = "Show sample result",
            })
            {
                Title = $"{args.Query} sample result {itemNumber}",
                Subtitle = "A fallback source returned this result.",
            };
        }

        var hasMoreItems = itemCount < TotalItemCount;
        Func<uint, CancellationToken, IProgress<IFallbackCommandResult>, Task<IFallbackCommandResult>> loadMore = hasMoreItems
            ? (requestedItemCount, cancellationToken, progress) => LoadMoreAsync(args, itemCount, requestedItemCount, cancellationToken)
            : null;
        return new FallbackQueryResult(
            args.Query,
            args.QueryId,
            items,
            hasMoreItems,
            loadMore);
    }

    private static Task<IFallbackCommandResult> LoadMoreAsync(
        IFallbackQueryArgs args,
        int currentCount,
        uint requestedItemCount,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nextCount = Math.Min(TotalItemCount, currentCount + (int)requestedItemCount);
        return Task.FromResult<IFallbackCommandResult>(CreateResult(args, nextCount));
    }
}
