// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Contains one complete fallback result snapshot.
///
/// When you're implementing a fallback command that returns multiple items, you
/// need to use the FallbackResultsItem class and implement QueryAsync. That
/// method is called by the host to retrieve results for a given query. The
/// extension should return one of these FallbackQueryResults to encapsulate all
/// the results into one object.
///
/// This also allows an extension to provide additional results to the host,
/// should the host want to load more. You can indicate that by setting
/// <c>hasMoreItems</c> to true and providing a <c>loadMore</c> callback.
/// </summary>
public sealed partial class FallbackQueryResult : IFallbackCommandResult, IDisposable
{
    private Continuation? _continuation;

    /// <summary>
    /// Initializes a new instance of the <see cref="FallbackQueryResult"/> class.
    /// Creates a single <b>set</b> of results for a query. If this query might
    /// have more results that the host can retrieve, your extension should set
    /// <paramref name="hasMoreItems"/> to true, and use the <paramref
    /// name="loadMore"/> callback to provide additional results.
    /// </summary>
    /// <param name="query">The query that produced this result.</param>
    /// <param name="queryId">The identifier of the query that produced this
    /// result.</param>
    /// <param name="items">The items in this result.</param>
    /// <param name="hasMoreItems">Indicates to the host whether more items are
    /// available. If this is false, then the loadMore callback will be
    /// ignored.</param>
    /// <param name="loadMore">The callback used to load more items. This is
    /// only invoked if <paramref name="hasMoreItems"/> is true.</param>
    /// <param name="close">The callback invoked when the result is closed. This
    /// is only invoked if <paramref name="hasMoreItems"/> is true and <paramref
    /// name="loadMore"/> is not null.</param>
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
