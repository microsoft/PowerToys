#include "pch.h"
#include "WindowCreationHandler.h"

WindowCreationHandler::WindowCreationHandler(std::function<void(HWND)> windowCreatedCallback) :
    m_windowCreatedCallback(windowCreatedCallback)
{
    s_instance = this;
    InitHooks();
}

WindowCreationHandler::~WindowCreationHandler()
{
    m_staticWinEventHooks.erase(std::remove_if(begin(m_staticWinEventHooks),
                                               end(m_staticWinEventHooks),
                                               [](const HWINEVENTHOOK hook) {
                                                   return UnhookWinEvent(hook);
                                               }),
                                end(m_staticWinEventHooks));
}

void WindowCreationHandler::InitHooks()
{
    std::array<DWORD, 3> events_to_subscribe = {
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
            Logger::error(L"Failed to initialize win event hooks");
        }
    }
}

void WindowCreationHandler::HandleWinHookEvent(DWORD event, HWND window) noexcept
{
    switch (event)
    {
    //case EVENT_OBJECT_UNCLOAKED:
    //case EVENT_OBJECT_SHOW:
    case EVENT_OBJECT_CREATE:
    {
        if (m_windowCreatedCallback)
        {
            m_windowCreatedCallback(window);
        }
    }
    break;

    default:
        break;
    }
}
