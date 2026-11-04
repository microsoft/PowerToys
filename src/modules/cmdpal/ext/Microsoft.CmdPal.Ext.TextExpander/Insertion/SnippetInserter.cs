// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>
/// Puts a snippet into the app Command Palette was opened over, carefully, as Text Expander does: it pastes with the
/// user's clipboard put back afterwards, marking both writes to stay out of Clipboard History, and types instead when
/// the clipboard can't be put back exactly or Text Expander is set to always type.
/// </summary>
internal sealed class SnippetInserter
{
    /// <summary>How long Command Palette may take to hide and hand the front back to the app it was over.</summary>
    internal const int TargetWaitMs = 1500;

    /// <summary>How long to wait for the user to let go of Shift, Ctrl, Alt or the Windows key, as Text Expander does.</summary>
    internal const int ModifierReleaseWaitMs = 1000;

    internal const int PollMs = 10;

    internal const int RestoreAttempts = 3;

    internal const int RestoreRetryDelayMs = 40;

    /// <summary>The pause between typed characters: Windows 11 Notepad drops characters that come faster.</summary>
    internal const int KeyDelayMs = 25;

    internal const int SentenceBoundaryDelayMs = 25;

    private readonly IClipboardAccess _clipboard;
    private readonly IDesktop _desktop;
    private readonly IInsertionClock _clock;

    public SnippetInserter()
        : this(new Win32Clipboard(), new Win32Desktop(), new SystemInsertionClock())
    {
    }

    internal SnippetInserter(IClipboardAccess clipboard, IDesktop desktop, IInsertionClock clock)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(clock);

        _clipboard = clipboard;
        _desktop = desktop;
        _clock = clock;
    }

    public IClipboardAccess Clipboard => _clipboard;

    /// <summary>
    /// Hides Command Palette, waits for the app it was opened over to come back to the front, and puts
    /// <paramref name="text"/> there.
    /// </summary>
    public InsertOutcome Insert(string text, bool alwaysType)
    {
        ArgumentNullException.ThrowIfNull(text);

        _desktop.HideCommandPalette();
        if (text.Length == 0)
        {
            return InsertOutcome.Empty;
        }

        var target = WaitForTarget();
        if (target == 0)
        {
            return InsertOutcome.NoTarget;
        }

        if (!_desktop.CanDrive(target))
        {
            return InsertOutcome.TargetOutOfReach;
        }

        WaitForModifierRelease();

        var clipboardLost = false;
        InsertOutcome? outcome = null;
        if (!alwaysType)
        {
            (outcome, clipboardLost) = TryPaste(text, target);
        }

        // Null when the clipboard couldn't be borrowed: the user's copy stays, and the snippet is typed.
        outcome ??= Type(text, target);
        return clipboardLost ? InsertOutcome.ClipboardNotRestored : outcome.Value;
    }

    /// <summary>
    /// Pastes through the clipboard. Returns a null outcome, so the snippet is typed, when the clipboard can't be
    /// put back exactly, can't be written, or doesn't hold the snippet after the write.
    /// </summary>
    private (InsertOutcome? Outcome, bool ClipboardLost) TryPaste(string text, nint target)
    {
        using var saved = _clipboard.Save();
        if (saved is null)
        {
            return (null, false);
        }

        var write = _clipboard.WriteText(text, keepOutOfHistory: true);
        if (write == ClipboardWrite.Refused)
        {
            return (null, false);
        }

        // Taken right after the write, and only while the clipboard is still ours: a copy someone made in between would
        // pass for ours, and putting the user's clipboard back would destroy it. That copy stays, and the snippet is typed.
        if (ClipboardLoan.TakeAfterWrite(() => _clipboard.SequenceNumber, () => _clipboard.IsOurs) is not { } loan)
        {
            return (null, false);
        }

        var handBack = true;
        var handedBack = ClipboardRestore.Untouched;
        InsertOutcome? outcome = null;
        try
        {
            // Checked, not assumed: a write can succeed and leave something else on the clipboard.
            if (write == ClipboardWrite.Written && _clipboard.HoldsText(text))
            {
                if (_desktop.ForegroundWindow != target)
                {
                    outcome = InsertOutcome.TargetChanged;
                }
                else
                {
                    var sentAt = _clock.ElapsedMilliseconds;
                    if (_desktop.Send(KeyStrokes.Paste(_desktop.HeldModifiers())))
                    {
                        // An app reads the clipboard after it gets the keys. One that's still busy at the cap has the
                        // Ctrl+V queued, so its clipboard stays on the snippet.
                        handBack = PasteSettle.WaitForRead(() => IsOpenBy(target), () => _desktop.IsTakingMessages(target), _clock, sentAt);
                        outcome = InsertOutcome.Inserted;
                    }
                    else
                    {
                        outcome = InsertOutcome.Failed;
                    }
                }
            }
        }
        finally
        {
            if (handBack)
            {
                handedBack = RestoreClipboard(saved, loan);
            }
        }

        // Someone else's newer copy winning isn't a loss, and nothing is lost when the clipboard was empty.
        var lost = handedBack is not (ClipboardRestore.Restored or ClipboardRestore.Superseded) && !saved.IsEmpty;
        return (outcome, lost);
    }

    /// <summary>
    /// Hands the user's clipboard back with Text Expander's own loop (<see cref="ClipboardHandBack.Run"/> in
    /// TextExpander.Core), trying again while another app holds it. If someone else copied after the paste, their copy
    /// wins and nothing is put back: that's a skip, not a loss.
    /// </summary>
    private ClipboardRestore RestoreClipboard(SavedClipboard saved, ClipboardLoan loan)
        => ClipboardHandBack.Run(stillOurs => _clipboard.Restore(saved, stillOurs), loan, () => _clipboard.SequenceNumber, _clock.Sleep, RestoreAttempts, RestoreRetryDelayMs);

    /// <summary>Types one character per batch, paced as Text Expander types, and stops if another window comes to the front.</summary>
    private InsertOutcome Type(string text, nint target)
    {
        foreach (var character in KeyStrokes.Typing(text))
        {
            if (_desktop.ForegroundWindow != target)
            {
                return InsertOutcome.TargetChanged;
            }

            var strokes = character.IsKey ? KeyStrokes.AfterReleasing(_desktop.HeldModifiers(), character.Strokes) : character.Strokes;
            if (!_desktop.Send(strokes))
            {
                return InsertOutcome.Failed;
            }

            _clock.Sleep(KeyDelayMs + (character.PausesLonger ? SentenceBoundaryDelayMs : 0));
        }

        return InsertOutcome.Inserted;
    }

    /// <summary>Waits for a window of another app to be in front: Command Palette hides on its own thread.</summary>
    private nint WaitForTarget()
    {
        var start = _clock.ElapsedMilliseconds;
        while (true)
        {
            var window = _desktop.ForegroundWindow;
            if (window != 0)
            {
                var processId = _desktop.ProcessIdOf(window);
                if (processId != 0 && processId != _desktop.CurrentProcessId)
                {
                    return window;
                }
            }

            if (_clock.ElapsedMilliseconds - start >= TargetWaitMs)
            {
                return 0;
            }

            _clock.Sleep(PollMs);
        }
    }

    /// <summary>Waits for the user to let go of the modifier keys. Any still held are let go of in the batch that's sent.</summary>
    private void WaitForModifierRelease()
    {
        var start = _clock.ElapsedMilliseconds;
        while (_desktop.HeldModifiers().Count > 0 && _clock.ElapsedMilliseconds - start < ModifierReleaseWaitMs)
        {
            _clock.Sleep(PollMs);
        }
    }

    private bool IsOpenBy(nint target)
    {
        var processId = _clipboard.OpenedByProcessId();
        return processId != 0 && processId == _desktop.ProcessIdOf(target);
    }
}
