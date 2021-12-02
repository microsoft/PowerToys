#pragma once

#include "FrameDrawer.h"

class WindowTracker
{
public:
    WindowTracker(HWND window);
    WindowTracker(WindowTracker&& other);
    ~WindowTracker();

    bool Init(HINSTANCE hinstance);

    void DrawFrame() const;
    void Hide() const;

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
    {
        auto thisRef = reinterpret_cast<WindowTracker*>(GetWindowLongPtr(window, GWLP_USERDATA));
        if ((thisRef == nullptr) && (message == WM_CREATE))
        {
            auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
            thisRef = reinterpret_cast<WindowTracker*>(createStruct->lpCreateParams);
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
};

