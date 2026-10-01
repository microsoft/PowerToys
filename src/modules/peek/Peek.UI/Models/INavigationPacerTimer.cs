// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;

namespace Peek.UI.Models
{
    /// <summary>
    /// Supplies the timer operations needed by navigation pacing without coupling its
    /// logic to WinUI, allowing tests to deliver ticks manually.
    /// </summary>
    /// <remarks>Ticks must be delivered on the same thread that controls the pacer.
    /// </remarks>
    internal interface INavigationPacerTimer
    {
        /// <summary>Raised when a pacing interval elapses.</summary>
        event Action? Tick;

        /// <summary>Gets a value indicating whether the timer is running.</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Starts periodic ticks using the timer's configured interval.
        /// </summary>
        void Start();

        /// <summary>
        /// Stops future intervals. Consumers must tolerate a tick that was already queued
        /// before stopping.
        /// </summary>
        void Stop();
    }
}
