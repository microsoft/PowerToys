#pragma once

#include "KeyboardListener.g.h"
#include <mutex>
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
        bool doNotActivateOnGameMode{ true };
        std::chrono::milliseconds inputTime{ 300 }; // Should match with UI.Library.PowerAccentSettings.DefaultInputTimeMs
        std::vector<std::wstring> excludedApps;
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
        void SetIsLanguageLetterDelegate(IsLanguageLetter IsLanguageLetterDelegate);

        void UpdateActivationKey(int32_t activationKey);
        void UpdateDoNotActivateOnGameMode(bool doNotActivateOnGameMode);
        void UpdateInputTime(int32_t inputTime);
        void UpdateExcludedApps(std::wstring_view excludedApps);

        static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);

    private:
        bool OnKeyDown(KBDLLHOOKSTRUCT info) noexcept;
        bool OnKeyUp(KBDLLHOOKSTRUCT info) noexcept;
        bool IsSuppressedByGameMode();
        bool IsForegroundAppExcluded();

        static inline KeyboardListener* s_instance;
        HHOOK s_llKeyboardHook = nullptr;
        bool m_toolbarVisible;
        PowerAccentSettings m_settings;
        std::function<void(LetterKey)> m_showToolbarCb;
        std::function<void(InputType)> m_hideToolbarCb;
        std::function<void(TriggerKey, bool)> m_nextCharCb;
        std::function<bool(LetterKey)> m_isLanguageLetterCb;
        bool m_triggeredWithSpace;
        bool m_triggeredWithLeftArrow;
        bool m_triggeredWithRightArrow;
        spdlog::stopwatch m_stopwatch;
        bool m_leftShiftPressed;
        bool m_rightShiftPressed;

        std::mutex m_mutex_excluded_apps;
        std::pair<HWND, bool> m_prevForegroundAppExcl{ NULL, false };

        static inline const std::vector<LetterKey> letters = { LetterKey::VK_0,
                                                               LetterKey::VK_1,
                                                               LetterKey::VK_2,
                                                               LetterKey::VK_3,
                                                               LetterKey::VK_4,
                                                               LetterKey::VK_5,
                                                               LetterKey::VK_6,
                                                               LetterKey::VK_7,
                                                               LetterKey::VK_8,
                                                               LetterKey::VK_9,
                                                               LetterKey::VK_A,
                                                               LetterKey::VK_B,
                                                               LetterKey::VK_C,
                                                               LetterKey::VK_D,
                                                               LetterKey::VK_E,
                                                               LetterKey::VK_F,
                                                               LetterKey::VK_G,
                                                               LetterKey::VK_H,
                                                               LetterKey::VK_I,
                                                               LetterKey::VK_J,
                                                               LetterKey::VK_K,
                                                               LetterKey::VK_L,
                                                               LetterKey::VK_M,
                                                               LetterKey::VK_N,
                                                               LetterKey::VK_O,
                                                               LetterKey::VK_P,
                                                               LetterKey::VK_Q,
                                                               LetterKey::VK_R,
                                                               LetterKey::VK_S,
                                                               LetterKey::VK_T,
                                                               LetterKey::VK_U,
                                                               LetterKey::VK_V,
                                                               LetterKey::VK_W,
                                                               LetterKey::VK_X,
                                                               LetterKey::VK_Y,
                                                               LetterKey::VK_Z,
                                                               LetterKey::VK_PLUS,
                                                               LetterKey::VK_COMMA,
                                                               LetterKey::VK_PERIOD,
                                                               LetterKey::VK_MINUS,
                                                               LetterKey::VK_SLASH_,
                                                               LetterKey::VK_DIVIDE_,
                                                               LetterKey::VK_MULTIPLY_,
                                                               LetterKey::VK_BACKSLASH, };
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
