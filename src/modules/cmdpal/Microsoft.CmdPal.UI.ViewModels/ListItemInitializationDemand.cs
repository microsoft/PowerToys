// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// One requester's interest in initializing an item. Its lifetime is independent
/// of the coordinator, and releasing it cannot cancel another requester's demand.
/// </summary>
internal sealed class ListItemInitializationDemand(ListItemViewModel item, CancellationToken cancellationToken)
{
    private int _released;

    internal ListItemViewModel Item { get; } = item;

    internal bool IsActive => Volatile.Read(ref _released) == 0 && !cancellationToken.IsCancellationRequested;

    // Selection cancellation is observed when the worker examines the request.
    // No CTS callback executes coordinator work on the thread changing selection.
    internal void Release() => Interlocked.Exchange(ref _released, 1);
}
