#include "pch.h"
#include "SettingsWindow.h"

namespace util
{
    using namespace robmikh::common::desktop;
    using namespace robmikh::common::desktop::controls;
}

const std::wstring SettingsWindow::ClassName = L"CropAndLock.SettingsWindow";
std::once_flag SettingsWindowClassRegistration;

void SettingsWindow::RegisterWindowClass()
{
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));
    WNDCLASSEXW wcex = {};
    wcex.cbSize = sizeof(wcex);
    wcex.style = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc = WndProc;
    wcex.hInstance = instance;
    wcex.hIcon = LoadIconW(instance, IDI_APPLICATION);
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    wcex.hbrBackground = (HBRUSH)(COLOR_WINDOW + 1);
    wcex.lpszClassName = ClassName.c_str();
    wcex.hIconSm = LoadIconW(wcex.hInstance, IDI_APPLICATION);
    winrt::check_bool(RegisterClassExW(&wcex));
}

SettingsWindow::SettingsWindow(std::wstring const& title, int width, int height)
{
    auto instance = winrt::check_pointer(GetModuleHandleW(nullptr));

    std::call_once(SettingsWindowClassRegistration, []() { RegisterWindowClass(); });

    auto exStyle = 0;
    auto style = WS_OVERLAPPEDWINDOW;

    winrt::check_bool(CreateWindowExW(exStyle, ClassName.c_str(), title.c_str(), style,
        CW_USEDEFAULT, CW_USEDEFAULT, width, height, nullptr, nullptr, instance, this));
    WINRT_ASSERT(m_window);

    auto dpi = GetDpiForWindow(m_window);

    RECT rect = { 0, 0, width, height };
    winrt::check_bool(AdjustWindowRectExForDpi(&rect, style, false, exStyle, dpi));
    auto adjustedWidth = rect.right - rect.left;
    auto adjustedHeight = rect.bottom - rect.top;
    winrt::check_bool(SetWindowPos(m_window, nullptr, 0, 0, adjustedWidth, adjustedHeight, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOZORDER));

    m_font = util::GetFontForDpi(dpi);

    m_windowTypes =
    {
        { CropAndLockType::Reparent, L"Reparent" },
        { CropAndLockType::Thumbnail, L"Thumbnail" },
    };

    m_trayIconMenu = std::make_unique<util::PopupMenu>();
    m_trayIconMenu->AppendMenuItem(L"Settings", std::bind(&SettingsWindow::OnSettingsMenuItemClicked, this));
    m_trayIconMenu->AppendMenuItem(L"Exit", []() { PostQuitMessage(0); });

    CreateControls(instance);
}

LRESULT SettingsWindow::MessageHandler(UINT const message, WPARAM const wparam, LPARAM const lparam)
{
    switch (message)
    {
    case WM_CLOSE:
        // We don't want to destroy the window, just hide it.
        Hide();
        break;
    case TrayIconMessage:
    {
        // We only have one icon, so we won't check the id.
        auto iconMessage = LOWORD(lparam);
        auto x = GET_X_LPARAM(wparam);
        auto y = GET_Y_LPARAM(wparam);
        switch (iconMessage)
        {
        case WM_CONTEXTMENU:
            m_trayIconMenu->ShowMenu(m_window, x, y);
            break;
        default:
            break;
        }
    }
    break;
    case WM_MENUCOMMAND:
        if (auto result = m_trayIconMenu->MessageHandler(wparam, lparam))
        {
            return result.value();
        }
        break;
    case WM_COMMAND:
    {
        auto command = HIWORD(wparam);
        auto hwnd = (HWND)lparam;
        switch (command)
        {
        case CBN_SELCHANGE:
        {
            auto index = SendMessageW(hwnd, CB_GETCURSEL, 0, 0);
            if (hwnd == m_windowTypeComboBox)
            {
                auto type = m_windowTypes[index];
                m_currentCropAndLockType = type.Type;
            }
        }
        break;
        default:
            break;
        }
    }
    break;
    case WM_CTLCOLORSTATIC:
        return util::StaticControlColorMessageHandler(wparam, lparam);
    default:
        return base_type::MessageHandler(message, wparam, lparam);
    }
    return 0;
}

void SettingsWindow::Hide()
{
    ShowWindow(m_window, SW_HIDE);
}

void SettingsWindow::CreateControls(HINSTANCE instance)
{
    auto dpi = GetDpiForWindow(m_window);

    m_controls = std::make_unique<util::StackPanel>(m_window, instance, m_font, 10, 10, 40, 250, 30);

    // Key mode
    m_controls->CreateControl(util::ControlType::Label, L"Crop and Lock mode:");
    m_windowTypeComboBox = m_controls->CreateControl(util::ControlType::ComboBox, L"");
    for (auto& type : m_windowTypes)
    {
        SendMessageW(m_windowTypeComboBox, CB_ADDSTRING, 0, (LPARAM)type.DisplayName.c_str());
    }
    // The default mode is reparent
    SendMessageW(m_windowTypeComboBox, CB_SETCURSEL, 0, 0);
}

void SettingsWindow::OnSettingsMenuItemClicked()
{
    ShowWindow(m_window, SW_SHOW);
}
