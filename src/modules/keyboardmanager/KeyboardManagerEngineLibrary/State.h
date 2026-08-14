#pragma once
#include <keyboardmanager/common/MappingConfiguration.h>
#include <atomic>
#include <mutex>
#include <unordered_map>
#include <unordered_set>

enum class TextReplacementContextStatus : uint8_t
{
    Pending,
    Editable,
    Blocked,
};

enum class SingleKeyRemapPressOwner : uint8_t
{
    OriginalPassthrough,
    Suppressed,
    RemappedTarget,
};

struct SingleKeyRemapPressState
{
    SingleKeyRemapPressOwner owner = SingleKeyRemapPressOwner::OriginalPassthrough;
    bool releasePending = false;
    bool suppressedPhysicalPressHeld = false;
    std::vector<INPUT> repeatEvents;
    std::vector<INPUT> releaseEvents;
};

class State : public MappingConfiguration
{
private:
    // Stores the activated target application in app-specific shortcut
    std::wstring activatedAppSpecificShortcutTarget;

    // A physical press chooses one owner on its first key-down and never changes owner on
    // repeats. Release events are retained independently of the mapping table so a failed
    // target key-up can be retried without leaking the original key-up.
    std::unordered_map<DWORD, SingleKeyRemapPressState> singleKeyRemapPressStates;

    // Key-up events required to finish cleanup after a partial SendInput. The hook
    // thread normally owns this ledger; the mutex also permits shutdown to make one
    // final best-effort attempt after input hooks have been detached.
    mutable std::mutex pendingInputCleanupMutex;
    std::vector<INPUT> pendingInputCleanup;

public:
    // Source keys whose remapped target key/shortcut is currently held down by
    // Keyboard Manager. Settings reload waits for these physical keys to release
    // before replacing the configuration that owns their matching key-up.
    std::unordered_set<DWORD> singleKeyRemapActiveKeys;

    // Stores typed characters for text replacement matching.
    std::wstring textReplacementBuffer;

    // Stores the foreground process id associated with textReplacementBuffer.
    DWORD textReplacementProcessId = 0;

    // Stores the focused window/control associated with textReplacementBuffer.
    HWND textReplacementWindow = nullptr;

    // Runtime state used only by the serialized low-level input hook thread.
    wchar_t textReplacementPendingPacketHighSurrogate = L'\0';
    bool textReplacementDeadKeyPending = false;
    bool textReplacementCapsLockOn = false;
    // Activation key-downs that fired a replacement. Repeats and the matching key-up
    // must remain suppressed even if the input context changes in the meantime.
    std::unordered_set<DWORD> textReplacementSuppressedTriggerKeys;
    // Physical trigger keys currently held down. A replacement may only activate on the
    // first down transition; an auto-repeat can never retroactively consume a passed pair.
    std::unordered_set<DWORD> textReplacementTriggerKeysDown;
    uint64_t textReplacementObservedContextEpoch = 0;

    // Other threads only request invalidation. The hook thread observes these atomics
    // and performs all std::wstring mutations itself.
    std::atomic_uint64_t textReplacementContextEpoch = 1;

    // The accessibility classifier publishes a fail-closed snapshot here.
    std::atomic_bool textReplacementContextTrackingEnabled = false;
    std::atomic<TextReplacementContextStatus> textReplacementContextStatus = TextReplacementContextStatus::Pending;
    std::atomic<HWND> textReplacementContextWindow = nullptr;
    std::atomic<DWORD> textReplacementContextProcessId = 0;
    std::atomic_uint64_t textReplacementClassifiedContextEpoch = 0;
    std::atomic<HANDLE> textReplacementContextRefreshEvent = nullptr;

    void InvalidateTextReplacementContext() noexcept
    {
        textReplacementContextStatus.store(TextReplacementContextStatus::Pending, std::memory_order_release);
        textReplacementContextEpoch.fetch_add(1, std::memory_order_acq_rel);

        if (const HANDLE refreshEvent = textReplacementContextRefreshEvent.load(std::memory_order_acquire))
        {
            SetEvent(refreshEvent);
        }
    }

    // Function to get the iterator of a single key remap given the source key. Returns nullopt if it isn't remapped
    std::optional<SingleKeyRemapTable::iterator> GetSingleKeyRemap(const DWORD& originalKey);

    // Function to get a unicode string remap given the source key. Returns nullopt if it isn't remapped
    std::optional<std::wstring> GetSingleKeyToTextRemapEvent(const DWORD originalKey) const;

    bool CheckShortcutRemapInvoked(const std::optional<std::wstring>& appName);

    // Function to get the source and target of a shortcut remap given the source shortcut. Returns nullopt if it isn't remapped
    ShortcutRemapTable& GetShortcutRemapTable(const std::optional<std::wstring>& appName);

    std::vector<Shortcut>& GetSortedShortcutRemapVector(const std::optional<std::wstring>& appName);

    // Sets the activated target application in app-specific shortcut
    void SetActivatedApp(const std::wstring& appName);

    // Gets the activated target application in app-specific shortcut
    std::wstring GetActivatedApp();

    const SingleKeyRemapPressState* GetSingleKeyRemapPressState(DWORD sourceKey) const noexcept;
    SingleKeyRemapPressState* GetSingleKeyRemapPressState(DWORD sourceKey) noexcept;
    void SetSingleKeyRemapPassthrough(DWORD sourceKey);
    void SetSingleKeyRemapSuppressed(DWORD sourceKey);
    void SetSingleKeyRemapTarget(DWORD sourceKey, std::vector<INPUT> repeatEvents, std::vector<INPUT> releaseEvents);
    void SetSingleKeyRemapReleasePending(DWORD sourceKey);
    void SetSingleKeyRemapSuppressedPhysicalPressHeld(DWORD sourceKey, bool held);
    void ClearSingleKeyRemapPressState(DWORD sourceKey);
    void ClearSingleKeyRemapPressStates();
    bool HasSingleKeyRemapPressState(DWORD sourceKey) const noexcept;
    std::vector<DWORD> GetSingleKeyRemapReleasePendingKeys() const;

    void QueuePendingInputCleanup(std::vector<INPUT> cleanupEvents);
    void PrependPendingInputCleanup(std::vector<INPUT> cleanupEvents);
    std::vector<INPUT> TakePendingInputCleanup();
    void ClearPendingInputCleanup();
    bool HasPendingInputCleanup() const;
};
