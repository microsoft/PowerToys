// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Deferral = global::Windows.Foundation.Deferral;

namespace Microsoft.PowerToys.Common.UI.Controls.Window;

/// <summary>
/// Data for <see cref="TransparentWindow.Hiding"/>. Supports deferrals so an
/// animated surface can keep the window visible until its out-animation has
/// finished. If no handler takes a deferral, the window hides immediately.
/// </summary>
public sealed class HidingEventArgs : EventArgs
{
    private int _outstanding;
    private bool _raised;
    private Action? _continuation;

    /// <summary>
    /// Requests that the window stay visible until the returned deferral is
    /// completed. Call <see cref="Deferral.Complete"/> once the out-animation
    /// has finished.
    /// </summary>
    /// <returns>A deferral that must be completed to allow the window to hide.</returns>
    public Deferral GetDeferral()
    {
        Interlocked.Increment(ref _outstanding);
        return new Deferral(OnDeferralCompleted);
    }

    /// <summary>
    /// Called by the window after raising the event to register what should run
    /// once every outstanding deferral has completed (or immediately if none
    /// were taken).
    /// </summary>
    internal void RunWhenComplete(Action continuation)
    {
        _continuation = continuation;
        _raised = true;
        TryComplete();
    }

    private void OnDeferralCompleted()
    {
        Interlocked.Decrement(ref _outstanding);
        TryComplete();
    }

    private void TryComplete()
    {
        if (_raised && Volatile.Read(ref _outstanding) == 0)
        {
            var continuation = _continuation;
            _continuation = null;
            continuation?.Invoke();
        }
    }
}
