// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Puts a replacement into the focused window, either by pasting it or by typing it.
///
/// Typed text is sent as KEYEVENTF_UNICODE so it is layout-independent — no need to map
/// characters back to virtual keys.
/// </summary>
internal static class Injector
{
    /// <summary>
    /// Gets or sets which injection path to use. See <see cref="InjectionPolicy"/>.
    ///
    /// <para>
    /// Volatile-backed: written from the settings-watcher thread when a host reloads its settings,
    /// read from the expansion worker.
    /// </para>
    /// </summary>
    public static InjectionBackend Backend
    {
        get => (InjectionBackend)_backend;
        set => _backend = (int)value;
    }

    private static volatile int _backend = (int)InjectionBackend.Auto;

    /// <summary>
    /// Gets or sets length at which a replacement is pasted instead of typed. See
    /// <see cref="InjectionPolicy.DefaultClipboardThresholdChars"/> for why it is this low.
    ///
    /// <para>Volatile-backed, for the same reason as <see cref="Backend"/>.</para>
    /// </summary>
    public static int ClipboardThresholdChars
    {
        get => _clipboardThresholdChars;
        set => _clipboardThresholdChars = value;
    }

    private static volatile int _clipboardThresholdChars =
        InjectionPolicy.DefaultClipboardThresholdChars;

    /// <summary>
    /// Gets or sets upper bound on how long to let the target consume the paste before the clipboard is
    /// restored. Reached only when the target's read is never observed.
    ///
    /// Measured too short at a flat 120ms: under load the restore landed before the target had
    /// read the clipboard, so the *previous* clipboard contents were pasted instead of the
    /// snippet. Rather than pay the safe 350ms every single time, the wait now watches for the
    /// target actually opening the clipboard (see <see cref="WaitForPasteToSettle"/>) and this
    /// value is the ceiling for when that never happens.
    /// </summary>
    public static int PasteSettleMs { get; set; } = 350;

    /// <summary>Gets or sets how often to check whether the target has picked the paste up.</summary>
    public static int PasteSettlePollMs { get; set; } = 5;

    /// <summary>
    /// Gets or sets shortest the settle may be cut to, however quickly the target's read is observed. Keeps a
    /// margin over the 120 ms that was measured too short as an unconditional sleep, so an early
    /// exit is never faster than a figure already known to be marginal.
    /// </summary>
    public static int PasteSettleFloorMs { get; set; } = 60;

    /// <summary>
    /// Gets or sets grace period after the target closes the clipboard again. It has the data by then; this
    /// only covers an application that reopens the clipboard immediately to read a second format.
    /// </summary>
    public static int PasteSettleGraceMs { get; set; } = 20;

    /// <summary>
    /// Gets or sets pause between pressing Ctrl and pressing V, and again before releasing Ctrl. Without it
    /// the target can act on V before it has registered the modifier, pasting a literal "v".
    /// </summary>
    public static int ModifierSettleMs { get; set; } = 30;

    /// <summary>
    /// Gets or sets delay between injected characters on the typing path, which now only handles replacements
    /// shorter than <see cref="ClipboardThresholdChars"/> plus anything the paste path refused.
    /// Generous on purpose: at four characters or fewer the total cost is still under a tenth of
    /// a second, and Windows 11 Notepad drops and smears characters when synthetic input arrives
    /// faster (".com" arriving as ".mmm"). Set POWERTOYS_TEXT_EXPANDER_INJECTION_BACKEND=clipboard to stop typing altogether.
    /// </summary>
    public static int KeyDelayMs { get; set; } = 25;

    /// <summary>
    /// Gets or sets additional pause after sentence-ending characters. Corruption is not uniformly
    /// distributed: it clusters immediately after '.', '!', '?' and newlines, where the editor
    /// does extra work (autocorrect, capitalisation, spell check). Paying a larger delay only at
    /// those few positions is far cheaper than raising the delay for every character.
    /// </summary>
    public static int SentenceBoundaryDelayMs { get; set; } = 25;

    /// <summary>
    /// Gets or sets grace period before backspacing. The hook sees a keystroke before the target application
    /// has finished processing the preceding ones, so erasing immediately can race ahead of the
    /// text actually appearing on screen.
    /// </summary>
    public static int PreBackspaceDelayMs { get; set; } = 15;

    /// <summary>Gets or sets how many times to try handing the user's clipboard back before giving up.</summary>
    public static int RestoreAttempts { get; set; } = 3;

    /// <summary>Gets or sets pause between those attempts, to let whoever holds the clipboard let go.</summary>
    public static int RestoreRetryDelayMs { get; set; } = 40;

    /// <summary>
    /// Gets or sets invoked when the user's clipboard could not be handed back. Losing someone's clipboard
    /// silently is not acceptable — by that point it has already been emptied, so the user is
    /// worse off than if the expansion had never run and deserves to be told why.
    /// </summary>
    public static Action<string>? ClipboardRestoreFailed { get; set; }

    /// <summary>
    /// Erases the text the user actually typed, then inserts <paramref name="text"/>.
    ///
    /// The backspace count is derived from text elements, not UTF-16 code units. A trigger
    /// containing an emoji or a combining sequence occupies several chars in a C# string but is
    /// deleted as a single unit by editors, so using string.Length would over-delete and eat the
    /// character before the trigger.
    /// </summary>
    /// <param name="targetWindow">
    /// The window the trigger was typed into, re-checked before anything is inserted. Pass
    /// <see cref="IntPtr.Zero"/> when it is not known.
    /// </param>
    public static bool ReplaceTyped(string typedPrefix, string text, IntPtr targetWindow = default, int cursorLeftUnits = 0)
        => Replace(CountDeletableUnits(typedPrefix), text, targetWindow, cursorLeftUnits);

    internal static int CountDeletableUnits(string value)
    {
        if (value.Length == 0)
        {
            return 0;
        }

        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(value);
        int count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    /// <summary>
    /// Erases <paramref name="backspaceCount"/> characters, then inserts <paramref name="text"/>.
    /// </summary>
    public static bool Replace(int backspaceCount, string text, IntPtr targetWindow = default, int cursorLeftUnits = 0)
    {
        bool erased = false;

        // A paste is one event, so there is no per-character race to lose. Typing stays as the
        // fallback for everything the paste path cannot do: a clipboard we could not borrow, a
        // clipboard we could not safely put back, or a target we are not allowed to drive.
        if (text.Length > 0 && InjectionPolicy.ShouldUseClipboard(text, ClipboardThresholdChars, Backend))
        {
            PasteOutcome outcome = TryPaste(backspaceCount, text, targetWindow, cursorLeftUnits);
            InjectionDiagnostics.RecordOutcome(outcome);

            switch (outcome)
            {
                case PasteOutcome.Pasted:
                    return true;

                // Focus moved while the clipboard was being borrowed. Typing instead would
                // erase and insert into whatever window the user switched to, destroying text
                // they did type — so the expansion is abandoned rather than misdelivered.
                case PasteOutcome.TargetLost:
                    return false;

                // The paste got as far as erasing the trigger before failing, so the typing
                // fallback must not erase a second time and eat the preceding characters.
                case PasteOutcome.FailedAfterErase:
                    erased = true;
                    break;
            }
        }
        else
        {
            InjectionDiagnostics.RecordOutcome(PasteOutcome.NotAttempted);
        }

        if (!erased && !Erase(backspaceCount, targetWindow))
        {
            // Focus moved during the pre-backspace grace period. Typing the replacement now
            // would put it in the wrong window, so the expansion is abandoned instead.
            InjectionDiagnostics.RecordTypingAbandoned();
            return false;
        }

        if (text.Length > 0)
        {
            SendText(text, targetWindow);
        }

        SendLeft(cursorLeftUnits);
        return true;
    }

    private static void SendLeft(int count)
    {
        if (count <= 0)
        {
            return;
        }

        var inputs = new Native.INPUT[count * 2];
        for (int i = 0; i < count; i++)
        {
            inputs[i * 2] = KeyDown(0x25);
            inputs[(i * 2) + 1] = KeyUp(0x25);
        }

        Dispatch(inputs);
    }

    /// <summary>
    /// Erases the trigger. Returns false when focus moved while waiting to do so and nothing was
    /// sent, which the caller must treat as a reason to abandon the whole expansion: the erase
    /// and the insertion have to land in the same window or they destroy text between them.
    /// </summary>
    private static bool Erase(int backspaceCount, IntPtr targetWindow)
    {
        if (backspaceCount <= 0)
        {
            return true;
        }

        if (PreBackspaceDelayMs > 0)
        {
            Thread.Sleep(PreBackspaceDelayMs);
        }

        // Re-checked after the delay, not before it. Backspaces are the destructive half of an
        // expansion, and sending them into whatever window is focused *now* deletes text the user
        // typed there rather than the trigger they typed here.
        if (HasTargetMoved(targetWindow))
        {
            return false;
        }

        SendBackspaces(backspaceCount);
        return true;
    }

    /// <summary>True when input is no longer going to the window the trigger was typed into.</summary>
    private static bool HasTargetMoved(IntPtr targetWindow)
        => targetWindow != IntPtr.Zero && Native.GetForegroundWindow() != targetWindow;

    /// <summary>Where a target window stands with respect to receiving synthetic input.</summary>
    private enum TargetState
    {
        /// <summary>Focused and drivable.</summary>
        Ready,

        /// <summary>Focus has moved elsewhere.</summary>
        Moved,

        /// <summary>Still focused, but at an integrity level our input cannot reach.</summary>
        Unreachable,
    }

    /// <summary>
    /// Pastes via the clipboard, restoring whatever the user had there.
    ///
    /// Ordered so that nothing destructive happens until the paste is known to be possible: the
    /// target is checked, the clipboard is borrowed and the takeover verified, and only then is
    /// the trigger erased. A refusal at any point up to that leaves the user's screen and
    /// clipboard exactly as they were.
    /// </summary>
    private static PasteOutcome TryPaste(int backspaceCount, string text, IntPtr targetWindow, int cursorLeftUnits)
    {
        PasteOutcome? refusal = CheckTarget(targetWindow);
        if (refusal is not null)
        {
            return refusal.Value;
        }

        // Taken before anything is overwritten. A null snapshot means the clipboard could not be
        // read, holds more than we are willing to copy, or holds something we could not put back.
        ClipboardSnapshot? saved = Clipboard.Snapshot();
        if (saved is null)
        {
            return PasteOutcome.RefusedBeforeErase;
        }

        ClipboardWrite write = Clipboard.SetText(text);
        if (write == ClipboardWrite.Refused)
        {
            return PasteOutcome.RefusedBeforeErase; // clipboard untouched; nothing to put back
        }

        // Taken straight after our own write, so anything that moves it from here on was somebody
        // else. Restoring over a newer owner's data destroys whatever the user copied during the
        // settle — which is a window of hundreds of milliseconds in which they are still working.
        var loan = new ClipboardLoan(Clipboard.SequenceNumber);

        // Verified rather than assumed: SetClipboardData can report success and still leave the
        // clipboard holding something else, and a Ctrl+V sent on that assumption pastes whatever
        // is actually there — the user's previous clipboard — into their document.
        if (write == ClipboardWrite.Failed || !Clipboard.HoldsText(text))
        {
            RestoreClipboard(saved, loan);
            return PasteOutcome.RefusedBeforeErase;
        }

        try
        {
            // Borrowing the clipboard is slow — retried opens, and formats whose owners render
            // them synchronously — so focus is re-checked here rather than trusted from before
            // it. Erasing into whatever window is focused *now* would destroy text the user
            // never typed, and a single Ctrl+V lands wherever focus is.
            refusal = CheckTarget(targetWindow);
            if (refusal is not null)
            {
                return refusal.Value;
            }

            // A skipped erase means focus moved in the grace period. Sending Ctrl+V anyway would
            // paste the expansion into whatever the user switched to, so the paste is abandoned
            // with the trigger left intact. The clipboard is still handed back by the finally.
            if (!Erase(backspaceCount, targetWindow))
            {
                return PasteOutcome.TargetLost;
            }

            if (!SendPasteChord())
            {
                return PasteOutcome.FailedAfterErase;
            }

            // Queued straight behind Ctrl+V rather than after the settle wait below: the target
            // handles its input in order, so the caret still moves after the paste lands, but
            // the user no longer watches it sit at the end for up to PasteSettleMs first.
            SendLeft(cursorLeftUnits);

            // The target reads the clipboard asynchronously, so restoring immediately would
            // race the paste and insert the previous contents instead.
            WaitForPasteToSettle(targetWindow);
            return PasteOutcome.Pasted;
        }
        finally
        {
            RestoreClipboard(saved, loan);
        }
    }

    /// <summary>
    /// Waits for the target to take the paste, rather than sleeping long enough to cover the
    /// slowest imaginable case.
    ///
    /// <para>
    /// Reading a paste means calling <c>OpenClipboard</c>, so watching for the target's own
    /// process holding the clipboard open — and then letting go of it — is direct evidence the
    /// paste has been consumed. When that is observed the wait ends in a few tens of
    /// milliseconds instead of a flat 350 ms.
    /// </para>
    ///
    /// <para>
    /// Every uncertainty resolves back to the old behaviour rather than to a shorter wait. An
    /// open that cannot be attributed to the target is ignored; a read too brief to fall in a poll
    /// is never seen; a target that reads lazily is never seen either. All three simply pay
    /// <see cref="PasteSettleMs"/>, so the worst case is exactly what it was before and only the
    /// common case gets faster.
    /// </para>
    /// </summary>
    private static void WaitForPasteToSettle(IntPtr targetWindow)
    {
        int budget = PasteSettleMs;
        if (budget <= 0)
        {
            return;
        }

        int poll = Math.Max(1, PasteSettlePollMs);
        int floor = Math.Min(PasteSettleFloorMs, budget);
        bool observedRead = false;

        for (int waited = 0; waited < budget; waited += poll)
        {
            if (Clipboard.IsOpenByTarget(targetWindow))
            {
                observedRead = true;
            }
            else if (observedRead && waited >= floor)
            {
                // Opened and closed again: the target has the data.
                if (PasteSettleGraceMs > 0)
                {
                    Thread.Sleep(PasteSettleGraceMs);
                }

                return;
            }

            Thread.Sleep(poll);
        }
    }

    /// <summary>
    /// Checks whether <paramref name="targetWindow"/> is still where input is going and is a
    /// window we are actually permitted to drive. Returns null when the paste may proceed, or
    /// the outcome the caller should report.
    /// </summary>
    private static PasteOutcome? CheckTarget(IntPtr targetWindow)
    {
        switch (ReadTargetState(targetWindow))
        {
            case TargetState.Moved:
                return PasteOutcome.TargetLost;

            // Typing will not reach an elevated window either, but it is the documented
            // fallback and it costs nothing; what matters is that the clipboard is left alone.
            case TargetState.Unreachable:
                return PasteOutcome.RefusedBeforeErase;

            default:
                return null;
        }
    }

    private static TargetState ReadTargetState(IntPtr targetWindow)
    {
        IntPtr foreground = Native.GetForegroundWindow();
        if (targetWindow != IntPtr.Zero && foreground != targetWindow)
        {
            return TargetState.Moved;
        }

        return InputTarget.CanDrive(foreground) ? TargetState.Ready : TargetState.Unreachable;
    }

    /// <summary>
    /// Hands the user's clipboard back, retrying because the usual reason a restore fails is
    /// that another process held the clipboard for a moment. Reports a failure rather than
    /// swallowing it: the clipboard has already been emptied by this point.
    ///
    /// <para>
    /// Refuses outright when somebody else has written to the clipboard since our own write. What
    /// they put there is newer than the snapshot, and republishing the snapshot over it would
    /// silently destroy something the user copied moments ago — the expansion's own text going
    /// unrestored is the lesser harm.
    /// </para>
    /// </summary>
    private static void RestoreClipboard(ClipboardSnapshot saved, ClipboardLoan loan)
    {
        for (int attempt = 0; attempt < RestoreAttempts; attempt++)
        {
            if (!loan.MayRestore(Clipboard.SequenceNumber))
            {
                InjectionDiagnostics.RecordClipboardRestoreSkipped();
                return;
            }

            if (saved.Restore())
            {
                return;
            }

            // A failed restore can still have emptied the clipboard before failing to republish,
            // which moves the sequence number. Re-arming against our own change keeps the retry
            // from mistaking that for somebody else taking ownership.
            loan.Rearm(Clipboard.SequenceNumber);

            if (attempt + 1 < RestoreAttempts && RestoreRetryDelayMs > 0)
            {
                Thread.Sleep(RestoreRetryDelayMs);
            }
        }

        // Nothing was lost if there was nothing there: the clipboard was empty and now holds the
        // replacement, which is untidy but not the user's data going missing.
        if (saved.IsEmpty)
        {
            return;
        }

        InjectionDiagnostics.RecordClipboardRestoreFailure();

        ClipboardRestoreFailed?.Invoke("Your clipboard could not be restored after an expansion, so its previous contents " + "have been lost.\n\nSet POWERTOYS_TEXT_EXPANDER_INJECTION_BACKEND=type to stop expansions from using " + "the clipboard at all.");
    }

    /// <summary>
    /// Sends Ctrl+V, staggered rather than as one batch: a single SendInput carrying the whole
    /// chord occasionally had the target process V before it had registered Ctrl going down,
    /// which inserts a literal "v" instead of pasting.
    ///
    /// Returns false when SendInput inserted nothing, which means another thread has called
    /// BlockInput. It deliberately says nothing about UIPI — SendInput cannot report that, which
    /// is why <see cref="InputTarget"/> is asked before we get here.
    /// </summary>
    private static bool SendPasteChord()
    {
        ReleaseHeldModifiers();

        if (Dispatch([KeyDown(Native.VK_CONTROL)]) == 0)
        {
            return false;
        }

        try
        {
            if (ModifierSettleMs > 0)
            {
                Thread.Sleep(ModifierSettleMs);
            }

            if (Dispatch([KeyDown(Native.VK_V), KeyUp(Native.VK_V)]) == 0)
            {
                return false;
            }

            if (ModifierSettleMs > 0)
            {
                Thread.Sleep(ModifierSettleMs);
            }

            return true;
        }
        finally
        {
            // Never leave Ctrl down, whatever happened above: a stuck modifier turns the user's
            // next keystroke into a shortcut.
            Dispatch([KeyUp(Native.VK_CONTROL)]);
        }
    }

    /// <summary>
    /// Releases modifiers the user is still physically holding. A trigger can end on a key that
    /// needed Shift or AltGr, and a leftover modifier turns our Ctrl+V into Ctrl+Shift+V or
    /// Ctrl+Alt+V — "paste special" in several editors, and nothing at all in others.
    ///
    /// Released but never re-pressed: the user's own key-up still arrives, whereas synthesising
    /// a key-down for a key they have since let go of would leave that modifier stuck on.
    /// </summary>
    private static void ReleaseHeldModifiers()
    {
        ReleaseIfHeld(Native.VK_LSHIFT, extended: false);
        ReleaseIfHeld(Native.VK_RSHIFT, extended: false);
        ReleaseIfHeld(Native.VK_LMENU, extended: false);
        ReleaseIfHeld(Native.VK_RMENU, extended: true);
        ReleaseIfHeld(Native.VK_LWIN, extended: true);
        ReleaseIfHeld(Native.VK_RWIN, extended: true);
    }

    private static void ReleaseIfHeld(int vk, bool extended)
    {
        const int PressedBit = 0x8000;
        if ((Native.GetAsyncKeyState(vk) & PressedBit) == 0)
        {
            return;
        }

        var up = KeyUp(vk);
        if (extended)
        {
            up.U.Ki.DwFlags |= Native.KEYEVENTF_EXTENDEDKEY;
        }

        Dispatch([up]);
    }

    private static void SendBackspaces(int count)
    {
        // Backspaces batch safely; only character insertion showed the corruption.
        var inputs = new Native.INPUT[count * 2];
        for (int i = 0; i < count; i++)
        {
            inputs[i * 2] = KeyDown(Native.VK_BACK);
            inputs[(i * 2) + 1] = KeyUp(Native.VK_BACK);
        }

        Dispatch(inputs);
    }

    private static void SendText(string text, IntPtr targetWindow)
    {
        for (int i = 0; i < text.Length; i++)
        {
            // Typing a long replacement one character at a time takes as long as the replacement
            // is — seconds, for the fallbacks that reach here. Focus is re-checked as we go rather
            // than once at the start, because a user who switches window mid-expansion would
            // otherwise have the remainder typed into whatever they switched to.
            if ((i & 7) == 0 && HasTargetMoved(targetWindow))
            {
                InjectionDiagnostics.RecordTypingAbandoned();
                return;
            }

            char ch = text[i];

            // Real key presses: many editors ignore a literal \n delivered as a Unicode event.
            if (ch == '\n')
            {
                Dispatch([KeyDown(Native.VK_RETURN), KeyUp(Native.VK_RETURN)]);
            }
            else if (ch == '\t')
            {
                Dispatch([KeyDown(Native.VK_TAB), KeyUp(Native.VK_TAB)]);
            }
            else if (ch == '\r')
            {
                continue;
            }
            else if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                // A surrogate pair must reach the target as a single uninterrupted unit.
                char low = text[i + 1];
                i++;
                Dispatch([UnicodeDown(ch), UnicodeUp(ch), UnicodeDown(low), UnicodeUp(low)]);
            }
            else
            {
                Dispatch([UnicodeDown(ch), UnicodeUp(ch)]);
            }

            if (KeyDelayMs > 0 || IsSentenceBoundary(ch))
            {
                int delay = KeyDelayMs + (IsSentenceBoundary(ch) ? SentenceBoundaryDelayMs : 0);
                if (delay > 0)
                {
                    Thread.Sleep(delay);
                }
            }
        }
    }

    private static bool IsSentenceBoundary(char ch) => ch is '.' or '!' or '?' or '\n';

    /// <summary>
    /// Hands input to the system. Returns the number of events actually inserted, which is zero
    /// when another thread has called BlockInput. A non-zero count is not proof of delivery:
    /// UIPI discards events further down without reporting anything.
    /// </summary>
    private static uint Dispatch(Native.INPUT[] inputs)
    {
        if (inputs.Length == 0)
        {
            return 0;
        }

        return Native.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Native.INPUT>());
    }

    private static Native.INPUT KeyDown(int vk) => new()
    {
        Type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion
        {
            Ki = new Native.KEYBDINPUT
            {
                WVk = (ushort)vk,
                DwFlags = 0,
                DwExtraInfo = KeyboardHook.InjectionSignature,
            },
        },
    };

    private static Native.INPUT KeyUp(int vk) => new()
    {
        Type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion
        {
            Ki = new Native.KEYBDINPUT
            {
                WVk = (ushort)vk,
                DwFlags = Native.KEYEVENTF_KEYUP,
                DwExtraInfo = KeyboardHook.InjectionSignature,
            },
        },
    };

    private static Native.INPUT UnicodeDown(char ch) => new()
    {
        Type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion
        {
            Ki = new Native.KEYBDINPUT
            {
                WVk = 0,
                WScan = ch,
                DwFlags = Native.KEYEVENTF_UNICODE,
                DwExtraInfo = KeyboardHook.InjectionSignature,
            },
        },
    };

    private static Native.INPUT UnicodeUp(char ch) => new()
    {
        Type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion
        {
            Ki = new Native.KEYBDINPUT
            {
                WVk = 0,
                WScan = ch,
                DwFlags = Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP,
                DwExtraInfo = KeyboardHook.InjectionSignature,
            },
        },
    };
}
