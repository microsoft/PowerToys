#pragma once
#include <keyboardmanager/common/KeyboardManagerState.h>

// Function to create the Edit Keyboard Window
__declspec(dllexport) void createEditKeyboardWindow(HINSTANCE hInst, KeyboardManagerState& keyboardManagerState);

// Function to check if there is already a window active if yes bring to foreground.
__declspec(dllexport) bool CheckEditKeyboardWindowActive();