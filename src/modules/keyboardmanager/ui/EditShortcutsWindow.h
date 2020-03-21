#pragma once
#include "keyboardmanager/common/KeyboardManagerState.h"
#include "keyboardmanager/common/Helpers.h"

// Function to create the Edit Shortcuts Window
__declspec(dllexport) void createEditShortcutsWindow(HINSTANCE hInst, KeyboardManagerState& keyboardManagerState);