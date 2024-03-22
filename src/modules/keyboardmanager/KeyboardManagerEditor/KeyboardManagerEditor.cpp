// KeyboardManagerEditor.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "KeyboardManagerEditor.h"

#include <common/utils/winapi_error.h>
#include <common/utils/logger_helper.h>
#include <common/utils/ProcessWaiter.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/gpo.h>

#include <trace.h>

#include <keyboardmanager/common/KeyboardEventHandlers.h>

#include <EditKeyboardWindow.h>
#include <EditShortcutsWindow.h>
#include <KeyboardManagerState.h>

std::unique_ptr<KeyboardManagerEditor> editor = nullptr;
const std::wstring instanceMutexName = L"Local\\PowerToys_KBMEditor_InstanceMutex";

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                      _In_opt_ HINSTANCE /*hPrevInstance*/,
                      _In_ LPWSTR /*lpCmdLine*/,
                      _In_ int /*nCmdShow*/)
{
    LoggerHelpers::init_logger(KeyboardManagerConstants::ModuleName, L"Editor", LogSettings::keyboardManagerLoggerName);

    if (powertoys_gpo::getConfiguredKeyboardManagerEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        return 0;
    }

    InitUnhandledExceptionHandler();
    Trace::RegisterProvider();

    auto mutex = CreateMutex(nullptr, true, instanceMutexName.c_str());
    if (mutex == nullptr)
    {
        Logger::error(L"Failed to create mutex. {}", get_last_error_or_default(GetLastError()));
    }
    else
    {
        Logger::trace(L"Created/Opened {} mutex", instanceMutexName);
    }

    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        Logger::info(L"KBM editor instance is already running");
        return 0;
    }

    int numArgs;
    LPWSTR* cmdArgs = CommandLineToArgvW(GetCommandLineW(), &numArgs);

    KeyboardManagerEditorType type = KeyboardManagerEditorType::KeyEditor;
    if (!IsDebuggerPresent())
    {
        if (cmdArgs == nullptr)
        {
            Logger::error(L"Keyboard Manager Editor cannot start as a standalone application");
            return -1;
        }

        if (numArgs < 2)
        {
            Logger::error(L"Invalid arguments on Keyboard Manager Editor start");
            return -1;
        }
    }

    if (numArgs > 1)
    {
        type = static_cast<KeyboardManagerEditorType>(_wtoi(cmdArgs[1]));
    }

    std::wstring keysForShortcutToEdit = L"";
    std::wstring action = L"";    


    // do some parsing of the cmdline arg to see if we need to behave different
    // like, single edit mode, or "delete" mode.    
    // These extra args are from "OpenEditor" in the KeyboardManagerViewModel
    if (numArgs >= 3)
    {
        if (numArgs >= 4)
        {
            keysForShortcutToEdit = std::wstring(cmdArgs[3]);
        }

        if (numArgs >= 5)
        {
            action = std::wstring(cmdArgs[4]);
        }


        std::wstring pid = std::wstring(cmdArgs[2]);
        Logger::trace(L"Editor started from the settings with pid {}", pid);
        if (!pid.empty())
        {
            auto mainThreadId = GetCurrentThreadId();
            ProcessWaiter::OnProcessTerminate(pid, [mainThreadId](int err) {
                if (err != ERROR_SUCCESS)
                {
                    Logger::error(L"Failed to wait for parent process exit. {}", get_last_error_or_default(err));
                }

                Logger::trace(L"Parent process exited. Exiting KeyboardManager editor");
                PostThreadMessage(mainThreadId, WM_QUIT, 0, 0);
            });
        }
    }

    editor = std::make_unique<KeyboardManagerEditor>(hInstance);
    if (!editor->StartLowLevelKeyboardHook())
    {
        DWORD errorCode = GetLastError();
        show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Keyboard Manager Editor");
        auto errorMessage = get_last_error_message(errorCode);
        Logger::error(L"Unable to start keyboard hook: {}", errorMessage.has_value() ? errorMessage.value() : L"");
        Trace::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"start_lowlevel_keyboard_hook.SetWindowsHookEx");

        return -1;
    }

    editor->OpenEditorWindow(type, keysForShortcutToEdit, action);

    editor = nullptr;

    Trace::UnregisterProvider();
    return 0;
}

KeyboardManagerEditor::KeyboardManagerEditor(HINSTANCE hInst) :
    hInstance(hInst)
{
    bool loadedSuccessful = mappingConfiguration.LoadSettings();
    if (!loadedSuccessful)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(500));

        // retry once
        mappingConfiguration.LoadSettings();
    }

    StartLowLevelKeyboardHook();
}

KeyboardManagerEditor::~KeyboardManagerEditor()
{
    UnhookWindowsHookEx(hook);
}

bool KeyboardManagerEditor::StartLowLevelKeyboardHook()
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

void KeyboardManagerEditor::OpenEditorWindow(KeyboardManagerEditorType type, std::wstring keysForShortcutToEdit, std::wstring action)
{
    switch (type)
    {
    case KeyboardManagerEditorType::KeyEditor:
        CreateEditKeyboardWindow(hInstance, keyboardManagerState, mappingConfiguration);
        break;
    case KeyboardManagerEditorType::ShortcutEditor:
        CreateEditShortcutsWindow(hInstance, keyboardManagerState, mappingConfiguration, keysForShortcutToEdit, action);
    }
}

intptr_t KeyboardManagerEditor::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    // If the Detect Key Window is currently activated, then suppress the keyboard event
    Helpers::KeyboardHookDecision singleKeyRemapUIDetected = keyboardManagerState.DetectSingleRemapKeyUIBackend(data);
    if (singleKeyRemapUIDetected == Helpers::KeyboardHookDecision::Suppress)
    {
        return 1;
    }
    else if (singleKeyRemapUIDetected == Helpers::KeyboardHookDecision::SkipHook)
    {
        return 0;
    }

    // If the Detect Shortcut Window from Remap Keys is currently activated, then suppress the keyboard event
    Helpers::KeyboardHookDecision remapKeyShortcutUIDetected = keyboardManagerState.DetectShortcutUIBackend(data, true);
    if (remapKeyShortcutUIDetected == Helpers::KeyboardHookDecision::Suppress)
    {
        return 1;
    }
    else if (remapKeyShortcutUIDetected == Helpers::KeyboardHookDecision::SkipHook)
    {
        return 0;
    }

    // If the Detect Shortcut Window is currently activated, then suppress the keyboard event
    Helpers::KeyboardHookDecision shortcutUIDetected = keyboardManagerState.DetectShortcutUIBackend(data, false);
    if (shortcutUIDetected == Helpers::KeyboardHookDecision::Suppress)
    {
        return 1;
    }
    else if (shortcutUIDetected == Helpers::KeyboardHookDecision::SkipHook)
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
        event.lParam->vkCode = Helpers::EncodeKeyNumpadOrigin(event.lParam->vkCode, event.lParam->flags & LLKHF_EXTENDED);

        if (editor->HandleKeyboardHookEvent(&event) == 1)
        {
            // Reset Num Lock whenever a NumLock key down event is suppressed since Num Lock key state change occurs before it is intercepted by low level hooks
            if (event.lParam->vkCode == VK_NUMLOCK && (event.wParam == WM_KEYDOWN || event.wParam == WM_SYSKEYDOWN) && event.lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
            {
                KeyboardEventHandlers::SetNumLockToPreviousState(editor->GetInputHandler());
            }
            return 1;
        }
    }

    return CallNextHookEx(hook, nCode, wParam, lParam);
}
