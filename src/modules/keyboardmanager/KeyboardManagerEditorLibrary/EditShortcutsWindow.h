#pragma once

namespace KBMEditor
{
    class KeyboardManagerState;
}

class ShortcutsMapping;

// Function to create the Edit Shortcuts Window
void CreateEditShortcutsWindow(HINSTANCE hInst, KBMEditor::KeyboardManagerState& keyboardManagerState, ShortcutsMapping& shortcutsMapping);

// Function to check if there is already a window active if yes bring to foreground
bool CheckEditShortcutsWindowActive();

// Function to close any active Edit Shortcuts window
void CloseActiveEditShortcutsWindow();