#pragma once

#include <common/hooks/LowlevelKeyboardEvent.h>
#include <functional>
#include "State.h"

namespace KeyboardManagerInput
{
    class InputInterface;
    struct SendVirtualInputResult;
}

namespace KeyboardEventHandlers
{
    enum class TextReplacementPreparationResult
    {
        // Preparation did not touch the target UI. The physical trigger may pass through.
        NotPrepared,
        // The exact verified suffix is selected and ready to be replaced.
        Prepared,
        // Preparation may have changed target selection but could not establish a safe
        // replacement transaction. The physical trigger must be swallowed.
        CommittedFailure,
    };

    struct TextReplacementTransactionCallbacks
    {
        std::function<TextReplacementPreparationResult(std::wstring_view trigger, bool targetContainsNewline)> prepare;
        // Restores and verifies the original collapsed caret after a prepared target input
        // failed before any event was injected. Returns true only when pass-through is safe.
        std::function<bool()> rollback;
        // Side-effect-free check that the prepared selection token, focus and context epoch
        // still own the target immediately before each SendInput batch.
        std::function<bool()> isCurrent;
        // Releases the prepared selection token after completed or partially injected input.
        std::function<void()> finish;
    };

    // Retries the exact key-up suffix left after a partial cleanup injection. Successfully
    // inserted events are advanced by count; a zero-result leaves the ledger unchanged.
    KeyboardManagerInput::SendVirtualInputResult RetryPendingInputCleanup(KeyboardManagerInput::InputInterface& ii, State& state) noexcept;

    struct ResetChordsResults
    {
        bool CurrentKeyIsModifierKey;
        bool AnyChordStarted;
    };

    // Function to handle a single key remap
    intptr_t HandleSingleKeyRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    /* This feature has not been enabled (code from proof of concept stage)
        // Function to change a key's behavior from toggle to modifier
        __declspec(dllexport) intptr_t HandleSingleKeyToggleToModEvent(InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;
    */

    // Function to handle a shortcut remap
    intptr_t HandleShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state, const std::optional<std::wstring>& activatedApp = std::nullopt, bool allowNewRemappings = true) noexcept;

    // Function to reset chord matching
    void ResetAllStartedChords(State& state, const std::optional<std::wstring>& activatedApp);

    // Function to reset chord matching
    void ResetAllOtherStartedChords(State& state, const std::optional<std::wstring>& activatedApp, DWORD keyToKeep);

    std::wstring URL_encode(const std::wstring& value);

    std::wstring ConvertPathToURI(const std::wstring& filePath);

    // Function to reset chord matching if needed
    ResetChordsResults ResetChordsIfNeeded(LowlevelKeyboardEvent* data, State& state, const std::optional<std::wstring>& activatedApp);

    // Function to handle (start or show) programs for shortcuts
    void CreateOrShowProcessForShortcut(Shortcut shortcut) noexcept;

    void CloseProcessByName(const std::wstring& fileNamePart);

    void TerminateProcessesByName(const std::wstring& fileNamePart);

    void toast(winrt::param::hstring const& message1, winrt::param::hstring const& message2) noexcept;

    // Function to help FindMainWindow
    BOOL CALLBACK EnumWindowsCallback(HWND handle, LPARAM lParam);

    // Function to help FindMainWindow
    BOOL CALLBACK EnumWindowsCallbackAllowNonVisible(HWND handle, LPARAM lParam);

    // Function to FindMainWindow
    HWND FindMainWindow(unsigned long process_id, const bool allowNonVisible);

    // Function to GetProcessIdByName
    DWORD GetProcessIdByName(const std::wstring& processName);

    // Function to GetProcessesIdByName
    std::vector<DWORD> GetProcessesIdByName(const std::wstring& processName);

    // Function to get just the file name from a fill path
    std::wstring GetFileNameFromPath(const std::wstring& fullPath);

    // Function to find and show a running program
    bool ShowProgram(DWORD pid, std::wstring programName, bool isNewProcess, bool minimizeIfVisible, int retryCount);

    bool HideProgram(DWORD pid, std::wstring programName, int retryCount);

    // Function to handle an os-level shortcut remap
    intptr_t HandleOSLevelShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Function to handle an app-specific shortcut remap
    intptr_t HandleAppSpecificShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Continues a single-key press that already chose an owner. This never starts a new
    // remap, but it also retries pending target releases on later physical input.
    intptr_t HandleActiveSingleKeyRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Continues an already-invoked shortcut in exactly one table. It never activates a
    // fresh shortcut while the editor is open.
    intptr_t HandleActiveShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state, const std::optional<std::wstring>& activatedApp = std::nullopt) noexcept;

    // Continues only remaps that already own output state while the editor is open.
    intptr_t HandleActiveRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Function to generate a unicode string in response to a single keypress
    intptr_t HandleSingleKeyToTextRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state);

    // Function to replace recently typed text with configured replacement text
    intptr_t HandleTextReplacementEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state, const TextReplacementTransactionCallbacks& transactionCallbacks);

    // Suppresses repeats and the matching key-up for a text replacement trigger key
    // whose initial key-down was already consumed.
    intptr_t HandleTextReplacementSuppressedKeyEvent(LowlevelKeyboardEvent* data, State& state) noexcept;

    // Clears text replacement state that is tied to the current input context.
    void ResetTextReplacementRuntimeState(State& state) noexcept;

    // Refreshes and updates Caps Lock independently from the hook thread keyboard queue.
    void InitializeTextReplacementToggleKeyState(State& state) noexcept;
    void UpdateTextReplacementToggleKeyState(const LowlevelKeyboardEvent* data, bool eventSuppressed, State& state) noexcept;

    // Function to ensure Ctrl/Shift/Alt modifier key state is not detected as pressed down by applications which detect keys at a lower level than hooks when it is remapped for scenarios where its required
    KeyboardManagerInput::SendVirtualInputResult ResetIfModifierKeyForLowerLevelKeyHandlers(KeyboardManagerInput::InputInterface& ii, DWORD key, DWORD target);
};
