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

    // Function to a handle a single key remap
    intptr_t HandleSingleKeyRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    /* This feature has not been enabled (code from proof of concept stage)
        // Function to a change a key's behavior from toggle to modifier
        __declspec(dllexport) intptr_t HandleSingleKeyToggleToModEvent(InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;
    */

    // Function to a handle a shortcut remap
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

    // Function to a find and show a running program
    bool ShowProgram(DWORD pid, std::wstring programName, bool isNewProcess, bool minimizeIfVisible, int retryCount);

    bool HideProgram(DWORD pid, std::wstring programName, int retryCount);

    // Function to a handle an os-level shortcut remap
    intptr_t HandleOSLevelShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Function to a handle an app-specific shortcut remap
    intptr_t HandleAppSpecificShortcutRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state) noexcept;

    // Function to generate a unicode string in response to a single keypress
    intptr_t HandleSingleKeyToTextRemapEvent(KeyboardManagerInput::InputInterface& ii, LowlevelKeyboardEvent* data, State& state);

    // Function to ensure Ctrl/Shift/Alt modifier key state is not detected as pressed down by applications which detect keys at a lower level than hooks when it is remapped for scenarios where its required
    void ResetIfModifierKeyForLowerLevelKeyHandlers(KeyboardManagerInput::InputInterface& ii, DWORD key, DWORD target);
};
