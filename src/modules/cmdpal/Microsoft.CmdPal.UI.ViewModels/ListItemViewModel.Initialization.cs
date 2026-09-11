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
    private int _initializationState;
    private TaskCompletionSource<bool>? _initializationCompletion;
    private ListItemInitializationCoordinator? _initializationCoordinator;
    private ListItemInitializationDemandStack _initializationDemands; // can't be readonly

    internal bool IsInitializationComplete => Volatile.Read(ref _initializationState) >= InitializationState.Succeeded;

    internal bool InitializationWasSuccessful => Volatile.Read(ref _initializationState) == InitializationState.Succeeded;

    internal void AttachInitializationCoordinator(ListItemInitializationCoordinator coordinator)
    {
        // Both publication paths need a full fence: either an arriving demand sees
        // the new coordinator, or this replay sees the demand. A release/acquire
        // store/load pair alone could let both sides miss one another.
        Interlocked.Exchange(ref _initializationCoordinator, coordinator);

        var head = _initializationDemands.CaptureAndPrune();
        if (head is null)
        {
            return;
        }

        // TryEnqueue re-checks the demand, so a released head costs only this call.
        coordinator.TryEnqueue(head.Demand);

        var previous = head;
        while (previous.Next is { } current)
        {
            coordinator.TryEnqueue(current.Demand);
            previous = current;
        }
    }

    internal bool IsAttachedTo(ListItemInitializationCoordinator coordinator)
    {
        return ReferenceEquals(Volatile.Read(ref _initializationCoordinator), coordinator);
    }

    public ListItemRealizationRegistration BeginRealization()
    {
        var demand = CreateInitializationDemand();
        if (demand is not null)
        {
            Volatile.Read(ref _initializationCoordinator)?.TryEnqueue(demand);
        }

        return new(demand);
    }

    // Must run on a background thread because initialization can block on extension calls.
    internal async Task<bool> RequestInitializationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InitializePropertiesOnce();
        cancellationToken.ThrowIfCancellationRequested();
        return await WaitForInitializationAsync(cancellationToken).ConfigureAwait(false);
    }

    internal void InitializePropertiesOnce()
    {
        // CAS _initializationState
        // Only the winner may run the extension's initialization.
        if (Interlocked.CompareExchange(ref _initializationState, InitializationState.InProgress, InitializationState.NotStarted) != InitializationState.NotStarted)
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
            Interlocked.Exchange(ref _initializationState, succeeded ? InitializationState.Succeeded : InitializationState.Failed);
            _initializationDemands.Clear();
            Volatile.Read(ref _initializationCompletion)?.TrySetResult(succeeded);
        }
    }

    internal Task<bool> WaitForInitializationAsync(CancellationToken cancellationToken)
    {
        var state = Volatile.Read(ref _initializationState);
        if (state >= InitializationState.Succeeded)
        {
            return Task.FromResult(state == InitializationState.Succeeded);
        }

        var completion = Volatile.Read(ref _initializationCompletion);
        if (completion is null)
        {
            var newCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // CAS _initializationCompletion
            // All waiters must use the same published completion source, even callers that lose this race.
            completion = Interlocked.CompareExchange(ref _initializationCompletion, newCompletion, null) ?? newCompletion;
        }

        // Initialization may have completed between the first state read and
        // publishing the completion source. Complete it here as the finding
        // thread so no waiter can be stranded by that race.
        state = Volatile.Read(ref _initializationState);
        if (state >= InitializationState.Succeeded)
        {
            completion.TrySetResult(state == InitializationState.Succeeded);
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
        _initializationDemands.Clear();

        // CAS _initializationState
        // Cleanup settles only an unclaimed initialization.
        // In-progress initializer retains responsibility for its waiters.
        if (Interlocked.CompareExchange(ref _initializationState, InitializationState.Failed, InitializationState.NotStarted) == InitializationState.NotStarted)
        {
            Volatile.Read(ref _initializationCompletion)?.TrySetResult(false);
        }
    }

    private ListItemInitializationDemand? CreateInitializationDemand()
    {
        if (IsInitializationComplete)
        {
            return null;
        }

        var demand = new ListItemInitializationDemand(this);

        // Recycling must not retain every past realization while the page is
        // suspended or an extension getter prevents initialization from finishing.
        _initializationDemands.Push(demand);

        // Demand is only retained for the pending initialization, not for the item's
        // entire lifetime. Close the race with completion clearing the list too.
        if (IsInitializationComplete)
        {
            _initializationDemands.Clear();
            demand.Release();
            return null;
        }

        return demand;
    }

    /// <summary>
    /// Integer states used to atomically coordinate an item's run-once initialization.
    /// </summary>
    /// <remarks>
    /// Completion checks compare the state with <see cref="Succeeded"/>.
    /// Both terminal states must remain greater than or equal to that value,
    /// and both pending states must remain below it.
    /// </remarks>
    private static class InitializationState
    {
        /// <summary>
        /// Initialization has not been claimed.
        /// </summary>
        public const int NotStarted = 0;

        /// <summary>
        /// One caller owns initialization and is responsible for settling its waiters.
        /// </summary>
        public const int InProgress = 1;

        /// <summary>
        /// The complete initialization call succeeded.
        /// </summary>
        public const int Succeeded = 2;

        /// <summary>
        /// Initialization failed, or cleanup prevented it from starting.
        /// </summary>
        public const int Failed = 3;
    }
}
