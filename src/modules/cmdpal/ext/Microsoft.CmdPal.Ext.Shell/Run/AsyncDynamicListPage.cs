// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Run;

/// <summary>
/// An abstract base class for dynamic list pages that support asynchronous
/// search updates. This follows the pattern used by the ShellListPage, the
/// winget page, and a bunch of other async pages in CmdPal.
///
/// This class takes care of all the cancellation and task management for you.
/// You just need to implement BuildListItemsForSearchAsync to populate the list
/// items based on the search text. When BuildListItemsForSearchAsync completes,
/// this class will raise a ItemsChanged event on your behalf, to call into
/// whatever your GetItems implementation is.
///
/// Use BuildListItemsForSearchAsync to create a list of list items based on the
/// search text. Then return that list in your GetItems implementation.
/// </summary>
public abstract partial class AsyncDynamicListPage : DynamicListPage, IDisposable
{
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _currentSearchTask;

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (newSearch == oldSearch)
        {
            return;
        }

        DoUpdateSearchText(newSearch);
    }

    protected void DoUpdateSearchText(string newSearch)
    {
        // Cancel any ongoing search
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();

        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        try
        {
            // Save the latest search task
            _currentSearchTask = BuildListItemsForSearchAsync(newSearch, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // DO NOTHING HERE
            return;
        }
        catch (Exception)
        {
            // Handle other exceptions
            return;
        }

        // Await the task to ensure only the latest one gets processed
        _ = ProcessSearchResultsAsync(_currentSearchTask, newSearch);
    }

    protected async Task ProcessSearchResultsAsync(Task searchTask, string newSearch)
    {
        try
        {
            await searchTask;

            // Ensure this is still the latest task
            if (_currentSearchTask == searchTask)
            {
                // The search results have already been updated in BuildListItemsForSearchAsync
                IsLoading = false;
                RaiseItemsChanged();
            }
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation gracefully
        }
        catch (Exception)
        {
            // Handle other exceptions
            IsLoading = false;
        }
    }

    protected abstract Task BuildListItemsForSearchAsync(string newSearch, CancellationToken cancellationToken);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}
