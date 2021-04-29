#pragma once

namespace KBMEditor
{
    class KeyboardManagerState;
}

class ShortcutsMapping;

// Function to create the Edit Keyboard Window
void CreateEditKeyboardWindow(HINSTANCE hInst, KBMEditor::KeyboardManagerState& keyboardManagerState, ShortcutsMapping& shortcutsMapping);

// Function to check if there is already a window active if yes bring to foreground
bool CheckEditKeyboardWindowActive();

// Function to close any active Edit Keyboard window
void CloseActiveEditKeyboardWindow();