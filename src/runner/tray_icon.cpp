#include "pch.h"
#include "Generated files/resource.h"
#include "settings_window.h"
#include "tray_icon.h"
#include "centralized_hotkeys.h"
#include "centralized_kb_hook.h"
#include <Windows.h>

#include <common/utils/resources.h>
#include <common/version/version.h>
#include <common/logger/logger.h>
#include <common/utils/elevation.h>
#include "bug_report.h"

namespace
{
    HWND tray_icon_hwnd = NULL;

    enum
    {
        wm_icon_notify = WM_APP,
        wm_run_on_main_ui_thread,
    };

    // Contains the Windows Message for taskbar creation.
    UINT wm_taskbar_restart = 0;

    NOTIFYICONDATAW tray_icon_data;
    bool tray_icon_created = false;

    bool about_box_shown = false;

    HMENU h_menu = nullptr;
    HMENU h_sub_menu = nullptr;
    bool double_click_timer_running = false;
    bool double_clicked = false;
    POINT tray_icon_click_point;

}

// Struct to fill with callback and the data. The window_proc is responsible for cleaning it.
struct run_on_main_ui_thread_msg
{
    main_loop_callback_function _callback;
    PVOID data;
};

bool dispatch_run_on_main_ui_thread(main_loop_callback_function _callback, PVOID data)
{
    if (tray_icon_hwnd == NULL)
    {
        return false;
    }
    struct run_on_main_ui_thread_msg* wnd_msg = new struct run_on_main_ui_thread_msg();
    wnd_msg->_callback = _callback;
    wnd_msg->data = data;

    PostMessage(tray_icon_hwnd, wm_run_on_main_ui_thread, 0, reinterpret_cast<LPARAM>(wnd_msg));

    return true;
}

void change_menu_item_text(const UINT item_id, wchar_t* new_text)
{
    MENUITEMINFOW menuitem = { .cbSize = sizeof(MENUITEMINFOW), .fMask = MIIM_TYPE | MIIM_DATA };
    GetMenuItemInfoW(h_menu, item_id, false, &menuitem);
    menuitem.dwTypeData = new_text;
    SetMenuItemInfoW(h_menu, item_id, false, &menuitem);
}

void open_quick_access_flyout_window(const POINT flyout_position)
{
    open_settings_window(std::nullopt, true, flyout_position);
}

void handle_tray_command(HWND window, const WPARAM command_id, LPARAM lparam)
{
    switch (command_id)
    {
    case ID_SETTINGS_MENU_COMMAND:
    {
        std::wstring settings_window{ winrt::to_hstring(ESettingsWindowNames_to_string(static_cast<ESettingsWindowNames>(lparam))) };
        open_settings_window(settings_window, false);
    }
    break;
    case ID_EXIT_MENU_COMMAND:
        if (h_menu)
        {
            DestroyMenu(h_menu);
        }
        DestroyWindow(window);
        break;
    case ID_ABOUT_MENU_COMMAND:
        if (!about_box_shown)
        {
            about_box_shown = true;
            std::wstring about_msg = L"PowerToys\nVersion " + get_product_version() + L"\n\xa9 2019 Microsoft Corporation";
            MessageBoxW(nullptr, about_msg.c_str(), L"About PowerToys", MB_OK);
            about_box_shown = false;
        }
        break;
    case ID_REPORT_BUG_COMMAND:
    {
        launch_bug_report();
        break;
    }

    case ID_DOCUMENTATION_MENU_COMMAND:
    {
        RunNonElevatedEx(L"https://aka.ms/PowerToysOverview", L"", L"");
        break;
    }
    case ID_QUICK_ACCESS_MENU_COMMAND:
    {
        POINT mouse_pointer;
        GetCursorPos(&mouse_pointer);
        open_quick_access_flyout_window(mouse_pointer);
        break;
    }
    }
}

void click_timer_elapsed()
{
    double_click_timer_running = false;
    if (!double_clicked)
    {
        open_quick_access_flyout_window(tray_icon_click_point);
    }
}

LRESULT __stdcall tray_icon_window_proc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
{
    switch (message)
    {
    case WM_HOTKEY:
    {
        // We use the tray icon WndProc to avoid creating a dedicated window just for this message.
        const auto modifiersMask = LOWORD(lparam);
        const auto vkCode = HIWORD(lparam);
        Logger::trace(L"On {} hotkey", CentralizedHotkeys::ToWstring({ modifiersMask, vkCode }));
        CentralizedHotkeys::PopulateHotkey({ modifiersMask, vkCode });
        break;
    }
    case WM_CREATE:
        if (wm_taskbar_restart == 0)
        {
            tray_icon_hwnd = window;
            wm_taskbar_restart = RegisterWindowMessageW(L"TaskbarCreated");
        }
        break;
    case WM_DESTROY:
        if (tray_icon_created)
        {
            Shell_NotifyIcon(NIM_DELETE, &tray_icon_data);
            tray_icon_created = false;
        }
        close_settings_window();
        PostQuitMessage(0);
        break;
    case WM_CLOSE:
        DestroyWindow(window);
        break;
    case WM_COMMAND:
        handle_tray_command(window, wparam, lparam);
        break;
    // Shell_NotifyIcon can fail when we invoke it during the time explorer.exe isn't present/ready to handle it.
    // We'll also never receive wm_taskbar_restart message if the first call to Shell_NotifyIcon failed, so we use
    // WM_WINDOWPOSCHANGING which is always received on explorer startup sequence.
    case WM_WINDOWPOSCHANGING:
    {
        if (!tray_icon_created)
        {
            tray_icon_created = Shell_NotifyIcon(NIM_ADD, &tray_icon_data) == TRUE;
        }
        break;
    }
    default:
        if (message == wm_icon_notify)
        {
            switch (lparam)
            {
            case WM_RBUTTONUP:
            case WM_CONTEXTMENU:
            {
                if (!h_menu)
                {
                    h_menu = LoadMenu(reinterpret_cast<HINSTANCE>(&__ImageBase), MAKEINTRESOURCE(ID_TRAY_MENU));
                }
                if (h_menu)
                {
                    static std::wstring settings_menuitem_label = GET_RESOURCE_STRING(IDS_SETTINGS_MENU_TEXT);
                    static std::wstring exit_menuitem_label = GET_RESOURCE_STRING(IDS_EXIT_MENU_TEXT);
                    static std::wstring submit_bug_menuitem_label = GET_RESOURCE_STRING(IDS_SUBMIT_BUG_TEXT);
                    static std::wstring documentation_menuitem_label = GET_RESOURCE_STRING(IDS_DOCUMENTATION_MENU_TEXT);
                    static std::wstring quick_access_menuitem_label = GET_RESOURCE_STRING(IDS_QUICK_ACCESS_MENU_TEXT);
                    change_menu_item_text(ID_SETTINGS_MENU_COMMAND, settings_menuitem_label.data());
                    change_menu_item_text(ID_EXIT_MENU_COMMAND, exit_menuitem_label.data());
                    change_menu_item_text(ID_REPORT_BUG_COMMAND, submit_bug_menuitem_label.data());
                    change_menu_item_text(ID_DOCUMENTATION_MENU_COMMAND, documentation_menuitem_label.data());
                    change_menu_item_text(ID_QUICK_ACCESS_MENU_COMMAND, quick_access_menuitem_label.data());
                }
                if (!h_sub_menu)
                {
                    h_sub_menu = GetSubMenu(h_menu, 0);
                }
                POINT mouse_pointer;
                GetCursorPos(&mouse_pointer);
                SetForegroundWindow(window); // Needed for the context menu to disappear.
                TrackPopupMenu(h_sub_menu, TPM_CENTERALIGN | TPM_BOTTOMALIGN, mouse_pointer.x, mouse_pointer.y, 0, window, nullptr);
                break;
            }
            case WM_LBUTTONUP:
            {
                // ignore event if this is the second click of a double click
                if (!double_click_timer_running)
                {
                    // save the cursor position for sending where to show the popup.
                    GetCursorPos(&tray_icon_click_point);

                    // start timer for detecting single or double click
                    double_click_timer_running = true;
                    double_clicked = false;

                    UINT doubleClickTime = GetDoubleClickTime();
                    std::thread([doubleClickTime]() {
                        std::this_thread::sleep_for(std::chrono::milliseconds(doubleClickTime));
                        click_timer_elapsed();
                    }).detach();
                }
                break;
            }
            case WM_LBUTTONDBLCLK:
            {
                double_clicked = true;
                open_settings_window(std::nullopt, false);
                break;
            }
            break;
            }
        }
        else if (message == wm_run_on_main_ui_thread)
        {
            if (lparam != NULL)
            {
                struct run_on_main_ui_thread_msg* msg = reinterpret_cast<struct run_on_main_ui_thread_msg*>(lparam);
                msg->_callback(msg->data);
                delete msg;
                lparam = NULL;
            }
            break;
        }
        else if (message == wm_taskbar_restart)
        {
            tray_icon_created = Shell_NotifyIcon(NIM_ADD, &tray_icon_data) == TRUE;
            break;
        }
    }
    return DefWindowProc(window, message, wparam, lparam);
}

void start_tray_icon(bool isProcessElevated)
{
    auto h_instance = reinterpret_cast<HINSTANCE>(&__ImageBase);
    auto icon = LoadIcon(h_instance, MAKEINTRESOURCE(APPICON));
    if (icon)
    {
        UINT id_tray_icon = 1;

        WNDCLASS wc = {};
        wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
        wc.hInstance = h_instance;
        wc.lpszClassName = pt_tray_icon_window_class;
        wc.style = CS_HREDRAW | CS_VREDRAW;
        wc.lpfnWndProc = tray_icon_window_proc;
        wc.hIcon = icon;
        RegisterClass(&wc);
        auto hwnd = CreateWindowW(wc.lpszClassName,
                                  pt_tray_icon_window_class,
                                  WS_OVERLAPPEDWINDOW | WS_POPUP,
                                  CW_USEDEFAULT,
                                  CW_USEDEFAULT,
                                  CW_USEDEFAULT,
                                  CW_USEDEFAULT,
                                  nullptr,
                                  nullptr,
                                  wc.hInstance,
                                  nullptr);
        WINRT_VERIFY(hwnd);
        CentralizedHotkeys::RegisterWindow(hwnd);
        CentralizedKeyboardHook::RegisterWindow(hwnd);
        memset(&tray_icon_data, 0, sizeof(tray_icon_data));
        tray_icon_data.cbSize = sizeof(tray_icon_data);
        tray_icon_data.hIcon = icon;
        tray_icon_data.hWnd = hwnd;
        tray_icon_data.uID = id_tray_icon;
        tray_icon_data.uCallbackMessage = wm_icon_notify;

        std::wstringstream pt_version_tooltip_stream;
        if (isProcessElevated)
        {
            pt_version_tooltip_stream << GET_RESOURCE_STRING(IDS_TRAY_ICON_ADMIN_TOOLTIP) << L": ";
        }

        pt_version_tooltip_stream << L"PowerToys " << get_product_version() << '\0';
        std::wstring pt_version_tooltip = pt_version_tooltip_stream.str();
        wcscpy_s(tray_icon_data.szTip, sizeof(tray_icon_data.szTip) / sizeof(WCHAR), pt_version_tooltip.c_str());
        tray_icon_data.uFlags = NIF_ICON | NIF_TIP | NIF_MESSAGE;
        ChangeWindowMessageFilterEx(hwnd, WM_COMMAND, MSGFLT_ALLOW, nullptr);

        tray_icon_created = Shell_NotifyIcon(NIM_ADD, &tray_icon_data) == TRUE;
    }
}

void stop_tray_icon()
{
    if (tray_icon_created)
    {
        SendMessage(tray_icon_hwnd, WM_CLOSE, 0, 0);
    }
}
