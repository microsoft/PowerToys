#pragma once

class WindowCreationHandler
{
public:
    WindowCreationHandler(std::function<void(HWND)> windowCreatedCallback);
    ~WindowCreationHandler();

private:
    static inline WindowCreationHandler* s_instance = nullptr;
    std::vector<HWINEVENTHOOK> m_staticWinEventHooks;
    std::function<void(HWND)> m_windowCreatedCallback;

    void InitHooks();
    void HandleWinHookEvent(DWORD event, HWND window) noexcept;

    static void CALLBACK WinHookProc(HWINEVENTHOOK winEventHook,
                                     DWORD event,
                                     HWND window,
                                     LONG object,
                                     LONG child,
                                     DWORD eventThread,
                                     DWORD eventTime)
    {
        if (s_instance)
        {
            s_instance->HandleWinHookEvent(event, window);
        }
    }
};
