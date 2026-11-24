#pragma once
#include <keyboardmanager/common/MappingConfiguration.h>
#include <keyboardmanager/common/InputInterface.h>
#include <cstdint>
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
// input) is identified by its virtual key instead.
//
// Used on the low-level keyboard hook thread only (the thread that installs the hook and runs its
// message loop). Nothing here is cleared by a settings reload.
//
// Known limitations:
//  - A key-up that never arrives (the secure desktop, sleep, the keyboard unplugged, another hook
//    swallowing it) leaves the record of that press behind. The next key-down of the key is then
//    taken for an auto-repeat of the old press: a key with a sequence remap is ignored once, or
//    passes through once without its sequence. The key-up of that press removes the record, so the
//    press after it is handled normally. Installing the hook again also clears all records.
//  - The same key held at once from two sources (two keyboards, or a keyboard and another
//    program's injected input) shares one record, and the first key-up ends it.
//  - A press without a scan code is paired by virtual key, so the keypad case above is not paired
//    for such input.
//  - Keys that are already down when tracking starts are only known by virtual key, taken from the
//    key state the system reports (see SeedForwardedPresses). That misses a key whose key-down an
//    earlier remap suppressed, and it cannot tell apart two keys with the same virtual key (for
//    example the keypad and the navigation End).
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

    // Identity of a physical key: scan code + extended-key flag, or the virtual key if the event
    // has no scan code.
    static uint64_t MakeKeyId(DWORD scanCode, bool extended, DWORD vkCode) noexcept;

    // For a key-down: the disposition of the press the key already belongs to (the key-down is an
    // auto-repeat), or nullopt if this is a new press. ii is only asked for the key state when
    // keys seeded by SeedForwardedPresses are left.
    std::optional<Disposition> FindPress(const KBDLLHOOKSTRUCT& key, KeyboardManagerInput::InputInterface& ii);

    // Records the disposition of a new press, or changes the one recorded for the key.
    void RecordPress(const KBDLLHOOKSTRUCT& key, Disposition disposition);

    // For a key-up: ends the press of the key and returns its disposition, or nullopt if no press
    // of the key was recorded.
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
    std::unordered_map<uint64_t, Disposition> m_presses;

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

public:
    // Function to get the iterator of a single key remap given the source key. Returns nullopt if it isn't remapped
    std::optional<SingleKeyRemapTable::iterator> GetSingleKeyRemap(const DWORD& originalKey);

    // Function to get a unicode string remap given the source key. Returns nullopt if it isn't remapped
    std::optional<std::wstring> GetSingleKeyToTextRemapEvent(const DWORD originalKey) const;

    // Function to get the iterator of an "Alone" single key remap given the source key. Returns nullopt if it isn't remapped
    std::optional<SingleKeyRemapTable::iterator> GetSingleKeyAloneRemap(const DWORD& originalKey);

    // Function to get the iterator of a single-key-to-sequence remap. Returns nullopt if not remapped.
    std::optional<SingleKeyToSequenceRemapTable::iterator> GetSingleKeyToSequenceRemap(const DWORD& originalKey);

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