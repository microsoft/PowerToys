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
/// Matching is therefore done inline here because it is cheap, while the expansion — which
/// deliberately sleeps between injected characters — is handed to a dedicated worker thread.
/// Posting it back to this thread's own message loop would not help: the blocking would simply
/// move from inside the callback to just after it, still starving the next hook dispatch.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    /// <summary>Tags our own synthetic input so we never react to it.</summary>
    public static readonly IntPtr InjectionSignature = new(0x47544558); // "GTEX"

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
    private long _authorizationGeneration;

    // The window the hook last asked to have checked, so it asks once per window rather than per key.
    private IntPtr _requestedAuthorizationWindow;

    private IntPtr _hookHandle;
    private IntPtr _mouseHookHandle;
    private IntPtr _winEventHandle;

    // Grows with the longest trigger (EnsureCapacity); guarded by _bufferLock.
    private char[] _buffer = new char[MinimumBufferCapacity];
    private int _count;
    private bool _disposed;
    private UndoEntry? _undo;
    private long _undoGeneration;

    /// <summary>Gets triggers detected but not yet expanded. Drained by the expansion worker.</summary>
    public BlockingCollection<PendingExpansion> Pending { get; } = new();

    private volatile bool _enabled = true;
    private volatile bool _suspended;

    private sealed record UndoEntry(string Trigger, string Replacement, IntPtr TargetWindow);

    internal sealed record TargetAuthorization(IntPtr Window, uint ProcessId, bool CanDrive);

    internal long UndoGeneration => Volatile.Read(ref _undoGeneration);

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
    /// Gets or sets a value indicating whether set while our own UI has focus so we ignore the user typing into it. Also erases the
    /// buffer, for the same reason as <see cref="Enabled"/>: what was typed before a prompt
    /// dialog opened is not needed after it.
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

    public KeyboardHook(SnippetStore store)
    {
        _store = store;
        _hookProc = HookCallback;
        _mouseHookProc = MouseCallback;
        _winEventProc = ForegroundChanged;
    }

    public bool Install()
    {
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
        if (live > 0)
        {
            Array.Clear(_buffer, 0, live);
        }
    }

    // ── Hook callback ────────────────────────────────────────
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != Native.HC_ACTION)
        {
            return Native.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        int message = wParam.ToInt32();
        if (message != Native.WM_KEYDOWN && message != Native.WM_SYSKEYDOWN)
        {
            return Native.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        Native.KBDLLHOOKSTRUCT data = System.Runtime.InteropServices.Marshal
            .PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);

        // Ignore input we synthesised ourselves — which is what prevents an expansion from
        // re-triggering — and the synthetic input PowerToys' own hooks replay. Deliberately NOT
        // filtering on LLKHF_INJECTED generally: the Windows touch keyboard, on-screen keyboard
        // and remote-desktop clients all inject their input, and blanket-filtering would silently
        // break expansion on 2-in-1 ARM devices. See HostInputTags for which tags are honoured.
        if (HostInputTags.ShouldIgnore(data.DwExtraInfo, InjectionSignature))
        {
            return Native.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        if (Suspended)
        {
            return Native.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        if (HandleKey(data))
        {
            return new IntPtr(1); // swallow the final trigger character
        }

        return Native.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    /// <summary>
    /// Clears the typed-character buffer on any mouse button press, because a click can move the
    /// caret inside the current window without ever raising a foreground-change event.
    ///
    /// This runs for every mouse event system-wide, so it must be as cheap as the keyboard
    /// callback: if this thread misses the 300 ms LowLevelHooksTimeout the hook is silently
    /// removed. We therefore read only wParam to identify the message and never marshal the
    /// MSLLHOOKSTRUCT behind lParam. Mouse input is never swallowed — the result of
    /// CallNextHookEx is always returned.
    /// </summary>
    private IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == Native.HC_ACTION && !Suspended)
        {
            // Only button-down messages reposition the caret. Wheel and WM_MOUSEMOVE do not, and
            // clearing on every move would be both wrong and far too expensive for this hook.
            switch (wParam.ToInt32())
            {
                case Native.WM_LBUTTONDOWN:
                case Native.WM_RBUTTONDOWN:
                case Native.WM_MBUTTONDOWN:
                case Native.WM_NCLBUTTONDOWN:
                case Native.WM_XBUTTONDOWN:
                    ResetBuffer();
                    ClearUndo();
                    break;
            }
        }

        return Native.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
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
        if (!CanCaptureForForeground())
        {
            ResetBuffer();
            ClearUndo();
            return false;
        }

        if (IsFocusedPasswordEdit())
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
            win: IsDown(Native.VK_LWIN) || IsDown(Native.VK_RWIN));
    }

    /// <summary>Processes a key-down that passed the capture checks, so tests can drive key sequences.</summary>
    internal bool ProcessKey(uint vk, uint scanCode, bool ctrl, bool alt, bool shift, bool win)
    {
        lock (_bufferLock)
        {
            return !_disposed && ProcessKeyLocked(vk, scanCode, ctrl, alt, shift, win);
        }
    }

    private bool ProcessKeyLocked(uint vk, uint scanCode, bool ctrl, bool alt, bool shift, bool win)
    {
        if (vk == Native.VK_BACK)
        {
            ClearUndo();

            // Overwritten, not just uncounted: the character the user deleted is exactly the kind
            // they most often meant to take back.
            if (_count > 0)
            {
                _buffer[--_count] = '\0';
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
                Pending.Add(new PendingExpansion(undo.Trigger, string.Empty, undo.Replacement, undo.TargetWindow, IsUndo: true));
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

        if (!TryDecode(vk, scanCode, out char ch))
        {
            return false;
        }

        SnippetStore.Snapshot snapshot = _store.Current;
        EnsureCapacity(snapshot.MaxTriggerLength);
        Append(ch);

        if (!Enabled)
        {
            return false;
        }

        if (snapshot.MaxTriggerLength == 0)
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

        int tailLength = Math.Min(usable, snapshot.MaxTriggerLength);
        string? trigger = snapshot.Triggers.MatchLongestSuffix(_buffer.AsSpan(usable - tailLength, tailLength));
        if (trigger is null)
        {
            return false;
        }

        // A trigger sitting inside a longer word is not the word the user typed. Only checked in
        // word mode: without it, holding back the expansion until a space would still let "btw"
        // fire at the end of "abtw", which is the exact accident this setting exists to prevent.
        if (wordMode && !WordBoundary.StartsAtWordBoundary(_buffer.AsSpan(0, usable - trigger.Length), trigger))
        {
            return false;
        }

        // Resolve against the same snapshot we matched against. A reload between here and the
        // worker running would otherwise leave the swallowed keystroke unaccounted for.
        if (!snapshot.Map.TryGetValue(trigger, out string? replacement))
        {
            return false;
        }

        ResetBuffer();

        string typedPrefix = wordMode ? trigger : trigger[..^1];
        string expandedText = wordMode && WordBoundary.KeepTerminator ? replacement + ch : replacement;
        string? typedText = wordMode ? trigger + ch : null;

        Pending.Add(new PendingExpansion(
            trigger,
            typedPrefix,
            expandedText,
            Native.GetForegroundWindow(),
            TypedText: typedText,
            UndoGeneration: UndoGeneration));
        return true;
    }

    public bool RecordExpansion(string trigger, string replacement, IntPtr targetWindow)
        => RecordExpansion(trigger, replacement, targetWindow, UndoGeneration);

    internal bool RecordExpansion(string trigger, string replacement, IntPtr targetWindow, long undoGeneration)
    {
        lock (_undoLock)
        {
            if (undoGeneration != Volatile.Read(ref _undoGeneration))
            {
                return false;
            }

            _undo = new UndoEntry(trigger, replacement, targetWindow);
            return true;
        }
    }

    public void ClearUndo()
    {
        Interlocked.Increment(ref _undoGeneration);
        lock (_undoLock)
        {
            _undo = null;
        }
    }

    /// <summary>
    /// Translates a virtual key to a character using the *foreground window's* keyboard layout.
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
    private static unsafe bool TryDecode(uint vk, uint scanCode, out char ch)
    {
        ch = '\0';

        // Unicode-injected input (touch keyboard, emoji picker, remote desktop, automation)
        // carries the character in scanCode rather than a real virtual key, and ToUnicodeEx
        // cannot translate it.
        if (vk == Native.VK_PACKET)
        {
            char packet = (char)scanCode;
            if (packet == '\0' || char.IsControl(packet))
            {
                return false;
            }

            ch = packet;
            return true;
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

        Span<char> buffer = stackalloc char[8];
        int written;
        fixed (byte* statePtr = state)
        {
            fixed (char* bufferPtr = buffer)
        {
            written = Native.ToUnicodeEx(vk, scanCode, statePtr, bufferPtr, buffer.Length, Native.TOUNICODE_NO_STATE_CHANGE, layout);
        }
        }

        // written < 0 is a dead key; the composed character arrives later and we cannot observe
        // it reliably from here, so the buffer is dropped rather than corrupted.
        if (written <= 0)
        {
            return false;
        }

        char candidate = buffer[written - 1];
        if (char.IsControl(candidate))
        {
            return false;
        }

        ch = candidate;
        return true;
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

    private void ForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        ResetBuffer();
        ClearUndo();
        _requestedAuthorizationWindow = IntPtr.Zero;
        RefreshTargetAuthorization(Native.GetForegroundWindow(), asynchronous: true);
    }

    private bool CanCaptureForForeground()
    {
        IntPtr foreground = Native.GetForegroundWindow();
        uint foregroundProcessId = 0;
        uint thread = foreground == IntPtr.Zero
            ? 0
            : Native.GetWindowThreadProcessId(foreground, out foregroundProcessId);
        if (thread == 0 || foregroundProcessId == 0)
        {
            return false;
        }

        TargetAuthorization authorization = Volatile.Read(ref _targetAuthorization);
        if (IsAuthorizedForeground(foreground, foregroundProcessId, authorization))
        {
            return true;
        }

        // A window activated through AttachThreadInput, as WindowFocus.Force does when a prompt
        // hands focus back, raises the foreground event while GetForegroundWindow still reports
        // the window it is leaving, so that event checks the wrong window. Check the one actually
        // in front instead, off this path and once, so typing there expands again.
        if (NeedsAuthorization(foreground, authorization, _requestedAuthorizationWindow))
        {
            _requestedAuthorizationWindow = foreground;
            RefreshTargetAuthorization(foreground, asynchronous: true);
        }

        return false;
    }

    private void RefreshTargetAuthorization(IntPtr window, bool asynchronous)
    {
        long generation = Interlocked.Increment(ref _authorizationGeneration);
        Volatile.Write(ref _targetAuthorization, new TargetAuthorization(IntPtr.Zero, 0, false));

        if (window == IntPtr.Zero)
        {
            return;
        }

        uint thread = Native.GetWindowThreadProcessId(window, out uint processId);
        if (thread == 0 || processId == 0)
        {
            return;
        }

        void CheckAuthorization()
        {
            // Off the hook's path: this runs once per foreground change, on the thread pool.
            bool canDrive = InputTarget.CanDrive(window) && !InputTarget.IsPowerToysSettings(processId);
            if (generation != Volatile.Read(ref _authorizationGeneration)
                || Native.GetForegroundWindow() != window)
            {
                return;
            }

            uint currentThread = Native.GetWindowThreadProcessId(window, out uint currentProcessId);
            if (currentThread != 0 && currentProcessId == processId)
            {
                Volatile.Write(ref _targetAuthorization, new TargetAuthorization(window, processId, canDrive));
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
    /// Gets a value indicating whether the window in front has never been checked and isn't being
    /// checked already. A window that was checked and refused is not asked about again.
    /// </summary>
    internal static bool NeedsAuthorization(IntPtr foregroundWindow, TargetAuthorization authorization, IntPtr requestedWindow)
        => foregroundWindow != IntPtr.Zero
            && authorization.Window != foregroundWindow
            && requestedWindow != foregroundWindow;

    public void Dispose()
    {
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
    }
}
