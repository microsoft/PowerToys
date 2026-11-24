// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ManagedCommon
{
    /// <summary>
    /// Outcome of <see cref="InputInjection.SendChord(System.Collections.Generic.IReadOnlyList{ushort}, InputChordOptions)"/>.
    /// </summary>
    public sealed class InputChordResult
    {
        public uint Requested { get; init; }

        public uint Sent { get; init; }

        /// <summary>
        /// Gets the number of key-ups needed to undo the key-downs of a partially sent batch.
        /// </summary>
        public uint CleanupRequested { get; init; }

        public uint CleanupSent { get; init; }

        /// <summary>
        /// Gets the Win32 error after SendInput sent fewer events than requested.
        /// </summary>
        public int Error { get; init; }

        /// <summary>
        /// Gets a value indicating whether modifiers were still held when the wait timed out; key-ups were injected for them.
        /// </summary>
        public bool ModifierWaitTimedOut { get; init; }

        public bool Succeeded => Sent == Requested;
    }
}
