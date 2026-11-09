// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// A Treiber stack for initialization demand. Its head is stored inline in the
/// owner, avoiding a separate stack object per item.
/// Unlike ConcurrentStack, this supports pruning inactive interior nodes while
/// a consumer is stalled, without removing and republishing live demands.
/// Inactive demands must never become active again.
/// </summary>
/// <remarks>
/// Owners must keep this mutable struct in a non-readonly field and call it directly.
/// Do not copy it or pass it by value: copies have independent heads but share
/// existing nodes. All concurrent operations must use the same field.
/// </remarks>
internal struct ListItemInitializationDemandStack
{
    private ListItemInitializationDemandNode? _head;

    /// <summary>
    /// Publishes a new head with a full fence, then prunes inactive interior nodes.
    /// </summary>
    internal void Push(ListItemInitializationDemand demand)
    {
        var node = new ListItemInitializationDemandNode(demand);
        ListItemInitializationDemandNode? head;

        // CAS _head
        // Refresh both head and node.Next on every retry. An arriving producer
        // or a concurrent detach/clear must not be overwritten with a stale chain.
        do
        {
            head = Volatile.Read(ref _head);
            node.Next = head;
        }
        while (Interlocked.CompareExchange(ref _head, node, head) != head);

        node.PruneInactive();
    }

    /// <summary>
    /// Captures the current head and prunes its inactive interior nodes for replay.
    /// Callers must recheck demand activity, including that of the captured head.
    /// </summary>
    internal ListItemInitializationDemandNode? CaptureAndPrune()
    {
        var head = Volatile.Read(ref _head);
        head?.PruneInactive();
        return head;
    }

    /// <summary>
    /// Atomically detaches the current chain. Producers may still be pruning its
    /// links, so callers may traverse them but must not reverse or reuse them.
    /// </summary>
    internal ListItemInitializationDemandNode? TakeAll()
    {
        return Interlocked.Exchange(ref _head, null);
    }

    /// <summary>
    /// Drops the current chain. This does not prevent later pushes; the owner must
    /// close publication races using its own completion or acceptance state.
    /// </summary>
    internal void Clear()
    {
        Interlocked.Exchange(ref _head, null);
    }
}
