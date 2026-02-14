#include "pch.h"
#include "Clipboard.h"
#include "Clipboard.g.cpp"

const static inline ULONG_PTR CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG = 0x110;

void try_inject_modifier_key_up(std::vector<INPUT>& inputs, short modifier)
{
    // Most significant bit is set if key is down
    if ((GetAsyncKeyState(static_cast<int>(modifier)) & 0x8000) != 0)
    {
        INPUT input_event = {};
        input_event.type = INPUT_KEYBOARD;
        input_event.ki.wVk = modifier;
        input_event.ki.dwFlags = KEYEVENTF_KEYUP;
        inputs.push_back(input_event);
    }
}

void try_inject_modifier_key_restore(std::vector<INPUT>& inputs, short modifier)
{
    // Most significant bit is set if key is down
    if ((GetAsyncKeyState(static_cast<int>(modifier)) & 0x8000) != 0)
    {
        INPUT input_event = {};
        input_event.type = INPUT_KEYBOARD;
        input_event.ki.wVk = modifier;
        inputs.push_back(input_event);
    }
}

namespace winrt::PowerToys::Interop::implementation
{
    void Clipboard::PasteAsPlainText()
    {
        std::wstring clipboard_text;

        {
            // Read clipboard data begin

            if (!OpenClipboard(NULL))
            {
                return;
            }
            HANDLE h_clipboard_data = GetClipboardData(CF_UNICODETEXT);

            if (h_clipboard_data == NULL)
            {
                CloseClipboard();
                return;
            }

            wchar_t* pch_data = static_cast<wchar_t*>(GlobalLock(h_clipboard_data));

            if (NULL == pch_data)
            {
                CloseClipboard();
                return;
            }

            clipboard_text = pch_data;
            GlobalUnlock(h_clipboard_data);

            CloseClipboard();
            // Read clipboard data end
        }

        {
            // Copy text to clipboard begin
            UINT no_clipboard_history_or_roaming_format = 0;

            // Get the format identifier for not adding the data to the clipboard history or roaming.
            // https://learn.microsoft.com/windows/win32/dataxchg/clipboard-formats#cloud-clipboard-and-clipboard-history-formats
            if (0 == (no_clipboard_history_or_roaming_format = RegisterClipboardFormat(L"ExcludeClipboardContentFromMonitorProcessing")))
            {
                return;
            }

            if (!OpenClipboard(NULL))
            {
                return;
            }

            HGLOBAL h_clipboard_data;

            if (NULL == (h_clipboard_data = GlobalAlloc(GMEM_MOVEABLE, (clipboard_text.length() + 1) * sizeof(wchar_t))))
            {
                CloseClipboard();
                return;
            }
            wchar_t* pch_data = static_cast<wchar_t*>(GlobalLock(h_clipboard_data));

            if (NULL == pch_data)
            {
                GlobalFree(h_clipboard_data);
                CloseClipboard();
                return;
            }

            wcscpy_s(pch_data, clipboard_text.length() + 1, clipboard_text.c_str());

            EmptyClipboard();

            if (NULL == SetClipboardData(CF_UNICODETEXT, h_clipboard_data))
            {
                GlobalUnlock(h_clipboard_data);
                GlobalFree(h_clipboard_data);
                CloseClipboard();
                return;
            }

            // Don't show in history or allow data roaming.
            SetClipboardData(no_clipboard_history_or_roaming_format, 0);

            CloseClipboard();
            // Copy text to clipboard end
        }
        {
            // Clear kb state and send Ctrl+V begin

            // we can assume that the last pressed key is...
            //  (1) not a modifier key and
            //  (2) marked as handled (so it never gets a key down input event).
            // So, let's check which modifiers were pressed,
            // and, if they were, inject a key up event for each of them
            std::vector<INPUT> inputs;
            try_inject_modifier_key_up(inputs, VK_LCONTROL);
            try_inject_modifier_key_up(inputs, VK_RCONTROL);
            try_inject_modifier_key_up(inputs, VK_LWIN);
            try_inject_modifier_key_up(inputs, VK_RWIN);
            try_inject_modifier_key_up(inputs, VK_LSHIFT);
            try_inject_modifier_key_up(inputs, VK_RSHIFT);
            try_inject_modifier_key_up(inputs, VK_LMENU);
            try_inject_modifier_key_up(inputs, VK_RMENU);

            // send Ctrl+V (key downs and key ups)
            {
                INPUT input_event = {};
                input_event.type = INPUT_KEYBOARD;
                input_event.ki.wVk = VK_CONTROL;
                inputs.push_back(input_event);
            }

            {
                INPUT input_event = {};
                input_event.type = INPUT_KEYBOARD;
                input_event.ki.wVk = 0x56; // V
                // Avoid triggering detection by the centralized keyboard hook. Allows using Control+V as activation.
                input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
                inputs.push_back(input_event);
            }

            {
                INPUT input_event = {};
                input_event.type = INPUT_KEYBOARD;
                input_event.ki.wVk = 0x56; // V
                input_event.ki.dwFlags = KEYEVENTF_KEYUP;
                // Avoid triggering detection by the centralized keyboard hook. Allows using Control+V as activation.
                input_event.ki.dwExtraInfo = CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG;
                inputs.push_back(input_event);
            }

            {
                INPUT input_event = {};
                input_event.type = INPUT_KEYBOARD;
                input_event.ki.wVk = VK_CONTROL;
                input_event.ki.dwFlags = KEYEVENTF_KEYUP;
                inputs.push_back(input_event);
            }

            try_inject_modifier_key_restore(inputs, VK_LCONTROL);
            try_inject_modifier_key_restore(inputs, VK_RCONTROL);
            try_inject_modifier_key_restore(inputs, VK_LWIN);
            try_inject_modifier_key_restore(inputs, VK_RWIN);
            try_inject_modifier_key_restore(inputs, VK_LSHIFT);
            try_inject_modifier_key_restore(inputs, VK_RSHIFT);
            try_inject_modifier_key_restore(inputs, VK_LMENU);
            try_inject_modifier_key_restore(inputs, VK_RMENU);

            // After restoring the modifier keys send a dummy key to prevent Start Menu from activating
            INPUT dummyEvent = {};
            dummyEvent.type = INPUT_KEYBOARD;
            dummyEvent.ki.wVk = 0xFF;
            dummyEvent.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs.push_back(dummyEvent);

            auto uSent = SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
            if (uSent != inputs.size())
            {
                return;
            }

            // Clear kb state and send Ctrl+V end
        }
    }
}
