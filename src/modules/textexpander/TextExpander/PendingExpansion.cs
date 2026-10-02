// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// A trigger match handed from the keyboard hook to the expansion worker.
///
/// The replacement is captured at match time rather than looked up later, because snippets.txt
/// hot-reloads and the trigger may no longer exist by the time the worker runs — which would
/// silently eat the keystroke the hook already swallowed. The target window is captured for the
/// same reason: injection is deliberately slow, and focus can move before it starts.
/// </summary>
/// <param name="Trigger">The matched trigger.</param>
/// <param name="TypedPrefix">
/// The part of the trigger that actually reached the target application. The hook swallows the
/// final keystroke, so this is the trigger minus its last character, and it is what must be
/// erased before the replacement is typed.
/// </param>
/// <param name="Replacement">The raw replacement text from the snapshot that matched.</param>
/// <param name="TargetWindow">The foreground window at the moment the trigger matched.</param>
/// <param name="IsUndo">True when this work item reverts the previous expansion.</param>
/// <param name="TypedText">
/// Everything the user typed for this match, restored by Ctrl+Z. Null means the trigger itself;
/// in word mode it also carries the terminator that released the trigger.
/// </param>
internal readonly record struct PendingExpansion(string Trigger, string TypedPrefix, string Replacement, IntPtr TargetWindow, bool IsUndo = false, string? TypedText = null);
