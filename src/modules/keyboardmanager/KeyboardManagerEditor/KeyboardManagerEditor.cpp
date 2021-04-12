// KeyboardManagerEditor.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "KeyboardManagerEditor.h"

#include <KeyboardManagerState.h>
#include <EditKeyboardWindow.h>
#include <EditShortcutsWindow.h>

enum class KeyboardManagerEditorType
{
    KeyEditor = 0,
    ShortcutEditor,
};

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                     _In_opt_ HINSTANCE hPrevInstance,
                     _In_ LPWSTR    lpCmdLine,
                     _In_ int       nCmdShow)
{
    UNREFERENCED_PARAMETER(hPrevInstance);

    int numArgs;
    LPWSTR* cmdArgs = CommandLineToArgvW(GetCommandLineW(), &numArgs);

    if (cmdArgs == nullptr)
    {
        // TODO: report error
        return -1;
    }

    if (numArgs != 2)
    {
        // TODO: report error
        return -1;
    }

    KeyboardManagerEditorType type = static_cast<KeyboardManagerEditorType>(_wtoi(cmdArgs[1]));
    KeyboardManagerState state;

    switch (type)
    {
    case KeyboardManagerEditorType::KeyEditor:
        createEditKeyboardWindow(hInstance, state);
        break;
    case KeyboardManagerEditorType::ShortcutEditor:
        createEditShortcutsWindow(hInstance, state);
    }   

    return 0;
}
