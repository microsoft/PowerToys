// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Contains one complete fallback result snapshot.
/// </summary>
public sealed partial class FallbackQueryResult : IFallbackCommandResult, IDisposable
{
    private Continuation? _continuation;

    public FallbackQueryResult(
        string query,
        string queryId,
        IListItem[]? items,
        bool hasMoreItems = false,
        Func<uint, CancellationToken, IProgress<IFallbackCommandResult>, Task<IFallbackCommandResult>>? loadMore = null,
        Action? close = null)
    {
        Query = query;
        QueryId = queryId;
        Items = items ?? [];
        HasMoreItems = hasMoreItems && loadMore is not null;
        _continuation = HasMoreItems ? new(loadMore!, close) : null;
    }

    public string Query { get; }

    public string QueryId { get; }

    public IListItem[] Items { get; }

    public bool HasMoreItems { get; }

    public IAsyncOperationWithProgress<IFallbackCommandResult, IFallbackCommandResult> LoadMoreItemsAsync(uint requestedItemCount)
    {
        return AsyncInfo.Run<IFallbackCommandResult, IFallbackCommandResult>(async (cancellationToken, progress) =>
        {
            var continuation = Interlocked.Exchange(ref _continuation, null);
            if (continuation is null)
            {
                return new FallbackQueryResult(Query, QueryId, Items);
            }

            try
            {
                var result = await continuation.LoadMore(requestedItemCount, cancellationToken, progress).ConfigureAwait(false);
                if (result is null)
                {
                    throw new InvalidOperationException("The load-more callback returned null.");
                }

                return result;
            }
            catch
            {
                continuation.Close?.Invoke();
                throw;
            }
        });
    }

    public void Close()
    {
        Interlocked.Exchange(ref _continuation, null)?.Close?.Invoke();
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }

    private sealed record Continuation(
        Func<uint, CancellationToken, IProgress<IFallbackCommandResult>, Task<IFallbackCommandResult>> LoadMore,
        Action? Close);
}
