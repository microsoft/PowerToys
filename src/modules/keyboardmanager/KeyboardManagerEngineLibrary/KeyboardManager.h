#pragma once
#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/utils/EventWaiter.h>
#include <keyboardmanager/common/Input.h>
#include <mutex>
#include <string_view>
#include <thread>
#include "State.h"

class KeyboardManager
{
public:
    static const inline DWORD ReloadSettingsMessageID = WM_APP + 1;

    // Constructor
    KeyboardManager();
    ~KeyboardManager();

    void StartLowlevelKeyboardHook();
    void StopLowlevelKeyboardHook();
    void ReloadSettings();

    bool HasRegisteredRemappings() const;

private:
    enum class TextReplacementPreparationOutcome
    {
        NotPrepared,
        Prepared,
        CommittedFailure,
    };

    enum class TextReplacementContextRequestKind
    {
        Prepare,
        Rollback,
    };

    enum class TextReplacementContextRequestPhase
    {
        Idle,
        Querying,
        SelectingOrSelected,
    };

    struct TextReplacementContextRequest
    {
        uint64_t id = 0;
        TextReplacementContextRequestKind kind = TextReplacementContextRequestKind::Prepare;
        std::wstring trigger;
        HWND expectedWindow = nullptr;
        DWORD expectedProcessId = 0;
        uint64_t expectedContextEpoch = 0;
        bool targetHasNewline = false;
        bool canceled = false;
    };

    // Contains the non localized module name
    std::wstring moduleName = KeyboardManagerConstants::ModuleName;

    // Low level hook handles
    static HHOOK hookHandle;

    // Required for Unhook in old versions of Windows
    static HHOOK hookHandleCopy;

    // Low-level mouse hook used to invalidate caret-sensitive text replacement state.
    static HHOOK mouseHookHandle;

    // Static pointer to the current KeyboardManager object required for accessing the HandleKeyboardHookEvent function in the hook procedure
    // Only global or static variables can be accessed in a hook procedure CALLBACK
    static KeyboardManager* keyboardManagerObjectPtr;

    // Variable which stores all the state information to be shared between the UI and back-end
    State state;

    // Object of class which implements InputInterface. Required for calling library functions while enabling testing
    KeyboardManagerInput::Input inputHandler;

    // Auto reset event for waiting for settings changes. The event is signaled when settings are changed
    EventWaiter settingsEventWaiter;

    HANDLE editorIsRunningEvent = nullptr;

    // Hook procedure definition
    static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam);
    static void CALLBACK TextReplacementWinEventProc(HWINEVENTHOOK hook, DWORD event, HWND window, LONG objectId, LONG childId, DWORD eventThread, DWORD eventTime);

    void StartTextReplacementContextTracking();
    void StopTextReplacementContextTracking() noexcept;
    void TextReplacementContextThreadProc();
    TextReplacementPreparationOutcome PrepareTextReplacement(std::wstring_view trigger, bool targetHasNewline) noexcept;
    bool RollbackPreparedTextReplacement() noexcept;
    void FinishPreparedTextReplacement() noexcept;
    bool IsPreparedTextReplacementCurrent() const noexcept;
    bool IsEditorRunning();
    bool HasActiveRemap() const;
    void QueueDeferredSettingsReloadIfReady();

    HWINEVENTHOOK textReplacementForegroundHook = nullptr;
    HWINEVENTHOOK textReplacementFocusHook = nullptr;
    HWINEVENTHOOK textReplacementDesktopHook = nullptr;
    HWINEVENTHOOK textReplacementSelectionHook = nullptr;
    HANDLE textReplacementContextStopEvent = nullptr;
    HANDLE textReplacementContextRefreshEvent = nullptr;
    HANDLE textReplacementContextRequestEvent = nullptr;
    HANDLE textReplacementContextReadyEvent = nullptr;
    HANDLE textReplacementContextCommitEvent = nullptr;
    HANDLE textReplacementContextCancelEvent = nullptr;
    HANDLE textReplacementContextFinishedEvent = nullptr;
    std::thread textReplacementContextThread;
    std::atomic<DWORD> textReplacementContextThreadId = 0;
    std::atomic_bool textReplacementRecoveryBlocksInput = false;
    std::atomic<HWND> textReplacementRecoveryWindow = nullptr;
    std::atomic<DWORD> textReplacementRecoveryProcessId = 0;
    std::atomic_bool textReplacementSelectionTrackingAvailable = false;
    std::atomic_uint64_t textReplacementPreparedSelectionRequestId = 0;
    std::atomic_uint64_t textReplacementLastPreparedSelectionRequestId = 0;
    std::atomic<HWND> textReplacementIgnoredSelectionEventWindow = nullptr;
    std::atomic_bool textReplacementIgnoreNextSelectionEvent = false;
    std::atomic<DWORD> textReplacementIgnoredSelectionEventExpires = 0;
    mutable std::mutex textReplacementContextRequestMutex;
    TextReplacementContextRequest textReplacementContextRequest;
    uint64_t textReplacementContextNextRequestId = 0;
    uint64_t textReplacementContextCompletedRequestId = 0;
    uint64_t textReplacementContextFinishedSelectionId = 0;
    uint64_t textReplacementLastSuccessfullyRestoredRequestId = 0;
    uint64_t textReplacementRecoveryGuardRequestId = 0;
    bool textReplacementContextCandidateReady = false;
    bool textReplacementContextRequestInFlight = false;
    TextReplacementContextRequestPhase textReplacementContextRequestPhase = TextReplacementContextRequestPhase::Idle;
    TextReplacementPreparationOutcome textReplacementContextPreparationOutcome = TextReplacementPreparationOutcome::NotPrepared;
    ULONGLONG textReplacementTransactionDeadline = 0;
    bool settingsReloadDeferred = false;

    // Load settings from the file.
    bool LoadSettings();

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
};
