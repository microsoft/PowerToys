#pragma once

#include <functional>
#include <optional>
#include <common/hooks/LowlevelKeyboardEvent.h>
#include "State.h"

namespace KeyboardManagerInput
{
    class InputInterface;
}

namespace KeyboardEventHandlers
{
    namespace ProgramLauncher
    {
        // Override only the OS launch boundary, after path resolution and already-running actions.
        // Unit tests can launch isolated targets without requiring an interactive Explorer shell.
        using LaunchCallback = std::function<void(const std::wstring&, const std::wstring&, const std::wstring&)>;

        constexpr bool ShouldUseExplorerShell(Shortcut::StartWindowType startWindowType) noexcept
        {
            return startWindowType == Shortcut::StartWindowType::Normal;
        }

        std::optional<std::wstring> ExpandAndGetAbsolutePath(const std::wstring& value);

        std::wstring GetWorkingDirectory(const std::wstring& filePath, const std::wstring& configuredDirectory);
    }

    struct ResetChordsResults
    {
        bool CurrentKeyIsModifierKey;
        bool AnyChordStarted;
    };

    // Function to handle a single key remap
    intptr_t HandleSingleKeyRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Function to handle an "Alone" single key remap (dual-key / Karabiner to_if_alone): applies the
    // remapped action only when the source key is tapped alone; in combination the original key passes
    // through. Must run before HandleSingleKeyRemapEvent in the hook dispatch chain.
    intptr_t HandleSingleKeyAloneRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Function to handle a single-key-to-sequence remap (#5679): pressing the source key sends an
    // ordered list of key taps (down+up each), in order, once per press. The source key's own key-down
    // (including auto-repeats) and key-up are suppressed; the whole sequence is injected on the first
    // key-down. A target key that is already down is not tapped, so the sequence only releases keys
    // it pressed. A shortcut remap that is invoked at that moment is put back to the physically held
    // keys first, and the taps do not trigger shortcut remaps (see KEYBOARDMANAGER_SEQUENCE_FLAG).
    // This is ResolveSequencePress followed, for a new press, by StartSequencePress; the
    // hook's handler chain (DispatchKeyboardEvent) calls those two itself, because other steps of
    // the chain come in between.
    intptr_t HandleSingleKeyToSequenceRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // What the key-sequence remap says about an event.
    enum class SequencePressStep
    {
        // Not part of a tracked physical press: sent by Keyboard Manager itself, neither a key-down
        // nor a key-up, or there is no key-sequence remap at all.
        NotTracked,

        // First key-down of a press. Nothing is recorded for it yet.
        NewPress,

        // Belongs to a press the key-sequence remap left alone (or a key-up with no known key-down):
        // the other handlers decide.
        Forwarded,

        // Belongs to a press that was replaced by its sequence: suppress the event.
        Suppress,

        // Belongs to a press whose sequence could not be injected: pass the event to the system
        // without running any other handler.
        PassThrough,
    };

    // Pairs an event with its physical press, before the mapping tables are looked at: an
    // auto-repeat or a key-up gets the answer recorded for the first key-down of its press (and a
    // key-up ends the press). Only a first key-down returns NewPress, and it is the caller that
    // records what becomes of it: StartSequencePress, or SequencePressTracker::RecordPress with
    // Forwarded if the press does not get that far.
    SequencePressStep ResolveSequencePress(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Handles the first key-down of a press: if the key (the virtual key of this key-down) has a
    // sequence remap, sends the sequence. Records the outcome for the rest of the press and returns
    // it: Suppress, Forwarded (no sequence remap), or PassThrough (the injection failed).
    SequencePressStep StartSequencePress(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Promote every currently-pending ("tap candidate") "Alone" key into a started combination by
    // injecting its original key-down as a real key/modifier, so in-combination behavior works (e.g.
    // Right Ctrl acting as Ctrl for Ctrl+H, Ctrl+Click or Ctrl+Wheel). Shared by the keyboard
    // combination path (another key was pressed) and the low-level mouse hook (a click/scroll while an
    // alone key is held). `exceptKey` is left pending (used to skip the alone key's own auto-repeat);
    // pass 0 to promote all pending keys.
    void PromotePendingAloneKeysToCombination(KeyboardManagerInput::InputInterface& ii, State& state, DWORD exceptKey = 0) noexcept;

    // Append a key event that re-injects an "Alone" key's ORIGINAL (source) key while preserving its
    // numpad origin. Alone keys are tracked by the numpad-origin-encoded value of
    // `LowlevelKeyboardEvent::vkCode` (see EncodeKeyNumpadOrigin), whose marker rides in bit 31; a bare
    // `WORD` cast would drop it and re-inject e.g. a NumLock-off numpad navigation key as its extended
    // (arrow-cluster) twin. Exposed for unit testing.
    void AppendAloneSourceKeyEvent(std::vector<INPUT>& keyEventList, DWORD encodedKey, bool keyUp) noexcept;

    /* This feature has not been enabled (code from proof of concept stage)
        // Function to change a key's behavior from toggle to modifier
        __declspec(dllexport) intptr_t HandleSingleKeyToggleToModEvent(InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;
    */

    // Function to handle a shortcut remap
    intptr_t HandleShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state, const std::optional<std::wstring>& activatedApp = std::nullopt) noexcept;

    // Function to reset chord matching
    void ResetAllStartedChords(State& state, const std::optional<std::wstring>& activatedApp);

    // Function to reset chord matching
    void ResetAllOtherStartedChords(State& state, const std::optional<std::wstring>& activatedApp, DWORD keyToKeep);

    std::wstring URL_encode(const std::wstring& value);

    std::wstring ConvertPathToURI(const std::wstring& filePath);

    // Function to reset chord matching if needed
    ResetChordsResults ResetChordsIfNeeded(LowlevelKeyboardEvent* data, State& state, const std::optional<std::wstring>& activatedApp);

    // Function to handle (start or show) programs for shortcuts
    void CreateOrShowProcessForShortcut(Shortcut shortcut, const ProgramLauncher::LaunchCallback& launchProgram = {}) noexcept;

    void CloseProcessByName(const std::wstring& fileNamePart);

    // Returns whether matching processes were found, even if none could be opened.
    bool CloseAndEndProcessesByName(const std::wstring& fileNamePart);

    namespace ProcessCloser
    {
        // Implementation details exposed for deterministic process-replacement tests.
        struct ProcessSnapshot
        {
            DWORD pid;
            FILETIME creationTime;
            std::wstring imagePath;
        };

        std::optional<ProcessSnapshot> SnapshotProcess(HANDLE process);
        // The caller keeps each identity's query handle open through this call.
        void CloseAndEndProcesses(const std::vector<ProcessSnapshot>& snapshots);
    }

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
    bool TryShowExistingProgram(DWORD pid, const std::wstring& programName);

    bool HideProgram(DWORD pid, std::wstring programName, int retryCount);

    // Function to handle an os-level shortcut remap
    intptr_t HandleOSLevelShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Function to handle an app-specific shortcut remap
    intptr_t HandleAppSpecificShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Function to generate a unicode string in response to a single keypress
    intptr_t HandleSingleKeyToTextRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state);

    // Function to ensure Ctrl/Shift/Alt modifier key state is not detected as pressed down by applications which detect keys at a lower level than hooks when it is remapped for scenarios where its required
    void ResetIfModifierKeyForLowerLevelKeyHandlers(KeyboardManagerInput::InputInterface& ii, DWORD key, DWORD target);

    // Runs one low-level keyboard event through the remap handlers, in their fixed order. Returns 1
    // to suppress the event and 0 to pass it on. remappingSuspended is true while remapping is
    // bypassed for a moment (settings are being loaded, or the editor window is open); the mapping
    // tables are not read then, but physical presses are still paired with their releases. This is the
    // whole body of the hook callback, kept apart from KeyboardManager so the unit tests can drive
    // the handler chain as a whole.
    intptr_t DispatchKeyboardEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state, bool remappingSuspended) noexcept;
};
