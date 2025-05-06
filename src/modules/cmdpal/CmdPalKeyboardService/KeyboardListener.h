#pragma once

#include "KeyboardListener.g.h"
#include <mutex>
#include <spdlog/stopwatch.h>
#include <set>

namespace winrt::CmdPalKeyboardService::implementation
{
    struct KeyboardListener : KeyboardListenerT<KeyboardListener>
    {
        struct Hotkey
        {
            bool win = false;
            bool ctrl = false;
            bool shift = false;
            bool alt = false;
            unsigned char key = 0;

            std::strong_ordering operator<=>(const Hotkey&) const = default;
        };

        struct HotkeyDescriptor
        {
            Hotkey hotkey;
            std::wstring id;

            bool operator<(const HotkeyDescriptor& other) const
            {
                return hotkey < other.hotkey;
            };
        };

        KeyboardListener();

        void Start();
        void Stop();
        void SetHotkeyAction(bool win, bool ctrl, bool shift, bool alt, uint8_t key, hstring const& id);
        void ClearHotkey(hstring const& id);
        void ClearHotkeys();
        void SetProcessCommand(ProcessCommand processCommand);

        static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);

    private:
        LRESULT CALLBACK DoLowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);

        static inline KeyboardListener* s_instance;
        HHOOK s_llKeyboardHook = nullptr;

        // Max DWORD for key code to disable keys.
        const DWORD VK_DISABLED = 0x100;
        DWORD vkCodePressed = VK_DISABLED;

        std::multiset<HotkeyDescriptor> hotkeyDescriptors;
        std::mutex mutex;

        std::function<void(hstring const&)> m_processCommandCb;
    };
}

namespace winrt::CmdPalKeyboardService::factory_implementation
{
    struct KeyboardListener : KeyboardListenerT<KeyboardListener, implementation::KeyboardListener>
    {
    };
}
