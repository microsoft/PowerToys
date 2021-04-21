// KeyboardManagerEditor.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "KeyboardManagerEditor.h"

#include <common/utils/winapi_error.h>

#include <KeyboardEventHandlers.h>
#include <KeyboardManagerState.h>
#include <SettingsHelper.h>

#include <EditKeyboardWindow.h>
#include <EditShortcutsWindow.h>
#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandlerX64.h>

std::unique_ptr<KeyboardManagerEditor> editor = nullptr;

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                     _In_opt_ HINSTANCE hPrevInstance,
                     _In_ LPWSTR    lpCmdLine,
                     _In_ int       nCmdShow)
{
    UNREFERENCED_PARAMETER(hPrevInstance);

    LoggerHelpers::init_logger(KeyboardManagerConstants::ModuleName, L"Editor", LogSettings::keyboardManagerLoggerName);
    InitUnhandledExceptionHandler();

    int numArgs;
    LPWSTR* cmdArgs = CommandLineToArgvW(GetCommandLineW(), &numArgs);

    if (cmdArgs == nullptr)
    {
        Logger::error(L"Keyboard Manager Editor cannot start as a standalone application");
        return -1;
    }

    if (numArgs != 2)
    {
        Logger::error(L"Invalid arguments on Keyboard Manager Editor start");
        return -1;
    }

    editor = std::make_unique<KeyboardManagerEditor>(hInstance);
    if (!editor->startLowLevelKeyboardHook())
    {
        DWORD errorCode = GetLastError();
        show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Keyboard Manager Editor");
        auto errorMessage = get_last_error_message(errorCode);
        Logger::error(L"Unable to start keyboard hook: {}", errorMessage.has_value() ? errorMessage.value() : L"");
        // TODO: Trace::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"start_lowlevel_keyboard_hook.SetWindowsHookEx");
        
        return -1;
    }
    
    KeyboardManagerEditorType type = static_cast<KeyboardManagerEditorType>(_wtoi(cmdArgs[1]));
    editor->openEditorWindow(type);
    
    editor = nullptr;

    return 0;
}

KeyboardManagerEditor::KeyboardManagerEditor(HINSTANCE hInst) :
    hInstance(hInst)
{
    SettingsHelper::loadConfig(keyboardManagerState);
    startLowLevelKeyboardHook();
}

KeyboardManagerEditor::~KeyboardManagerEditor()
{
    UnhookWindowsHookEx(hook);
}

bool KeyboardManagerEditor::startLowLevelKeyboardHook()
{
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
    if (IsDebuggerPresent())
    {
        return true;
    }
#endif

    hook = SetWindowsHookEx(WH_KEYBOARD_LL, KeyHookProc, GetModuleHandle(NULL), NULL);
    return (hook != nullptr);
}

void KeyboardManagerEditor::openEditorWindow(KeyboardManagerEditorType type)
{
    switch (type)
    {
    case KeyboardManagerEditorType::KeyEditor:
        createEditKeyboardWindow(hInstance, keyboardManagerState);
        break;
    case KeyboardManagerEditorType::ShortcutEditor:
        createEditShortcutsWindow(hInstance, keyboardManagerState);
    }
}

intptr_t KeyboardManagerEditor::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    // If the Detect Key Window is currently activated, then suppress the keyboard event
    KeyboardManagerHelper::KeyboardHookDecision singleKeyRemapUIDetected = keyboardManagerState.DetectSingleRemapKeyUIBackend(data);
    if (singleKeyRemapUIDetected == KeyboardManagerHelper::KeyboardHookDecision::Suppress)
    {
        return 1;
    }
    else if (singleKeyRemapUIDetected == KeyboardManagerHelper::KeyboardHookDecision::SkipHook)
    {
        return 0;
    }

    // If the Detect Shortcut Window from Remap Keys is currently activated, then suppress the keyboard event
    KeyboardManagerHelper::KeyboardHookDecision remapKeyShortcutUIDetected = keyboardManagerState.DetectShortcutUIBackend(data, true);
    if (remapKeyShortcutUIDetected == KeyboardManagerHelper::KeyboardHookDecision::Suppress)
    {
        return 1;
    }
    else if (remapKeyShortcutUIDetected == KeyboardManagerHelper::KeyboardHookDecision::SkipHook)
    {
        return 0;
    }

    // If the Detect Shortcut Window is currently activated, then suppress the keyboard event
    KeyboardManagerHelper::KeyboardHookDecision shortcutUIDetected = keyboardManagerState.DetectShortcutUIBackend(data, false);
    if (shortcutUIDetected == KeyboardManagerHelper::KeyboardHookDecision::Suppress)
    {
        return 1;
    }
    else if (shortcutUIDetected == KeyboardManagerHelper::KeyboardHookDecision::SkipHook)
    {
        return 0;
    }

    return 0;
}

// Hook procedure definition
LRESULT KeyboardManagerEditor::KeyHookProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    LowlevelKeyboardEvent event;
    if (nCode == HC_ACTION)
    {
        event.lParam = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
        event.wParam = wParam;
        if (editor->HandleKeyboardHookEvent(&event) == 1)
        {
            // Reset Num Lock whenever a NumLock key down event is suppressed since Num Lock key state change occurs before it is intercepted by low level hooks
            if (event.lParam->vkCode == VK_NUMLOCK && (event.wParam == WM_KEYDOWN || event.wParam == WM_SYSKEYDOWN) && event.lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
            {
                KeyboardEventHandlers::SetNumLockToPreviousState(editor->getInputHandler());
            }
            return 1;
        }
    }

    return CallNextHookEx(hook, nCode, wParam, lParam);
}
