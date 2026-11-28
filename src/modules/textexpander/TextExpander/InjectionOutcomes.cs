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
    /// Opened without an owner: readable, but never written, because EmptyClipboard on an unowned handle
    /// leaves the clipboard ownerless and every following SetClipboardData fails.
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
    /// Focus left the window the trigger was typed into, the window can't be driven (an elevated window), or a key
    /// press or click may have moved the caret; nothing was or may be sent.
    /// </summary>
    TargetLost,

    /// <summary>
    /// The trigger was erased but the input sequence did not complete. Stop: retrying could
    /// compound a partial paste or a stuck key.
    /// </summary>
    FailedAfterErase,

    /// <summary>
    /// No paste was attempted (short text or the typing backend). Recorded only by the diagnostics.
    /// </summary>
    NotAttempted,
}
