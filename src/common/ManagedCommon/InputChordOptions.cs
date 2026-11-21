// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ManagedCommon
{
    /// <summary>
    /// Options for <see cref="InputInjection.SendChord(System.Collections.Generic.IReadOnlyList{ushort}, InputChordOptions)"/>.
    /// </summary>
    public sealed class InputChordOptions
    {
        /// <summary>
        /// Gets the dwExtraInfo set on every injected event, so hooks (e.g. the runner's centralized keyboard hook or
        /// Keyboard Manager) can recognize it.
        /// </summary>
        public UIntPtr ExtraInfo { get; init; }

        /// <summary>
        /// Gets how long to wait for the user to release the modifier keys before forcing key-ups. Zero doesn't wait.
        /// </summary>
        public TimeSpan ModifierReleaseTimeout { get; init; } = TimeSpan.Zero;

        public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(10);

        /// <summary>
        /// Gets a value indicating whether to inject key-ups for modifiers that are still held, so the target doesn't
        /// see e.g. Win+Shift+Ctrl+V.
        /// </summary>
        public bool ReleaseHeldModifiers { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether to send the Start menu suppression key before releasing modifiers.
        /// </summary>
        public bool SuppressStartMenu { get; init; } = true;
    }
}
