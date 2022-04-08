#include "pch.h"
#include "FancyZonesApp.h"

#include <common/display/dpi_aware.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/UnhandledExceptionHandler.h>

#include <FancyZonesLib/Generated Files/resource.h>
#include <FancyZonesLib/FancyZonesData.h>
#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/trace.h>


FancyZonesApp::FancyZonesApp(const std::wstring& appName, const std::wstring& appKey)
{
    DPIAware::EnableDPIAwarenessForThisProcess();
        
    InitializeWinhookEventIds();
    m_app = MakeFancyZones(reinterpret_cast<HINSTANCE>(&__ImageBase), std::bind(&FancyZonesApp::DisableModule, this));

    InitHooks();

    s_instance = this;
}

FancyZonesApp::~FancyZonesApp()
{
    if (m_app)
    {
        m_app->Destroy();
        m_app = nullptr;

        if (s_llKeyboardHook)
        {
            if (UnhookWindowsHookEx(s_llKeyboardHook))
            {
                s_llKeyboardHook = nullptr;
            }
        }

        m_staticWinEventHooks.erase(std::remove_if(begin(m_staticWinEventHooks),
                                                   end(m_staticWinEventHooks),
                                                   [](const HWINEVENTHOOK hook) {
                                                       return UnhookWinEvent(hook);
                                                   }),
                                    end(m_staticWinEventHooks));
        if (m_objectLocationWinEventHook)
        {
            if (UnhookWinEvent(m_objectLocationWinEventHook))
            {
                m_objectLocationWinEventHook = nullptr;
            }
        }
    }
}

void FancyZonesApp::Run()
{
    if (m_app)
    {
        m_app->Run();
    }
}

void FancyZonesApp::InitHooks()
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
            show_last_error_message(L"SetWindowsHookEx", errorCode, GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str());
            auto errorMessage = get_last_error_message(errorCode);
            Trace::FancyZones::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"enable.SetWindowsHookEx");
        }
    }

    std::array<DWORD, 6> events_to_subscribe = {
        EVENT_SYSTEM_MOVESIZESTART,
        EVENT_SYSTEM_MOVESIZEEND,
        EVENT_OBJECT_NAMECHANGE,
        EVENT_OBJECT_UNCLOAKED,
        EVENT_OBJECT_SHOW,
        EVENT_OBJECT_CREATE
    };
    for (const auto event : events_to_subscribe)
    {
        auto hook = SetWinEventHook(event, event, nullptr, WinHookProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (hook)
        {
            m_staticWinEventHooks.emplace_back(hook);
        }
        else
        {
            MessageBoxW(NULL,
                        GET_RESOURCE_STRING(IDS_WINDOW_EVENT_LISTENER_ERROR).c_str(),
                        GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str(),
                        MB_OK | MB_ICONERROR);
        }
    }
}

void FancyZonesApp::DisableModule() noexcept
{
    PostQuitMessage(0);
}

void FancyZonesApp::HandleWinHookEvent(WinHookEvent* data) noexcept
{
    auto fzCallback = m_app.as<IFancyZonesCallback>();
    switch (data->event)
    {
    case EVENT_SYSTEM_MOVESIZESTART:
    {
        fzCallback->HandleWinHookEvent(data);
        if (!m_objectLocationWinEventHook)
        {
            m_objectLocationWinEventHook = SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE,
                                                           EVENT_OBJECT_LOCATIONCHANGE,
                                                           nullptr,
                                                           WinHookProc,
                                                           0,
                                                           0,
                                                           WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        }
    }
    break;

    case EVENT_SYSTEM_MOVESIZEEND:
    {
        if (UnhookWinEvent(m_objectLocationWinEventHook))
        {
            m_objectLocationWinEventHook = nullptr;
        }
        fzCallback->HandleWinHookEvent(data);
    }
    break;

    case EVENT_OBJECT_LOCATIONCHANGE:
    {
        fzCallback->HandleWinHookEvent(data);
    }
    break;

    case EVENT_OBJECT_NAMECHANGE:
    {
        // The accessibility name of the desktop window changes whenever the user
        // switches virtual desktops.
        if (data->hwnd == GetDesktopWindow())
        {
            m_app.as<IFancyZonesCallback>()->VirtualDesktopChanged();
        }
    }
    break;

    case EVENT_OBJECT_UNCLOAKED:
    case EVENT_OBJECT_SHOW:
    case EVENT_OBJECT_CREATE:
    {
        fzCallback->HandleWinHookEvent(data);
    }
    break;

    default:
        break;
    }
}

intptr_t FancyZonesApp::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    return m_app.as<IFancyZonesCallback>()->OnKeyDown(data->lParam);
}
