#pragma once

#include <keyboardmanager/common/Input.h>
#include <keyboardmanager/common/MappingConfiguration.h>

#include <KeyboardManagerState.h>

enum class KeyboardManagerEditorType
{
    KeyEditor = 0,
    ShortcutEditor,
};

class KeyboardManagerEditor
{
public:
    KeyboardManagerEditor(HINSTANCE hInstance);
    ~KeyboardManagerEditor();

    KeyboardManagerInput::Input& GetInputHandler() noexcept
    {
        return inputHandler;
    }

    bool StartLowLevelKeyboardHook();
    void OpenEditorWindow(KeyboardManagerEditorType type, std::wstring keysForShortcutToEdit, std::wstring action);

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;

private:
    static LRESULT CALLBACK KeyHookProc(int nCode, WPARAM wParam, LPARAM lParam);

    inline static HHOOK hook;
    HINSTANCE hInstance;

    KBMEditor::KeyboardManagerState keyboardManagerState;
    MappingConfiguration mappingConfiguration;

    // Object of class which implements InputInterface. Required for calling library functions while enabling testing
    KeyboardManagerInput::Input inputHandler;
};
