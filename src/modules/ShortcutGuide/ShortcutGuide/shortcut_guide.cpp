#include "pch.h"
#include "shortcut_guide.h"
#include "target_state.h"
#include "trace.h"

#include <common/SettingsAPI/settings_objects.h>
#include <common/debug_control.h>
#include <common/interop/shared_constants.h>
#include <sstream>

#include <common/SettingsAPI/settings_helpers.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/logger/logger.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/string_utils.h>
#include <common/utils/winapi_error.h>
#include <common/utils/window.h>
#include <Psapi.h>
#include <common/hooks/LowlevelKeyboardEvent.h>

// TODO: refactor singleton
OverlayWindow* overlay_window_instance = nullptr;

namespace
{
    // Window properties relevant to ShortcutGuide
    struct ShortcutGuideWindowInfo
    {
        HWND hwnd = nullptr; // Handle to the top-level foreground window or nullptr if there is no such window
        bool snappable = false; // True, if the window can react to Windows Snap keys
        bool disabled = false;
    };

    ShortcutGuideWindowInfo GetShortcutGuideWindowInfo(HWND active_window)
    {
        ShortcutGuideWindowInfo result;
        active_window = GetAncestor(active_window, GA_ROOT);
        if (!IsWindowVisible(active_window))
        {
            return result;
        }

        auto style = GetWindowLong(active_window, GWL_STYLE);
        auto exStyle = GetWindowLong(active_window, GWL_EXSTYLE);
        if ((style & WS_CHILD) == WS_CHILD ||
            (style & WS_DISABLED) == WS_DISABLED ||
            (exStyle & WS_EX_TOOLWINDOW) == WS_EX_TOOLWINDOW ||
            (exStyle & WS_EX_NOACTIVATE) == WS_EX_NOACTIVATE)
        {
            return result;
        }
        std::array<char, 256> class_name;
        GetClassNameA(active_window, class_name.data(), static_cast<int>(class_name.size()));
        if (is_system_window(active_window, class_name.data()))
        {
            return result;
        }
        static HWND cortana_hwnd = nullptr;
        if (cortana_hwnd == nullptr)
        {
            if (strcmp(class_name.data(), "Windows.UI.Core.CoreWindow") == 0 &&
                get_process_path(active_window).ends_with(L"SearchUI.exe"))
            {
                cortana_hwnd = active_window;
                return result;
            }
        }
        else if (cortana_hwnd == active_window)
        {
            return result;
        }
        result.hwnd = active_window;
        // In reality, Windows Snap works if even one of those styles is set
        // for a window, it is just limited. If there is no WS_MAXIMIZEBOX using
        // WinKey + Up just won't maximize the window. Similarly, without
        // WS_MINIMIZEBOX the window will not get minimized. A "Save As..." dialog
        // is a example of such window - it can be snapped to both sides and to
        // all screen corners, but will not get maximized nor minimized.
        // For now, since ShortcutGuide can only disable entire "Windows Controls"
        // group, we require that the window supports all the options.
        result.snappable = ((style & WS_MAXIMIZEBOX) == WS_MAXIMIZEBOX) &&
                           ((style & WS_MINIMIZEBOX) == WS_MINIMIZEBOX) &&
                           ((style & WS_THICKFRAME) == WS_THICKFRAME);
        return result;
    }

    const LPARAM eventActivateWindow = 1;

    bool wasWinPressed = false;
    bool isWinPressed()
    {
        return (GetAsyncKeyState(VK_LWIN) & 0x8000) || (GetAsyncKeyState(VK_RWIN) & 0x8000);
    }

    // all modifiers without win key
    std::vector<int> modifierKeys = { VK_SHIFT, VK_LSHIFT, VK_RSHIFT, VK_CONTROL, VK_LCONTROL, VK_RCONTROL, VK_MENU, VK_LMENU, VK_RMENU };

    // returns false if there are other modifiers pressed or win key isn' pressed
    bool onlyWinPressed()
    {
        if (!isWinPressed())
        {
            return false;
        }

        for (auto key : modifierKeys)
        {
            if (GetAsyncKeyState(key) & 0x8000)
            {
                return false;
            }
        }

        return true;
    }

    constexpr bool isWin(int key)
    {
        return key == VK_LWIN || key == VK_RWIN;
    }

    constexpr bool isKeyDown(LowlevelKeyboardEvent event)
    {
        return event.wParam == WM_KEYDOWN || event.wParam == WM_SYSKEYDOWN;
    }

    LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        LowlevelKeyboardEvent event;
        if (nCode == HC_ACTION)
        {
            event.lParam = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
            event.wParam = wParam;

            if (event.lParam->vkCode == VK_ESCAPE)
            {
                Logger::trace(L"ESC key was pressed");
                overlay_window_instance->CloseWindow(HideWindowType::ESC_PRESSED);
            }

            if (wasWinPressed && !isKeyDown(event) && isWin(event.lParam->vkCode))
            {
                Logger::trace(L"Win key was released");
                overlay_window_instance->CloseWindow(HideWindowType::WIN_RELEASED);
            }

            if (isKeyDown(event) && isWin(event.lParam->vkCode))
            {
                wasWinPressed = true;
            }

            if (onlyWinPressed() && isKeyDown(event) && !isWin(event.lParam->vkCode))
            {
                Logger::trace(L"Shortcut with win key was pressed");
                overlay_window_instance->CloseWindow(HideWindowType::WIN_SHORTCUT_PRESSED);
            }
        }

        return CallNextHookEx(NULL, nCode, wParam, lParam);
    }

    LRESULT CALLBACK LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0)
        {
            switch (wParam)
            {
            case WM_LBUTTONUP:
            case WM_RBUTTONUP:
            case WM_MBUTTONUP:
            case WM_XBUTTONUP:
                // Don't close with mouse click if activation is windows key and the key is pressed
                if (!overlay_window_instance->win_key_activation() || !isWinPressed())
                {
                    overlay_window_instance->CloseWindow(HideWindowType::MOUSE_BUTTONUP);
                }
                break;
            default:
                break;
            }
        }

        return CallNextHookEx(0, nCode, wParam, lParam);
    }

    std::wstring ToWstring(HideWindowType type)
    {
        switch (type)
        {
        case HideWindowType::ESC_PRESSED:
            return L"ESC_PRESSED";
        case HideWindowType::WIN_RELEASED:
            return L"WIN_RELEASED";
        case HideWindowType::WIN_SHORTCUT_PRESSED:
            return L"WIN_SHORTCUT_PRESSED";
        case HideWindowType::THE_SHORTCUT_PRESSED:
            return L"THE_SHORTCUT_PRESSED";
        case HideWindowType::MOUSE_BUTTONUP:
            return L"MOUSE_BUTTONUP";
        }

        return L"";
    }
}

OverlayWindow::OverlayWindow(HWND activeWindow)
{
    overlay_window_instance = this;
    this->activeWindow = activeWindow;
    app_name = GET_RESOURCE_STRING(IDS_SHORTCUT_GUIDE);

    Logger::info("Overlay Window is creating");
    init_settings();
    keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandle(NULL), NULL);
    if (!keyboardHook)
    {
        Logger::warn(L"Failed to create low level keyboard hook. {}", get_last_error_or_default(GetLastError()));
    }

    mouseHook = SetWindowsHookEx(WH_MOUSE_LL, LowLevelMouseProc, GetModuleHandle(NULL), NULL);
    if (!mouseHook)
    {
        Logger::warn(L"Failed to create low level mouse hook. {}", get_last_error_or_default(GetLastError()));
    }
}

void OverlayWindow::ShowWindow()
{
    winkey_popup = std::make_unique<D2DOverlayWindow>();
    winkey_popup->apply_overlay_opacity(overlayOpacity.value / 100.0f);
    winkey_popup->set_theme(theme.value);

    // The press time only takes effect when the shortcut guide is activated by pressing the win key.
    if (shouldReactToPressedWinKey.value)
    {
        winkey_popup->apply_press_time_for_global_windows_shortcuts(windowsKeyPressTimeForGlobalWindowsShortcuts.value);
        winkey_popup->apply_press_time_for_taskbar_icon_shortcuts(windowsKeyPressTimeForTaskbarIconShortcuts.value);
    }
    else
    {
        winkey_popup->apply_press_time_for_global_windows_shortcuts(0);
        winkey_popup->apply_press_time_for_taskbar_icon_shortcuts(0);
    }

    target_state = std::make_unique<TargetState>();
    try
    {
        winkey_popup->initialize();
    }
    catch (...)
    {
        Logger::critical("Winkey popup failed to initialize");
        return;
    }

    target_state->toggle_force_shown();
}

void OverlayWindow::CloseWindow(HideWindowType type, int mainThreadId)
{
    if (mainThreadId == 0)
    {
        mainThreadId = GetCurrentThreadId();
    }

    if (this->winkey_popup)
    {
        if (shouldReactToPressedWinKey.value)
        {
            // Send a dummy key to prevent Start Menu from activating
            INPUT dummyEvent[1] = {};
            dummyEvent[0].type = INPUT_KEYBOARD;
            dummyEvent[0].ki.wVk = 0xFF;
            dummyEvent[0].ki.dwFlags = KEYEVENTF_KEYUP;
            SendInput(1, dummyEvent, sizeof(INPUT));
        }
        this->winkey_popup->SetWindowCloseType(ToWstring(type));
        Logger::trace(L"Terminating process");
        PostThreadMessage(mainThreadId, WM_QUIT, 0, 0);
    }
}

bool OverlayWindow::IsDisabled()
{
    WCHAR exePath[MAX_PATH] = L"";
    overlay_window_instance->get_exe_path(activeWindow, exePath);
    if (wcslen(exePath) > 0)
    {
        return is_disabled_app(exePath);
    }

    return false;
}

OverlayWindow::~OverlayWindow()
{
    if (event_waiter)
    {
        event_waiter.reset();
    }

    if (winkey_popup)
    {
        winkey_popup->hide();
    }

    if (target_state)
    {
        target_state->exit();
        target_state.reset();
    }

    if (winkey_popup)
    {
        winkey_popup.reset();
    }

    if (keyboardHook)
    {
        UnhookWindowsHookEx(keyboardHook);
    }
}

void OverlayWindow::on_held()
{
    auto windowInfo = GetShortcutGuideWindowInfo(activeWindow);
    if (windowInfo.disabled)
    {
        target_state->was_hidden();
        return;
    }
    winkey_popup->show(windowInfo.hwnd, windowInfo.snappable);
}

void OverlayWindow::quick_hide()
{
    winkey_popup->quick_hide();
}

void OverlayWindow::was_hidden()
{
    target_state->was_hidden();
}

bool OverlayWindow::overlay_visible() const
{
    return target_state->active();
}

bool OverlayWindow::win_key_activation() const
{
    return shouldReactToPressedWinKey.value;
}

void OverlayWindow::init_settings()
{
    auto settings = GetSettings();
    overlayOpacity.value = settings.overlayOpacity;
    theme.value = settings.theme;
    disabledApps.value = settings.disabledApps;
    shouldReactToPressedWinKey.value = settings.shouldReactToPressedWinKey;
    windowsKeyPressTimeForGlobalWindowsShortcuts.value = settings.windowsKeyPressTimeForGlobalWindowsShortcuts;
    windowsKeyPressTimeForTaskbarIconShortcuts.value = settings.windowsKeyPressTimeForTaskbarIconShortcuts;
    update_disabled_apps();
}

bool OverlayWindow::is_disabled_app(wchar_t* exePath)
{
    if (exePath == nullptr)
    {
        return false;
    }

    auto exePathUpper = std::wstring(exePath);
    CharUpperBuffW(exePathUpper.data(), static_cast<DWORD>(exePathUpper.length()));
    for (const auto& row : disabled_apps_array)
    {
        const auto pos = exePathUpper.rfind(row);
        const auto last_slash = exePathUpper.rfind('\\');
        // Check that row occurs in disabled_apps_array, and its last occurrence contains in itself the first character after the last backslash.
        if (pos != std::wstring::npos && pos <= last_slash + 1 && pos + row.length() > last_slash)
        {
            return true;
        }
    }
    return false;
}

void OverlayWindow::update_disabled_apps()
{
    disabled_apps_array.clear();
    auto disabledUppercase = disabledApps.value;
    CharUpperBuffW(disabledUppercase.data(), static_cast<DWORD>(disabledUppercase.length()));
    std::wstring_view view(disabledUppercase);
    view = trim(view);
    while (!view.empty())
    {
        auto pos = (std::min)(view.find_first_of(L"\r\n"), view.length());
        disabled_apps_array.emplace_back(view.substr(0, pos));
        view.remove_prefix(pos);
        view = trim(view);
    }
}

void OverlayWindow::get_exe_path(HWND window, wchar_t* path)
{
    if (disabled_apps_array.empty())
    {
        return;
    }

    DWORD pid = 0;
    GetWindowThreadProcessId(window, &pid);
    if (pid != 0)
    {
        HANDLE processHandle = OpenProcess(PROCESS_QUERY_INFORMATION, FALSE, pid);
        if (processHandle && GetProcessImageFileName(processHandle, path, MAX_PATH) > 0)
        {
            CloseHandle(processHandle);
        }
    }
}

ShortcutGuideSettings OverlayWindow::GetSettings() noexcept
{
    ShortcutGuideSettings settings;
    json::JsonObject properties;
    try
    {
        PowerToysSettings::PowerToyValues settingsValues =
            PowerToysSettings::PowerToyValues::load_from_settings_file(app_key);

        auto settingsObject = settingsValues.get_raw_json();
        if (!settingsObject.GetView().Size())
        {
            return settings;
        }

        properties = settingsObject.GetNamedObject(L"properties");
    }
    catch (...)
    {
        Logger::warn("Failed to read settings. Use default settings");
        return settings;
    }

    try
    {
        settings.hotkey = PowerToysSettings::HotkeyObject::from_json(properties.GetNamedObject(OpenShortcut::name)).to_string();
    }
    catch (...)
    {
    }

    try
    {
        settings.overlayOpacity = static_cast<int>(properties.GetNamedObject(OverlayOpacity::name).GetNamedNumber(L"value"));
    }
    catch (...)
    {
    }

    try
    {
        settings.shouldReactToPressedWinKey = properties.GetNamedObject(ShouldReactToPressedWinKey::name).GetNamedBoolean(L"value");
    }
    catch (...)
    {
    }

    try
    {
        settings.windowsKeyPressTimeForGlobalWindowsShortcuts = static_cast<int>(properties.GetNamedObject(WindowsKeyPressTimeForGlobalWindowsShortcuts::name).GetNamedNumber(L"value"));
    }
    catch (...)
    {
    }

    try
    {
        settings.windowsKeyPressTimeForTaskbarIconShortcuts = static_cast<int>(properties.GetNamedObject(WindowsKeyPressTimeForTaskbarIconShortcuts::name).GetNamedNumber(L"value"));
    }
    catch (...)
    {
    }

    try
    {
        settings.theme = (std::wstring)properties.GetNamedObject(Theme::name).GetNamedString(L"value");
    }
    catch (...)
    {
    }

    try
    {
        settings.disabledApps = (std::wstring)properties.GetNamedObject(DisabledApps::name).GetNamedString(L"value");
    }
    catch (...)
    {
    }

    return settings;
}
