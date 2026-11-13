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
/// <item>From the match itself, the hook holds every key event that isn't ours, after the key it swallowed
/// (<see cref="HoldAfterMatch"/>): a key typed straight after a trigger used to reach the app before the start marker,
/// which dropped the insertion and lost the swallowed key. The insertion takes those keys over when it begins.</item>
/// <item>At the start marker it decides whether the insertion still goes ahead: not if a key other than a modifier, or a
/// click, reached the app since the trigger matched. Either way, every key event that isn't ours is held from then on,
/// in order. Clicks aren't held. If it goes ahead, the swallowed key isn't sent back: the snippet takes its place, or the
/// worker gives it back after the snippet. If not, it goes back first, so what was typed stays as typed.</item>
/// <item>Once the hook has the end marker, the worker sends the held keys back, after the modifiers it let go of, with
/// another marker after them; then what was held meanwhile, until a round brings nothing. While they go back, only they
/// pass, in order, and what Keyboard Manager sends in place of one. Anything else, typed or injected, waits for the next
/// round, so a key-up never overtakes its own key-down.</item>
/// <item>Held text and presses only go back to the window they were typed for. On discard, a held modifier release whose
/// down already passed is finished by a worker. The hook rejects that cleanup if newer modifier input has arrived.</item>
/// </list>
/// The hook only holds keys and takes markers. Sending from the hook can nest its input handling and let a slow hook
/// elsewhere make Windows remove ours. Markers, not SendInput returning, confirm the drain. The guard never keeps the keyboard
/// for long. It ends by itself with no event of ours or renewal for <see cref="TimeoutMs"/>, or with <see cref="MaxHeld"/>
/// key events held, and so does a hold since a match the worker hasn't taken, <see cref="TimeoutMs"/> after a key press
/// or a release paired with a modifier Windows already received. The worker ends it once typing has held keys for <see cref="HoldCapMs"/>, and renews it while
/// a paste settles, for <see cref="Injector.PasteBusyCapMs"/> at most.
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

    // Distinct from our normal tag and the host's small tags, with Keyboard Manager's injected bit preserved.
    private const int CleanupTagBits = HostInputTags.KeyboardManagerInjected | 0x2;

    /// <summary>The deadline of a hold since a match with no key pressed since it: there's none.</summary>
    private const long NoDeadline = long.MaxValue;

    private readonly object _lock = new();
    private readonly Func<long> _inputGeneration;
    private readonly Func<long> _clicks;
    private readonly Timer _timer;

    // Under _lock. Only a modifier down that passed this hook can need a release after held input is discarded.
    // The event generation invalidates a queued release when newer input arrives. The external generation keeps
    // the original release's identity as held input is replayed or taken over by another match.
    private readonly bool[] _passedModifierDown = new bool[256];
    private readonly long[] _modifierGeneration = new long[256];
    private readonly long[] _externalModifierGeneration = new long[256];
    private readonly List<HeldModifierRelease> _modifierReleases = [];
    private readonly Dictionary<IntPtr, ModifierCleanup> _cleanup = [];
    private readonly List<ReplayBatch> _replays = [];
    private int _pendingCleanup;
    private int _resending;
    private bool _disposed;

    private sealed class HeldModifierRelease(List<Native.KBDLLHOOKSTRUCT> owner, int index, Native.KBDLLHOOKSTRUCT key, long generation)
    {
        public List<Native.KBDLLHOOKSTRUCT> Owner { get; set; } = owner;

        public int Index { get; set; } = index;

        public bool Discarded { get; set; }

        public Native.KBDLLHOOKSTRUCT Key { get; } = key;

        public long Generation { get; } = generation;
    }

    private sealed record ModifierCleanup(Native.KBDLLHOOKSTRUCT Key, long Generation);

    private sealed class ReplayBatch(Native.INPUT[] keys)
    {
        public Native.INPUT[] Keys { get; } = keys;

        public Dictionary<int, HeldModifierRelease> Releases { get; } = [];

        public int Next { get; set; }
    }

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

    // Kept through the drain's final marker: a mid-insertion replay changes the document but cannot start another match.
    private bool _matchReturnedKeys = true;

    // Under _lock. The keys held since a match the worker hasn't taken yet, the key the hook swallowed first; the match's
    // input generation, which the insertion that takes them over begins with; their window; and until when, once a key is
    // pressed since the match.
    // Kept apart from _held: a match among held keys going back holds what follows them for its own insertion.
    private List<Native.KBDLLHOOKSTRUCT> _afterMatch = [];
    private long _afterMatchGeneration;
    private IntPtr _afterMatchTarget;
    private long _afterMatchDeadline;
    private volatile bool _holdingAfterMatch;

    // Under _lock. Whether _held starts with the key the hook swallowed for this insertion's trigger, taken over from the
    // hold since the match: it goes back only if the insertion doesn't go ahead.
    private bool _swallowedFirst;

    // Under _lock. Whether the insertion is still waiting for its start marker; whether it goes in unguarded, decided when
    // the guard ended without that marker; and whether the keys held since its match went back to the app instead of to
    // it, for which the marker can't tell, since they can get to the hook after it.
    private bool _awaitingStart;
    private bool _unguarded;
    private bool _keysWentBack;

    // Under _lock. The input generation of the last match whose held keys went back to the app, as they ran out.
    private long? _afterMatchSentBack;

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

    /// <summary>Gets or sets the worker's final check that a modifier is still down in Windows before releasing it.</summary>
    internal Func<int, bool> ModifierIsDown { get; set; } = static vk => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;

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

    /// <summary>
    /// Gets a value indicating whether the user's keys are being held, so none reaches the app while the paste keeps its Ctrl
    /// down. Not while the insertion goes in unguarded.
    /// </summary>
    public bool Holding => _mode == Mode.Holding;

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
            if (_disposed)
            {
                return false;
            }

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
            if (_disposed)
            {
                return;
            }

            _target = target;
            StartHoldingLocked();
        }
    }

    /// <summary>
    /// Run in the hook as a trigger matches, before its work is queued: holds the user's keys from now on, after
    /// <paramref name="swallowed"/>, the key the hook swallowed, for the insertion into <paramref name="target"/> the worker
    /// begins with input generation <paramref name="generation"/>, which takes them over (<see cref="Begin"/>). Ends by
    /// itself <see cref="TimeoutMs"/> after the first key press or passed modifier's release since, or at <see cref="MaxHeld"/> key events, sending them
    /// back. With nothing pressed, it delays nobody, so it waits for the insertion, which a busy machine can be slow to
    /// begin. Returns false, holding nothing, while keys are held since another match: none reaches the matcher then but a
    /// lower-integrity app's. Arms no timer: the key that matched must be answered quickly, or Windows passes it on.
    /// </summary>
    public bool HoldAfterMatch(IntPtr target, long generation, Native.KBDLLHOOKSTRUCT swallowed)
    {
        lock (_lock)
        {
            if (_disposed || _holdingAfterMatch)
            {
                return false;
            }

            // Matching runs after the downstream hooks, so this event has already been remapped. Returning it must
            // change the input generation without firing the same trigger again, even if its hook callback arrives late.
            _afterMatch.Add(swallowed with { DwExtraInfo = HostInputTags.ReturnedTriggerTag(KeyboardHook.InjectionSignature) });
            _afterMatchGeneration = generation;
            _afterMatchTarget = target;
            _afterMatchDeadline = NoDeadline;
            _holdingAfterMatch = true;
            return true;
        }
    }

    /// <summary>
    /// Runs a hold since a match once, on a guard of its own, before the hook is installed: the first trigger the user types
    /// then compiles nothing, and starts no timer thread, on the hook's thread. Windows gives the hook about 300 ms a key,
    /// and passes on a key it doesn't answer in time; on a busy single-core machine, a first trigger came close.
    /// </summary>
    internal static void WarmUp()
    {
        using var guard = new InsertionGuard(static () => 0, static _ => false);
        var key = new Native.KBDLLHOOKSTRUCT { VkCode = Native.VK_PACKET };
        if (guard.HoldAfterMatch(IntPtr.Zero, 0, key))
        {
            _ = guard.Swallows(key);
            guard.ReleaseAfterMatch(0);
        }
    }

    /// <summary>
    /// Ends the hold since the match with input generation <paramref name="generation"/>, when no insertion took it over:
    /// the key the hook swallowed goes back, then the keys typed after it, if their window is in front. Does nothing for
    /// another match's.
    /// </summary>
    public void ReleaseAfterMatch(long generation)
    {
        List<Native.KBDLLHOOKSTRUCT> keys;
        IntPtr target;
        lock (_lock)
        {
            if (!_holdingAfterMatch || _afterMatchGeneration != generation)
            {
                return;
            }

            target = _afterMatchTarget;
            keys = TakeAfterMatchLocked();
        }

        SendBackAsTheyCame(keys, target);
    }

    /// <summary>
    /// Run before a window of ours opens for the match with input generation <paramref name="generation"/>. With no key
    /// but a modifier pressed since the swallowed one, ends its hold: what it held after that key goes back, so no key
    /// stays down, and the swallowed key stays with the worker, which gives it back after the window as before. A
    /// modifier held through the trigger repeats, which isn't typing on. Returns false, holding on, when keys were typed
    /// on: the window would take them, so the worker cancels the snippet and gives everything back as typed.
    /// </summary>
    public bool ReleaseAfterMatchBeforeAWindow(long generation)
    {
        List<Native.KBDLLHOOKSTRUCT> keys;
        IntPtr target;
        lock (_lock)
        {
            if (!_holdingAfterMatch || _afterMatchGeneration != generation)
            {
                return true;
            }

            for (int i = 1; i < _afterMatch.Count; i++)
            {
                if ((_afterMatch[i].Flags & Native.LLKHF_UP) == 0 && !KeyboardHook.IsModifierKey(_afterMatch[i].VkCode))
                {
                    return false;
                }
            }

            target = _afterMatchTarget;
            keys = TakeAfterMatchLocked();
            RemoveSwallowedLocked(keys);
        }

        SendBackAsTheyCame(keys, target);
        return true;
    }

    /// <summary>
    /// Readies the guard for an insertion into <paramref name="target"/>, for a trigger matched at input generation
    /// <paramref name="generation"/>, and returns the id its markers carry. The keys held since that match are its own from
    /// now on, the swallowed key first.
    /// </summary>
    public byte Begin(long generation, IntPtr target)
    {
        lock (_lock)
        {
            // Only the insertion worker calls Begin. Finish an older discarded release before it samples modifiers:
            // otherwise its temporary key-up could cancel cleanup and its later pressAgain leave that key down again.
            // A detached replay is included until it has either returned the up or decided to discard it.
            while (!_disposed && (_pendingCleanup != 0 || _modifierReleases.Any(release => release.Discarded
                || (!ReferenceEquals(release.Owner, _held) && !ReferenceEquals(release.Owner, _expected) && !ReferenceEquals(release.Owner, _afterMatch)))))
            {
                Monitor.Wait(_lock);
            }

            if (_disposed)
            {
                _unguarded = false;
                return _id;
            }

            // A fresh id, so a marker of an earlier insertion arriving late is told apart. A scan code carries one byte.
            _id = (byte)((_id % 255) + 1);
            _generation = generation;
            _target = target;
            _markers = 0;
            _over = false;
            _cancelled = false;
            _awaitingStart = true;
            _unguarded = false;

            // The keys held since its match ran out as the worker came to it: they're on their way to the app, and may get to
            // the hook after the start marker. It doesn't go ahead.
            _keysWentBack = _afterMatchSentBack == generation;
            if (_holdingAfterMatch && _afterMatchGeneration == generation)
            {
                // Holding first: without the lock, the hook checks for a hold since a match before it checks the mode.
                if (_mode == Mode.None)
                {
                    StartHoldingLocked();

                    // Typing holds keys for HoldCapMs at most, counted from the first key pressed since the match, since when
                    // the user has been waiting; with none, from now. Not from the match: a slow start would be overdue at once.
                    if (_afterMatchDeadline != NoDeadline)
                    {
                        _holdStart = _afterMatchDeadline - TimeoutMs;
                    }
                }

                List<Native.KBDLLHOOKSTRUCT> keys = TakeAfterMatchLocked();
                foreach (HeldModifierRelease release in _modifierReleases)
                {
                    if (ReferenceEquals(release.Owner, _held))
                    {
                        release.Owner = keys;
                        release.Index += keys.Count;
                    }
                }

                keys.AddRange(_held);
                Zero(_held);
                _held = keys;
                _swallowedFirst = true;
            }

            return _id;
        }
    }

    /// <summary>
    /// Waits for the hook to take the start marker, and returns whether the insertion goes ahead: not if the guard ended
    /// first, since what it held has gone back where the erase would take it. If the marker doesn't come, because another
    /// hook swallowed it or ours is gone, the guard ends, and the insertion goes in unguarded only if nothing was typed
    /// since its match (<see cref="EndWithoutMarkerLocked"/>), whether this wait or the timer ends it.
    /// </summary>
    public bool AwaitStart()
    {
        List<Native.KBDLLHOOKSTRUCT> held;
        IntPtr target;
        bool unguarded;
        lock (_lock)
        {
            if (Wait(MarkerWaitMs, () => _markers > 0 || _over))
            {
                return _over ? _unguarded : _committed;
            }

            target = _target;
            held = EndWithoutMarkerLocked(out unguarded);
        }

        SendBack(held, target);
        return unguarded;
    }

    /// <summary>
    /// Once the hook has the end marker, waited for at most <see cref="MarkerWaitMs"/> if <paramref name="markerSent"/>,
    /// sends back what it held, after <paramref name="pressAgain"/>, the modifiers let go of, with a marker after them;
    /// once that one is in, what was held meanwhile, and so on until nothing is. With another window in front, what's
    /// held is dropped instead. If a marker doesn't come, the guard ends here. Only a confirmed complete drain returns
    /// its input generation, captured before fresh input can pass. A mid-insertion drain sets
    /// <paramref name="matchReturnedKeys"/> false so its keys cannot queue work at a position the remainder of this
    /// insertion is about to change.
    /// </summary>
    public (bool Completed, long InputGeneration) AwaitEnd(bool markerSent, Native.INPUT[] pressAgain, bool matchReturnedKeys = true)
    {
        int awaited = 2;
        while (markerSent)
        {
            Native.INPUT[] batch;
            ReplayBatch replay;
            IntPtr target;
            lock (_lock)
            {
                if (!Wait(MarkerWaitMs, () => _over || _markers >= awaited))
                {
                    break;
                }

                if (_over)
                {
                    return (false, 0);
                }

                if (_held.Count == 0 && pressAgain.Length == 0)
                {
                    long generation = _inputGeneration();
                    DiscardLocked(EndLocked());
                    return (true, generation);
                }

                // Kept, so the hook knows them coming back: zeroed once the next round or the end replaces them.
                _matchReturnedKeys = matchReturnedKeys;
                _mode = Mode.Draining;
                DiscardLocked(_expected);
                _expected = _held;
                _held = [];
                _next = 0;
                batch = [.. pressAgain, .. _expected.Select(ToInput), Marker(_id)];
                replay = BeginResendLocked(batch, _expected);
                target = _target;
                awaited = _markers + 1;
            }

            try
            {
                if (!TargetInFront(target))
                {
                    Drop();
                    return (false, 0);
                }

                markerSent = Resend(batch) == (uint)batch.Length;
            }
            finally
            {
                EndResend(replay);
                Array.Clear(batch);
            }

            pressAgain = [];
        }

        Abandon();
        return (false, 0);
    }

    /// <summary>Ends the guard and drops what it held, zeroed: its window isn't in front, so it can't go back.</summary>
    public void Drop()
    {
        lock (_lock)
        {
            _cancelled = true;
            DiscardLocked(EndLocked());
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
    /// drops what it held, and cancels the rest. Before then, as when a prompt hands focus back, the worker checks. Keys
    /// held since a match are dropped too when another window comes to the front: they were typed for the match's.
    /// </summary>
    public void ForegroundChanged(IntPtr window)
    {
        lock (_lock)
        {
            if (!_over && _markers > 0 && window != _target)
            {
                _cancelled = true;
                DiscardLocked(EndLocked());
            }

            if (_holdingAfterMatch && window != _afterMatchTarget)
            {
                DiscardLocked(TakeAfterMatchLocked());
            }
        }
    }

    /// <summary>
    /// The guard's part in the keyboard hook, ahead of everything else: whether a key event is swallowed. Markers always
    /// are. While guarding, so is every other key event that isn't ours, held to be sent back, but for the held keys
    /// coming back. Since a match the worker hasn't taken, so is every one that gets past that. It never sends anything or
    /// asks about a window: the worker and the timer do.
    /// </summary>
    public bool Swallows(Native.KBDLLHOOKSTRUCT key) => Swallows(key, observePassed: true);

    internal bool Swallows(Native.KBDLLHOOKSTRUCT key, bool observePassed)
        => Swallows(key, observePassed, out _);

    internal bool Swallows(Native.KBDLLHOOKSTRUCT key, bool observePassed, out bool mayMatch)
    {
        mayMatch = true;
        if ((key.DwExtraInfo.ToInt64() & 0xFFF) == CleanupTagBits)
        {
            lock (_lock)
            {
                if (_cleanup.TryGetValue(key.DwExtraInfo, out ModifierCleanup? cleanup))
                {
                    // Checked here, at the same place as physical input, rather than only before SendInput: a newer
                    // modifier press can arrive while that call is waiting for this hook.
                    if (!CanReleaseLocked(key, cleanup))
                    {
                        return true;
                    }

                    if (observePassed)
                    {
                        PassedLocked(key);
                    }

                    return false;
                }
            }
        }

        bool replayed = false;
        HeldModifierRelease? release = null;
        if (Volatile.Read(ref _resending) != 0 && (key.Flags & (Injected | Native.LLKHF_LOWER_IL_INJECTED)) == Injected)
        {
            lock (_lock)
            {
                foreach (ReplayBatch replay in _replays)
                {
                    if (replay.Next < replay.Keys.Length && SameInput(replay.Keys[replay.Next], key))
                    {
                        replayed = true;
                        replay.Releases.TryGetValue(replay.Next++, out release);
                        break;
                    }
                }
            }
        }

        if (!KeyboardHook.IsModifierKey(key.VkCode) || key.DwExtraInfo == HostInputTags.KeyboardManagerSuppressed)
        {
            return SwallowsCore(key, out mayMatch);
        }

        lock (_lock)
        {
            int vk = ModifierKey(key);
            _modifierGeneration[vk]++;
            if (key.DwExtraInfo != KeyboardHook.InjectionSignature && !replayed)
            {
                _externalModifierGeneration[vk]++;
            }

            bool swallowed = SwallowsCore(key, out mayMatch, release);
            if (!swallowed)
            {
                if (observePassed)
                {
                    PassedLocked(key);
                }

                if (release is not null)
                {
                    _modifierReleases.Remove(release);
                }
            }

            return swallowed;
        }
    }

    /// <summary>
    /// Records a key only after the remaining hooks let it through. A remapped original may have passed our guard but
    /// been swallowed by Keyboard Manager. Tests that call <see cref="Swallows(Native.KBDLLHOOKSTRUCT)"/> directly model an unblocked next hook.
    /// </summary>
    internal bool Passed(Native.KBDLLHOOKSTRUCT key)
    {
        if (!KeyboardHook.IsModifierKey(key.VkCode) || key.DwExtraInfo == HostInputTags.KeyboardManagerSuppressed)
        {
            return true;
        }

        lock (_lock)
        {
            // Another hook can inject input reentrantly. A cleanup must still be current after CallNextHookEx returns.
            if (_cleanup.TryGetValue(key.DwExtraInfo, out ModifierCleanup? cleanup) && !CanReleaseLocked(key, cleanup))
            {
                return false;
            }

            PassedLocked(key);
            return true;
        }
    }

    private bool SwallowsCore(Native.KBDLLHOOKSTRUCT key, out bool mayMatch, HeldModifierRelease? replayedRelease = null)
    {
        mayMatch = true;
        bool ours = key.DwExtraInfo == KeyboardHook.InjectionSignature;
        if (ours && key.VkCode == MarkerVk)
        {
            Mark((byte)key.ScanCode);
            return true;
        }

        // Input a lower-integrity app injected is never held: sent again, it would carry our integrity level. The hold since
        // a match is read first: an insertion taking it over starts holding before it ends it.
        if ((!_holdingAfterMatch && _mode == Mode.None) || (key.Flags & Native.LLKHF_LOWER_IL_INJECTED) != 0)
        {
            return false;
        }

        lock (_lock)
        {
            if (ours)
            {
                if (_mode != Mode.None)
                {
                    _deadline = Clock() + TimeoutMs;
                }

                return false;
            }

            // While held keys go back, the next one of them passes, and so does what Keyboard Manager sends in place of
            // one: our hook usually runs before its, so a Caps Lock remapped to Ctrl comes back as an injected Ctrl.
            bool passes = _mode == Mode.None;
            if (_mode == Mode.Draining && (key.Flags & Injected) != 0)
            {
                if (_next < _expected.Count && SameKey(_expected[_next], key))
                {
                    _next++;
                    passes = true;
                }
                else if ((key.DwExtraInfo & HostInputTags.KeyboardManagerInjected) != 0)
                {
                    passes = true;
                }
            }

            // Anything else, typed or injected, waits: let through, a key-up overtook its own key-down going back.
            if (!passes)
            {
                HoldLocked(_held, key, replayedRelease);
                return true;
            }

            mayMatch = _mode != Mode.Draining || _matchReturnedKeys;

            // Held keys going back can set off a trigger: what comes after it waits for that insertion. A key press or
            // release of a modifier Windows still holds starts its time limit; the swallowed key's own up needn't.
            if (_holdingAfterMatch)
            {
                HoldLocked(_afterMatch, key, replayedRelease);
                bool isUp = (key.Flags & Native.LLKHF_UP) != 0;
                bool passedModifierUp = isUp && KeyboardHook.IsModifierKey(key.VkCode)
                    && _passedModifierDown[ModifierKey(key)];
                if (_afterMatchDeadline == NoDeadline && (!isUp || passedModifierUp))
                {
                    _afterMatchDeadline = Clock() + TimeoutMs;
                    ArmTimerLocked();
                }

                return true;
            }

            return false;
        }
    }

    public void Dispose() => Dispose(pump: null);

    /// <summary>
    /// Stops holding input and finishes release-only cleanup before the hook is removed. Its installing thread supplies
    /// a sent-message pump, so SendInput remains on the worker and its hook callbacks can finish while this waits.
    /// </summary>
    internal void Dispose(Action? pump)
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                _cancelled = true;
                DiscardLocked(EndLocked());
                DiscardLocked(TakeAfterMatchLocked());

                // A timer or worker may have detached a replay list and be checking its target. Its release records
                // stay here until that operation finishes; disposal takes them over before closing the send gate.
                foreach (HeldModifierRelease release in _modifierReleases)
                {
                    release.Discarded = true;
                }

                QueueDiscardedReleasesLocked();
                _disposed = true;
            }
        }

        _timer.Dispose();
        while (true)
        {
            lock (_lock)
            {
                QueueDiscardedReleasesLocked();
                if (_pendingCleanup == 0 && _resending == 0)
                {
                    Array.Clear(_passedModifierDown);
                    return;
                }

                Monitor.Wait(_lock, pump is null ? Timeout.Infinite : 1);
            }

            pump?.Invoke();
        }
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
    /// the keyboard, or once <see cref="MaxHeld"/> key events are held; and so, too, a hold since a match the worker hasn't
    /// taken. Run by a timer; tests call it.
    /// </summary>
    internal void Expire()
    {
        List<Native.KBDLLHOOKSTRUCT>? held = null;
        List<Native.KBDLLHOOKSTRUCT>? afterMatch = null;
        IntPtr target = IntPtr.Zero;
        IntPtr afterMatchTarget = IntPtr.Zero;
        lock (_lock)
        {
            long now = Clock();
            if (_mode != Mode.None && (_deadline - now <= 0 || _held.Count >= MaxHeld))
            {
                target = _target;

                // Still waiting for its start marker, the insertion may go in unguarded, as when the wait for it ends.
                held = _awaitingStart ? EndWithoutMarkerLocked(out _) : EndLocked();
            }

            if (_holdingAfterMatch && (_afterMatchDeadline - now <= 0 || _afterMatch.Count >= MaxHeld))
            {
                afterMatchTarget = _afterMatchTarget;
                _afterMatchSentBack = _afterMatchGeneration;
                afterMatch = TakeAfterMatchLocked();
            }

            ArmTimerLocked();
        }

        // Held since a match among keys going back, those were typed before what that insertion still held for later. With
        // the swallowed key first, they reach the app, so the insertion still to begin for that match won't go ahead.
        if (afterMatch is not null)
        {
            SendBackAsTheyCame(afterMatch, afterMatchTarget);
        }

        if (held is not null)
        {
            SendBack(held, target);
        }
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
                // The decision that counts: the hook has seen every key and click that reached the app before this. But not
                // the keys held since the match that ran out as the worker came to it: they may get here after this.
                _awaitingStart = false;
                _committed = Unchanged(_generation) && !_keysWentBack;
                if (_mode == Mode.None)
                {
                    StartHoldingLocked();
                }

                // Going ahead, the worker gives the swallowed key back after the snippet, or it's the trigger's own last
                // character. Not, it goes back first, with the keys typed after it, so what was typed stays as typed.
                if (_swallowedFirst)
                {
                    _swallowedFirst = false;
                    if (_committed && _held.Count > 0)
                    {
                        RemoveSwallowedLocked(_held);
                    }
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
        ArmTimerLocked();
    }

    /// <summary>Called under <c>_lock</c>: ends the guard and the insertion, wakes the worker, and returns what was held.</summary>
    private List<Native.KBDLLHOOKSTRUCT> EndLocked()
    {
        _mode = Mode.None;
        _over = true;
        _swallowedFirst = false;
        _awaitingStart = false;
        DiscardLocked(_expected);
        List<Native.KBDLLHOOKSTRUCT> held = _held;
        _held = [];
        Monitor.PulseAll(_lock);
        return held;
    }

    /// <summary>
    /// Called under <c>_lock</c> once the start marker didn't come in time, by the wait for it or by the timer: ends the
    /// guard, and returns what goes back. The insertion goes in unguarded only if nothing is where its erase would take it:
    /// nothing went back to the app already, and no key but a modifier was pressed since its match. The key the hook
    /// swallowed doesn't count, and doesn't go back: going ahead, the insertion takes its place or gives it back.
    /// </summary>
    private List<Native.KBDLLHOOKSTRUCT> EndWithoutMarkerLocked(out bool unguarded)
    {
        int first = _swallowedFirst ? 1 : 0;
        unguarded = !_keysWentBack;
        for (int i = first; unguarded && i < _held.Count; i++)
        {
            unguarded = (_held[i].Flags & Native.LLKHF_UP) != 0 || KeyboardHook.IsModifierKey(_held[i].VkCode);
        }

        if (unguarded && first == 1 && _held.Count > 0)
        {
            RemoveSwallowedLocked(_held);
        }

        _unguarded = unguarded;
        return EndLocked();
    }

    /// <summary>Called under <c>_lock</c>: ends the hold since a match, and returns what it held, the swallowed key first.</summary>
    private List<Native.KBDLLHOOKSTRUCT> TakeAfterMatchLocked()
    {
        _holdingAfterMatch = false;
        List<Native.KBDLLHOOKSTRUCT> keys = _afterMatch;
        _afterMatch = [];
        return keys;
    }

    /// <summary>Called under <c>_lock</c>: holds <paramref name="key"/> in <paramref name="keys"/>; with too many, the timer ends that hold at once.</summary>
    private void HoldLocked(List<Native.KBDLLHOOKSTRUCT> keys, Native.KBDLLHOOKSTRUCT key, HeldModifierRelease? replayedRelease = null)
    {
        int index = keys.Count;
        keys.Add(key);
        if (KeyboardHook.IsModifierKey(key.VkCode) && (key.Flags & Native.LLKHF_UP) != 0 && key.DwExtraInfo != HostInputTags.KeyboardManagerSuppressed)
        {
            if (replayedRelease is not null)
            {
                // A trigger among replayed keys takes this exact release over; the old insertion no longer owns it.
                replayedRelease.Owner = keys;
                replayedRelease.Index = index;
                replayedRelease.Discarded = false;
            }
            else
            {
                _modifierReleases.Add(new HeldModifierRelease(keys, index, key, _externalModifierGeneration[ModifierKey(key)]));
            }
        }

        if (keys.Count == MaxHeld)
        {
            _timer.Change(0, Timeout.Infinite);
        }
    }

    /// <summary>Called under <c>_lock</c>: sets the timer for the earlier of the guard's deadline and the hold since a match's.</summary>
    private void ArmTimerLocked()
    {
        long now = Clock();
        long due = long.MaxValue;
        if (_mode != Mode.None)
        {
            due = _deadline - now;
        }

        if (_holdingAfterMatch && _afterMatchDeadline != NoDeadline)
        {
            due = Math.Min(due, _afterMatchDeadline - now);
        }

        if (due != long.MaxValue)
        {
            _timer.Change((int)Math.Clamp(due, 0, TimeoutMs), Timeout.Infinite);
        }
    }

    /// <summary>Sends <paramref name="held"/> back as <see cref="ToResend"/> does if <paramref name="target"/> is in front, and zeroes it.</summary>
    private void SendBack(List<Native.KBDLLHOOKSTRUCT> held, IntPtr target)
    {
        Native.INPUT[] resend = [];
        try
        {
            if (held.Count > 0 && TargetInFront(target))
            {
                resend = ToResend(held);
                _ = ResendWhileAlive(resend, held);
            }
        }
        finally
        {
            Array.Clear(resend);
            lock (_lock)
            {
                DiscardLocked(held);
            }
        }
    }

    /// <summary>
    /// Sends keys held since a match back exactly as they came if <paramref name="target"/> is in front, and zeroes them.
    /// Held without a break since the match, so a key still down has its key-up still to come: unlike
    /// <see cref="ToResend"/>, nothing is added or left out, and a modifier the user still holds stays down.
    /// </summary>
    private void SendBackAsTheyCame(List<Native.KBDLLHOOKSTRUCT> keys, IntPtr target)
    {
        Native.INPUT[] resend = [];
        try
        {
            if (keys.Count > 0 && TargetInFront(target))
            {
                resend = new Native.INPUT[keys.Count];
                for (int i = 0; i < keys.Count; i++)
                {
                    resend[i] = ToInput(keys[i]);
                }

                _ = ResendWhileAlive(resend, keys);
            }
        }
        finally
        {
            Array.Clear(resend);
            lock (_lock)
            {
                DiscardLocked(keys);
            }
        }
    }

    private static int ModifierKey(Native.KBDLLHOOKSTRUCT key) => key.VkCode switch
    {
        Native.VK_SHIFT => key.ScanCode == 0x36 ? Native.VK_RSHIFT : Native.VK_LSHIFT,
        Native.VK_CONTROL => (key.Flags & Native.LLKHF_EXTENDED) != 0 ? Native.VK_RCONTROL : Native.VK_LCONTROL,
        Native.VK_MENU => (key.Flags & Native.LLKHF_EXTENDED) != 0 ? Native.VK_RMENU : Native.VK_LMENU,
        _ => (int)(key.VkCode & 0xFF),
    };

    private void PassedLocked(Native.KBDLLHOOKSTRUCT key)
    {
        int vk = ModifierKey(key);
        _modifierGeneration[vk]++;
        _passedModifierDown[vk] = (key.Flags & (Native.LLKHF_UP | Native.LLKHF_LOWER_IL_INJECTED)) == 0;
    }

    private bool CanReleaseLocked(Native.KBDLLHOOKSTRUCT key, ModifierCleanup cleanup)
    {
        int vk = ModifierKey(cleanup.Key);
        return KeyboardHook.IsModifierKey(key.VkCode)
            && (key.Flags & Native.LLKHF_UP) != 0
            && ModifierKey(key) == vk
            && _passedModifierDown[vk]
            && _modifierGeneration[vk] == cleanup.Generation;
    }

    /// <summary>Removes the swallowed trigger key while keeping the held releases' positions with their own events.</summary>
    private void RemoveSwallowedLocked(List<Native.KBDLLHOOKSTRUCT> keys)
    {
        keys[0] = default;
        keys.RemoveAt(0);
        for (int i = _modifierReleases.Count - 1; i >= 0; i--)
        {
            HeldModifierRelease release = _modifierReleases[i];
            if (ReferenceEquals(release.Owner, keys))
            {
                if (release.Index == 0)
                {
                    _modifierReleases.RemoveAt(i);
                }
                else
                {
                    release.Index--;
                }
            }
        }
    }

    /// <summary>Drops text and unsent presses; only a release paired with a down Windows already received can go back.</summary>
    private void DiscardLocked(List<Native.KBDLLHOOKSTRUCT> keys)
    {
        foreach (HeldModifierRelease release in _modifierReleases)
        {
            if (ReferenceEquals(release.Owner, keys))
            {
                release.Discarded = true;
            }
        }

        QueueDiscardedReleasesLocked();
        Zero(keys);
        Monitor.PulseAll(_lock);
    }

    private void QueueDiscardedReleasesLocked()
    {
        // An in-flight replay still owns its modifier context, even if a timeout ended the guard. Its held text must
        // reach the app before any cleanup. A short send may restore a modifier without reaching its up; check after it.
        if (_resending != 0)
        {
            return;
        }

        for (int i = _modifierReleases.Count - 1; i >= 0; i--)
        {
            HeldModifierRelease release = _modifierReleases[i];
            if (release.Discarded)
            {
                QueueReleaseLocked(release);
                _modifierReleases.RemoveAt(i);
            }
        }
    }

    private void QueueReleaseLocked(HeldModifierRelease release)
    {
        int vk = ModifierKey(release.Key);
        if (!_passedModifierDown[vk] || release.Generation != _externalModifierGeneration[vk] || (release.Key.Flags & Native.LLKHF_LOWER_IL_INJECTED) != 0)
        {
            return;
        }

        // Counted before the worker is queued: disposal must not unhook in the gap before that worker starts.
        var cleanup = new ModifierCleanup(release.Key with { VkCode = (uint)vk }, _modifierGeneration[vk]);
        _pendingCleanup++;
        ThreadPool.QueueUserWorkItem(_ => ReleaseModifier(cleanup));
    }

    private void ReleaseModifier(ModifierCleanup cleanup)
    {
        IntPtr signature = IntPtr.Zero;
        Native.INPUT[] input = [];
        try
        {
            while (true)
            {
                IntPtr candidate = new((System.Security.Cryptography.RandomNumberGenerator.GetInt32(0x0100_0000, int.MaxValue) & ~0xFFF) | CleanupTagBits);
                lock (_lock)
                {
                    while (_resending != 0)
                    {
                        Monitor.Wait(_lock);
                    }

                    if (!CanReleaseLocked(cleanup.Key, cleanup))
                    {
                        return;
                    }

                    if (_cleanup.TryAdd(candidate, cleanup))
                    {
                        signature = candidate;
                        break;
                    }
                }
            }

            // Only the paired key-up, never text or a down, may reach a different foreground window. Its original
            // integrity was not lower than ours. Keyboard Manager leaves this tagged release alone.
            if (!ModifierIsDown(ModifierKey(cleanup.Key)))
            {
                return;
            }

            input = [ToInput(cleanup.Key with { DwExtraInfo = signature })];
            _ = Resend(input);
        }
        finally
        {
            Array.Clear(input);
            lock (_lock)
            {
                // This cleanup dispatch has returned; remove its local token and wake the waiting worker.
                _cleanup.Remove(signature);
                _pendingCleanup--;
                Monitor.PulseAll(_lock);
            }
        }
    }

    private static bool SameInput(Native.INPUT input, Native.KBDLLHOOKSTRUCT key)
    {
        Native.KEYBDINPUT sent = input.U.Ki;
        bool unicode = (sent.DwFlags & Native.KEYEVENTF_UNICODE) != 0;
        return key.DwExtraInfo == sent.DwExtraInfo
            && key.VkCode == (unicode ? Native.VK_PACKET : sent.WVk)
            && (sent.WScan == 0 || sent.WScan == key.ScanCode)
            && ((sent.DwFlags & Native.KEYEVENTF_KEYUP) != 0) == ((key.Flags & Native.LLKHF_UP) != 0)
            && ((sent.DwFlags & Native.KEYEVENTF_EXTENDEDKEY) != 0) == ((key.Flags & Native.LLKHF_EXTENDED) != 0);
    }

    private ReplayBatch BeginResendLocked(Native.INPUT[] keys, List<Native.KBDLLHOOKSTRUCT> source)
    {
        var replay = new ReplayBatch(keys);
        int next = 0;
        foreach (HeldModifierRelease release in _modifierReleases.Where(release => ReferenceEquals(release.Owner, source)).OrderBy(release => release.Index))
        {
            for (; next < keys.Length; next++)
            {
                if (SameInput(keys[next], release.Key))
                {
                    replay.Releases.Add(next++, release);
                    break;
                }
            }
        }

        _replays.Add(replay);
        _resending++;
        return replay;
    }

    private void EndResend(ReplayBatch replay)
    {
        lock (_lock)
        {
            _replays.Remove(replay);
            _resending--;
            QueueDiscardedReleasesLocked();
            Monitor.PulseAll(_lock);
        }
    }

    private uint ResendWhileAlive(Native.INPUT[] keys, List<Native.KBDLLHOOKSTRUCT> source)
    {
        ReplayBatch replay;
        lock (_lock)
        {
            if (_disposed)
            {
                return 0;
            }

            replay = BeginResendLocked(keys, source);
        }

        try
        {
            return Resend(keys);
        }
        finally
        {
            EndResend(replay);
        }
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
