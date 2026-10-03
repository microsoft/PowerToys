// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Peek.UI.Models
{
    /// <summary>
    /// Executes the first navigation request immediately and coalesces rapid repeats to
    /// the latest direction, paced by a timer rather than by image-loading completion.
    /// </summary>
    /// <remarks>
    /// Timer ticks and requests must run on the same thread. The timer abstraction allows
    /// deterministic tests without creating a WinUI dispatcher timer.
    /// </remarks>
    internal sealed class NavigationPacer
    {
        private readonly INavigationPacerTimer _timer;

        private readonly Action<NavigationDirection> _navigate;

        private NavigationDirection? _pendingDirection;

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationPacer"/> class
        /// with exclusive control of the supplied timer.
        /// </summary>
        /// <param name="timer">The timer defining the interval between navigation steps.
        /// </param>
        /// <param name="navigate">The callback that performs a navigation step on the
        /// calling thread.</param>
        public NavigationPacer(INavigationPacerTimer timer, Action<NavigationDirection> navigate)
        {
            _timer = timer;
            _navigate = navigate;
            _timer.Tick += OnTimerTick;
        }

        /// <summary>
        /// Gets a value indicating whether navigation requests are currently ignored.
        /// </summary>
        public bool IsSuspended { get; private set; }

        /// <summary>
        /// Navigates immediately when idle, or replaces the pending direction during a
        /// pacing interval. Requests received while suspended are discarded rather than
        /// replayed later.
        /// </summary>
        /// <param name="direction">The requested direction through the item collection.
        /// </param>
        public void Request(NavigationDirection direction)
        {
            if (IsSuspended)
            {
                return;
            }

            if (!_timer.IsEnabled)
            {
                // Start before invoking navigation so reentrant requests are coalesced,
                // and suspension during the callback cannot leave a running timer behind.
                _timer.Start();
                _navigate(direction);
            }
            else
            {
                _pendingDirection = direction;
            }
        }

        /// <summary>
        /// Discards queued navigation and stops pacing without changing suspension state.
        /// </summary>
        public void CancelPending()
        {
            _pendingDirection = null;
            _timer.Stop();
        }

        /// <summary>
        /// Cancels pending navigation and blocks new requests so the current item remains
        /// stable during operations such as delete confirmation.
        /// </summary>
        public void Suspend()
        {
            IsSuspended = true;
            CancelPending();
        }

        /// <summary>
        /// Accepts fresh requests again without restarting the timer or replaying
        /// discarded steps.
        /// </summary>
        public void Resume() => IsSuspended = false;

        private void OnTimerTick()
        {
            if (IsSuspended || !_timer.IsEnabled)
            {
                return;
            }

            if (_pendingDirection is NavigationDirection direction)
            {
                _pendingDirection = null;
                _navigate(direction);
            }
            else
            {
                _timer.Stop();
            }
        }
    }
}
