#pragma once
#include <keyboardmanager/common/MappingConfiguration.h>
#include <unordered_set>

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

    // Key-sequence source keys that are currently held, i.e. whose sequence already fired on this
    // press. Auto-repeat key-downs of a held key are swallowed instead of sending the sequence again.
    std::unordered_set<DWORD> heldSequenceKeys;
    // Key-sequence source keys whose injection was blocked on this press, so the original key-down
    // was passed through. The rest of the press (auto-repeats and the key-up) is passed through too,
    // without retrying, so the key can't end up down in the app with its key-up swallowed.
    std::unordered_set<DWORD> passedThroughSequenceKeys;
    // Both sets are used by the low-level keyboard hook thread. heldSequenceKeys is also cleared by
    // the settings thread on reload (ClearAllSequenceKeyState), the same as the alone sets above.

public:
    // Function to get the iterator of a single key remap given the source key. Returns nullopt if it isn't remapped
    std::optional<SingleKeyRemapTable::iterator> GetSingleKeyRemap(const DWORD& originalKey);

    // Function to get a unicode string remap given the source key. Returns nullopt if it isn't remapped
    std::optional<std::wstring> GetSingleKeyToTextRemapEvent(const DWORD originalKey) const;

    // Function to get the iterator of an "Alone" single key remap given the source key. Returns nullopt if it isn't remapped
    std::optional<SingleKeyRemapTable::iterator> GetSingleKeyAloneRemap(const DWORD& originalKey);

    // Function to get the iterator of a single-key-to-sequence remap. Returns nullopt if not remapped.
    std::optional<SingleKeyToSequenceRemapTable::iterator> GetSingleKeyToSequenceRemap(const DWORD& originalKey);

    // Marks a key-sequence source key as held. Returns false if it was already held (the key-down
    // is an auto-repeat), true if this is a fresh press.
    bool MarkSequenceKeyHeld(const DWORD key);
    // Forgets that a key-sequence source key is held (on its key-up). Returns true if it was held.
    bool ClearSequenceKeyHeld(const DWORD key);
    // Marks / checks / forgets that this press of a key-sequence source key is being passed through
    // because its injection was blocked (see passedThroughSequenceKeys).
    void SetSequenceKeyPassedThrough(const DWORD key);
    bool IsSequenceKeyPassedThrough(const DWORD key) const;
    bool ClearSequenceKeyPassedThrough(const DWORD key);
    // Forgets all held key-sequence source keys. Called when the mapping tables are reloaded, like
    // ClearAllAloneKeyState, so a key-up lost before the reload can't make the next press be ignored.
    // Passed-through presses are kept: their key-down already reached the app, so their key-up must
    // still be passed through.
    void ClearAllSequenceKeyState();

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