#include "pch.h"
#include "KeyboardListener.h"
#include "KeyboardListener.g.cpp"

#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/winapi_error.h>

namespace winrt::PowerToys::PowerAccentKeyboardService::implementation
{
    KeyboardListener::KeyboardListener() :
        m_toolbarVisible(false), m_triggeredWithSpace(false)
    {
        s_instance = this;
        LoggerHelpers::init_logger(L"PowerAccent", L"PowerAccentKeyboardService", "PowerAccent");
    }

    void KeyboardListener::InitHook()
    {
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
        const bool hook_disabled = IsDebuggerPresent();
#else
        const bool hook_disabled = false;
#endif

        if (!hook_disabled)
        {
            s_llKeyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandle(NULL), NULL);
            if (!s_llKeyboardHook)
            {
                DWORD errorCode = GetLastError();
                show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - PowerAccent");
                auto errorMessage = get_last_error_message(errorCode);
                Logger::error(errorMessage.has_value() ? errorMessage.value() : L"");
            }
        }
    }

    void KeyboardListener::UnInitHook()
    {
        if (s_llKeyboardHook)
        {
            if (UnhookWindowsHookEx(s_llKeyboardHook))
            {
                s_llKeyboardHook = nullptr;
            }
        }
    }

    void KeyboardListener::SetShowToolbarEvent(ShowToolbar showToolbarEvent)
    {
        m_showToolbarCb = [trigger = std::move(showToolbarEvent)](LetterKey key) {
            trigger(key);
        };
    }

    void KeyboardListener::SetHideToolbarEvent(HideToolbar hideToolbarEvent)
    {
        m_hideToolbarCb = [trigger = std::move(hideToolbarEvent)](InputType inputType) {
            trigger(inputType);
        };
    }

    void KeyboardListener::SetNextCharEvent(NextChar nextCharEvent)
    {
        m_nextCharCb = [trigger = std::move(nextCharEvent)](TriggerKey triggerKey) {
            trigger(triggerKey);
        };
    }

    void KeyboardListener::UpdateActivationKey(int32_t activationKey)
    {
        m_settings.activationKey = static_cast<PowerAccentActivationKey>(activationKey);
    }

    void KeyboardListener::UpdateInputTime(int32_t inputTime)
    {
        m_settings.inputTime = std::chrono::milliseconds(inputTime);
    }

    bool KeyboardListener::OnKeyDown(KBDLLHOOKSTRUCT info) noexcept
    {
        if (std::find(std::begin(letters), end(letters), static_cast<LetterKey>(info.vkCode)) != end(letters))
        {
            m_stopwatch.reset();
            letterPressed = static_cast<LetterKey>(info.vkCode);
        }

        UINT triggerPressed = 0;
        if (letterPressed != LetterKey::None)
        {
            if (std::find(std::begin(triggers), end(triggers), static_cast<TriggerKey>(info.vkCode)) != end(triggers))
            {
                triggerPressed = info.vkCode;

                if ((triggerPressed == VK_SPACE && m_settings.activationKey == PowerAccentActivationKey::LeftRightArrow) ||
                    ((triggerPressed == VK_LEFT || triggerPressed == VK_RIGHT) && m_settings.activationKey == PowerAccentActivationKey::Space))
                {
                    triggerPressed = 0;
                    Logger::info(L"Reset trigger key");
                }
            }
        }

        if (!m_toolbarVisible && letterPressed != LetterKey::None && triggerPressed)
        {
            Logger::info(L"Show toolbar. Letter: %d, Trigger: %d", letterPressed, triggerPressed);

            // Keep track if it was triggered with space so that it can be typed on false starts.
            m_triggeredWithSpace = triggerPressed == VK_SPACE;
            m_toolbarVisible = true;

            m_showToolbarCb(letterPressed);
        }

        if (m_toolbarVisible && triggerPressed)
        {
            if (triggerPressed == VK_LEFT)
            {
                Logger::info(L"Next toolbar position - left");
                m_nextCharCb(TriggerKey::Left);
            }
            else if (triggerPressed == VK_RIGHT)
            {
                Logger::info(L"Next toolbar position - right");
                m_nextCharCb(TriggerKey::Right);
            }
            else if (triggerPressed == VK_SPACE)
            {
                Logger::info(L"Next toolbar position - space");
                m_nextCharCb(TriggerKey::Space);
            }

            return true;
        }

        return false;
    }

    bool KeyboardListener::OnKeyUp(KBDLLHOOKSTRUCT info) noexcept
    {
        if (std::find(std::begin(letters), end(letters), static_cast<LetterKey>(info.vkCode)) != end(letters))
        {
            letterPressed = LetterKey::None;

            if (m_toolbarVisible)
            {
                if (m_stopwatch.elapsed() < m_settings.inputTime)
                {
                    Logger::info(L"Activation too fast. Do nothing.");

                    // False start, we should output the space if it was the trigger.
                    if (m_triggeredWithSpace)
                    {
                        m_hideToolbarCb(InputType::Space);
                    }
                    else
                    {
                        m_hideToolbarCb(InputType::None);
                    }

                    m_toolbarVisible = false;
                    return true;
                }

                Logger::info(L"Hide toolbar event and input char");

                m_hideToolbarCb(InputType::Char);

                m_toolbarVisible = false;
            }
        }

        return false;
    }

    LRESULT KeyboardListener::LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        {
            if (nCode == HC_ACTION && s_instance != nullptr)
            {
                KBDLLHOOKSTRUCT* key = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
                switch (wParam)
                {
                case WM_KEYDOWN:
                {
                    if (s_instance->OnKeyDown(*key))
                    {
                        return true;
                    }
                }
                break;
                case WM_KEYUP:
                {
                    if (s_instance->OnKeyUp(*key))
                    {
                        return true;
                    }
                }
                break;
                }
            }

            return CallNextHookEx(NULL, nCode, wParam, lParam);
        }
    }
}
