#pragma once

#include <SettingsObserver.h>

class FrameDrawer;

class WindowBorder : public SettingsObserver
{
    WindowBorder(HWND window);
    WindowBorder(WindowBorder&& other) = default;

public:
    static std::unique_ptr<WindowBorder> Create(HWND window, HINSTANCE hinstance);
    ~WindowBorder();

    void UpdateBorderPosition() const;
    void UpdateBorderProperties() const;

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
    {
        auto thisRef = reinterpret_cast<WindowBorder*>(GetWindowLongPtr(window, GWLP_USERDATA));
        if ((thisRef == nullptr) && (message == WM_CREATE))
        {
            auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
            thisRef = static_cast<WindowBorder*>(createStruct->lpCreateParams);
            SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
        }

        return (thisRef != nullptr) ? thisRef->WndProc(message, wparam, lparam) :
                                      DefWindowProc(window, message, wparam, lparam);
    }

private:
    UINT_PTR m_timer_id = {};
    HWND m_window = {};
    HWND m_trackingWindow = {};
    std::unique_ptr<FrameDrawer> m_frameDrawer;

    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;

    bool Init(HINSTANCE hinstance);
    virtual void SettingsUpdate(SettingId id) override;
};
