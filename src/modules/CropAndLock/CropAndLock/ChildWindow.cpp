#include "pch.h"
#include "ChildWindow.h"

namespace util
{
    using namespace robmikh::common::desktop;
    using namespace robmikh::common::desktop::controls;
}

const std::wstring ChildWindow::ClassName = L"CropAndLock.ChildWindow";
std::once_flag ChildWindowClassRegistration;

void ChildWindow::RegisterWindowClass()
{
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));
    WNDCLASSEXW wcex = {};
    wcex.cbSize = sizeof(wcex);
    wcex.style = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc = WndProc;
    wcex.hInstance = instance;
    wcex.hIcon = LoadIconW(instance, IDI_APPLICATION);
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wcex.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    wcex.lpszClassName = ClassName.c_str();
    wcex.hIconSm = LoadIconW(wcex.hInstance, IDI_APPLICATION);
    winrt::check_bool(RegisterClassExW(&wcex));
}

ChildWindow::ChildWindow(int width, int height, HWND parent)
{
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));

    std::call_once(ChildWindowClassRegistration, []() { RegisterWindowClass(); });

    auto exStyle = 0;
    auto style = WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS;

    winrt::check_bool(CreateWindowExW(exStyle, ClassName.c_str(), L"", style,
        0, 0, width, height, parent, nullptr, instance, this));
    WINRT_ASSERT(m_window);

    ShowWindow(m_window, SW_SHOW);
    UpdateWindow(m_window);
}

LRESULT ChildWindow::MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam)
{
    switch (message)
    {
    case WM_DESTROY:
        break;
    default:
        return base_type::MessageHandler(message, wparam, lparam);
    }
    return 0;
}
