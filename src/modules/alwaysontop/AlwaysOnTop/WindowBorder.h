#pragma once

#include <SettingsObserver.h>

class FrameDrawer;

class WindowBorder : public SettingsObserver
{
public:
    WindowBorder(HWND window);
    WindowBorder(WindowBorder&& other);
    ~WindowBorder();

    bool Init(HINSTANCE hinstance);

    void Show() const;
    void Hide() const;

    void UpdateBorderPosition() const;
    void UpdateBorderProperties() const;

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
    {
        auto thisRef = reinterpret_cast<WindowBorder*>(GetWindowLongPtr(window, GWLP_USERDATA));
        if ((thisRef == nullptr) && (message == WM_CREATE))
        {
            auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
            thisRef = reinterpret_cast<WindowBorder*>(createStruct->lpCreateParams);
            SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
        }

        return (thisRef != nullptr) ? thisRef->WndProc(message, wparam, lparam) :
                                      DefWindowProc(window, message, wparam, lparam);
    }

private:
    HWND m_window;
    HWND m_trackingWindow;
    std::unique_ptr<FrameDrawer> m_frameDrawer;

    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;

    virtual void SettingsUpdate(SettingId id) override;
};
