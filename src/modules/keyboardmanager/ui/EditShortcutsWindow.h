#pragma once
#include "keyboardmanager/common/KeyboardManagerState.h"
#include "keyboardmanager/common/Shortcut.h"
#include "keyboardmanager/common/Helpers.h"

// Function to create the Edit Shortcuts Window
void createEditShortcutsWindow(HINSTANCE hInst, KeyboardManagerState& keyboardManagerState);

// Function to check if there is already a window active if yes bring to foreground
bool CheckEditShortcutsWindowActive();

// Function to close any active Edit Shortcuts window
void CloseActiveEditShortcutsWindow();