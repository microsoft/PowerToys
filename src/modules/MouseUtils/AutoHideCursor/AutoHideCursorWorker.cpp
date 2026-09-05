// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#include "AutoHideCursorState.h"
#include "SystemCursorHider.h"

#include <shellapi.h>
#include <string>

namespace
{
    constexpr UINT hideCursorMessage = WM_APP + 1;
    constexpr UINT showCursorMessage = WM_APP + 2;
    constexpr DWORD timerIntervalMs = 100;

    struct Options
    {
        DWORD parentProcessId = 0;
        std::wstring stopEventName;
        auto_hide_cursor::Configuration configuration;
    };

    bool TryParseUnsigned(const wchar_t* value, unsigned long& parsedValue)
    {
        wchar_t* end = nullptr;
        errno = 0;
        parsedValue = std::wcstoul(value, &end, 10);
        return errno == 0 && end != value && *end == L'\0';
    }

    bool TryParseOptions(int argc, wchar_t* argv[], Options& options)
    {
        if (argc != 11)
        {
            return false;
        }

        unsigned long parentProcessId = 0;
        unsigned long hideOnTyping = 0;
        unsigned long hideOnIdle = 0;
        unsigned long idleDelayMs = 0;
        if (std::wstring_view{ argv[1] } != L"--parent-pid" ||
            !TryParseUnsigned(argv[2], parentProcessId) ||
            std::wstring_view{ argv[3] } != L"--stop-event" ||
            std::wstring_view{ argv[5] } != L"--hide-on-typing" ||
            !TryParseUnsigned(argv[6], hideOnTyping) ||
            std::wstring_view{ argv[7] } != L"--hide-on-idle" ||
            !TryParseUnsigned(argv[8], hideOnIdle) ||
            std::wstring_view{ argv[9] } != L"--idle-delay-ms" ||
            !TryParseUnsigned(argv[10], idleDelayMs))
        {
            return false;
        }

        if (parentProcessId == 0 || argv[4][0] == L'\0' ||
            hideOnTyping > 1 || hideOnIdle > 1)
        {
            return false;
        }

        options.parentProcessId = static_cast<DWORD>(parentProcessId);
        options.stopEventName = argv[4];
        options.configuration.hideOnTyping = hideOnTyping != 0;
        options.configuration.hideOnIdle = hideOnIdle != 0;
        options.configuration.idleDelayMs = static_cast<std::uint32_t>(idleDelayMs);
        options.configuration = auto_hide_cursor::State::NormalizeConfiguration(options.configuration);
        return options.configuration.hideOnTyping || options.configuration.hideOnIdle;
    }

    class Worker
    {
    public:
        explicit Worker(const Options& options) :
            m_options{ options },
            m_state{ options.configuration, GetTickCount64(), GetCursorPosition() }
        {
        }

        int Run()
        {
            m_parentProcess.reset(OpenProcess(SYNCHRONIZE, FALSE, m_options.parentProcessId));
            m_stopEvent.reset(OpenEventW(SYNCHRONIZE, FALSE, m_options.stopEventName.c_str()));
            if (!m_parentProcess || !m_stopEvent)
            {
                return ERROR_INVALID_HANDLE;
            }

            m_threadId = GetCurrentThreadId();
            MSG message{};
            PeekMessageW(&message, nullptr, WM_USER, WM_USER, PM_NOREMOVE);

            s_instance = this;
            if (!InstallHooks())
            {
                s_instance = nullptr;
                UninstallHooks();
                return static_cast<int>(m_error);
            }

            const HANDLE waitHandles[] = { m_parentProcess.get(), m_stopEvent.get() };
            bool running = true;
            while (running)
            {
                const auto waitResult = MsgWaitForMultipleObjects(
                    static_cast<DWORD>(std::size(waitHandles)),
                    waitHandles,
                    FALSE,
                    timerIntervalMs,
                    QS_ALLINPUT);

                if (waitResult == WAIT_OBJECT_0 || waitResult == WAIT_OBJECT_0 + 1)
                {
                    running = false;
                }
                else if (waitResult == WAIT_OBJECT_0 + std::size(waitHandles))
                {
                    ProcessMessages();
                }
                else if (waitResult != WAIT_TIMEOUT)
                {
                    m_error = GetLastError();
                    running = false;
                }

                if (running && waitResult == WAIT_TIMEOUT)
                {
                    QueueAction(m_state.OnTimer(GetTickCount64(), GetCursorPosition()));
                    ProcessMessages();
                }

                if (m_error != ERROR_SUCCESS)
                {
                    running = false;
                }
            }

            UninstallHooks();
            ApplyAction(m_state.Stop());
            s_instance = nullptr;
            return static_cast<int>(m_error);
        }

    private:
        struct HandleCloser
        {
            void operator()(HANDLE handle) const noexcept
            {
                if (handle)
                {
                    CloseHandle(handle);
                }
            }
        };

        using unique_handle = std::unique_ptr<void, HandleCloser>;

        static auto_hide_cursor::Point GetCursorPosition() noexcept
        {
            POINT point{};
            GetCursorPos(&point);
            return { point.x, point.y };
        }

        bool InstallHooks()
        {
            const auto module = GetModuleHandleW(nullptr);
            m_mouseHook = SetWindowsHookExW(WH_MOUSE_LL, MouseHookProc, module, 0);
            if (!m_mouseHook)
            {
                m_error = GetLastError();
                return false;
            }

            if (m_options.configuration.hideOnTyping)
            {
                m_keyboardHook = SetWindowsHookExW(WH_KEYBOARD_LL, KeyboardHookProc, module, 0);
                if (!m_keyboardHook)
                {
                    m_error = GetLastError();
                    return false;
                }
            }

            return true;
        }

        void UninstallHooks() noexcept
        {
            if (m_keyboardHook)
            {
                UnhookWindowsHookEx(m_keyboardHook);
                m_keyboardHook = nullptr;
            }

            if (m_mouseHook)
            {
                UnhookWindowsHookEx(m_mouseHook);
                m_mouseHook = nullptr;
            }
        }

        void QueueAction(auto_hide_cursor::CursorAction action) const noexcept
        {
            switch (action)
            {
            case auto_hide_cursor::CursorAction::Hide:
                PostThreadMessageW(m_threadId, hideCursorMessage, 0, 0);
                break;
            case auto_hide_cursor::CursorAction::Show:
                PostThreadMessageW(m_threadId, showCursorMessage, 0, 0);
                break;
            case auto_hide_cursor::CursorAction::None:
                break;
            }
        }

        void ProcessMessages()
        {
            MSG message{};
            while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
            {
                if (message.message == hideCursorMessage)
                {
                    ApplyAction(auto_hide_cursor::CursorAction::Hide);
                }
                else if (message.message == showCursorMessage)
                {
                    ApplyAction(auto_hide_cursor::CursorAction::Show);
                }
                else
                {
                    TranslateMessage(&message);
                    DispatchMessageW(&message);
                }
            }
        }

        void ApplyAction(auto_hide_cursor::CursorAction action)
        {
            if (action == auto_hide_cursor::CursorAction::Hide && !m_cursorHider.Hide())
            {
                m_error = GetLastError();
                if (m_error == ERROR_SUCCESS)
                {
                    m_error = ERROR_FUNCTION_FAILED;
                }
                m_state.HideFailed(GetTickCount64());
            }
            else if (action == auto_hide_cursor::CursorAction::Show && !m_cursorHider.Restore())
            {
                m_error = GetLastError();
                if (m_error == ERROR_SUCCESS)
                {
                    m_error = ERROR_FUNCTION_FAILED;
                }
                m_state.ShowFailed();
            }
        }

        static LRESULT CALLBACK KeyboardHookProc(int code, WPARAM message, LPARAM data) noexcept
        {
            if (code >= 0 && s_instance &&
                (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
            {
                const auto* keyboard = reinterpret_cast<const KBDLLHOOKSTRUCT*>(data);
                if ((keyboard->flags & (LLKHF_INJECTED | LLKHF_LOWER_IL_INJECTED)) == 0 &&
                    !auto_hide_cursor::State::IsModifierVirtualKey(keyboard->vkCode))
                {
                    s_instance->QueueAction(
                        s_instance->m_state.OnKeyboardInput(GetTickCount64(), GetCursorPosition()));
                }
            }

            return CallNextHookEx(nullptr, code, message, data);
        }

        static LRESULT CALLBACK MouseHookProc(int code, WPARAM message, LPARAM data) noexcept
        {
            if (code >= 0 && s_instance)
            {
                const auto* mouse = reinterpret_cast<const MSLLHOOKSTRUCT*>(data);
                if ((mouse->flags & (LLMHF_INJECTED | LLMHF_LOWER_IL_INJECTED)) == 0)
                {
                    auto inputKind = auto_hide_cursor::MouseInputKind::ButtonOrWheel;
                    if (message == WM_MOUSEMOVE)
                    {
                        inputKind = auto_hide_cursor::MouseInputKind::Move;
                    }
                    else if (message != WM_LBUTTONDOWN && message != WM_LBUTTONUP &&
                             message != WM_RBUTTONDOWN && message != WM_RBUTTONUP &&
                             message != WM_MBUTTONDOWN && message != WM_MBUTTONUP &&
                             message != WM_XBUTTONDOWN && message != WM_XBUTTONUP &&
                             message != WM_MOUSEWHEEL && message != WM_MOUSEHWHEEL)
                    {
                        return CallNextHookEx(nullptr, code, message, data);
                    }

                    const auto point = auto_hide_cursor::Point{ mouse->pt.x, mouse->pt.y };
                    s_instance->QueueAction(
                        s_instance->m_state.OnMouseInput(GetTickCount64(), point, inputKind));
                }
            }

            return CallNextHookEx(nullptr, code, message, data);
        }

        inline static Worker* s_instance = nullptr;

        Options m_options;
        auto_hide_cursor::State m_state;
        auto_hide_cursor::SystemCursorHider m_cursorHider;
        unique_handle m_parentProcess;
        unique_handle m_stopEvent;
        HHOOK m_keyboardHook = nullptr;
        HHOOK m_mouseHook = nullptr;
        DWORD m_threadId = 0;
        DWORD m_error = ERROR_SUCCESS;
    };
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    int argc = 0;
    auto argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (!argv)
    {
        return ERROR_INVALID_PARAMETER;
    }

    Options options;
    const auto validOptions = TryParseOptions(argc, argv, options);
    LocalFree(argv);
    if (!validOptions)
    {
        return ERROR_INVALID_PARAMETER;
    }

    Worker worker{ options };
    return worker.Run();
}
