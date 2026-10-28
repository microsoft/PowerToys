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
/// The part of the trigger that reached the target (the hook swallows the final keystroke); erased first.
/// </param>
/// <param name="Replacement">The raw replacement text from the snapshot that matched.</param>
/// <param name="TargetWindow">The foreground window at the moment the trigger matched.</param>
/// <param name="IsUndo">True when this work item reverts the previous expansion.</param>
/// <param name="TypedText">
/// Everything the user typed for this match, restored by Ctrl+Z. Null means the trigger itself;
/// in word mode it also carries the terminator that released the trigger.
/// </param>
/// <param name="UndoGeneration">
/// The keyboard-input generation at match time. The worker may retain undo only while it is still
/// current; later user input invalidates a slow expansion before it can publish stale undo state.
/// </param>
internal readonly record struct PendingExpansion(
    string Trigger,
    string TypedPrefix,
    string Replacement,
    IntPtr TargetWindow,
    bool IsUndo = false,
    string? TypedText = null,
    long UndoGeneration = 0);
