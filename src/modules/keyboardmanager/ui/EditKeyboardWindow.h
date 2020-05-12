#pragma once
#include <keyboardmanager/common/KeyboardManagerState.h>

// Function to create the Edit Keyboard Window
void createEditKeyboardWindow(HINSTANCE hInst, KeyboardManagerState& keyboardManagerState);

// Function to check if there is already a window active if yes bring to foreground
bool CheckEditKeyboardWindowActive();

// Function to close any active Edit Keyboard window
void CloseActiveEditKeyboardWindow();