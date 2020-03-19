#pragma once
#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

enum class KeyboardManagerUIState
{
    // If set to this value then there is no keyboard manager window currently active that requires a hook
    Deactivated,
    // If set to this value then the detect key window is currently active and it requires a hook
    DetectKeyWindowActivated,
    // If set to this value then the detect shortcut window is currently active and it requires a hook
    DetectShortcutWindowActivated
};

class KeyboardManagerState
{
public:
    // State variable used to store which UI window is currently active that requires interaction with the hook
    KeyboardManagerUIState uiState;
    // Window handle for the current UI window which is active. Should be set to nullptr if UI is deactivated
    HWND currentUIWindow;

public:
    KeyboardManagerState() :
        uiState(KeyboardManagerUIState::Deactivated), currentUIWindow(nullptr)
    {
    }

    void ResetUIState();
    bool CheckUIState(KeyboardManagerUIState state);
    void SetCurrentUIWindow(HWND windowHandle);
    void SetUIState(KeyboardManagerUIState state, HWND windowHandle = nullptr);
};