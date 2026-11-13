// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Low-level keyboard hook that maintains a rolling buffer of typed characters and detects
/// snippet triggers. It also installs a low-level mouse hook: clicking with the mouse moves the
/// caret without ever changing the foreground window, so the buffer must be cleared on any mouse
/// button press. Otherwise a trigger could still match against characters that are no longer
/// behind the caret, and the expansion would send its backspaces at the new caret position and
/// silently delete unrelated text.
///
/// Windows delivers WH_KEYBOARD_LL and WH_MOUSE_LL callbacks by marshalling them to the thread
/// that installed the hook, so that thread must keep pumping messages. If it stops for longer than
/// LowLevelHooksTimeout (300 ms by default) the hook is silently removed with no notification.
/// Matching is therefore done inline here because it is cheap, while the expansion — which waits
/// for prompts and for the target to take a paste, and paces the characters it types — is handed
/// to a dedicated worker thread. Posting it back to this thread's own message loop would not help:
/// the blocking would simply move from inside the callback to just after it, still starving the
/// next hook dispatch. While the worker inserts, the hook keeps the user's keys out of the
/// insertion (see <see cref="InsertionGuard"/>).
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    /// <summary>
    /// Tags our own synthetic input so we never react to it. Random for each run, so no other program can pass its keys
    /// off as ours, and well clear of the small tags PowerToys' hooks use (0x1, 0x11, 0x101, 0x110, 0x111). It carries
    /// Keyboard Manager's injected bit, so Keyboard Manager's key remaps (a Caps Lock and Ctrl swap, a remapped
    /// Backspace) leave our keys alone instead of turning the paste chord or the erase into other keys.
    /// </summary>
    public static readonly IntPtr InjectionSignature =
        new((System.Security.Cryptography.RandomNumberGenerator.GetInt32(0x0100_0000, int.MaxValue) & ~0xFFF) | HostInputTags.KeyboardManagerInjected);

    private const int MinimumBufferCapacity = 256;

    // These delegates must be kept alive for the lifetime of the hooks. If they are collected
    // the callback address becomes invalid and the process crashes.
    private readonly Native.HookProc _hookProc;
    private readonly Native.HookProc _mouseHookProc;
    private readonly Native.WinEventProc _winEventProc;
    private readonly object _bufferLock = new();
    private readonly object _undoLock = new();
    private readonly SnippetStore _store;
    private TargetAuthorization _targetAuthorization = new(IntPtr.Zero, 0, false);

    // The window the hook last asked to have checked, so it asks once per window rather than per key.
    private IntPtr _requestedAuthorizationWindow;

    private IntPtr _hookHandle;
    private IntPtr _mouseHookHandle;
    private IntPtr _winEventHandle;

    // Grows with the longest trigger (EnsureCapacity); guarded by _bufferLock.
    private char[] _buffer = new char[MinimumBufferCapacity];
    private int _count;

    // The last key typed nothing yet: a dead key, whose accent the app keeps for the next key. Guarded by _bufferLock.
    private bool _afterDeadKey;
    private bool _disposed;
    private UndoEntry? _undo;
    private long _inputGeneration;
    private long _clicks;

    /// <summary>Gets triggers detected but not yet expanded. Drained by the expansion worker.</summary>
    public BlockingCollection<PendingExpansion> Pending { get; } = new();

    private volatile bool _enabled = true;
    private volatile bool _suspended;

    // The window the expansion waiting on a prompt will type into, or zero when it isn't known.
    private volatile IntPtr _promptTarget;

    // CursorLeft is how far before the end of Replacement the snippet's $|$ left the caret.
    private sealed record UndoEntry(string Trigger, string Replacement, IntPtr TargetWindow, int CursorLeft);

    internal sealed record TargetAuthorization(IntPtr Window, uint ProcessId, bool CanDrive);

    /// <summary>What the hook does with a key, from the check of the window in front (<see cref="CaptureFor"/>).</summary>
    internal enum ForegroundCapture
    {
        /// <summary>Dropped, with what was typed: nothing is in front, or the window was checked and refused.</summary>
        Refused,

        /// <summary>Kept while the window's check runs, but no trigger fires.</summary>
        Pending,

        /// <summary>Kept, and a trigger expands.</summary>
        Authorized,
    }

    /// <summary>
    /// Gets the input generation. Every key press other than a modifier, and every click, moves it on, because
    /// any of them can move the caret: work queued before one of them must not be applied where the caret is now.
    /// While a prompt is open, only those reaching the window the expansion will type into do. Keys held while an
    /// insertion goes in don't: they haven't reached the app yet.
    /// </summary>
    internal long InputGeneration => Volatile.Read(ref _inputGeneration);

    /// <summary>
    /// Gets how many clicks or wheel events have reached apps, but for those while a prompt is open, when no insertion runs. Unlike the
    /// input generation, keys the guard holds and sends back never move it.
    /// </summary>
    internal long Clicks => Volatile.Read(ref _clicks);

    /// <summary>Gets the guard that keeps the user's keys out of each insertion.</summary>
    internal InsertionGuard Guard { get; }

    /// <summary>
    /// Gets what runs for each key press and click of the user's that isn't held for an insertion: a notice showing at the
    /// caret starts to fade. Not for the engine's own input. Keys held during an insertion count as they go back, which is
    /// before that insertion's notice shows. Cheap and non-blocking, as everything in the callbacks must be. Tests pass their
    /// own.
    /// </summary>
    internal Action UserInput { get; init; } = NoticeBubble.OnUserInput;

    /// <summary>
    /// Gets what tells whether a click at a point on the screen lands on the notice at the caret, which a click only takes
    /// down. Cheap and non-blocking: one read while no notice shows. Tests pass their own.
    /// </summary>
    internal Func<Native.POINT, bool> IsOnNotice { get; init; } = NoticeBubble.IsAt;

    /// <summary>
    /// Gets what asks for a window's check off the hook's path, once a key is typed in a window in front that was never
    /// checked (see <see cref="CaptureForWindow"/>). Tests pass their own.
    /// </summary>
    internal Action<IntPtr> RequestAuthorization { get; init; }

    /// <summary>Gets what the check of the window in front says about a key typed there. Tests pass their own.</summary>
    internal Func<ForegroundCapture> CaptureForeground { get; init; }

    /// <summary>Gets whether a standard password box has the focus, where keys don't count. Tests pass their own.</summary>
    internal Func<bool> InPasswordBox { get; init; } = IsFocusedPasswordEdit;

    /// <summary>
    /// What a key types into <c>typed</c>, as the window in front's layout makes it: the number of UTF-16 units produced
    /// by the complete key (including ligatures and supplementary characters), zero for none, or -1 for a dead key.
    /// </summary>
    internal delegate int KeyDecoder(uint vk, uint scanCode, Span<char> typed);

    /// <summary>Gets what decodes each key. Tests pass their own, to type as a layout with dead keys does.</summary>
    internal KeyDecoder Decode { get; init; } = DecodeWithForegroundLayout;

    /// <summary>Continues the hook chain before ordinary text is committed. Tests supply a downstream remapping hook.</summary>
    internal Native.HookProc NextHook { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether when false, keystrokes are still observed but never expanded. Turning this off is how a
    /// user pauses the app, so it is also treated as a privacy boundary: whatever they had typed
    /// up to that point is erased rather than left sitting in memory.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value)
            {
                ResetBuffer();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether set while our own UI has focus so we ignore the user typing into it.
    /// Keys and clicks reaching the window the expansion will type into (see <see cref="SuspendFor"/>) still move
    /// <see cref="InputGeneration"/>. Also erases the buffer, for the same reason as <see cref="Enabled"/>: what
    /// was typed before a prompt dialog opened is not needed after it.
    /// </summary>
    public bool Suspended
    {
        get => _suspended;
        set
        {
            _suspended = value;
            if (value)
            {
                ResetBuffer();
            }
        }
    }

    /// <summary>
    /// Suspends the hook for a prompt asked for an expansion that will type into <paramref name="target"/>, or zero
    /// when that window isn't known. Clearing <see cref="Suspended"/> ends it.
    /// </summary>
    public void SuspendFor(IntPtr target)
    {
        _promptTarget = target;
        Suspended = true;
    }

    public KeyboardHook(SnippetStore store)
    {
        _store = store;
        _hookProc = HookCallback;
        _mouseHookProc = MouseCallback;
        _winEventProc = ForegroundChanged;
        Guard = new InsertionGuard(() => InputGeneration, TargetInFront, () => Clicks);
        RequestAuthorization = window => RefreshTargetAuthorization(window, asynchronous: true);
        CaptureForeground = CaptureForForeground;
        NextHook = (code, message, data) => Native.CallNextHookEx(_hookHandle, code, message, data);
    }

    public bool Install()
    {
        Guard.Resend = Injector.Dispatch;
        InsertionGuard.WarmUp();
        RefreshTargetAuthorization(Native.GetForegroundWindow(), asynchronous: false);

        IntPtr module = Native.GetModuleHandleW(null);
        _hookHandle = Native.SetWindowsHookExW(Native.WH_KEYBOARD_LL, _hookProc, module, 0);

        // Best-effort: a mouse click moves the caret within the same window (which the foreground // WinEvent never sees), so we clear the buffer on button presses. The keyboard hook below
        // remains the gate for success — without it the app has no function at all.
        _mouseHookHandle = Native.SetWindowsHookExW(Native.WH_MOUSE_LL, _mouseHookProc, module, 0);

        _winEventHandle = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND, Native.EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _winEventProc, 0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);

        RefreshTargetAuthorization(Native.GetForegroundWindow(), asynchronous: true);
        return _hookHandle != IntPtr.Zero;
    }

    public void ClearBuffer() => ResetBuffer();

    /// <summary>Clears and overwrites the characters typed since the last reset.</summary>
    private void ResetBuffer()
    {
        lock (_bufferLock)
        {
            ClearBufferLocked();
        }
    }

    private void ClearBufferLocked()
    {
        int live = _count;
        _count = 0;
        _afterDeadKey = false;
        if (live > 0)
        {
            Array.Clear(_buffer, 0, live);
        }
    }

    // ── Hook callback ────────────────────────────────────────
    internal unsafe IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != Native.HC_ACTION)
        {
            return NextHook(nCode, wParam, lParam);
        }

        // Copied, not marshalled, which would allocate on every key event on the machine.
        Native.KBDLLHOOKSTRUCT key = *(Native.KBDLLHOOKSTRUCT*)lParam;
        if (Guard.Swallows(key, observePassed: false, out bool mayMatch))
        {
            return new IntPtr(1);
        }

        IntPtr next = NextHook(nCode, wParam, lParam);
        if (next != IntPtr.Zero)
        {
            return next;
        }

        // A later hook can swallow the original and inject a remapped key instead. Only the event it lets through
        // belongs in the buffer, or the original and remapped characters would both be counted.
        return ProcessPassedKeyEvent(key, wParam.ToInt32(), mayMatch) || !Guard.Passed(key) ? new IntPtr(1) : IntPtr.Zero;
    }

    /// <summary>Handles a key event, and returns whether it's swallowed. Every one, key-ups too, goes to the guard first.</summary>
    internal bool OnKeyEvent(Native.KBDLLHOOKSTRUCT data, int message, bool observePassed = true)
    {
        if (Guard.Swallows(data, observePassed, out bool mayMatch))
        {
            return true;
        }

        return ProcessPassedKeyEvent(data, message, mayMatch);
    }

    private bool ProcessPassedKeyEvent(Native.KBDLLHOOKSTRUCT data, int message, bool mayMatch)
    {
        if (_disposed || (message != Native.WM_KEYDOWN && message != Native.WM_SYSKEYDOWN))
        {
            return false;
        }

        // Ignore input we synthesised ourselves — which is what prevents an expansion from
        // re-triggering — and the synthetic input PowerToys' own hooks replay. Deliberately NOT
        // filtering on LLKHF_INJECTED generally: the Windows touch keyboard, on-screen keyboard
        // and remote-desktop clients all inject their input, and blanket-filtering would silently
        // break expansion on 2-in-1 ARM devices. See HostInputTags for which tags are honoured.
        HostInputKind kind = HostInputTags.Classify(data.DwExtraInfo, InjectionSignature);
        if (kind == HostInputKind.Ignored)
        {
            return false;
        }

        UserInput();
        if (Suspended)
        {
            KeyWhileSuspended(data.VkCode, ReachesPromptTarget(Native.GetForegroundWindow()));
            return false;
        }

        if (kind == HostInputKind.Unmatched || !mayMatch)
        {
            UnmatchedKey(data.VkCode);
            return false;
        }

        // Swallows the final trigger character.
        return HandleKey(data);
    }

    /// <summary>
    /// Clears the typed-character buffer on any mouse button press, because a click can move the
    /// caret inside the current window without ever raising a foreground-change event.
    ///
    /// This runs for every mouse event system-wide, so it must be as cheap as the keyboard
    /// callback: if this thread misses the 300 ms LowLevelHooksTimeout the hook is silently
    /// removed. We therefore read only wParam to identify the message, and read the
    /// MSLLHOOKSTRUCT behind lParam only for a button press. Mouse input is never swallowed —
    /// the result of CallNextHookEx is always returned.
    /// </summary>
    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == Native.HC_ACTION)
        {
            OnMouseEvent(wParam.ToInt32(), lParam);
        }

        return Native.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    /// <summary>Handles a mouse event, given its message and the MSLLHOOKSTRUCT behind <paramref name="data"/>.</summary>
    internal unsafe void OnMouseEvent(int message, IntPtr data)
    {
        // Moving the mouse alone changes no text. Buttons and the wheel can change the caret or an editable selection.
        switch (message)
        {
            case Native.WM_LBUTTONDOWN:
            case Native.WM_RBUTTONDOWN:
            case Native.WM_MBUTTONDOWN:
            case Native.WM_NCLBUTTONDOWN:
            case Native.WM_XBUTTONDOWN:
                // MSLLHOOKSTRUCT starts with the point clicked. Copied, not marshalled, which would allocate on every click.
                OnButtonDown(*(Native.POINT*)data);
                break;
            case Native.WM_MOUSEWHEEL:
                UserInput();
                InvalidatePositionInput(*(Native.POINT*)data);
                break;
        }
    }

    /// <summary>
    /// Handles a button press at <paramref name="point"/>, on the screen: a notice showing starts to fade, and what was
    /// typed and Ctrl+Z are dropped, since a click can move the caret. Not for a click on the notice, which only takes the
    /// notice down and leaves the caret where it was.
    /// </summary>
    internal void OnButtonDown(Native.POINT point)
    {
        UserInput();
        if (IsOnNotice(point))
        {
            return;
        }

        InvalidatePositionInput(point);
    }

    private void InvalidatePositionInput(Native.POINT point)
    {
        if (!Suspended)
        {
            Click();
        }
        else
        {
            ClickWhileSuspended(ReachesPromptTarget(Native.GetAncestor(Native.WindowFromPoint(point), Native.GA_ROOT)));
        }
    }

    /// <summary>
    /// Invalidates a click or wheel event with no prompt open. It can move the caret, so what was typed before it can't complete a trigger,
    /// undo ends, and an insertion going in stops (see <see cref="Clicks"/>).
    /// </summary>
    internal void Click()
    {
        // Counted first: whoever sees the input generation move for it sees the click too.
        Interlocked.Increment(ref _clicks);
        ResetBuffer();
        ClearUndo();
    }

    /// <summary>
    /// Handles a key press while a prompt is open. It isn't recorded, but unless it is a modifier, one that reaches
    /// the window the expansion will type into can move the caret there.
    /// </summary>
    internal void KeyWhileSuspended(uint vk, bool reachesTarget)
    {
        if (reachesTarget && !IsModifierKey(vk))
        {
            ClearUndo();
        }
    }

    /// <summary>Handles a click while a prompt is open: one on the window the expansion will type into can move the caret there.</summary>
    internal void ClickWhileSuspended(bool reachesTarget)
    {
        if (reachesTarget)
        {
            ClearUndo();
        }
    }

    /// <summary>
    /// Handles a key press that reaches the app but isn't matched (<see cref="HostInputKind.Unmatched"/>). Unless it
    /// is a modifier it can move the caret, so what was typed before it can't complete a trigger, and undo ends.
    /// </summary>
    internal void UnmatchedKey(uint vk)
    {
        if (!IsModifierKey(vk))
        {
            ResetBuffer();
            ClearUndo();
        }
    }

    /// <summary>
    /// Whether a key going to <paramref name="window"/>, or a click on it, can move the caret where the expansion
    /// waiting on a prompt will type. Only the <paramref name="target"/> window's can, so answering the prompt or
    /// copying the answer from another app cancels nothing. Root windows are compared, so an app hosted by
    /// ApplicationFrameHost matches. With no target known, any window that isn't ours might be it.
    /// </summary>
    internal static bool ReachesTarget(IntPtr window, IntPtr target, Func<IntPtr, bool> isOurs)
        => target != IntPtr.Zero ? window == target : !isOurs(window);

    private bool ReachesPromptTarget(IntPtr window) => ReachesTarget(window, _promptTarget, BelongsToThisProcess);

    /// <summary>Whether <paramref name="window"/> is one of ours, such as the prompt. No window counts as another app's.</summary>
    private static bool BelongsToThisProcess(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        _ = Native.GetWindowThreadProcessId(window, out uint processId);
        return processId == (uint)Environment.ProcessId;
    }

    /// <summary>Returns true when the keystroke should be swallowed because it completed a trigger.</summary>
    private bool HandleKey(Native.KBDLLHOOKSTRUCT data)
    {
        lock (_bufferLock)
        {
            return _disposed ? false : HandleKeyLocked(data);
        }
    }

    private bool HandleKeyLocked(Native.KBDLLHOOKSTRUCT data)
    {
        ForegroundCapture capture = CaptureForeground();
        if (capture == ForegroundCapture.Refused)
        {
            ResetBuffer();
            ClearUndo();
            return false;
        }

        if (InPasswordBox())
        {
            ResetBuffer();
            ClearUndo();
            return false;
        }

        return ProcessKeyLocked(
            data.VkCode,
            data.ScanCode,
            ctrl: IsDown(Native.VK_CONTROL),
            alt: IsDown(Native.VK_MENU),
            shift: IsDown(Native.VK_SHIFT),
            win: IsDown(Native.VK_LWIN) || IsDown(Native.VK_RWIN),
            mayExpand: capture == ForegroundCapture.Authorized,
            key: data);
    }

    /// <summary>
    /// Processes a key-down that passed the capture checks, so tests can drive key sequences. With
    /// <paramref name="mayExpand"/> false, as while the window in front is being checked, keys count but no trigger fires.
    /// Driven this way, without the hook's key event, a match holds no keys after it.
    /// </summary>
    internal bool ProcessKey(uint vk, uint scanCode, bool ctrl, bool alt, bool shift, bool win, bool mayExpand = true)
    {
        lock (_bufferLock)
        {
            return !_disposed && ProcessKeyLocked(vk, scanCode, ctrl, alt, shift, win, mayExpand, key: null);
        }
    }

    private bool ProcessKeyLocked(uint vk, uint scanCode, bool ctrl, bool alt, bool shift, bool win, bool mayExpand, Native.KBDLLHOOKSTRUCT? key)
    {
        if (vk == Native.VK_BACK)
        {
            ClearUndo();

            // After a dead key, Backspace only drops the accent the app was keeping: no character goes.
            if (_afterDeadKey && !(ctrl || alt || win))
            {
                _afterDeadKey = false;
                return false;
            }

            // Ctrl+Backspace deletes a whole word, and with Alt or Win it is a shortcut, so only a
            // plain or shifted Backspace is known to remove exactly one character.
            if (ctrl || alt || win)
            {
                ResetBuffer();
            }
            else if (_count > 0)
            {
                // Overwritten, not just uncounted: the character the user deleted is exactly the kind
                // they most often meant to take back.
                // A valid surrogate pair is one recorded scalar; other text-element deletion rules are unchanged.
                bool pair = _count > 1 && char.IsLowSurrogate(_buffer[_count - 1]) && char.IsHighSurrogate(_buffer[_count - 2]);
                _buffer[--_count] = '\0';
                if (pair)
                {
                    _buffer[--_count] = '\0';
                }
            }

            return false;
        }

        if (IsUndoChord(vk, ctrl, alt, shift, win))
        {
            UndoEntry? undo;
            lock (_undoLock)
            {
                undo = _undo;
                _undo = null;
            }

            if (undo is not null && (undo.TargetWindow == IntPtr.Zero || Native.GetForegroundWindow() == undo.TargetWindow))
            {
                Pending.Add(new PendingExpansion(undo.Trigger, string.Empty, undo.Replacement, undo.TargetWindow, IsUndo: true, InputGeneration: InputGeneration, CursorLeft: undo.CursorLeft));
                ResetBuffer();
                return true;
            }
        }

        // Undo is intentionally one-step: any input after an expansion means the document is no
        // longer in the state where replaying the trigger would be safe. Modifiers arrive as
        // key-downs of their own before the key they modify, so they do not count as input;
        // otherwise the Ctrl that necessarily precedes Z would always discard the undo.
        if (!IsModifierKey(vk))
        {
            ClearUndo();
        }

        // With nothing to expand, nothing typed is kept.
        SnippetStore.Snapshot snapshot = _store.Current;
        if (!Enabled || snapshot.MaxTriggerLength == 0)
        {
            return false;
        }

        if (IsResetKey(vk))
        {
            ResetBuffer();
            return false;
        }

        // Ctrl+X and Alt+X are shortcuts, not text. Ctrl+Alt together is AltGr on international
        // layouts and does produce characters, so it falls through to ToUnicodeEx.
        if ((ctrl ^ alt) || win)
        {
            ResetBuffer();
            return false;
        }

        Span<char> typed = stackalloc char[8];
        int written = Decode(vk, scanCode, typed);
        if (written < 0)
        {
            _afterDeadKey = true;
            return false;
        }

        if (written == 0)
        {
            return false;
        }

        bool afterDeadKey = _afterDeadKey;
        _afterDeadKey = false;

        EnsureCapacity(snapshot.MaxTriggerLength);
        for (int i = 0; i < written; i++)
        {
            Append(typed[i]);
        }

        char ch = typed[written - 1];

        // The key after a dead key types what the app composes from the two: the accent alone, for a space. The app keeps
        // the accent until this key comes, so swallowing the key to release or complete a trigger left the accent waiting
        // in the app, where it took the erase's first Backspace and the trigger's first character stayed
        // ("!Layout letters OK^"). Such a key never releases or completes a trigger. ToUnicodeEx gives the composed
        // character here only once the app has taken the dead key, so what was appended can also be the key's own.
        if (afterDeadKey)
        {
            return false;
        }

        bool wordMode = WordBoundary.Required;

        // Normally this keystroke is the trigger's last character. In word mode it is the
        // terminator *after* a trigger that is already complete, so the match has to run against
        // the text in front of it.
        int usable = wordMode ? _count - 1 : _count;

        if (wordMode && (!WordBoundary.IsTerminator(ch) || usable <= 0))
        {
            return false;
        }

        // The index bounds its own walk, but needs the preceding text to choose the longest suffix at a word start.
        string? trigger = snapshot.Triggers.MatchLongestSuffix(_buffer.AsSpan(0, usable), wordMode);
        if (trigger is null)
        {
            return false;
        }

        // Resolve against the same snapshot we matched against. A reload between here and the
        // worker running would otherwise leave the swallowed keystroke unaccounted for.
        if (!snapshot.Map.TryGetValue(trigger, out string? replacement))
        {
            return false;
        }

        // Typed before the window in front passed its check, which can't be known on this thread.
        // The trigger stays as typed, as in a window that was refused.
        if (!mayExpand)
        {
            ResetBuffer();
            return false;
        }

        // As typed, so with case-insensitive matching its capitalization can differ from the saved
        // trigger: it is what gets erased and what Ctrl+Z puts back. In word mode it ends with the
        // space or punctuation that released the trigger.
        int start = usable - trigger.Length;
        string typedText = new(_buffer, start, _count - start);
        string typedPrefix = typedText[..Math.Max(0, typedText.Length - written)];
        string swallowedText = new(typed[..written]);
        ResetBuffer();

        // From here on the user's keys are held, after this one, until the insertion takes them over: one typed straight
        // after the trigger reached the app before the insertion began, which dropped it and lost the swallowed key.
        // Before the work is queued, so the worker can't begin first.
        IntPtr target = Native.GetForegroundWindow();
        long generation = InputGeneration;
        if (key is { } swallowed)
        {
            _ = Guard.HoldAfterMatch(target, generation, swallowed);
        }

        // The keystroke that completed the match is swallowed; everything typed before it reached the target.
        Pending.Add(new PendingExpansion(
            trigger,
            typedPrefix,
            replacement,
            target,
            TypedText: typedText,
            InputGeneration: generation,
            Terminator: wordMode && WordBoundary.KeepTerminator ? ch : null,
            SwallowedText: swallowedText));
        return true;
    }

    public bool RecordExpansion(string trigger, string replacement, IntPtr targetWindow)
        => RecordExpansion(trigger, replacement, targetWindow, InputGeneration);

    internal bool RecordExpansion(string trigger, string replacement, IntPtr targetWindow, long inputGeneration, int cursorLeft = 0)
    {
        lock (_undoLock)
        {
            if (inputGeneration != Volatile.Read(ref _inputGeneration))
            {
                return false;
            }

            _undo = new UndoEntry(trigger, replacement, targetWindow, cursorLeft);
            return true;
        }
    }

    public void ClearUndo()
    {
        Interlocked.Increment(ref _inputGeneration);
        DiscardUndo();
    }

    private void DiscardUndo()
    {
        lock (_undoLock)
        {
            _undo = null;
        }
    }

    /// <summary>
    /// Decodes a key with the *foreground window's* keyboard layout (see <see cref="KeyDecoder"/>).
    /// The 0x4 flag is essential: without it ToUnicodeEx mutates kernel dead-key state and
    /// breaks accented input on international layouts.
    ///
    /// <para>
    /// Both scratch buffers come from the stack. Allocating them on the managed heap cost 264
    /// bytes per keystroke — every keystroke on the machine, forever — which is pure garbage
    /// collector pressure created by a callback that is on a 300 ms deadline it cannot afford to
    /// miss. <c>stackalloc</c> costs a pointer bump and is zeroed by the runtime.
    /// </para>
    /// </summary>
    private static unsafe int DecodeWithForegroundLayout(uint vk, uint scanCode, Span<char> typed)
    {
        // Unicode-injected input (touch keyboard, emoji picker, remote desktop, automation)
        // carries the character in scanCode rather than a real virtual key, and ToUnicodeEx
        // cannot translate it.
        if (vk == Native.VK_PACKET)
        {
            char packet = (char)scanCode;
            if (packet == '\0' || char.IsControl(packet))
            {
                return 0;
            }

            typed[0] = packet;
            return 1;
        }

        IntPtr foreground = Native.GetForegroundWindow();
        uint threadId = foreground == IntPtr.Zero
            ? 0
            : Native.GetWindowThreadProcessId(foreground, out _);
        IntPtr layout = Native.GetKeyboardLayout(threadId);

        Span<byte> state = stackalloc byte[256];
        state.Clear();
        SetKeyState(state, Native.VK_SHIFT, Native.VK_LSHIFT, Native.VK_RSHIFT);
        SetKeyState(state, Native.VK_CONTROL, Native.VK_LCONTROL, Native.VK_RCONTROL);
        SetKeyState(state, Native.VK_MENU, Native.VK_LMENU, Native.VK_RMENU);
        if ((Native.GetKeyState(Native.VK_CAPITAL) & 1) != 0)
        {
            state[Native.VK_CAPITAL] = 0x01;
        }

        int written;
        fixed (byte* statePtr = state)
        {
            fixed (char* typedPtr = typed)
            {
                written = Native.ToUnicodeEx(vk, scanCode, statePtr, typedPtr, typed.Length, Native.TOUNICODE_NO_STATE_CHANGE, layout);
            }
        }

        // written < 0 is a dead key: it types nothing until the next key, whose character the app composes from the two.
        if (written < 0)
        {
            return -1;
        }

        // A physical key can produce a ligature or a supplementary character, not only a dead-key combination.
        int count = 0;
        for (int i = 0; i < written; i++)
        {
            if (!char.IsControl(typed[i]))
            {
                typed[count++] = typed[i];
            }
        }

        return count;
    }

    private static void SetKeyState(Span<byte> state, int generic, int left, int right)
    {
        if (IsDown(generic))
        {
            state[generic] = 0x80;
        }

        if (IsDown(left))
        {
            state[left] = 0x80;
        }

        if (IsDown(right))
        {
            state[right] = 0x80;
        }
    }

    private static bool IsDown(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;

    private static unsafe bool IsFocusedPasswordEdit()
    {
        IntPtr foreground = Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        uint thread = Native.GetWindowThreadProcessId(foreground, out _);
        var guiInfo = new Native.GUITHREADINFO
        {
            CbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.GUITHREADINFO>(),
        };

        if (thread == 0 || !Native.GetGUIThreadInfo(thread, ref guiInfo) || guiInfo.HwndFocus == IntPtr.Zero)
        {
            return false;
        }

        Span<char> className = stackalloc char[64];
        int length;
        fixed (char* classNamePointer = className)
        {
            length = Native.GetClassNameW(guiInfo.HwndFocus, classNamePointer, className.Length);
        }

        if (length <= 0)
        {
            return false;
        }

        return IsPasswordEditControl(className[..length], Native.GetWindowLongW(guiInfo.HwndFocus, Native.GWL_STYLE));
    }

    internal static bool IsPasswordEditControl(ReadOnlySpan<char> className, int style)
        => (style & Native.ES_PASSWORD) != 0
            && (className.Equals("Edit", StringComparison.OrdinalIgnoreCase)
                || className.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase)

                // WinForms TextBox and MaskedTextBox: "WindowsForms10.EDIT.app.0.<hash>". The class
                // name and style are enough; the hook never sends messages to another process.
                || className.StartsWith("WindowsForms10.EDIT.", StringComparison.OrdinalIgnoreCase));

    /// <summary>Ctrl+Z on its own. Ctrl+Shift+Z is redo in many apps, and Ctrl+Alt is AltGr.</summary>
    internal static bool IsUndoChord(uint vk, bool ctrl, bool alt, bool shift, bool win)
        => vk == Native.VK_Z && ctrl && !alt && !shift && !win;

    internal static bool IsModifierKey(uint vk) => vk switch
    {
        Native.VK_SHIFT or Native.VK_CONTROL or Native.VK_MENU => true,
        Native.VK_LSHIFT or Native.VK_RSHIFT or Native.VK_LCONTROL or Native.VK_RCONTROL => true,
        Native.VK_LMENU or Native.VK_RMENU or Native.VK_LWIN or Native.VK_RWIN => true,
        _ => false,
    };

    private static bool IsResetKey(uint vk) => vk switch
    {
        Native.VK_RETURN or Native.VK_TAB or Native.VK_ESCAPE => true,
        0x21 or 0x22 => true,                     // PageUp / PageDown
        0x23 or 0x24 => true,                     // End / Home
        0x25 or 0x26 or 0x27 or 0x28 => true,     // Arrow keys
        0x2D or 0x2E => true,                     // Insert / Delete
        >= 0x70 and <= 0x87 => true,              // F1..F24
        _ => false,
    };

    /// <summary>
    /// Gets the buffer size that always holds the longest trigger. A full buffer keeps its newest
    /// half, so that half must fit the trigger, the character in front of it (for the word-start
    /// check) and, in word mode, the space or punctuation after it.
    /// </summary>
    internal static int CapacityFor(int maxTriggerLength) => Math.Max(MinimumBufferCapacity, 2 * (maxTriggerLength + 2));

    /// <summary>Grows the buffer for a longer trigger, keeping what was typed. It never shrinks.</summary>
    private void EnsureCapacity(int maxTriggerLength)
    {
        int needed = CapacityFor(maxTriggerLength);
        if (_buffer.Length >= needed)
        {
            return;
        }

        char[] larger = new char[needed];
        Array.Copy(_buffer, larger, _count);

        // Keystrokes don't stay behind in the old array.
        Array.Clear(_buffer);
        _buffer = larger;
    }

    private void Append(char ch)
    {
        if (_count == _buffer.Length)
        {
            // Keep the most recent half, which EnsureCapacity makes long enough for any trigger.
            int keep = _buffer.Length / 2;
            Array.Copy(_buffer, _buffer.Length - keep, _buffer, 0, keep);
            _count = keep;

            // The discarded half is still sitting in the second half of the array. Blank it, both
            // to keep old keystrokes out of memory and to maintain the invariant ResetBuffer
            // relies on: nothing past _count is ever live data.
            Array.Clear(_buffer, keep, _buffer.Length - keep);
        }

        _buffer[_count++] = ch;
    }

    internal void ForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        ResetBuffer();
        Guard.ForegroundChanged(hwnd);

        // Ends undo without moving the input generation: a foreground change moves no caret by itself,
        // queued work checks its window before typing, and focus coming back after a prompt must not
        // cancel the expansion that asked.
        DiscardUndo();
        Volatile.Write(ref _requestedAuthorizationWindow, IntPtr.Zero);
        RefreshTargetAuthorization(Native.GetForegroundWindow(), asynchronous: true);
    }

    private ForegroundCapture CaptureForForeground()
    {
        IntPtr foreground = Native.GetForegroundWindow();
        uint foregroundProcessId = 0;
        uint thread = foreground == IntPtr.Zero
            ? 0
            : Native.GetWindowThreadProcessId(foreground, out foregroundProcessId);
        if (thread == 0 || foregroundProcessId == 0)
        {
            return ForegroundCapture.Refused;
        }

        return CaptureForWindow(foreground, foregroundProcessId);
    }

    /// <summary>
    /// What the hook does with a key typed in <paramref name="foreground"/>, asking for the window's check the first time
    /// one is. The foreground event can check the wrong window. Windows Terminal raises it for a new window before showing
    /// it, and a window activated through AttachThreadInput, as WindowFocus.Force does when a prompt hands focus back,
    /// raises it while GetForegroundWindow still reports the window it is leaving. Either way, the one actually in front is
    /// checked, off this path and once, and its keys are kept meanwhile: the first one may start a trigger.
    /// </summary>
    internal ForegroundCapture CaptureForWindow(IntPtr foreground, uint foregroundProcessId)
    {
        TargetAuthorization authorization = Volatile.Read(ref _targetAuthorization);
        if (NeedsAuthorization(foreground, authorization, Volatile.Read(ref _requestedAuthorizationWindow)))
        {
            // Whatever was typed before went into another window.
            ResetBuffer();
            ClearUndo();
            Volatile.Write(ref _requestedAuthorizationWindow, foreground);
            RequestAuthorization(foreground);
        }

        return CaptureFor(foreground, foregroundProcessId, authorization, Volatile.Read(ref _requestedAuthorizationWindow));
    }

    internal void RefreshTargetAuthorization(
        IntPtr window,
        bool asynchronous,
        Func<IntPtr, uint>? processIdFor = null,
        Func<IntPtr, uint, bool>? canDriveWindow = null,
        Func<IntPtr>? getForegroundWindow = null)
    {
        processIdFor ??= static window => Native.GetWindowThreadProcessId(window, out uint processId) == 0 ? 0 : processId;
        canDriveWindow ??= static (window, processId) => InputTarget.CanDrive(window) && !InputTarget.IsPowerToysSettings(processId);
        getForegroundWindow ??= Native.GetForegroundWindow;

        var pending = new TargetAuthorization(IntPtr.Zero, 0, false);
        Volatile.Write(ref _targetAuthorization, pending);

        if (window == IntPtr.Zero)
        {
            return;
        }

        uint processId = processIdFor(window);
        if (processId == 0)
        {
            return;
        }

        void CheckAuthorization()
        {
            // Off the hook's path: this runs once per foreground change, on the thread pool.
            bool canDrive = canDriveWindow(window, processId);
            if (!ReferenceEquals(pending, Volatile.Read(ref _targetAuthorization)))
            {
                return;
            }

            if (getForegroundWindow() != window)
            {
                // Left while it was checked. Should it come back without a foreground event, a key there asks again.
                _ = Interlocked.CompareExchange(ref _requestedAuthorizationWindow, IntPtr.Zero, window);
                return;
            }

            if (processIdFor(window) == processId)
            {
                // A new request can begin after the check above. Only this request's own pending state may be
                // replaced, so an older result cannot overwrite a newer check, even for the same window and process.
                _ = Interlocked.CompareExchange(ref _targetAuthorization, new TargetAuthorization(window, processId, canDrive), pending);
            }
        }

        if (asynchronous)
        {
            ThreadPool.QueueUserWorkItem(_ => CheckAuthorization());
        }
        else
        {
            CheckAuthorization();
        }
    }

    internal static bool IsAuthorizedForeground(IntPtr foregroundWindow, uint foregroundProcessId, TargetAuthorization authorization)
        => foregroundWindow != IntPtr.Zero
            && foregroundProcessId != 0
            && authorization.Window == foregroundWindow
            && authorization.ProcessId == foregroundProcessId
            && authorization.CanDrive;

    /// <summary>
    /// What the hook does with a key typed in <paramref name="foregroundWindow"/>: drops it in a window that was refused,
    /// keeps it without expanding anything while the window's check runs (<paramref name="requestedWindow"/>), and
    /// expands only in a window that passed.
    /// </summary>
    internal static ForegroundCapture CaptureFor(IntPtr foregroundWindow, uint foregroundProcessId, TargetAuthorization authorization, IntPtr requestedWindow)
    {
        if (IsAuthorizedForeground(foregroundWindow, foregroundProcessId, authorization))
        {
            return ForegroundCapture.Authorized;
        }

        return foregroundWindow != IntPtr.Zero
            && foregroundProcessId != 0
            && authorization.Window != foregroundWindow
            && requestedWindow == foregroundWindow
            ? ForegroundCapture.Pending
            : ForegroundCapture.Refused;
    }

    /// <summary>Whether <paramref name="target"/> is in front and our input reaches it, checked as the worker does.</summary>
    private static bool TargetInFront(IntPtr target)
        => target != IntPtr.Zero && Native.GetForegroundWindow() == target && InputTarget.CanDrive(target);

    /// <summary>
    /// Gets a value indicating whether the window in front has never been checked and isn't being
    /// checked already. A window that was checked and refused is not asked about again.
    /// </summary>
    internal static bool NeedsAuthorization(IntPtr foregroundWindow, TargetAuthorization authorization, IntPtr requestedWindow)
        => foregroundWindow != IntPtr.Zero
            && authorization.Window != foregroundWindow
            && requestedWindow != foregroundWindow;

    public void Dispose()
    {
        lock (_bufferLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Pending.CompleteAdding();
            ClearBufferLocked();
        }

        // Release-only cleanup runs on workers. Keep this hook installed and pump sent messages until those calls
        // return, so a newer physical modifier event can still invalidate a stale release before it reaches Windows.
        Guard.Dispose(_hookHandle != IntPtr.Zero ? PumpSentMessages : null);
        if (_hookHandle != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        if (_mouseHookHandle != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_mouseHookHandle);
            _mouseHookHandle = IntPtr.Zero;
        }

        if (_winEventHandle != IntPtr.Zero)
        {
            Native.UnhookWinEvent(_winEventHandle);
            _winEventHandle = IntPtr.Zero;
        }
    }

    private static void PumpSentMessages() => _ = Native.PeekMessageW(out _, IntPtr.Zero, 0, 0, Native.PM_QS_SENDMESSAGE);
}
