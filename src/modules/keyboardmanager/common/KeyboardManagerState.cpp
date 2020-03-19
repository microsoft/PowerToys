#include "KeyboardManagerState.h"

bool KeyboardManagerState::CheckUIState(KeyboardManagerUIState state)
{
    if (uiState == state)
    {
        if (uiState == KeyboardManagerUIState::Deactivated)
        {
            return true;
        }
        // If the UI state is activated then we also have to ensure that the UI window is in focus.
        // GetForegroundWindow can be used here since we only need to check the main parent window and not the sub windows within the content dialog. Using GUIThreadInfo will give more specific sub-windows within the XAML window which is not needed.
        else if (currentUIWindow == GetForegroundWindow())
        {
            return true;
        }
    }

    return false;
}

void KeyboardManagerState::SetCurrentUIWindow(HWND windowHandle)
{
    currentUIWindow = windowHandle;
}

void KeyboardManagerState::SetUIState(KeyboardManagerUIState state, HWND windowHandle)
{
    uiState = state;
    currentUIWindow = windowHandle;
}

void KeyboardManagerState::ResetUIState()
{
    SetUIState(KeyboardManagerUIState::Deactivated);
}