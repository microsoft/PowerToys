#pragma once
#include <keyboardmanager/common/MappingConfiguration.h>
#include <keyboardmanager/common/InputInterface.h>
#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>
#include <optional>
#include <unordered_map>
#include <unordered_set>

// Remembers what the key-sequence remap (#5679) did with the first key-down of each physical key
// that is down, until the matching key-up. Auto-repeats and the key-up of a press get the same
// answer as its first key-down, whatever the mapping tables say by then: a settings reload, a
// remap added or removed, or a temporary bypass of remapping in the middle of a press cannot
// separate a key-down from its key-up.
//
// A key is identified by its scan code and extended-key flag, not by its virtual key: the virtual
// key of a keypad key can change between its key-down and its key-up (NumPad1 goes down, Shift
// changes the state, and the key comes up as End). An event without a scan code (some injected
// input) is identified by its virtual key instead. A few keys change their scan code instead
// (Print Screen has another one while Alt is down), so a key-up that matches no press this way
// ends the press that began with the same virtual key.
//
// VK_PACKET events (text another program sends as Unicode input) are not presses of a key and are
// not tracked at all: their scan code field holds a character, which can equal the scan code of a
// real key.
//
// Used on the low-level keyboard hook thread only (the thread that installs the hook and runs its
// message loop). Nothing here is cleared by a settings reload.
//
// A key-up can fail to arrive: the secure desktop takes over in the middle of a press (a UAC
// prompt, Win+L), the keyboard is unplugged, another hook swallows it, or the key does not send
// one when it is released (as some IME keys of a Japanese keyboard are known to do). The record of
// that press stays behind. Each record therefore carries the time of the last event of its press,
// and a key-down that comes later than an auto-repeat could (see StalePressAfterMs) discards the
// record and counts as a new press. A key-up ends the record of its key whatever its age: a held
// key that no longer repeats sends nothing until it is released.
//
// Known limitations:
//  - After a key-up that never arrived, a key-down of the same key within StalePressAfterMs is
//    still taken for an auto-repeat of the old press: a key with a sequence remap is ignored, or
//    passes through without its sequence. The record stays until a key-up of the key arrives, a
//    key-down comes later than that, or the hook is installed again. If none of these happens
//    (the key is not touched again) the record just stays, which costs a lookup per key event.
//  - The time is the one the event carries. Input sent by another program with a made-up time
//    stamp can make an auto-repeat look like a new press, or the other way round.
//  - The same key held at once from two sources (two keyboards, or a keyboard and another
//    program's injected input) shares one record, and the first key-up ends it.
//  - A press without a scan code is paired by virtual key, so the keypad case above is not paired
//    for such input.
//  - A key-up whose scan code and virtual key both differ from those of its key-down is not
//    paired. A key-up that matches no press by scan code ends a press that began with the same
//    virtual key, even if it comes from another key with that virtual key.
//  - Keys that are already down when the hook is installed are only known by virtual key, taken
//    from the key state the system reports (see SeedForwardedPresses). That cannot tell apart two
//    keys with the same virtual key (for example the keypad and the navigation End), and it misses
//    a key whose key-down another program's hook kept from the system.
class SequencePressTracker
{
public:
    enum class Disposition
    {
        // The press was replaced by its key sequence. Its auto-repeats and its key-up are suppressed.
        SequenceFired,

        // The key-sequence remap left the press alone: the key had no sequence remap, another
        // handler took the key-down, remapping was suspended, or the key was already down when
        // tracking started. The rest of the press goes on to the other handlers; a sequence remap
        // that appears in the meantime waits for the next press.
        Forwarded,

        // The sequence could not be injected, so the key-down was passed to the system unchanged.
        // The rest of the press is passed through too, and no other handler gets to see it.
        PassThroughTerminal,
    };

    // A key-down that comes more than this many milliseconds (KBDLLHOOKSTRUCT.time) after the last
    // event of the press on record for its key is not an auto-repeat of that press but a new
    // press: the key-up in between never arrived.
    //
    // A held key repeats after the repeat delay (at most 1 s in the Windows keyboard settings) and
    // then at the repeat rate (at least about 2.5 a second), so the key-downs of one press are at
    // most about 1 s apart. A held key that stops repeating, because another key of the same
    // keyboard was pressed, sends no further key-down at all, only its key-up. 2 s is twice the
    // longest gap a repeating key can produce. (The Filter Keys accessibility feature can slow the
    // repeat down to 2 s and more. With such a setting a held key counts as pressed again at each
    // repeat, and its sequence is sent again.)
    static constexpr DWORD StalePressAfterMs = 2000;

    // Identity of a physical key: scan code + extended-key flag, or the virtual key if the event
    // has no scan code.
    static uint64_t MakeKeyId(DWORD scanCode, bool extended, DWORD vkCode) noexcept;

    // For a key-down: the disposition of the press the key already belongs to (the key-down is an
    // auto-repeat), or nullopt if this is a new press. A record whose last event is more than
    // StalePressAfterMs older than this key-down is discarded first. ii is only asked for the key
    // state when keys seeded by SeedForwardedPresses are left.
    std::optional<Disposition> FindPress(const KBDLLHOOKSTRUCT& key, KeyboardManagerInput::InputInterface& ii);

    // Records the disposition of a new press, or changes the one recorded for the key.
    void RecordPress(const KBDLLHOOKSTRUCT& key, Disposition disposition);

    // For a key-up: ends the press of the key and returns its disposition, or nullopt if no press
    // of the key was recorded. If no press has the identity of the key-up, a press whose key-down
    // had the same virtual key is taken instead.
    std::optional<Disposition> ReleasePress(const KBDLLHOOKSTRUCT& key);

    // Records every key the system reports as down as a forwarded press. Called when tracking
    // starts while keys may be held (the hook is installed), so that a remap of such a key waits
    // for its next press.
    void SeedForwardedPresses(KeyboardManagerInput::InputInterface& ii);

    // Forgets every press. Called before the hook is installed: no event was seen while there was
    // no hook, so the records can be out of date.
    void Reset() noexcept;

    bool Empty() const noexcept;

private:
    struct Press
    {
        Disposition disposition;

        // KBDLLHOOKSTRUCT.time of the last event of the press (its key-down or its latest auto-repeat)
        DWORD lastEventTime;

        // The virtual key of the first event of the press, as the hook reports it
        DWORD virtualKey;
    };

    std::unordered_map<uint64_t, Press> m_presses;

    // Virtual keys seeded by SeedForwardedPresses that had no event of their own yet.
    std::unordered_set<DWORD> m_seededKeys;
};

class State : public MappingConfiguration
{
private:
    // Stores the activated target application in app-specific shortcut
    std::wstring activatedAppSpecificShortcutTarget;

    // Source keys whose single-key remap key-down injection was blocked, so the original
    // key-down was passed through to the foreground app. The matching key-up must be
    // passed through too; otherwise the physical key is stranded DOWN. Only accessed from
    // the (serialized) low-level keyboard hook thread.
    std::unordered_set<DWORD> singleKeyRemapInjectionFailedKeys;

    // Dual-key ("Alone") runtime bookkeeping. Down and key-up arrive as separate hook
    // invocations, so we must remember, across invocations, whether an alone-mapped key that
    // is currently held is still a tap candidate (no other key intervened) or has already
    // started a combination (its original key-down was injected as a real modifier/key).
    // Only accessed from the (serialized) low-level keyboard hook thread.
    std::unordered_set<DWORD> alonePendingKeys;      // held, still a tap candidate
    std::unordered_set<DWORD> aloneCombinationKeys;  // real key-down injected (combination started)

    // What the key-sequence remap did with each physical key that is down (see SequencePressTracker).
    // Only accessed from the low-level keyboard hook thread; a settings reload does not touch it.
    SequencePressTracker sequencePresses;

    // The key-sequence table the hook uses: a finished table that is never changed, only replaced
    // as a whole by PublishStagedSequenceTable. Only accessed from the low-level keyboard hook
    // thread. The table the loader fills, MappingConfiguration::singleKeyToSequenceReMap, is never
    // read by the hook.
    std::shared_ptr<const SingleKeyToSequenceRemapTable> liveSequenceTable = std::make_shared<const SingleKeyToSequenceRemapTable>();

    // A finished table on its way from the thread that loaded the settings to the hook thread
    // (null when there is none). Together with the flag below, the only key-sequence data the two
    // threads share.
    std::mutex stagedSequenceTableMutex;
    std::shared_ptr<const SingleKeyToSequenceRemapTable> stagedSequenceTable;

    // True while stagedSequenceTable holds a table. Only written with the mutex held, so the two
    // always agree; read without it by the hook, which checks it at every key event and takes the
    // mutex only when there is something to publish.
    std::atomic_bool sequenceTableStaged = false;

public:
    // Function to get the iterator of a single key remap given the source key. Returns nullopt if it isn't remapped
    std::optional<SingleKeyRemapTable::iterator> GetSingleKeyRemap(const DWORD& originalKey);

    // Function to get a unicode string remap given the source key. Returns nullopt if it isn't remapped
    std::optional<std::wstring> GetSingleKeyToTextRemapEvent(const DWORD originalKey) const;

    // Function to get the iterator of an "Alone" single key remap given the source key. Returns nullopt if it isn't remapped
    std::optional<SingleKeyRemapTable::iterator> GetSingleKeyAloneRemap(const DWORD& originalKey);

    // Hands the key-sequence remaps that were just loaded (singleKeyToSequenceReMap) over to the
    // hook thread: copies them into a finished table and stages it, replacing a staged table that
    // was not published yet. Called by the thread that loaded the settings; the hook keeps using
    // the table it has until PublishStagedSequenceTable runs.
    void StageSequenceTable();

    // Makes the staged key-sequence table, if there is one, the table the hook uses. Low-level
    // keyboard hook thread only. The hook calls this itself at the start of every key event
    // (DispatchKeyboardEvent), before it reads the table, so the loading thread does not have to
    // notify the hook thread; the same is done before a hook is installed. When nothing is staged
    // this is one atomic read. Press records are kept: a press that began under the old table
    // ends the way it began. Returns false if nothing was staged.
    bool PublishStagedSequenceTable();

    // Gets the key-sequence remap ready for a hook that is about to be installed: publishes a
    // staged table, forgets the press records (no event was seen while there was no hook) and
    // records the keys that are down right now as presses that already reached the system.
    // Low-level keyboard hook thread only, before the hook can deliver anything.
    void StartSequencePressTracking(KeyboardManagerInput::InputInterface& ii);

    // The key-sequence table the hook uses. Low-level keyboard hook thread only. The caller keeps
    // the returned pointer for as long as it uses the table, so the table stays alive even if
    // another one is published in the meantime (a hook call nested in the caller's own injection
    // can do that).
    std::shared_ptr<const SingleKeyToSequenceRemapTable> GetLiveSequenceTable() const;

    // True if the hook has key-sequence remaps to apply. Low-level keyboard hook thread only.
    bool HasLiveSequenceRemaps() const noexcept;

    // The press records of the key-sequence remap. Low-level keyboard hook thread only.
    SequencePressTracker& SequencePresses() noexcept;

    // Dual-key ("Alone") runtime state transitions (see member declarations above).
    // Mark an alone-mapped key as held and still a tap candidate.
    void SetAlonePending(const DWORD key);
    bool IsAlonePending(const DWORD key) const;
    // Promote a pending alone key into a started combination (its real key-down was injected).
    void SetAloneCombination(const DWORD key);
    bool IsAloneCombination(const DWORD key) const;
    // Forget all alone runtime state for a key (on its key-up, once resolved).
    void ClearAloneKeyState(const DWORD key);
    // Forget ALL alone runtime state. Called when the mapping tables are reloaded so a key that was
    // physically held across a settings reload can't leave a stale pending/combination entry that a
    // later mouse/keyboard event would promote -- injecting an original key-down with no matching up.
    void ClearAllAloneKeyState();
    // Snapshot of currently pending (tap-candidate) alone keys, for flushing when another key arrives.
    std::vector<DWORD> GetPendingAloneKeys() const;
    // Cheap check for whether any alone key is currently held as a tap candidate. Used by the mouse
    // hook to early-out before doing any work on the common (no alone key held) path.
    bool HasPendingAloneKeys() const;
    // True if any alone-mapped key OTHER THAN `except` is currently held -- either pending (tap
    // candidate) or already promoted to a combination. Used on a key-down to tell that the key was
    // pressed while another alone key was down, so it is part of a combination and not a solo tap.
    bool HasOtherHeldAloneKey(const DWORD except) const;

    bool CheckShortcutRemapInvoked(const std::optional<std::wstring>& appName);

    // Function to get the source and target of a shortcut remap given the source shortcut. Returns nullopt if it isn't remapped
    ShortcutRemapTable& GetShortcutRemapTable(const std::optional<std::wstring>& appName);

    std::vector<Shortcut>& GetSortedShortcutRemapVector(const std::optional<std::wstring>& appName);

    // Sets the activated target application in app-specific shortcut
    void SetActivatedApp(const std::wstring& appName);

    // Gets the activated target application in app-specific shortcut
    std::wstring GetActivatedApp();

    // Records (failed == true) or clears (failed == false) that the single-key remap
    // key-down injection for sourceKey was blocked and the original key-down was passed
    // through to the foreground app.
    void SetSingleKeyRemapInjectionFailed(const DWORD sourceKey, const bool failed);

    // Returns true and clears the marker if sourceKey's single-key remap key-down
    // injection was previously blocked, indicating that its key-up should be passed
    // through as well.
    bool ConsumeSingleKeyRemapInjectionFailed(const DWORD sourceKey);
};