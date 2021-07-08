#pragma once

namespace KBMEditor
{
    class KeyboardManagerState;
}

class MappingConfiguration;

// Function to create the Edit Keyboard Window
void CreateEditKeyboardWindow(HINSTANCE hInst, KBMEditor::KeyboardManagerState& keyboardManagerState, MappingConfiguration& mappingConfiguration);

// Function to check if there is already a window active if yes bring to foreground
bool CheckEditKeyboardWindowActive();

// Function to close any active Edit Keyboard window
void CloseActiveEditKeyboardWindow();