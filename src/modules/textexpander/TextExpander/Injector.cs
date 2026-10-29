// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Puts a replacement into the focused window by pasting it or by typing it as layout-independent
/// KEYEVENTF_UNICODE input.
/// </summary>
internal static class Injector
{
    /// <summary>
    /// Gets or sets which injection path to use (<see cref="InjectionPolicy"/>). Volatile: written by the
    /// settings watcher, read by the expansion worker.
    /// </summary>
    public static InjectionBackend Backend
    {
        get => (InjectionBackend)_backend;
        set => _backend = (int)value;
    }

    private static volatile int _backend = (int)InjectionBackend.Auto;

    /// <summary>
    /// Gets or sets the length at which a replacement is pasted instead of typed. Volatile, like <see cref="Backend"/>.
    /// </summary>
    public static int ClipboardThresholdChars
    {
        get => _clipboardThresholdChars;
        set => _clipboardThresholdChars = value;
    }

    private static volatile int _clipboardThresholdChars =
        InjectionPolicy.DefaultClipboardThresholdChars;

    /// <summary>
    /// Gets or sets the upper bound on how long to let the target consume the paste before the clipboard is
    /// restored. Reached only when the target's read is never observed (see <see cref="WaitForPasteToSettle(IntPtr)"/>).
    /// </summary>
    public static int PasteSettleMs { get; set; } = 350;

    /// <summary>Gets or sets how often to check whether the target has picked the paste up.</summary>
    public static int PasteSettlePollMs { get; set; } = 5;

    /// <summary>
    /// Gets or sets the shortest the settle may be cut to, however quickly the target's read is observed. It keeps a
    /// margin over the flat 120 ms that proved too short, so an early exit is never faster than a figure already known
    /// to be marginal.
    /// </summary>
    public static int PasteSettleFloorMs { get; set; } = 150;

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
    /// Gets or sets the delay between injected characters on the typing path. Generous on purpose:
    /// Windows 11 Notepad drops and smears characters when synthetic input arrives faster.
    /// </summary>
    public static int KeyDelayMs { get; set; } = 25;

    /// <summary>
    /// Gets or sets the additional pause after sentence-ending characters, where editors do extra work
    /// (autocorrect, capitalization) and dropped characters cluster.
    /// </summary>
    public static int SentenceBoundaryDelayMs { get; set; } = 25;

    /// <summary>
    /// Gets or sets the grace period before backspacing, so the erase doesn't race ahead of the target
    /// still processing the trigger's last keystrokes.
    /// </summary>
    public static int PreBackspaceDelayMs { get; set; } = 15;

    /// <summary>
    /// How long an undo waits for the user to let go of Ctrl (and any other modifier) before it is
    /// abandoned. Backspaces sent while Ctrl is held arrive as Ctrl+Backspace, which deletes whole
    /// words in most editors.
    /// </summary>
    internal const int UndoModifierReleaseTimeoutMs = 1000;

    private const int ModifierReleasePollMs = 10;

    /// <summary>Gets or sets how many times to try handing the user's clipboard back before giving up.</summary>
    public static int RestoreAttempts { get; set; } = 3;

    /// <summary>Gets or sets pause between those attempts, to let whoever holds the clipboard let go.</summary>
    public static int RestoreRetryDelayMs { get; set; } = 40;

    /// <summary>
    /// Gets or sets the callback invoked when the user's clipboard could not be handed back, so the loss is never silent.
    /// </summary>
    public static Action<string>? ClipboardRestoreFailed { get; set; }

    /// <summary>
    /// Counts the Backspaces that erase <paramref name="value"/>: text elements, not UTF-16 units, so an emoji or
    /// combining sequence isn't over-deleted.
    /// </summary>
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
    /// <param name="targetWindow">
    /// The window the trigger was typed into, re-checked before anything is inserted, or
    /// <see cref="IntPtr.Zero"/> when unknown.
    /// </param>
    /// <param name="cursorRightUnits">
    /// How far to move the caret right before erasing, when an earlier insertion left it inside the text to erase.
    /// </param>
    /// <param name="inputUnchanged">
    /// Whether the user has pressed no key and clicked nowhere since the trigger matched, asked again right
    /// before anything is erased; null when there is nothing to check.
    /// </param>
    /// <param name="commit">
    /// Asked instead of <paramref name="inputUnchanged"/> right before anything is erased, the last check: once it
    /// passes, the work goes ahead.
    /// </param>
    public static bool Replace(int backspaceCount, string text, IntPtr targetWindow = default, int cursorLeftUnits = 0, int cursorRightUnits = 0, Func<bool>? inputUnchanged = null, Func<bool>? commit = null)
        => ReplaceCore(backspaceCount, text, targetWindow, cursorLeftUnits, InputTarget.CanDrive, cursorRightUnits: cursorRightUnits, inputUnchanged: inputUnchanged, commit: commit);

    internal static bool ReplaceCore(
        int backspaceCount,
        string text,
        IntPtr targetWindow,
        int cursorLeftUnits,
        Func<IntPtr, bool> canDrive,
        Func<IntPtr>? getForegroundWindow = null,
        Func<int, string, IntPtr, int, PasteOutcome>? tryPaste = null,
        Func<int, IntPtr, bool>? erase = null,
        Func<string, IntPtr, bool>? sendText = null,
        Func<int, IntPtr, bool>? sendLeft = null,
        int cursorRightUnits = 0,
        Func<int, IntPtr, bool>? sendRight = null,
        Func<bool>? inputUnchanged = null,
        Func<bool>? commit = null)
    {
        ArgumentNullException.ThrowIfNull(canDrive);

        Func<bool> unchanged = inputUnchanged ?? (static () => true);
        Func<bool> lastCheck = commit ?? unchanged;
        getForegroundWindow ??= Native.GetForegroundWindow;
        tryPaste ??= (count, value, target, left) => TryPaste(count, value, target, left, lastCheck);
        erase ??= (count, target) => Erase(count, target, lastCheck);
        sendText ??= static (value, target) => SendText(value, target);
        sendLeft ??= SendLeft;
        sendRight ??= SendRight;

        // The window must still be in front and drivable, and nothing since the match may have moved the caret.
        if (!unchanged() || !IsAuthorizedTarget(targetWindow, canDrive, getForegroundWindow))
        {
            InjectionDiagnostics.RecordTypingAbandoned();
            return false;
        }

        // Erasing counts back from the end of the text, so a caret left inside it goes back to the end first.
        if (!sendRight(cursorRightUnits, targetWindow))
        {
            return false;
        }

        // Typing stays the fallback whenever the paste path refuses.
        if (text.Length > 0 && InjectionPolicy.ShouldUseClipboard(text, ClipboardThresholdChars, Backend))
        {
            PasteOutcome outcome = tryPaste(backspaceCount, text, targetWindow, cursorLeftUnits);
            InjectionDiagnostics.RecordOutcome(outcome);

            switch (outcome)
            {
                case PasteOutcome.Pasted:
                    return true;

                // Focus or the caret moved while the clipboard was borrowed; typing now would land in the wrong place, so abandon.
                case PasteOutcome.TargetLost:
                    return false;

                // The trigger is gone and SendInput did not complete: typing could compound a partial paste,
                // and reporting success would keep an undo entry for text that may not be there.
                case PasteOutcome.FailedAfterErase:
                    return false;
            }
        }
        else
        {
            InjectionDiagnostics.RecordOutcome(PasteOutcome.NotAttempted);
        }

        // Re-authorize before falling back so a paste refusal never becomes an unguarded injection route.
        if (!IsAuthorizedTarget(targetWindow, canDrive, getForegroundWindow))
        {
            return false;
        }

        if (!erase(backspaceCount, targetWindow))
        {
            // Focus moved, or the user pressed a key or clicked, during the pre-backspace grace period;
            // abandon rather than type in the wrong place.
            InjectionDiagnostics.RecordTypingAbandoned();
            return false;
        }

        if (text.Length > 0)
        {
            if (!sendText(text, targetWindow))
            {
                return false;
            }
        }

        bool completed = sendLeft(cursorLeftUnits, targetWindow);
        if (completed)
        {
            InjectionDiagnostics.RecordTypingSucceeded();
        }

        return completed;
    }

    private static bool SendLeft(int count, IntPtr targetWindow) => SendArrow(Native.VK_LEFT, count, targetWindow);

    private static bool SendRight(int count, IntPtr targetWindow) => SendArrow(Native.VK_RIGHT, count, targetWindow);

    private static bool SendArrow(int vk, int count, IntPtr targetWindow)
    {
        if (count <= 0)
        {
            return true;
        }

        if (!IsAuthorizedTarget(targetWindow, InputTarget.CanDrive))
        {
            return false;
        }

        var inputs = new Native.INPUT[count * 2];
        for (int i = 0; i < count; i++)
        {
            inputs[i * 2] = KeyDown(vk);
            inputs[(i * 2) + 1] = KeyUp(vk);
        }

        return DispatchExactly(Dispatch, inputs);
    }

    /// <summary>
    /// Erases the trigger. Returns false, having sent nothing, when focus moved or the user pressed a key or
    /// clicked while waiting; the caller must then abandon the expansion because erase and insertion have to
    /// land where the trigger was typed.
    /// </summary>
    private static bool Erase(int backspaceCount, IntPtr targetWindow, Func<bool> inputUnchanged)
    {
        if (backspaceCount > 0 && PreBackspaceDelayMs > 0)
        {
            Thread.Sleep(PreBackspaceDelayMs);
        }

        // Re-checked after the delay, right before the first destructive key, even with nothing to erase:
        // the insertion would land wherever the caret moved to.
        if (!inputUnchanged())
        {
            return false;
        }

        if (backspaceCount <= 0)
        {
            return true;
        }

        // Backspaces sent to a newly focused window would delete the user's text.
        if (!IsAuthorizedTarget(targetWindow, InputTarget.CanDrive))
        {
            return false;
        }

        return SendBackspaces(backspaceCount, targetWindow);
    }

    private static bool IsAuthorizedTarget(IntPtr targetWindow, Func<IntPtr, bool> canDrive, Func<IntPtr>? getForegroundWindow = null)
    {
        IntPtr foreground = (getForegroundWindow ?? Native.GetForegroundWindow)();
        return foreground != IntPtr.Zero
            && (targetWindow == IntPtr.Zero || foreground == targetWindow)
            && canDrive(foreground);
    }

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
    /// Pastes via the clipboard, restoring whatever the user had there. Nothing destructive happens until
    /// the target is checked and the clipboard takeover verified; only then is the trigger erased.
    /// </summary>
    private static PasteOutcome TryPaste(int backspaceCount, string text, IntPtr targetWindow, int cursorLeftUnits, Func<bool> inputUnchanged)
    {
        PasteOutcome? refusal = CheckTarget(targetWindow);
        if (refusal is not null)
        {
            return refusal.Value;
        }

        // Null when the clipboard can't be read, is too large to copy, or holds something we couldn't put back.
        using ClipboardSnapshot? saved = Clipboard.Snapshot();
        if (saved is null)
        {
            return PasteOutcome.RefusedBeforeErase;
        }

        ClipboardWrite write = Clipboard.SetText(text);
        if (write == ClipboardWrite.Refused)
        {
            return PasteOutcome.RefusedBeforeErase; // clipboard untouched; nothing to put back
        }

        // Taken right after our own write: any later change is someone else's, and restoring over it
        // would destroy what the user copied during the settle.
        var loan = new ClipboardLoan(Clipboard.SequenceNumber);

        // Verified, not assumed: SetClipboardData can succeed and still leave other data on the clipboard.
        if (write == ClipboardWrite.Failed || !Clipboard.HoldsText(text))
        {
            RestoreClipboard(saved, loan);
            return PasteOutcome.RefusedBeforeErase;
        }

        try
        {
            // Borrowing the clipboard can be slow, so focus is re-checked before anything is erased.
            refusal = CheckTarget(targetWindow);
            if (refusal is not null)
            {
                return refusal.Value;
            }

            // Focus moved, or the user pressed a key or clicked, in the grace period: abandon with the trigger
            // intact (the finally restores the clipboard).
            if (!Erase(backspaceCount, targetWindow, inputUnchanged))
            {
                return PasteOutcome.TargetLost;
            }

            if (!SendPasteChord(targetWindow, out bool pasteMayHaveBeenSent))
            {
                if (pasteMayHaveBeenSent)
                {
                    WaitForPasteToSettle(targetWindow);
                }

                return PasteOutcome.FailedAfterErase;
            }

            // Queued behind Ctrl+V (input is processed in order) instead of after the settle wait.
            bool cursorMoved = SendLeft(cursorLeftUnits, targetWindow);

            // The target reads the clipboard asynchronously; restoring now would paste the previous contents.
            WaitForPasteToSettle(targetWindow);
            return cursorMoved ? PasteOutcome.Pasted : PasteOutcome.FailedAfterErase;
        }
        finally
        {
            RestoreClipboard(saved, loan);
        }
    }

    /// <summary>
    /// Waits for the target to take the paste: its process opening and then closing the clipboard is direct
    /// evidence the paste was consumed. Anything not attributable to the target falls back to waiting
    /// <see cref="PasteSettleMs"/>.
    /// </summary>
    private static void WaitForPasteToSettle(IntPtr targetWindow)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        WaitForPasteToSettle(() => Clipboard.IsOpenByTarget(targetWindow), Thread.Sleep, () => (long)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }

    /// <summary>
    /// The wait behind <see cref="WaitForPasteToSettle(IntPtr)"/>, with the clock and the clipboard check passed in
    /// for tests. Timed by the clock, not by counting polls: a short sleep lasts a whole timer tick (about 15 ms),
    /// so counted 5 ms polls held the clipboard for over a second.
    /// </summary>
    internal static void WaitForPasteToSettle(Func<bool> isOpenByTarget, Action<int> sleep, Func<long> elapsedMs)
    {
        int budget = PasteSettleMs;
        if (budget <= 0)
        {
            return;
        }

        int poll = Math.Max(1, PasteSettlePollMs);
        int floor = Math.Min(PasteSettleFloorMs, budget);
        bool observedRead = false;

        for (long waited = elapsedMs(); waited < budget; waited = elapsedMs())
        {
            if (isOpenByTarget())
            {
                observedRead = true;
            }
            else if (observedRead && waited >= floor)
            {
                // Opened and closed again: the target has the data.
                if (PasteSettleGraceMs > 0)
                {
                    sleep(PasteSettleGraceMs);
                }

                return;
            }

            sleep(poll);
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

            // Typing won't reach it either, but it's the documented fallback; the clipboard is left alone.
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
    /// Hands the user's clipboard back, retrying while another process holds it, and reports a failure.
    /// Refuses when someone else wrote to the clipboard after our own write: their newer data wins.
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

            // A failed restore may have emptied the clipboard, moving the sequence number; re-arm so the
            // retry doesn't mistake our own change for a new owner.
            loan.Rearm(Clipboard.SequenceNumber);

            if (attempt + 1 < RestoreAttempts && RestoreRetryDelayMs > 0)
            {
                Thread.Sleep(RestoreRetryDelayMs);
            }
        }

        // Nothing was lost if the clipboard was empty.
        if (saved.IsEmpty)
        {
            return;
        }

        InjectionDiagnostics.RecordClipboardRestoreFailure();

        ClipboardRestoreFailed?.Invoke(EngineText.Get(
            "ClipboardRestoreFailed",
            "Your clipboard couldn't be restored after a snippet expanded, so what you had copied is gone.\n\nTo keep snippets from using the clipboard, open PowerToys Settings > Text Expander > Advanced, and set Insertion method to Always type."));
    }

    /// <summary>
    /// Sends Ctrl+V staggered rather than as one batch, so the target registers Ctrl before V. Returns false
    /// unless every key event was accepted; <paramref name="pasteMayHaveBeenSent"/> says whether a partial
    /// V pair may already have pasted. SendInput can't report UIPI, so <see cref="InputTarget"/> is asked first.
    /// </summary>
    internal static bool SendPasteChord(
        IntPtr targetWindow,
        out bool pasteMayHaveBeenSent,
        Func<Native.INPUT[], uint>? dispatch = null,
        Func<IntPtr, bool>? canDrive = null,
        Func<IntPtr>? getForegroundWindow = null,
        Func<int, short>? getAsyncKeyState = null,
        Action<int>? sleep = null)
    {
        pasteMayHaveBeenSent = false;
        dispatch ??= Dispatch;
        canDrive ??= InputTarget.CanDrive;
        getForegroundWindow ??= Native.GetForegroundWindow;
        getAsyncKeyState ??= Native.GetAsyncKeyState;
        sleep ??= Thread.Sleep;

        if (!ReleaseHeldModifiers(dispatch, getAsyncKeyState)
            || !IsAuthorizedTarget(targetWindow, canDrive, getForegroundWindow))
        {
            return false;
        }

        if (!DispatchExactly(dispatch, [KeyDown(Native.VK_CONTROL)]))
        {
            return false;
        }

        bool pasteSent = false;
        bool controlReleased = false;
        try
        {
            if (ModifierSettleMs > 0)
            {
                sleep(ModifierSettleMs);
            }

            if (IsAuthorizedTarget(targetWindow, canDrive, getForegroundWindow))
            {
                Native.INPUT[] paste = [KeyDown(Native.VK_V), KeyUp(Native.VK_V)];
                pasteSent = DispatchExactly(dispatch, paste, out uint inserted);
                pasteMayHaveBeenSent = inserted > 0;

                if (pasteSent && ModifierSettleMs > 0)
                {
                    sleep(ModifierSettleMs);
                }
            }
        }
        finally
        {
            // Never leave Ctrl down, whatever happened above: a stuck modifier turns the user's
            // next keystroke into a shortcut.
            controlReleased = DispatchExactly(dispatch, [KeyUp(Native.VK_CONTROL)]);
        }

        return pasteSent && controlReleased;
    }

    /// <summary>
    /// Waits until no modifier is physically held. Returns false if one is still held after
    /// <paramref name="timeoutMs"/>, in which case the caller must not send its input.
    /// </summary>
    internal static bool WaitForModifierRelease(int timeoutMs, Func<int, bool>? isKeyDown = null)
    {
        isKeyDown ??= static vk => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
        long deadline = Environment.TickCount64 + timeoutMs;

        while (isKeyDown(Native.VK_CONTROL)
            || isKeyDown(Native.VK_MENU)
            || isKeyDown(Native.VK_SHIFT)
            || isKeyDown(Native.VK_LWIN)
            || isKeyDown(Native.VK_RWIN))
        {
            if (Environment.TickCount64 >= deadline)
            {
                return false;
            }

            Thread.Sleep(ModifierReleasePollMs);
        }

        return true;
    }

    /// <summary>
    /// Releases modifiers the user is still holding (a trigger ending in Shift or AltGr would turn Ctrl+V
    /// into "paste special"). Never re-pressed: the user's own key-up still arrives.
    /// </summary>
    private static bool ReleaseHeldModifiers(
        Func<Native.INPUT[], uint> dispatch,
        Func<int, short> getAsyncKeyState)
    {
        bool released = true;
        released &= ReleaseIfHeld(Native.VK_LSHIFT, extended: false, dispatch, getAsyncKeyState);
        released &= ReleaseIfHeld(Native.VK_RSHIFT, extended: false, dispatch, getAsyncKeyState);
        released &= ReleaseIfHeld(Native.VK_LMENU, extended: false, dispatch, getAsyncKeyState);
        released &= ReleaseIfHeld(Native.VK_RMENU, extended: true, dispatch, getAsyncKeyState);
        released &= ReleaseIfHeld(Native.VK_LWIN, extended: true, dispatch, getAsyncKeyState);
        released &= ReleaseIfHeld(Native.VK_RWIN, extended: true, dispatch, getAsyncKeyState);
        return released;
    }

    private static bool ReleaseIfHeld(
        int vk,
        bool extended,
        Func<Native.INPUT[], uint> dispatch,
        Func<int, short> getAsyncKeyState)
    {
        const int PressedBit = 0x8000;
        if ((getAsyncKeyState(vk) & PressedBit) == 0)
        {
            return true;
        }

        var up = KeyUp(vk);
        if (extended)
        {
            up.U.Ki.DwFlags |= Native.KEYEVENTF_EXTENDEDKEY;
        }

        return DispatchExactly(dispatch, [up]);
    }

    private static bool SendBackspaces(int count, IntPtr targetWindow)
    {
        // Backspaces batch safely; only character insertion showed the corruption.
        var inputs = new Native.INPUT[count * 2];
        for (int i = 0; i < count; i++)
        {
            inputs[i * 2] = KeyDown(Native.VK_BACK);
            inputs[(i * 2) + 1] = KeyUp(Native.VK_BACK);
        }

        if (!IsAuthorizedTarget(targetWindow, InputTarget.CanDrive))
        {
            return false;
        }

        return DispatchExactly(Dispatch, inputs);
    }

    internal static bool SendText(
        string text,
        IntPtr targetWindow,
        Func<Native.INPUT[], uint>? dispatch = null,
        Func<IntPtr, bool>? canDrive = null,
        Func<IntPtr>? getForegroundWindow = null,
        Action<int>? sleep = null)
    {
        dispatch ??= Dispatch;
        canDrive ??= InputTarget.CanDrive;
        getForegroundWindow ??= Native.GetForegroundWindow;
        sleep ??= Thread.Sleep;

        for (int i = 0; i < text.Length; i++)
        {
            // Typing a long fallback takes seconds, so focus is re-checked as we go.
            if ((i & 7) == 0 && !IsAuthorizedTarget(targetWindow, canDrive, getForegroundWindow))
            {
                InjectionDiagnostics.RecordTypingAbandoned();
                return false;
            }

            char ch = text[i];

            // Real key presses: many editors ignore a literal \n delivered as a Unicode event.
            if (ch == '\n')
            {
                if (!DispatchExactly(dispatch, [KeyDown(Native.VK_RETURN), KeyUp(Native.VK_RETURN)]))
                {
                    return false;
                }
            }
            else if (ch == '\t')
            {
                if (!DispatchExactly(dispatch, [KeyDown(Native.VK_TAB), KeyUp(Native.VK_TAB)]))
                {
                    return false;
                }
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
                if (!DispatchExactly(dispatch, [UnicodeDown(ch), UnicodeUp(ch), UnicodeDown(low), UnicodeUp(low)]))
                {
                    return false;
                }
            }
            else
            {
                if (!DispatchExactly(dispatch, [UnicodeDown(ch), UnicodeUp(ch)]))
                {
                    return false;
                }
            }

            if (KeyDelayMs > 0 || IsSentenceBoundary(ch))
            {
                int delay = KeyDelayMs + (IsSentenceBoundary(ch) ? SentenceBoundaryDelayMs : 0);
                if (delay > 0)
                {
                    sleep(delay);
                }
            }
        }

        return true;
    }

    private static bool IsSentenceBoundary(char ch) => ch is '.' or '!' or '?' or '\n';

    private static bool DispatchExactly(
        Func<Native.INPUT[], uint> dispatch,
        Native.INPUT[] inputs)
        => DispatchExactly(dispatch, inputs, out _);

    private static bool DispatchExactly(
        Func<Native.INPUT[], uint> dispatch,
        Native.INPUT[] inputs,
        out uint inserted)
    {
        inserted = dispatch(inputs);
        if (inserted == (uint)inputs.Length)
        {
            return true;
        }

        InjectionDiagnostics.RecordSendInputFailure();
        return false;
    }

    /// <summary>
    /// Hands input to the system. Returns the number of events actually inserted, which is zero
    /// when another thread has called BlockInput. A non-zero count is not proof of delivery:
    /// UIPI discards events further down without reporting anything.
    /// </summary>
    internal static uint Dispatch(Native.INPUT[] inputs)
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
