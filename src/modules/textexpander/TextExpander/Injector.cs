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

    /// <summary>
    /// Gets or sets how long a target that isn't taking messages (busy or hung) may keep its paste pending. Its Ctrl+V is
    /// still queued, so the clipboard waits: handed back now, it would paste what the user copied instead of the snippet.
    /// Past this, the snippet stays on the clipboard and the user is told.
    /// </summary>
    public static int PasteBusyCapMs { get; set; } = 5000;

    /// <summary>Gets or sets how long to wait for a target's answer when asking whether it's taking messages.</summary>
    public static int ResponsiveProbeMs { get; set; } = 50;

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

    /// <summary>
    /// The most caret moves that place a snippet's $|$. One further back than that is left at the end of the text:
    /// placing it would take thousands of key presses.
    /// </summary>
    internal const int MaxCaretMoves = 2_000;

    /// <summary>The most key events one call sends: a longer erase or caret move goes out in parts.</summary>
    private const int MaxInputsPerCall = 1_000;

    /// <summary>Gets or sets how many times to try handing the user's clipboard back before giving up.</summary>
    public static int RestoreAttempts { get; set; } = 3;

    /// <summary>Gets or sets pause between those attempts, to let whoever holds the clipboard let go.</summary>
    public static int RestoreRetryDelayMs { get; set; } = 40;

    /// <summary>
    /// Gets or sets the callback invoked when the user's clipboard could not be handed back, so the loss is never silent.
    /// </summary>
    public static Action<string>? ClipboardRestoreFailed { get; set; }

    // The user's clipboard couldn't be handed back: told once the guard is over. Only the expansion worker touches it.
    private static bool _clipboardLost;

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
    /// Erases <paramref name="backspaceCount"/> characters, then inserts <paramref name="text"/>, with
    /// <paramref name="guard"/> keeping the user's keys out of it.
    /// </summary>
    /// <param name="targetWindow">
    /// The window the trigger was typed into, re-checked before anything is inserted, or
    /// <see cref="IntPtr.Zero"/> when unknown.
    /// </param>
    /// <param name="cursorRightUnits">
    /// How far to move the caret right before erasing, when an earlier insertion left it inside the text to erase.
    /// </param>
    /// <param name="inputGeneration">
    /// The input generation when the trigger matched. A key pressed or a click since then may have moved the caret, so
    /// nothing is erased or inserted.
    /// </param>
    /// <param name="inserted">Runs once it's all in, before the keys held meanwhile go back: a Ctrl+Z among them undoes it.</param>
    /// <param name="givenBack">
    /// How many characters at the end of <paramref name="text"/> are the user's own keystrokes given back, such as
    /// the space that released the trigger. They don't count toward <see cref="ClipboardThresholdChars"/>.
    /// </param>
    public static bool Replace(int backspaceCount, string text, IntPtr targetWindow, int cursorLeftUnits, int cursorRightUnits, InsertionGuard guard, long inputGeneration, Action? inserted, int givenBack)
        => ReplaceCore(backspaceCount, text, targetWindow, cursorLeftUnits, InputTarget.CanDrive, guard, inputGeneration, cursorRightUnits: cursorRightUnits, inserted: inserted, givenBack: givenBack);

    internal static bool ReplaceCore(
        int backspaceCount,
        string text,
        IntPtr targetWindow,
        int cursorLeftUnits,
        Func<IntPtr, bool> canDrive,
        InsertionGuard guard,
        long inputGeneration,
        Func<IntPtr>? getForegroundWindow = null,
        Func<string, IntPtr, Func<(bool Whole, bool SentAny)>, PasteOutcome>? tryPaste = null,
        Func<Native.INPUT[], uint>? dispatch = null,
        int cursorRightUnits = 0,
        Func<int, short>? getAsyncKeyState = null,
        Action<int>? sleep = null,
        Action? inserted = null,
        int givenBack = 0)
    {
        ArgumentNullException.ThrowIfNull(canDrive);

        getForegroundWindow ??= Native.GetForegroundWindow;
        tryPaste ??= TryPaste;
        dispatch ??= Dispatch;
        getAsyncKeyState ??= Native.GetAsyncKeyState;
        sleep ??= Thread.Sleep;

        // The window must still be in front and drivable, and nothing since the match may have moved the caret.
        if (!Ready())
        {
            InjectionDiagnostics.RecordTypingAbandoned();
            return false;
        }

        // A grace period, so the erase doesn't race ahead of the target still processing the trigger's last keystrokes.
        if (backspaceCount > 0 && PreBackspaceDelayMs > 0)
        {
            sleep(PreBackspaceDelayMs);
        }

        bool pasting = text.Length > 0 && InjectionPolicy.ShouldUseClipboard(text, ClipboardThresholdChars, Backend, givenBack);
        PasteOutcome outcome = pasting ? PasteOutcome.TargetLost : PasteOutcome.NotAttempted;
        bool sentAny = false;
        bool guarded = false;
        bool succeeded = false;
        Native.INPUT marker = default;
        int[] modifiers = [];
        Native.INPUT[] pressAgain = [];

        // Asked again right before anything goes out.
        if (Ready() && Start())
        {
            if (pasting)
            {
                outcome = tryPaste(text, targetWindow, () => (Insert(paste: true), sentAny));
            }

            // Typing is the fallback, inside the same guard, only when the paste was refused before anything went out.
            succeeded = outcome == PasteOutcome.Pasted || (Typing() && Insert(paste: false));
        }

        EndGuard();
        InjectionDiagnostics.RecordOutcome(outcome);
        if (succeeded && Typing())
        {
            InjectionDiagnostics.RecordTypingSucceeded();
        }
        else if (!sentAny && Typing())
        {
            InjectionDiagnostics.RecordTypingAbandoned();
        }

        // Only now: the message box takes the focus.
        if (_clipboardLost)
        {
            _clipboardLost = false;
            ReportClipboardLoss();
        }

        return succeeded;

        bool Ready() => guard.Unchanged(inputGeneration) && IsAuthorizedTarget(targetWindow, canDrive, getForegroundWindow);

        bool Typing() => outcome is PasteOutcome.NotAttempted or PasteOutcome.RefusedBeforeErase;

        // The guard keeps the user's keys out (see InsertionGuard). The start marker goes out alone, and nothing else until
        // the hook has decided: a discarded insertion sends only its markers, and borrows no clipboard, so a Ctrl+V the
        // user pressed first pastes what they copied. The modifiers read after the decision are exactly the ones the user
        // holds: any pressed since is held.
        bool Start()
        {
            marker = InsertionGuard.Marker(guard.Begin(inputGeneration, targetWindow != IntPtr.Zero ? targetWindow : getForegroundWindow()));
            if (!DispatchExactly(dispatch, [marker]))
            {
                guard.Abandon();
                return false;
            }

            guarded = true;
            if (!guard.AwaitStart())
            {
                return false;
            }

            modifiers = HeldModifiers(getAsyncKeyState);
            pressAgain = Modifiers(modifiers, up: false);
            return true;
        }

        // The erase, Ctrl+V or the text, the caret moves, and the undo record. A slow clipboard borrow can outlast the
        // guard, and a modifier pressed after it reaches the app: then a new start marker decides again, and the modifiers
        // are read again, rather than erase unguarded. Nothing is erased if a click, which isn't held, moved the caret.
        bool Insert(bool paste)
        {
            bool done = (guard.Renew() || (!guard.Cancelled && Start()))
                && guard.Unchanged(inputGeneration)
                && SendChunks(Erase(modifiers, cursorRightUnits, backspaceCount))
                && (paste ? PasteChord() : TypeText())
                && SendChunks(Presses(Native.VK_LEFT, cursorLeftUnits));
            if (done)
            {
                inserted?.Invoke();
            }

            return done;
        }

        // The end marker, after the paste's settle and restore: the keys held meanwhile go back after it all.
        void EndGuard()
        {
            if (guarded)
            {
                guarded = false;
                guard.AwaitEnd(DispatchExactly(dispatch, [marker]), pressAgain);
            }
        }

        // Checked before each call: once the target has lost the front, the insertion goes no further, and what the
        // guard held is dropped.
        bool OnTarget()
        {
            if (!guard.Cancelled && IsAuthorizedTarget(targetWindow, canDrive, getForegroundWindow))
            {
                return true;
            }

            guard.Drop();
            return false;
        }

        bool Send(Native.INPUT[] keys)
        {
            bool whole = DispatchExactly(dispatch, keys, out uint count);
            sentAny |= count > 0;
            return whole;
        }

        // A long run goes out in parts, with the target checked before each.
        bool SendChunks(Native.INPUT[] keys)
        {
            for (int at = 0; at < keys.Length; at += MaxInputsPerCall)
            {
                if (!OnTarget() || !Send(keys.Length <= MaxInputsPerCall ? keys : keys[at..Math.Min(keys.Length, at + MaxInputsPerCall)]))
                {
                    return false;
                }
            }

            return true;
        }

        bool PasteChord()
        {
            if (!OnTarget())
            {
                return false;
            }

            if (Send([KeyDown(Native.VK_CONTROL), KeyDown(Native.VK_V), KeyUp(Native.VK_V), KeyUp(Native.VK_CONTROL)]))
            {
                return true;
            }

            // Cut off: never leave Ctrl down.
            _ = dispatch([KeyUp(Native.VK_CONTROL)]);
            return false;
        }

        // A character per call, paced: Windows 11 Notepad drops and smears characters that arrive faster.
        bool TypeText()
        {
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\r')
                {
                    continue;
                }

                // Before every character, however many UTF-16 units it takes.
                if (!OnTarget())
                {
                    return false;
                }

                Native.INPUT[] keys;
                if (ch is '\n' or '\t')
                {
                    // Real key presses: many editors ignore a literal \n delivered as a Unicode event. A modifier the user
                    // holds once the guard has let go is let go of first, or Tab would become Shift+Tab.
                    int vk = ch == '\n' ? Native.VK_RETURN : Native.VK_TAB;
                    keys = [.. Modifiers(HeldModifiers(getAsyncKeyState), up: true), KeyDown(vk), KeyUp(vk)];
                }
                else if (char.IsHighSurrogate(ch) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    // A surrogate pair must reach the target as a single uninterrupted unit.
                    char low = text[++i];
                    keys = [UnicodeDown(ch), UnicodeUp(ch), UnicodeDown(low), UnicodeUp(low)];
                }
                else
                {
                    keys = [UnicodeDown(ch), UnicodeUp(ch)];
                }

                if (!Send(keys))
                {
                    return false;
                }

                int delay = KeyDelayMs + (IsSentenceBoundary(ch) ? SentenceBoundaryDelayMs : 0);
                if (delay > 0)
                {
                    sleep(delay);
                }

                // Keys are held for HoldCapMs at most; the rest of the text goes in unguarded.
                if (guarded && guard.Overdue)
                {
                    EndGuard();
                }
            }

            return true;
        }
    }

    private static bool IsSentenceBoundary(char ch) => ch is '.' or '!' or '?' or '\n';

    private static bool IsAuthorizedTarget(IntPtr targetWindow, Func<IntPtr, bool> canDrive, Func<IntPtr> getForegroundWindow)
    {
        IntPtr foreground = getForegroundWindow();
        return foreground != IntPtr.Zero
            && (targetWindow == IntPtr.Zero || foreground == targetWindow)
            && canDrive(foreground);
    }

    /// <summary>
    /// Pastes via the clipboard, restoring whatever the user had there. It's borrowed only once the guard has decided,
    /// so a Ctrl+V the user pressed first pastes what they copied. Nothing destructive happens until the clipboard
    /// takeover is verified; only then does <paramref name="send"/> erase the trigger and paste, checking the target
    /// before each call. The paste settles and the clipboard goes back before the end marker, so a Ctrl+V among the
    /// keys held meanwhile pastes what the user copied too.
    /// </summary>
    private static PasteOutcome TryPaste(string text, IntPtr targetWindow, Func<(bool Whole, bool SentAny)> send)
    {
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
        bool handBack = true;
        try
        {
            // Verified, not assumed: SetClipboardData can succeed and still leave other data on the clipboard.
            if (write == ClipboardWrite.Failed || !Clipboard.HoldsText(text))
            {
                return PasteOutcome.RefusedBeforeErase;
            }

            (bool pasted, bool sentAny) = send();

            // The target reads the clipboard asynchronously; restoring at once would paste the previous contents. One
            // still busy at the cap keeps the snippet, since its Ctrl+V may yet come.
            handBack = !sentAny || WaitForPasteToSettle(targetWindow);

            // Not sent at all: focus moved, a click may have moved the caret, or deciding again dropped it. The trigger is intact.
            return !sentAny ? PasteOutcome.TargetLost : pasted ? PasteOutcome.Pasted : PasteOutcome.FailedAfterErase;
        }
        finally
        {
            if (handBack)
            {
                RestoreClipboard(saved, loan);
            }
            else if (!saved.IsEmpty)
            {
                InjectionDiagnostics.RecordClipboardRestoreFailure();
                _clipboardLost = true;
            }
        }
    }

    /// <summary>
    /// Waits for the target to take the paste: its process opening and then closing the clipboard is direct
    /// evidence the paste was consumed. Anything not attributable to the target falls back to waiting
    /// <see cref="PasteSettleMs"/>, and longer while the target isn't taking messages. Returns false if it still
    /// isn't after <see cref="PasteBusyCapMs"/>: the clipboard must not be handed back under a pending paste.
    /// </summary>
    private static bool WaitForPasteToSettle(IntPtr targetWindow)
    {
        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        return WaitForPasteToSettle(() => Clipboard.IsOpenByTarget(targetWindow), Thread.Sleep, () => (long)System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds, () => IsTakingMessages(targetWindow));
    }

    /// <summary>
    /// Whether <paramref name="window"/>'s thread answers a message within <see cref="ResponsiveProbeMs"/>. A window
    /// that's gone counts as answering: nothing is left to paste into.
    /// </summary>
    private static bool IsTakingMessages(IntPtr window)
        => window == IntPtr.Zero
            || !Native.IsWindow(window)
            || Native.SendMessageTimeoutW(window, Native.WM_NULL, IntPtr.Zero, IntPtr.Zero, Native.SMTO_BLOCK | Native.SMTO_ABORTIFHUNG, (uint)Math.Max(1, ResponsiveProbeMs), out _) != IntPtr.Zero;

    /// <summary>
    /// The wait behind <see cref="WaitForPasteToSettle(IntPtr)"/>, with the clock and the target checks passed in
    /// for tests. Timed by the clock, not by counting polls: a short sleep lasts a whole timer tick (about 15 ms),
    /// so counted 5 ms polls held the clipboard for over a second. Returns whether the clipboard may be handed back.
    /// </summary>
    internal static bool WaitForPasteToSettle(Func<bool> isOpenByTarget, Action<int> sleep, Func<long> elapsedMs, Func<bool>? isTakingMessages = null)
    {
        int budget = PasteSettleMs;
        if (budget <= 0)
        {
            return true;
        }

        int poll = Math.Max(1, PasteSettlePollMs);
        int floor = Math.Min(PasteSettleFloorMs, budget);
        long cap = Math.Max(budget, PasteBusyCapMs);
        long readBy = budget;
        bool observedRead = false;
        bool wasBusy = false;

        for (long waited = elapsedMs(); ; waited = elapsedMs())
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

                return true;
            }

            // Not seen reading by now. A target taking messages had its chance; a busy one still has the Ctrl+V queued,
            // to be handled right after it answers again, so the read gets the floor from then.
            if (waited >= readBy)
            {
                if (isTakingMessages?.Invoke() != false)
                {
                    if (!wasBusy)
                    {
                        return true;
                    }

                    wasBusy = false;
                    readBy = waited + floor;
                }
                else if (waited >= cap)
                {
                    return false;
                }
                else
                {
                    wasBusy = true;
                }
            }

            sleep(poll);
        }
    }

    /// <summary>
    /// Hands the user's clipboard back, retrying while another process holds it. Refuses when someone else wrote to the
    /// clipboard after our own write: their newer data wins. A loss is reported once the guard is over.
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
        if (!saved.IsEmpty)
        {
            InjectionDiagnostics.RecordClipboardRestoreFailure();
            _clipboardLost = true;
        }
    }

    /// <summary>
    /// Tells the user their clipboard is lost, on a thread of its own, and doesn't wait: the handler opens a message
    /// box, which on the worker held up every expansion after it until someone found and closed it. Never while the
    /// guard holds keys, as the box takes the focus.
    /// </summary>
    internal static void ReportClipboardLoss()
    {
        Action<string>? report = ClipboardRestoreFailed;
        if (report is not null)
        {
            new Thread(() => report(EngineText.Get(
                "ClipboardRestoreFailed",
                "Your clipboard couldn't be restored after a snippet expanded, so what you had copied is gone.\n\nTo keep snippets from using the clipboard, open PowerToys Settings > Text Expander > Advanced, and set Insertion method to Always type."))) { IsBackground = true }.Start();
        }
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
    /// The modifiers the user holds, to let go of: Backspace with Ctrl deletes whole words, and Shift or AltGr turns
    /// Ctrl+V into "paste special".
    /// </summary>
    private static int[] HeldModifiers(Func<int, short> getAsyncKeyState)
    {
        int[] modifiers = [Native.VK_LSHIFT, Native.VK_RSHIFT, Native.VK_LCONTROL, Native.VK_RCONTROL, Native.VK_LMENU, Native.VK_RMENU, Native.VK_LWIN, Native.VK_RWIN];
        return Array.FindAll(modifiers, vk => (getAsyncKeyState(vk) & 0x8000) != 0);
    }

    /// <summary>Key-ups that let go of <paramref name="modifiers"/>, or key-downs that press them again.</summary>
    private static Native.INPUT[] Modifiers(int[] modifiers, bool up)
    {
        var keys = new Native.INPUT[modifiers.Length];
        for (int i = 0; i < modifiers.Length; i++)
        {
            int vk = modifiers[i];
            keys[i] = up ? KeyUp(vk) : KeyDown(vk);
            if (vk is Native.VK_RCONTROL or Native.VK_RMENU or Native.VK_LWIN or Native.VK_RWIN)
            {
                keys[i].U.Ki.DwFlags |= Native.KEYEVENTF_EXTENDEDKEY;
            }
        }

        return keys;
    }

    /// <summary>
    /// The start of an insertion, in one array: the modifiers the user holds let go of, the caret back to the end of the
    /// text to erase, and the erase.
    /// </summary>
    private static Native.INPUT[] Erase(int[] modifiers, int cursorRightUnits, int backspaceCount)
    {
        Native.INPUT[] releases = Modifiers(modifiers, up: true);
        var keys = new Native.INPUT[releases.Length + (2 * (Math.Max(0, cursorRightUnits) + Math.Max(0, backspaceCount)))];
        releases.CopyTo(keys, 0);
        AddPresses(keys, AddPresses(keys, releases.Length, Native.VK_RIGHT, cursorRightUnits), Native.VK_BACK, backspaceCount);
        return keys;
    }

    private static Native.INPUT[] Presses(int vk, int count)
    {
        var keys = new Native.INPUT[Math.Max(0, count) * 2];
        AddPresses(keys, 0, vk, count);
        return keys;
    }

    /// <summary>Puts <paramref name="count"/> presses of <paramref name="vk"/> in <paramref name="keys"/> at <paramref name="next"/>, and returns where they end.</summary>
    private static int AddPresses(Native.INPUT[] keys, int next, int vk, int count)
    {
        for (int i = 0; i < count; i++)
        {
            keys[next++] = KeyDown(vk);
            keys[next++] = KeyUp(vk);
        }

        return next;
    }

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
