#pragma once

#include <common/hooks/LowlevelKeyboardEvent.h>
#include "State.h"

namespace KeyboardManagerInput
{
    class InputInterface;
}

namespace KeyboardEventHandlers
{

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

    // Function to generate a unicode string in response to a single keypress
    intptr_t HandleSingleKeyToTextRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state);

    // Function to ensure Ctrl/Shift/Alt modifier key state is not detected as pressed down by applications which detect keys at a lower level than hooks when it is remapped for scenarios where its required
    void ResetIfModifierKeyForLowerLevelKeyHandlers(KeyboardManagerInput::InputInterface& ii, DWORD key, DWORD target);
};
