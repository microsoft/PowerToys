#pragma once
#include <common/utils/event_waiter.h>
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardmanager/common/Input.h>

class KeyboardManager
{
private:
    //contains the non localized key of the powertoy
    std::wstring app_key = KeyboardManagerConstants::ModuleName;

    // Low level hook handles
    static HHOOK hook_handle;

    // Required for Unhook in old versions of Windows
    static HHOOK hook_handle_copy;

    // Static pointer to the current keyboardmanager object required for accessing the HandleKeyboardHookEvent function in the hook procedure (Only global or static variables can be accessed in a hook procedure CALLBACK)
    static KeyboardManager* keyboardmanager_object_ptr;

    // Variable which stores all the state information to be shared between the UI and back-end
    KeyboardManagerState keyboardManagerState;

    // Object of class which implements InputInterface. Required for calling library functions while enabling testing
    KeyboardManagerInput::Input inputHandler;

    event_waiter eventWaiter;

    std::atomic_bool loadingSettings = false;
public:
    // Constructor
    KeyboardManager();

    // Load settings from the file.
    void load_settings();

    // Hook procedure definition
    static LRESULT CALLBACK hook_proc(int nCode, WPARAM wParam, LPARAM lParam);

    void start_lowlevel_keyboard_hook();

    // Function to terminate the low level hook
    void stop_lowlevel_keyboard_hook();

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
};