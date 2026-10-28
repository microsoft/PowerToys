// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// A trigger match handed from the keyboard hook to the expansion worker. The replacement and target
/// window are captured at match time: the snippet file can reload and focus can move before the worker runs.
/// </summary>
/// <param name="Trigger">The matched trigger.</param>
/// <param name="TypedPrefix">
/// The part of <paramref name="TypedText"/> that reached the target (the hook swallows the final keystroke); erased first.
/// </param>
/// <param name="Replacement">The raw replacement text from the snapshot that matched.</param>
/// <param name="TargetWindow">The foreground window at the moment the trigger matched.</param>
/// <param name="IsUndo">True when this work item reverts the previous expansion.</param>
/// <param name="TypedText">
/// Everything the user typed for this match, exactly as typed, restored by Ctrl+Z. With case-insensitive
/// matching its capitalization can differ from <paramref name="Trigger"/>; in word mode it also carries the
/// terminator that released the trigger. Null on an undo, whose trigger is the text to restore.
/// </param>
/// <param name="InputGeneration">
/// The keyboard and mouse input generation when this was queued. A later key press or click can move the
/// caret, so the worker erases, inserts and keeps undo only while it is still current.
/// </param>
/// <param name="CursorLeft">
/// On an undo, how far before the end of the expansion its $|$ left the caret; the caret goes back to the end
/// before the expansion is erased.
/// </param>
internal readonly record struct PendingExpansion(
    string Trigger,
    string TypedPrefix,
    string Replacement,
    IntPtr TargetWindow,
    bool IsUndo = false,
    string? TypedText = null,
    long InputGeneration = 0,
    int CursorLeft = 0);
