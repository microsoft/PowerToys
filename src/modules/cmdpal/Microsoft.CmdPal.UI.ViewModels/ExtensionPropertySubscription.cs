// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Coordinates a single extension event subscription with concurrent or reentrant cleanup.
/// </summary>
/// <remarks>Store as a field; copying it would split responsibility for the subscription.</remarks>
internal struct ExtensionPropertySubscription
{
    private int _state;

    /// <summary>Gets a value indicating whether cleanup has closed the subscription.</summary>
    public bool IsClosed => Volatile.Read(ref _state) == State.Closed;

    /// <summary>Attaches once, leaving an in-progress event add responsible for detaching if cleanup wins.</summary>
    /// <remarks>A failed add may still install the handler, so cleanup must still attempt removal.</remarks>
    public bool TrySubscribe(INotifyPropChanged model, TypedEventHandler<object, IPropChangedEventArgs> handler)
    {
        if (Interlocked.CompareExchange(ref _state, State.Attaching, State.Unsubscribed) != State.Unsubscribed)
        {
            return false;
        }

        var addSucceeded = false;
        try
        {
            model.PropChanged += handler;
            addSucceeded = true;
        }
        finally
        {
            // Cleanup must not remove the handler until the extension's event add has returned.
            if (Interlocked.CompareExchange(ref _state, State.Attached, State.Attaching) == State.Closed)
            {
                try
                {
                    model.PropChanged -= handler;
                }
                catch (Exception ex) when (!addSucceeded)
                {
                    // Preserve the original event add failure.
                    CoreLogger.LogDebug(ex.ToString());
                }
            }
        }

        return !IsClosed;
    }

    /// <summary>Closes the subscription and returns whether the caller must attempt to detach the handler.</summary>
    public bool Close() => Interlocked.Exchange(ref _state, State.Closed) == State.Attached;

    /// <summary>States that transfer responsibility for removing the event subscription.</summary>
    private static class State
    {
        /// <summary>No initialization call has claimed the subscription.</summary>
        public const int Unsubscribed = 0;

        /// <summary>The initializer owns the in-progress event add.</summary>
        public const int Attaching = 1;

        /// <summary>The event add has returned; cleanup owes removal even if the add threw.</summary>
        public const int Attached = 2;

        /// <summary>An in-progress add must detach itself when it returns.</summary>
        public const int Closed = 3;
    }
}
