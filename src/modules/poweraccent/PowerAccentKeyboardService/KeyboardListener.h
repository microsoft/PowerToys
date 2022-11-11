#pragma once

#include "KeyboardListener.g.h"

#include <spdlog/stopwatch.h>

namespace winrt::PowerToys::PowerAccentKeyboardService::implementation
{
    enum PowerAccentActivationKey
    {
        LeftRightArrow,
        Space,
        Both,
    };

    struct PowerAccentSettings
    {
        PowerAccentActivationKey activationKey{ PowerAccentActivationKey::Both };
        std::chrono::milliseconds inputTime{ 200 };
    };

    struct KeyboardListener : KeyboardListenerT<KeyboardListener>
    {
        using LetterKey = winrt::PowerToys::PowerAccentKeyboardService::LetterKey;
        using TriggerKey = winrt::PowerToys::PowerAccentKeyboardService::TriggerKey;
        using InputType = winrt::PowerToys::PowerAccentKeyboardService::InputType;

        KeyboardListener();

        void KeyboardListener::InitHook();
        void KeyboardListener::UnInitHook();
        void SetShowToolbarEvent(ShowToolbar showToolbarEvent);
        void SetHideToolbarEvent(HideToolbar hideToolbarEvent);
        void SetNextCharEvent(NextChar NextCharEvent);

        void UpdateActivationKey(int32_t activationKey);
        void UpdateInputTime(int32_t inputTime);

        static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);

    private:
        bool OnKeyDown(KBDLLHOOKSTRUCT info) noexcept;
        bool OnKeyUp(KBDLLHOOKSTRUCT info) noexcept;

        static inline KeyboardListener* s_instance;
        HHOOK s_llKeyboardHook = nullptr;
        bool m_toolbarVisible;
        PowerAccentSettings m_settings;
        std::function<void(LetterKey)> m_showToolbarCb;
        std::function<void(InputType)> m_hideToolbarCb;
        std::function<void(TriggerKey)> m_nextCharCb;
        bool m_triggeredWithSpace;
        spdlog::stopwatch m_stopwatch;

        static inline const std::vector<LetterKey> letters = { LetterKey::VK_A,
                                                            LetterKey::VK_C,
                                                            LetterKey::VK_E,
                                                            LetterKey::VK_I,
                                                            LetterKey::VK_N,
                                                            LetterKey::VK_O,
                                                            LetterKey::VK_S,
                                                            LetterKey::VK_U,
                                                            LetterKey::VK_Y };
        LetterKey letterPressed{};

        static inline const std::vector<TriggerKey> triggers = { TriggerKey::Right, TriggerKey::Left, TriggerKey::Space };
    };
}

namespace winrt::PowerToys::PowerAccentKeyboardService::factory_implementation
{
    struct KeyboardListener : KeyboardListenerT<KeyboardListener, implementation::KeyboardListener>
    {
    };
}
