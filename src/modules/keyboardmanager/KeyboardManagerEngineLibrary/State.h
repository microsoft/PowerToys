#pragma once
#include <keyboardmanager/common/MappingConfiguration.h>
#include <atomic>
#include <memory>
#include <unordered_set>

enum class TextReplacementContextStatus : uint8_t
{
    Pending,
    Editable,
    Blocked,
};

struct TextReplacementRuntimeConfiguration
{
    TextReplacementTable replacements;
    size_t maxTriggerLength = 0;
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

public:
    // Publishes an immutable configuration generation for the hook thread. Settings
    // loading mutates MappingConfiguration on a worker thread, so the hook must never
    // read those containers directly while a reload is in progress.
    bool PublishTextReplacementRuntimeConfiguration() noexcept;
    std::shared_ptr<const TextReplacementRuntimeConfiguration> GetTextReplacementRuntimeConfiguration() const noexcept;
    bool HasTextReplacements() const noexcept;

    // Keep direct State mutations used by the engine tests synchronized with the
    // immutable runtime view. KeyboardManager::LoadSettings publishes once after the
    // base configuration has finished constructing a complete generation.
    void ClearTextReplacements();
    bool AddTextReplacement(const std::wstring& trigger, const std::wstring& text);
    bool DeleteTextReplacement(const std::wstring& trigger);
    bool UpdateTextReplacement(const std::wstring& oldTrigger, const std::wstring& newTrigger, const std::wstring& newText);

    // Stores typed characters for text replacement matching.
    std::wstring textReplacementBuffer;

    // Stores the foreground process id associated with textReplacementBuffer.
    DWORD textReplacementProcessId = 0;

    // Stores the focused window/control associated with textReplacementBuffer.
    HWND textReplacementWindow = nullptr;

    // Runtime state used only by the serialized low-level input hook thread.
    wchar_t textReplacementPendingPacketHighSurrogate = L'\0';
    bool textReplacementDeadKeyPending = false;
    bool textReplacementDeadKeyMustPassThrough = false;
    DWORD textReplacementDeadKeyThreadId = 0;
    HKL textReplacementDeadKeyLayout = nullptr;
    bool textReplacementCapsLockOn = false;
    bool textReplacementNumLockOn = false;
    bool textReplacementScrollLockOn = false;
    bool textReplacementToggleStateInitialized = false;
    uint64_t textReplacementObservedContextEpoch = 0;

    // Other threads only request invalidation. The hook thread observes these atomics
    // and performs all std::wstring mutations itself.
    std::atomic_bool textReplacementRuntimeResetRequested = false;
    std::atomic_uint64_t textReplacementContextEpoch = 1;

    // The accessibility classifier publishes a fail-closed snapshot here. Unit tests
    // that invoke the event handler directly leave tracking disabled.
    std::atomic_bool textReplacementContextTrackingEnabled = false;
    std::atomic_bool textReplacementContextInfrastructureReady = false;
    std::atomic_bool textReplacementContextEditable = false;
    std::atomic<TextReplacementContextStatus> textReplacementContextStatus = TextReplacementContextStatus::Pending;
    std::atomic<HWND> textReplacementContextWindow = nullptr;
    std::atomic<DWORD> textReplacementContextProcessId = 0;
    std::atomic_uint64_t textReplacementClassifiedContextEpoch = 0;
    std::atomic<HANDLE> textReplacementContextRefreshEvent = nullptr;

    void RequestTextReplacementRuntimeReset() noexcept
    {
        textReplacementRuntimeResetRequested.store(true, std::memory_order_release);
    }

    void InvalidateTextReplacementContext() noexcept
    {
        textReplacementContextEditable.store(false, std::memory_order_release);
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

    // Records (failed == true) or clears (failed == false) that the single-key remap
    // key-down injection for sourceKey was blocked and the original key-down was passed
    // through to the foreground app.
    void SetSingleKeyRemapInjectionFailed(const DWORD sourceKey, const bool failed);

    // Returns true and clears the marker if sourceKey's single-key remap key-down
    // injection was previously blocked, indicating that its key-up should be passed
    // through as well.
    bool ConsumeSingleKeyRemapInjectionFailed(const DWORD sourceKey);

private:
    std::atomic<std::shared_ptr<const TextReplacementRuntimeConfiguration>> textReplacementRuntimeConfiguration{
        std::make_shared<const TextReplacementRuntimeConfiguration>()
    };
};
