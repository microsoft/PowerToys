// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManagerEditorUI.Interop
{
    /// <summary>
    /// The subset of the native <c>ShortcutErrorType</c> that the overlap checks can return.
    /// </summary>
    /// <remarks>
    /// Values must stay in sync with
    /// <c>src/modules/keyboardmanager/KeyboardManagerEditorLibrary/ShortcutErrorType.h</c>.
    /// </remarks>
    public enum ShortcutOverlap
    {
        None = 0,

        /// <summary>The two keys are the same key.</summary>
        SameKeyPreviouslyMapped = 1,

        /// <summary>
        /// One key is a combined modifier and the other a side-specific one of the same type
        /// (Ctrl vs Left Ctrl). A left/right pair of the same modifier is NOT a conflict.
        /// </summary>
        ConflictingModifierKey = 3,

        /// <summary>The two shortcuts are identical.</summary>
        SameShortcutPreviouslyMapped = 4,

        /// <summary>
        /// The shortcuts share an action key and matching modifier types, and at least one side
        /// uses a combined modifier, so one covers the other.
        /// </summary>
        ConflictingModifierShortcut = 6,
    }
}
