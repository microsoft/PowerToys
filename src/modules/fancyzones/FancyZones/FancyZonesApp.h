#pragma once

#include <common/hooks/LowlevelKeyboardEvent.h>

#include <FancyZonesLib/FancyZones.h>

class FancyZonesApp
{
public:
    FancyZonesApp(const std::wstring& appName, const std::wstring& appKey);
    ~FancyZonesApp();

    void Run();

private:
    static inline FancyZonesApp* s_instance = nullptr;
    
    winrt::com_ptr<IFancyZones> m_app;
    HWINEVENTHOOK m_objectLocationWinEventHook = nullptr;
    std::vector<HWINEVENTHOOK> m_staticWinEventHooks;

    void DisableModule() noexcept;

    void InitHooks();

    void HandleWinHookEvent(WinHookEvent* data) noexcept;

    static void CALLBACK WinHookProc(HWINEVENTHOOK winEventHook,
                                     DWORD event,
                                     HWND window,
                                     LONG object,
                                     LONG child,
                                     DWORD eventThread,
                                     DWORD eventTime)
    {
        WinHookEvent data{ event, window, object, child, eventThread, eventTime };
        if (s_instance)
        {
            s_instance->HandleWinHookEvent(&data);
        }
    }
};
