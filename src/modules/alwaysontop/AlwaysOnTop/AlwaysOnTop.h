#pragma once

#include <map>

#include <Settings.h>
#include <SettingsObserver.h>
#include <Sound.h>
#include <VirtualDesktopUtils.h>
#include <WindowBorder.h>

#include <common/hooks/WinHookEvent.h>

class AlwaysOnTop : public SettingsObserver
{
public:
    AlwaysOnTop(bool useLLKH);
    ~AlwaysOnTop();

protected:
    static LRESULT CALLBACK WndProc_Helper(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
    {
        auto thisRef = reinterpret_cast<AlwaysOnTop*>(GetWindowLongPtr(window, GWLP_USERDATA));

        if (!thisRef && (message == WM_CREATE))
        {
            const auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
            thisRef = static_cast<AlwaysOnTop*>(createStruct->lpCreateParams);
            SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
        }

        return thisRef ? thisRef->WndProc(window, message, wparam, lparam) :
                         DefWindowProc(window, message, wparam, lparam);
    }

private:
    // IDs used to register hot keys (keyboard shortcuts).
    enum class HotkeyId : int
    {
        Pin = 1,
    };

    static inline AlwaysOnTop* s_instance = nullptr;
    std::vector<HWINEVENTHOOK> m_staticWinEventHooks{};
    Sound m_sound;
    VirtualDesktopUtils m_virtualDesktopUtils;

    HWND m_window{ nullptr };
    HINSTANCE m_hinstance;
    std::map<HWND, std::unique_ptr<WindowBorder>> m_topmostWindows{};
    HANDLE m_hPinEvent;
    std::thread m_thread;
    const bool m_useCentralizedLLKH;
    bool m_running = true;

    LRESULT WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;
    void HandleWinHookEvent(WinHookEvent* data) noexcept;
    
    bool InitMainWindow();
    void RegisterHotkey() const;
    void RegisterLLKH();
    void SubscribeToEvents();

    void ProcessCommand(HWND window);
    void StartTrackingTopmostWindows();
    void UnpinAll();
    void CleanUp();

    bool IsTracked(HWND window) const noexcept;
    bool IsTopmost(HWND window) const noexcept;
    bool IsPinned(HWND window) const noexcept;

    bool PinTopmostWindow(HWND window) const noexcept;
    bool UnpinTopmostWindow(HWND window) const noexcept;
    bool AssignBorder(HWND window);
    void RefreshBorders();

    virtual void SettingsUpdate(SettingId type) override;

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
