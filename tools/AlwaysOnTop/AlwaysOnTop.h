#pragma once

// common
struct WinHookEvent
{
    DWORD event;
    HWND hwnd;
    LONG idObject;
    LONG idChild;
    DWORD idEventThread;
    DWORD dwmsEventTime;
};

class AlwaysOnTop
{
public:
    AlwaysOnTop();
    ~AlwaysOnTop();

    void Init();

protected:
    static LRESULT CALLBACK WndProc_Helper(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
    {
        auto thisRef = reinterpret_cast<AlwaysOnTop*>(GetWindowLongPtr(window, GWLP_USERDATA));

        if (!thisRef && (message == WM_CREATE))
        {
            const auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
            thisRef = reinterpret_cast<AlwaysOnTop*>(createStruct->lpCreateParams);
            SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
        }

        return thisRef ? thisRef->WndProc(window, message, wparam, lparam) :
            DefWindowProc(window, message, wparam, lparam);
    }

private:
    static inline AlwaysOnTop* s_instance = nullptr;
    std::vector<HWINEVENTHOOK> m_staticWinEventHooks;

    HWND m_hotKeyHandleWindow{ nullptr };
    std::vector<HWND> m_topmostWindows;

    bool m_activateInGameMode = false;

    LRESULT WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;

    void ProcessCommand(HWND window);
    void StartTrackingTopmostWindows();
    void ResetAll();
    void CleanUp();
    bool OrderWindows() const noexcept;

    bool IsTopmost(HWND window) const noexcept;
    bool SetTopmostWindow(HWND window) const noexcept;
    bool ResetTopmostWindow(HWND window) const noexcept;

    bool IsTracked(HWND window) const noexcept;
    
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

