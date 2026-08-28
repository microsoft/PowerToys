// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// The run-once initialization latch and the demand a row carries while it waits
/// to be initialized. Kept apart from the view model's property marshalling so the
/// memory-ordering requirements can be read in one place.
/// </summary>
public partial class ListItemViewModel
{
    private const int InitializationNotStarted = 0;
    private const int InitializationInProgress = 1;
    private const int InitializationSucceeded = 2;
    private const int InitializationFailed = 3;

    private int _initializationState;
    private TaskCompletionSource<bool>? _initializationCompletion;
    private ListItemInitializationCoordinator? _initializationCoordinator;
    private ListItemInitializationDemandNode? _initializationDemands;

    internal bool IsInitializationComplete => Volatile.Read(ref _initializationState) >= InitializationSucceeded;

    internal bool InitializationWasSuccessful => Volatile.Read(ref _initializationState) == InitializationSucceeded;

    internal void AttachInitializationCoordinator(ListItemInitializationCoordinator coordinator)
    {
        // Both publication paths need a full fence: either an arriving demand sees
        // the new coordinator, or this replay sees the demand. A release/acquire
        // store/load pair alone could let both sides miss one another.
        Interlocked.Exchange(ref _initializationCoordinator, coordinator);

        var head = Volatile.Read(ref _initializationDemands);
        if (head is null)
        {
            return;
        }

        // TryEnqueue re-checks the demand, so a released head costs only this call.
        coordinator.TryEnqueue(head.Demand);

        // Prune inactive interior nodes during this already-required replay. Keep the
        // captured head: producers CAS-push new heads while background fetches serialize
        // attachment, so only this thread rewrites existing Next links. This retains live
        // demand plus at most one inactive node from this pass; newer nodes are pruned on
        // a later replay. Never write the head back: initialization or cleanup may have
        // cleared it while this pass was running.
        var previous = head;
        while (previous.Next is { } current)
        {
            if (current.Demand.IsActive)
            {
                coordinator.TryEnqueue(current.Demand);
                previous = current;
                continue;
            }

            previous.Next = current.Next;
        }
    }

    internal bool IsAttachedTo(ListItemInitializationCoordinator coordinator)
    {
        return ReferenceEquals(Volatile.Read(ref _initializationCoordinator), coordinator);
    }

    public ListItemRealizationRegistration BeginRealization()
    {
        var demand = CreateInitializationDemand(CancellationToken.None);
        if (demand is not null)
        {
            Volatile.Read(ref _initializationCoordinator)?.TryEnqueue(demand);
        }

        return new(demand);
    }

    // Called from the existing background selection task, never from the UI event
    // handler. Waiting follows item completion rather than a captured coordinator.
    internal async Task<bool> RequestInitializationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var demand = CreateInitializationDemand(cancellationToken);
        if (demand is null)
        {
            return InitializationWasSuccessful;
        }

        try
        {
            var initialization = WaitForInitializationAsync(cancellationToken);
            while (!initialization.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var coordinator = Volatile.Read(ref _initializationCoordinator);
                if (coordinator is null)
                {
                    InitializePropertiesOnce();
                    break;
                }

                if (!coordinator.TryEnqueue(demand))
                {
                    if (!IsAttachedTo(coordinator))
                    {
                        continue;
                    }

                    if (coordinator.Completion.IsCompleted)
                    {
                        // No running initializer owns this item. Keep the existing
                        // background selection fallback, but await a concurrent claim
                        // rather than reading an in-progress state as failure.
                        InitializePropertiesOnce();
                        break;
                    }
                }

                // Reached both after a successful enqueue and after a refusal that
                // leaves this coordinator still owning the item. Stop/replacement is
                // not initialization failure. A new coordinator replays this same
                // demand; if none replaces it, retry after the old worker has
                // returned before using the fallback above.
                await Task.WhenAny(initialization, coordinator.Completion).ConfigureAwait(false);
            }

            return await initialization.ConfigureAwait(false);
        }
        finally
        {
            demand.Release();
        }
    }

    internal void InitializePropertiesOnce()
    {
        if (Interlocked.CompareExchange(ref _initializationState, InitializationInProgress, InitializationNotStarted) != InitializationNotStarted)
        {
            return;
        }

        var succeeded = false;
        try
        {
            succeeded = SafeInitializeProperties();
        }
        finally
        {
            // The base phase flag is set before the derived tags/section work ends.
            // This separate latch covers the whole call. Its full fence pairs with
            // the waiter's completion-source publication so neither can miss the other.
            Interlocked.Exchange(ref _initializationState, succeeded ? InitializationSucceeded : InitializationFailed);
            Interlocked.Exchange(ref _initializationDemands, null);
            Volatile.Read(ref _initializationCompletion)?.TrySetResult(succeeded);
        }
    }

    internal Task<bool> WaitForInitializationAsync(CancellationToken cancellationToken)
    {
        var state = Volatile.Read(ref _initializationState);
        if (state >= InitializationSucceeded)
        {
            return Task.FromResult(state == InitializationSucceeded);
        }

        var completion = Volatile.Read(ref _initializationCompletion);
        if (completion is null)
        {
            var newCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            completion = Interlocked.CompareExchange(ref _initializationCompletion, newCompletion, null) ?? newCompletion;
        }

        // Initialization may have completed between the first state read and
        // publishing the completion source. Complete it here as the finding
        // thread so no waiter can be stranded by that race.
        state = Volatile.Read(ref _initializationState);
        if (state >= InitializationSucceeded)
        {
            completion.TrySetResult(state == InitializationSucceeded);
        }

        return cancellationToken.CanBeCanceled
            ? completion.Task.WaitAsync(cancellationToken)
            : completion.Task;
    }

    // Called from UnsafeCleanup. Detaches the row from any coordinator and settles
    // waiters, so removing a still-pending item cannot strand a selection.
    private void CleanupInitializationState()
    {
        Interlocked.Exchange(ref _initializationCoordinator, null);
        Interlocked.Exchange(ref _initializationDemands, null);
        if (Interlocked.CompareExchange(ref _initializationState, InitializationFailed, InitializationNotStarted) == InitializationNotStarted)
        {
            Volatile.Read(ref _initializationCompletion)?.TrySetResult(false);
        }
    }

    private ListItemInitializationDemand? CreateInitializationDemand(CancellationToken cancellationToken)
    {
        if (IsInitializationComplete)
        {
            return null;
        }

        var demand = new ListItemInitializationDemand(this, cancellationToken);
        var node = new ListItemInitializationDemandNode(demand);
        ListItemInitializationDemandNode? head;
        do
        {
            head = Volatile.Read(ref _initializationDemands);
            node.Next = head;
        }
        while (Interlocked.CompareExchange(ref _initializationDemands, node, head) != head);

        // Demand is only retained for the pending initialization, not for the item's
        // entire lifetime. Close the race with completion clearing the list too.
        if (IsInitializationComplete)
        {
            Interlocked.Exchange(ref _initializationDemands, null);
            demand.Release();
            return null;
        }

        return demand;
    }
}
