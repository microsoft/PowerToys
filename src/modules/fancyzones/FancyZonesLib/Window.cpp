#include "pch.h"
#include "Window.h"

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t WindowClassName[] = L"FancyZones_Window";
}

Window::Window() noexcept :
    m_window(NULL),
    m_proc(nullptr)
{
}

void Window::Init(HINSTANCE hinstance, WndProc proc, DWORD style, DWORD extendedStyle, FancyZonesUtils::Rect position, LPCWSTR windowName, HWND parent, HMENU menu, int showCommand) noexcept
{
    m_proc = proc;

    static ATOM windowClass = INVALID_ATOM;
    if (windowClass == INVALID_ATOM)
    {
        WNDCLASSEXW wcex{};
        wcex.cbSize = sizeof(WNDCLASSEX);
        wcex.lpfnWndProc = s_WndProc;
        wcex.style = CS_HREDRAW | CS_VREDRAW;
        wcex.hInstance = hinstance;
        wcex.lpszClassName = reinterpret_cast<LPCWSTR>(NonLocalizable::WindowClassName);
        wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
        windowClass = RegisterClassExW(&wcex);
    }

    m_window = CreateWindowExW(
        extendedStyle,
        MAKEINTATOM(windowClass),
        windowName,
        style,
        position.left(),
        position.top(),
        position.width(),
        position.height(),
        parent,
        menu,
        hinstance,
        this);

    if (showCommand != SW_HIDE)
    {
        ShowWindow(m_window, showCommand);
    }
}

LRESULT CALLBACK Window::s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    auto thisRef = reinterpret_cast<Window*>(GetWindowLongPtrW(window, GWLP_USERDATA));
    if (!thisRef && (message == WM_NCCREATE))
    {
        const auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
        thisRef = reinterpret_cast<Window*>(createStruct->lpCreateParams);
        SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
    }
    else if (thisRef && (message == WM_NCDESTROY))
    {
        thisRef = nullptr;
        SetWindowLongPtrW(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
    }

    return (thisRef && thisRef->m_proc) ?
        thisRef->m_proc(window, message, wparam, lparam) :
        DefWindowProcW(window, message, wparam, lparam);
}

Window::~Window()
{
    DestroyWindow(m_window);
}
