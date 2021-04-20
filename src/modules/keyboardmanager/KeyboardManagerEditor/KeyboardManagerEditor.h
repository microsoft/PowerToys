#pragma once

#include <KeyboardManagerState.h>
#include <Input.h>

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

    KeyboardManagerInput::Input& getInputHandler() noexcept
    {
        return inputHandler;
    }

    bool startLowLevelKeyboardHook();
    void openEditorWindow(KeyboardManagerEditorType type);

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;

private:
    static LRESULT CALLBACK KeyHookProc(int nCode, WPARAM wParam, LPARAM lParam);
    
    inline static HHOOK hook;
    HINSTANCE hInstance;

    KeyboardManagerState keyboardManagerState;
    
    // Object of class which implements InputInterface. Required for calling library functions while enabling testing
    KeyboardManagerInput::Input inputHandler;
};