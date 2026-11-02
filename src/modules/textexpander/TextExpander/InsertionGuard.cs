// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Keeps the user's keystrokes out of an insertion. SendInput doesn't keep a batch together while any low-level
/// keyboard hook is installed, and ours always is: a key typed meanwhile landed inside the paste's Ctrl (Ctrl+A), or
/// went in just before the erase and was erased in place of the trigger. So the worker brackets each insertion with
/// markers, and the keyboard hook, which sees every key event in the order the app will, guards it:
/// <list type="bullet">
/// <item>At the start marker it decides whether the insertion still goes ahead: not if a key other than a modifier, or a
/// click, reached the app since the trigger matched. Either way, every key event that isn't ours is held from then on,
/// in order. Clicks aren't held.</item>
/// <item>Once the hook has the end marker, the worker sends the held keys back, after the modifiers it let go of, with
/// another marker after them; then what was held meanwhile, until a round brings nothing. While they go back, only they
/// pass, in order, and what Keyboard Manager sends in place of one. Anything else, typed or injected, waits for the next
/// round, so a key-up never overtakes its own key-down.</item>
/// <item>Held keys only go back to the window they were typed for: with another one in front, they're dropped.</item>
/// </list>
/// The hook only holds keys and takes markers. SendInput returns once every low-level hook has seen each event, so sent
/// from the hook it nested, and a slow hook elsewhere could make Windows remove ours. The guard never keeps the keyboard
/// for long. It ends by itself with no event of ours or renewal for <see cref="TimeoutMs"/>, or with <see cref="MaxHeld"/>
/// key events held. The worker ends it once typing has held keys for <see cref="HoldCapMs"/>, and renews it while a paste
/// settles, for <see cref="Injector.PasteBusyCapMs"/> at most.
/// </summary>
internal sealed class InsertionGuard : IDisposable
{
    /// <summary>
    /// The virtual key the markers use, sent as a key-up with <see cref="KeyboardHook.InjectionSignature"/> and the
    /// insertion's id as its scan code. No key has it, and no app acts on it: the runner and Keyboard Manager send it
    /// themselves, so Start doesn't open when Win is let go of. Neither's hook acts on it, and ours swallows every marker.
    /// </summary>
    internal const int MarkerVk = 0xFF;

    /// <summary>How long the guard lasts with no event of ours or renewal, and the worker waits for the hook to take a marker.</summary>
    internal const int TimeoutMs = 2_000;

    /// <summary>How long the worker lets the guard hold keys while it types: a longer insertion goes on unguarded.</summary>
    internal const int HoldCapMs = 2_000;

    /// <summary>The most key events held: reaching it ends the guard.</summary>
    internal const int MaxHeld = 256;

    /// <summary>The flag on injected input, <c>LLKHF_INJECTED</c>.</summary>
    private const uint Injected = 0x10;

    private readonly object _lock = new();
    private readonly Func<long> _inputGeneration;
    private readonly Func<long> _clicks;
    private readonly Timer _timer;

    // Under _lock. The insertion being sent: the id its markers carry, the input generation of its trigger, its window,
    // and how many of its markers the hook has taken; over once it's done, or while none is being sent.
    private byte _id;
    private long _generation;
    private IntPtr _target;
    private int _markers;
    private bool _over = true;
    private bool _committed;
    private bool _cancelled;

    // Under _lock. What's held, since when, and until when with no event of ours; while held keys go back, those sent
    // this round, and how many of them have come through.
    private List<Native.KBDLLHOOKSTRUCT> _held = [];
    private List<Native.KBDLLHOOKSTRUCT> _expected = [];
    private int _next;
    private long _holdStart;
    private long _deadline;
    private volatile Mode _mode;

    public InsertionGuard(Func<long> inputGeneration, Func<IntPtr, bool> targetInFront, Func<long>? clicks = null)
    {
        _inputGeneration = inputGeneration;
        _clicks = clicks ?? (static () => 0);
        TargetInFront = targetInFront;
        _timer = new Timer(_ => Expire());
    }

    private enum Mode
    {
        None,
        Holding,
        Draining,
    }

    /// <summary>Gets or sets the clock, in milliseconds, that the guard's limits are measured on.</summary>
    internal Func<long> Clock { get; set; } = static () => Environment.TickCount64;

    /// <summary>
    /// Gets or sets how held keys are sent back. Until the hook is installed no real key can be held, so nothing is
    /// sent; tests capture them.
    /// </summary>
    internal Func<Native.INPUT[], uint> Resend { get; set; } = static _ => 0;

    /// <summary>Gets or sets whether a window is in front and may be driven: held keys go back only to their own.</summary>
    internal Func<IntPtr, bool> TargetInFront { get; set; }

    /// <summary>Gets or sets how long the worker waits for the hook to take a marker before it carries on without.</summary>
    internal int MarkerWaitMs { get; set; } = TimeoutMs;

    /// <summary>Gets a value indicating whether the insertion's window lost the front once it started: it stays cancelled.</summary>
    public bool Cancelled
    {
        get
        {
            lock (_lock)
            {
                return _cancelled;
            }
        }
    }

    /// <summary>Gets a value indicating whether keys have been held for <see cref="HoldCapMs"/>, time to let them go.</summary>
    public bool Overdue
    {
        get
        {
            lock (_lock)
            {
                return _mode == Mode.Holding && Clock() - _holdStart >= HoldCapMs;
            }
        }
    }

    /// <summary>Gets the input generation now.</summary>
    public long InputGeneration => _inputGeneration();

    /// <summary>
    /// Gets how many clicks have reached apps. Clicks aren't held, and unlike the input generation, the keys held and
    /// sent back never move it.
    /// </summary>
    public long Clicks => _clicks();

    /// <summary>
    /// Whether no key other than a modifier, and no click, has reached the app since the input generation was
    /// <paramref name="generation"/>, so the caret is still where the trigger was typed.
    /// </summary>
    public bool Unchanged(long generation) => _inputGeneration() == generation;

    /// <summary>
    /// Checked right before the erase: whether the guard is still on, renewing its deadline so it can't run out before
    /// the erase goes out. A slow clipboard borrow can outlast it. True too when the insertion goes in unguarded because
    /// the start marker never came. Renewed too while a paste settles, which sends nothing of ours.
    /// </summary>
    public bool Renew()
    {
        lock (_lock)
        {
            _deadline = Clock() + TimeoutMs;
            return _markers == 0 || !_over;
        }
    }

    /// <summary>
    /// Starts holding keys for an insertion into <paramref name="target"/> that hasn't begun: once a prompt's answer is
    /// accepted, what's typed right away is meant for after the expansion.
    /// </summary>
    public void Hold(IntPtr target)
    {
        lock (_lock)
        {
            _target = target;
            StartHoldingLocked();
        }
    }

    /// <summary>
    /// Readies the guard for an insertion into <paramref name="target"/>, for a trigger matched at input generation
    /// <paramref name="generation"/>, and returns the id its markers carry.
    /// </summary>
    public byte Begin(long generation, IntPtr target)
    {
        lock (_lock)
        {
            // A fresh id, so a marker of an earlier insertion arriving late is told apart. A scan code carries one byte.
            _id = (byte)((_id % 255) + 1);
            _generation = generation;
            _target = target;
            _markers = 0;
            _over = false;
            _cancelled = false;
            return _id;
        }
    }

    /// <summary>
    /// Waits for the hook to take the start marker, and returns whether the insertion goes ahead: not if the guard ended
    /// first, since what it held has gone back where the erase would take it. If the marker doesn't come, because another
    /// hook swallowed it or ours is gone, the guard ends, and the insertion goes in unguarded only if nothing was held.
    /// </summary>
    public bool AwaitStart()
    {
        bool unguarded;
        lock (_lock)
        {
            if (Wait(MarkerWaitMs, () => _markers > 0 || _over))
            {
                return !_over && _committed;
            }

            unguarded = _held.Count == 0;
        }

        Abandon();
        return unguarded;
    }

    /// <summary>
    /// Once the hook has the end marker, waited for at most <see cref="MarkerWaitMs"/> if <paramref name="markerSent"/>,
    /// sends back what it held, after <paramref name="pressAgain"/>, the modifiers let go of, with a marker after them;
    /// once that one is in, what was held meanwhile, and so on until nothing is. With another window in front, what's
    /// held is dropped instead. If a marker doesn't come, the guard ends here.
    /// </summary>
    public void AwaitEnd(bool markerSent, Native.INPUT[] pressAgain)
    {
        int awaited = 2;
        while (true)
        {
            Native.INPUT[] batch;
            IntPtr target;
            lock (_lock)
            {
                if (!Wait(markerSent ? MarkerWaitMs : 0, () => _over || _markers >= awaited))
                {
                    break;
                }

                if (_over)
                {
                    return;
                }

                if (_held.Count == 0 && pressAgain.Length == 0)
                {
                    Zero(EndLocked());
                    return;
                }

                // Kept, so the hook knows them coming back: zeroed once the next round or the end replaces them.
                _mode = Mode.Draining;
                Zero(_expected);
                _expected = _held;
                _held = [];
                _next = 0;
                batch = [.. pressAgain, .. _expected.Select(ToInput), Marker(_id)];
                target = _target;
                awaited = _markers + 1;
            }

            if (!TargetInFront(target))
            {
                Array.Clear(batch);
                Drop();
                return;
            }

            markerSent = Resend(batch) == (uint)batch.Length;
            Array.Clear(batch);
            pressAgain = [];
        }

        Abandon();
    }

    /// <summary>Ends the guard and drops what it held, zeroed: its window isn't in front, so it can't go back.</summary>
    public void Drop()
    {
        lock (_lock)
        {
            _cancelled = true;
            Zero(EndLocked());
        }
    }

    /// <summary>
    /// Ends the guard without its markers, as a fallback. Held keys go back as they came, with a key-up for any key
    /// still down but no modifier still down, if their window is in front; otherwise they're dropped. Does nothing
    /// without a guard.
    /// </summary>
    public void Abandon()
    {
        List<Native.KBDLLHOOKSTRUCT> held;
        IntPtr target;
        lock (_lock)
        {
            target = _target;
            held = EndLocked();
        }

        SendBack(held, target);
    }

    /// <summary>
    /// Takes a change of the window in front. Once an insertion has started, any window but its own ends the guard,
    /// drops what it held, and cancels the rest. Before then, as when a prompt hands focus back, the worker checks.
    /// </summary>
    public void ForegroundChanged(IntPtr window)
    {
        lock (_lock)
        {
            if (!_over && _markers > 0 && window != _target)
            {
                _cancelled = true;
                Zero(EndLocked());
            }
        }
    }

    /// <summary>
    /// The guard's part in the keyboard hook, ahead of everything else: whether a key event is swallowed. Markers always
    /// are. While guarding, so is every other key event that isn't ours, held to be sent back, but for the held keys
    /// coming back. It never sends anything or asks about a window: the worker and the timer do.
    /// </summary>
    public bool Swallows(Native.KBDLLHOOKSTRUCT key)
    {
        bool ours = key.DwExtraInfo == KeyboardHook.InjectionSignature;
        if (ours && key.VkCode == MarkerVk)
        {
            Mark((byte)key.ScanCode);
            return true;
        }

        // Input a lower-integrity app injected is never held: sent again, it would carry our integrity level.
        if (_mode == Mode.None || (key.Flags & Native.LLKHF_LOWER_IL_INJECTED) != 0)
        {
            return false;
        }

        lock (_lock)
        {
            if (ours)
            {
                _deadline = Clock() + TimeoutMs;
                return false;
            }

            if (_mode == Mode.None)
            {
                return false;
            }

            // While held keys go back, the next one of them passes, and so does what Keyboard Manager sends in place of
            // one: our hook usually runs before its, so a Caps Lock remapped to Ctrl comes back as an injected Ctrl.
            if (_mode == Mode.Draining && (key.Flags & Injected) != 0)
            {
                if (_next < _expected.Count && SameKey(_expected[_next], key))
                {
                    _next++;
                    return false;
                }

                if ((key.DwExtraInfo & HostInputTags.KeyboardManagerInjected) != 0)
                {
                    return false;
                }
            }

            // Anything else, typed or injected, waits: let through, a key-up overtook its own key-down going back.
            _held.Add(key);

            // Too many to hold: the timer ends the guard at once and sends them back.
            if (_held.Count == MaxHeld)
            {
                _timer.Change(0, Timeout.Infinite);
            }

            return true;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            Zero(EndLocked());
        }

        _timer.Dispose();
    }

    /// <summary>The marker of the insertion with id <paramref name="id"/>.</summary>
    internal static Native.INPUT Marker(byte id) => new()
    {
        Type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion
        {
            Ki = new Native.KEYBDINPUT
            {
                WVk = MarkerVk,
                WScan = id,
                DwFlags = Native.KEYEVENTF_KEYUP,
                DwExtraInfo = KeyboardHook.InjectionSignature,
            },
        },
    };

    /// <summary>
    /// The held key events to send back when the guard ends without its markers: as they came, with the same tag, but
    /// nothing comes back pressed. A key still down at the end gets a key-up (its real one, if it comes later, is a
    /// harmless extra), and a modifier still down is dropped, since its real key-up may already have gone by.
    /// </summary>
    internal static Native.INPUT[] ToResend(IReadOnlyList<Native.KBDLLHOOKSTRUCT> held)
    {
        // Walked backwards, so each key-down knows whether its key-up follows.
        Span<bool> released = stackalloc bool[256];
        var keys = new List<Native.INPUT>(held.Count);
        var keyUps = new List<Native.INPUT>();
        for (int i = held.Count - 1; i >= 0; i--)
        {
            Native.KBDLLHOOKSTRUCT key = held[i];
            int vk = (int)(key.VkCode & 0xFF);
            if ((key.Flags & Native.LLKHF_UP) == 0 && !released[vk])
            {
                if (KeyboardHook.IsModifierKey(key.VkCode))
                {
                    continue;
                }

                keyUps.Add(ToInput(key with { Flags = key.Flags | Native.LLKHF_UP }));
            }

            released[vk] = true;
            keys.Add(ToInput(key));
        }

        keys.Reverse();
        keyUps.Reverse();
        return [.. keys, .. keyUps];
    }

    /// <summary>
    /// Ends the guard once no event of ours or renewal has come for <see cref="TimeoutMs"/>, so a stalled worker can't keep
    /// the keyboard, or once <see cref="MaxHeld"/> key events are held. Run by a timer; tests call it.
    /// </summary>
    internal void Expire()
    {
        List<Native.KBDLLHOOKSTRUCT> held;
        IntPtr target;
        lock (_lock)
        {
            if (_mode == Mode.None)
            {
                return;
            }

            long left = _deadline - Clock();
            if (left > 0 && _held.Count < MaxHeld)
            {
                _timer.Change(left, Timeout.Infinite);
                return;
            }

            target = _target;
            held = EndLocked();
        }

        SendBack(held, target);
    }

    /// <summary>
    /// A held key event as it came, to send back: the same key, scan code and flags, and the same tag, so ordinary typing
    /// goes untagged and a key Keyboard Manager already remapped isn't remapped twice. SendInput sets the injected flag.
    /// </summary>
    private static Native.INPUT ToInput(Native.KBDLLHOOKSTRUCT key)
    {
        // A character another app typed, as the touch keyboard does, goes back as the character.
        bool packet = key.VkCode == Native.VK_PACKET;
        uint flags = packet ? Native.KEYEVENTF_UNICODE : (key.Flags & Native.LLKHF_EXTENDED) != 0 ? Native.KEYEVENTF_EXTENDEDKEY : 0;
        if ((key.Flags & Native.LLKHF_UP) != 0)
        {
            flags |= Native.KEYEVENTF_KEYUP;
        }

        return new Native.INPUT
        {
            Type = Native.INPUT_KEYBOARD,
            U = new Native.InputUnion
            {
                Ki = new Native.KEYBDINPUT
                {
                    WVk = packet ? (ushort)0 : (ushort)key.VkCode,
                    WScan = (ushort)key.ScanCode,
                    DwFlags = flags,
                    DwExtraInfo = key.DwExtraInfo,
                },
            },
        };
    }

    /// <summary>Whether <paramref name="key"/> is <paramref name="sent"/> coming back. Its other flags are the system's.</summary>
    private static bool SameKey(Native.KBDLLHOOKSTRUCT sent, Native.KBDLLHOOKSTRUCT key)
        => sent.VkCode == key.VkCode
            && sent.ScanCode == key.ScanCode
            && sent.DwExtraInfo == key.DwExtraInfo
            && ((sent.Flags ^ key.Flags) & (Native.LLKHF_EXTENDED | Native.LLKHF_UP)) == 0;

    /// <summary>Keystrokes don't stay behind in memory.</summary>
    private static void Zero(List<Native.KBDLLHOOKSTRUCT> keys)
    {
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(keys).Clear();
        keys.Clear();
    }

    /// <summary>Takes a marker the hook saw, and wakes the worker: the start marker decides, and the worker sends what's held.</summary>
    private void Mark(byte id)
    {
        lock (_lock)
        {
            // A marker of an insertion that's over, arriving late.
            if (_over || id != _id)
            {
                return;
            }

            if (++_markers == 1)
            {
                // The decision that counts: the hook has seen every key and click that reached the app before this.
                _committed = Unchanged(_generation);
                if (_mode == Mode.None)
                {
                    StartHoldingLocked();
                }
            }

            _deadline = Clock() + TimeoutMs;
            Monitor.PulseAll(_lock);
        }
    }

    /// <summary>Called under <c>_lock</c>: holds from now on, for at most <see cref="TimeoutMs"/> without an event of ours or renewal.</summary>
    private void StartHoldingLocked()
    {
        _mode = Mode.Holding;
        _holdStart = Clock();
        _deadline = _holdStart + TimeoutMs;
        _timer.Change(TimeoutMs, Timeout.Infinite);
    }

    /// <summary>Called under <c>_lock</c>: ends the guard and the insertion, wakes the worker, and returns what was held.</summary>
    private List<Native.KBDLLHOOKSTRUCT> EndLocked()
    {
        _mode = Mode.None;
        _over = true;
        Zero(_expected);
        List<Native.KBDLLHOOKSTRUCT> held = _held;
        _held = [];
        Monitor.PulseAll(_lock);
        return held;
    }

    /// <summary>Sends <paramref name="held"/> back as <see cref="ToResend"/> does if <paramref name="target"/> is in front, and zeroes it.</summary>
    private void SendBack(List<Native.KBDLLHOOKSTRUCT> held, IntPtr target)
    {
        if (held.Count > 0 && TargetInFront(target))
        {
            Native.INPUT[] resend = ToResend(held);
            _ = Resend(resend);
            Array.Clear(resend);
        }

        Zero(held);
    }

    /// <summary>Called under <c>_lock</c>: waits up to <paramref name="timeoutMs"/> for <paramref name="done"/>.</summary>
    private bool Wait(int timeoutMs, Func<bool> done)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (!done())
        {
            long left = deadline - Environment.TickCount64;
            if (left <= 0)
            {
                return false;
            }

            Monitor.Wait(_lock, (int)left);
        }

        return true;
    }
}
