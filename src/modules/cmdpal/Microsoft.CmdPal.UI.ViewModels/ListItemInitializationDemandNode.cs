// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// A link owned by one item or coordinator. Demand can belong to multiple lists
/// during replacement. Registrations must not retain those lists after completion.
/// </summary>
internal sealed class ListItemInitializationDemandNode(ListItemInitializationDemand demand)
{
    private ListItemInitializationDemandNode? _next;

    internal ListItemInitializationDemand Demand { get; } = demand;

    // Set only before publishing a new head. Published links may only skip inactive
    // nodes; a consumer must not reverse them while another thread is pruning.
    internal ListItemInitializationDemandNode? Next
    {
        get => Volatile.Read(ref _next);
        set => _next = value;
    }

    /// <summary>
    /// Prunes inactive demands after this node while preserving live demands and
    /// the captured head. <strong>Arrivals and coordinator replay may prune concurrently.</strong>
    /// </summary>
    internal void PruneInactive()
    {
        // LOAD BEARING:
        // - Arrivals and coordinator replay may prune concurrently.
        // - Inactive demands never become active again.
        // - Compare/exchange each link so one pass cannot overwrite another pass's change to it.
        // - Never republish the captured head: its owner may have cleared or detached
        // the list while this pass was running.
        var previous = this;
        while (previous.Next is { } current)
        {
            if (current.Demand.IsActive)
            {
                previous = current;
                continue;
            }

            // CAS _next
            // Unlink only if previous still points to current.
            // If not, read that link again before deciding which node to remove.
            Interlocked.CompareExchange(ref previous._next, current.Next, current);
        }
    }
}
