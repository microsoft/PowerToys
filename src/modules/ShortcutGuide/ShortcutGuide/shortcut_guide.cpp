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

// TODO: refactor singleton
OverlayWindow* instance = nullptr;

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
        // WinKey + Up just won't maximize the window. Similary, without
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
}

constexpr int alternative_switch_hotkey_id = 0x2;
constexpr UINT alternative_switch_modifier_mask = MOD_WIN | MOD_SHIFT;
constexpr UINT alternative_switch_vk_code = VK_OEM_2;

OverlayWindow::OverlayWindow(HWND activeWindow)
{
    instance = this;
    this -> activeWindow = activeWindow;
    app_name = GET_RESOURCE_STRING(IDS_SHORTCUT_GUIDE);

    Logger::info("Overlay Window is creating");
    init_settings();
}

void OverlayWindow::ShowWindow()
{
    winkey_popup = std::make_unique<D2DOverlayWindow>();
    winkey_popup->apply_overlay_opacity(((float)overlayOpacity.value) / 100.0f);
    winkey_popup->set_theme(theme.value);
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

bool OverlayWindow::IsDisabled()
{
    WCHAR exePath[MAX_PATH] = L"";
    instance->get_exe_path(activeWindow, exePath);
    if (wcslen(exePath) > 0)
    {
        return is_disabled_app(exePath);
    }

    return false;
}

OverlayWindow::~OverlayWindow()
{
    UnregisterHotKey(winkey_popup->get_window_handle(), alternative_switch_hotkey_id);
    event_waiter.reset();
    winkey_popup->hide();
    target_state->exit();
    target_state.reset();
    winkey_popup.reset();
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

void OverlayWindow::on_held_press(DWORD vkCode)
{
    winkey_popup->animate(vkCode);
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

void OverlayWindow::init_settings()
{
    auto s = GetSettings();
    overlayOpacity.value = s.overlayOpacity;
    theme.value = s.theme;
    disabledApps.value = s.theme;
}

bool OverlayWindow::is_disabled_app(wchar_t* exePath)
{
    if (exePath == nullptr)
    {
        return false;
    }

    auto exePathUpper = std::wstring(exePath);
    CharUpperBuffW(exePathUpper.data(), (DWORD)exePathUpper.length());

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
    CharUpperBuffW(disabledUppercase.data(), (DWORD)disabledUppercase.length());
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
    ShortcutGuideSettings s;
    json::JsonObject properties;
    try
    {
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(app_key);

        auto settingsObject = settings.get_raw_json();
        if (!settingsObject.GetView().Size())
        {
            return s;
        }

        properties = settingsObject.GetNamedObject(L"properties");
    }
    catch (...)
    {
        Logger::warn("Failed to read settings. Use default settings");
        return s;
    }

    try
    {
        s.hotkey = PowerToysSettings::HotkeyObject::from_json(properties.GetNamedObject(OpenShortcut::name)).to_string();
    }
    catch (...)
    {
    }

    try
    {
        s.overlayOpacity = (int)properties.GetNamedObject(OverlayOpacity::name).GetNamedNumber(L"value");
    }
    catch (...)
    {
    }

    try
    {
        s.theme = (std::wstring)properties.GetNamedObject(Theme::name).GetNamedString(L"value");
    }
    catch (...)
    {
    }

    try
    {
        s.disabledApps = (std::wstring)properties.GetNamedObject(DisabledApps::name).GetNamedString(L"value");
    }
    catch (...)
    {
    }

    return s;
}
