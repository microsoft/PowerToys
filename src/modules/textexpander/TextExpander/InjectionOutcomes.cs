// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>How the clipboard was opened, which decides what may be done with it.</summary>
internal enum ClipboardOpen
{
    /// <summary>Not opened at all.</summary>
    Failed,

    /// <summary>Opened against our own window, so it may be written to.</summary>
    Owned,

    /// <summary>
    /// Opened without an owner. Readable, but it must never be written: EmptyClipboard assigns
    /// ownership to the window that has the clipboard open, so on an unowned handle it leaves
    /// the clipboard ownerless and every SetClipboardData that follows fails. Emptying first and
    /// discovering that second is how a user's clipboard gets destroyed instead of borrowed.
    /// </summary>
    Unowned,
}

/// <summary>The outcome of writing to the clipboard, which decides whether a restore is owed.</summary>
internal enum ClipboardWrite
{
    /// <summary>Nothing was written and nothing was touched. The clipboard is exactly as it was.</summary>
    Refused,

    /// <summary>The clipboard was emptied but the write did not complete. A restore is owed.</summary>
    Failed,

    /// <summary>The clipboard now holds what was asked for.</summary>
    Written,
}

/// <summary>What became of an attempted paste, and what the caller still owes.</summary>
internal enum PasteOutcome
{
    /// <summary>The text is in the target. Nothing further to do.</summary>
    Pasted,

    /// <summary>Nothing was sent and the trigger is still on screen. Type it instead.</summary>
    RefusedBeforeErase,

    /// <summary>
    /// Focus left the window the trigger was typed into. Nothing was sent, and nothing more
    /// may be: the expansion belongs to a window that is no longer listening.
    /// </summary>
    TargetLost,

    /// <summary>The trigger was erased but the paste did not land. Type the text only.</summary>
    FailedAfterErase,

    /// <summary>
    /// No paste was attempted, because the replacement was short enough to type or the typing
    /// backend was selected. Never returned by the paste path; recorded by the diagnostics so
    /// an ordinary short expansion is not mistaken for a refusal.
    /// </summary>
    NotAttempted,
}
