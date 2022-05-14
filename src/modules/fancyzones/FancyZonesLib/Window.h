#pragma once
#include "pch.h"
#include "util.h"

using WndProc = std::function<LRESULT(HWND window, UINT message, WPARAM wparam, LPARAM lparam)>;

class Window
{
public:
    Window() noexcept;
    ~Window();

    Window(const Window&) = delete;
    Window& operator=(const Window&) = delete;

    operator HWND() const { return m_window; }

    void Init(HINSTANCE hinstance, WndProc proc, DWORD style, DWORD extendedStyle, FancyZonesUtils::Rect position, LPCWSTR windowName = NULL, HWND parent = NULL, HMENU menu = NULL, int show = SW_SHOWNOACTIVATE) noexcept;

private:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

    HWND m_window;
    WndProc m_proc;
};
