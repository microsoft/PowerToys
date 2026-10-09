#pragma once

namespace KBMEditor
{
    class KeyboardManagerState;
}

class MappingConfiguration;

// Function to create the Edit Shortcuts Window
void CreateEditShortcutsWindow(HINSTANCE hInst, KBMEditor::KeyboardManagerState& keyboardManagerState, MappingConfiguration& mappingConfiguration, std::wstring keysForShortcutToEdit, std::wstring action);

// Function to check if there is already a window active if yes bring to foreground
bool CheckEditShortcutsWindowActive();

// Function to close any active Edit Shortcuts window
void CloseActiveEditShortcutsWindow();