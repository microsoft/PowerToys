#include "pch.h"
#include "shortcut_guide.h"
#include "target_state.h"
#include "trace.h"
#include "resource.h"

#include <common/common.h>
#include <common/settings_objects.h>
#include <common/debug_control.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

// TODO: refactor singleton
OverlayWindow* instance = nullptr;

namespace
{
    LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        LowlevelKeyboardEvent event;
        if (nCode == HC_ACTION)
        {
            event.lParam = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
            event.wParam = wParam;
            if (instance->signal_event(&event) != 0)
            {
                return 1;
            }
        }
        return CallNextHookEx(NULL, nCode, wParam, lParam);
    }
}

OverlayWindow::OverlayWindow()
{
    app_name = GET_RESOURCE_STRING(IDS_SHORTCUT_GUIDE);
    init_settings();
}

const wchar_t* OverlayWindow::get_name()
{
    return app_name.c_str();
}

bool OverlayWindow::get_config(wchar_t* buffer, int* buffer_size)
{
    HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

    PowerToysSettings::Settings settings(hinstance, get_name());
    settings.set_description(GET_RESOURCE_STRING(IDS_SETTINGS_DESCRIPTION));
    settings.set_overview_link(L"https://aka.ms/PowerToysOverview_ShortcutGuide");
    settings.set_icon_key(L"pt-shortcut-guide");

    settings.add_int_spinner(
        pressTime.name,
        pressTime.resourceId,
        pressTime.value,
        100,
        10000,
        100);

    settings.add_int_spinner(
        overlayOpacity.name,
        overlayOpacity.resourceId,
        overlayOpacity.value,
        0,
        100,
        1);

    settings.add_choice_group(
        theme.name,
        theme.resourceId,
        theme.value,
        theme.keys_and_texts);

    return settings.serialize_to_buffer(buffer, buffer_size);
}

void OverlayWindow::set_config(const wchar_t* config)
{
    try
    {
        // save configuration
        PowerToysSettings::PowerToyValues _values =
            PowerToysSettings::PowerToyValues::from_json_string(config);
        _values.save_to_settings_file();
        Trace::SettingsChanged(pressTime.value, overlayOpacity.value, theme.value);

        // apply new settings if powertoy is enabled
        if (_enabled)
        {
            if (const auto press_delay_time = _values.get_int_value(pressTime.name))
            {
                pressTime.value = *press_delay_time;
                if (target_state)
                {
                    target_state->set_delay(*press_delay_time);
                }
            }
            if (const auto overlay_opacity = _values.get_int_value(overlayOpacity.name))
            {
                overlayOpacity.value = *overlay_opacity;
                if (winkey_popup)
                {
                    winkey_popup->apply_overlay_opacity(((float)overlayOpacity.value) / 100.0f);
                }
            }
            if (auto val = _values.get_string_value(theme.name))
            {
                theme.value = std::move(*val);
                if (winkey_popup)
                {
                    winkey_popup->set_theme(theme.value);
                }
            }
        }
    }
    catch (...)
    {
        // Improper JSON. TODO: handle the error.
    }
}

constexpr int alternative_switch_hotkey_id = 0x2;
constexpr UINT alternative_switch_modifier_mask = MOD_WIN | MOD_SHIFT;
constexpr UINT alternative_switch_vk_code = VK_OEM_2;

void OverlayWindow::enable()
{
    auto switcher = [&](HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam) -> LRESULT {
        if (msg == WM_KEYDOWN && wparam == VK_ESCAPE && instance->target_state->active())
        {
            instance->target_state->toggle_force_shown();
            return 0;
        }
        if (msg != WM_HOTKEY)
        {
            return 0;
        }
        const auto vk_code = HIWORD(lparam);
        const auto modifiers_mask = LOWORD(lparam);
        if (alternative_switch_vk_code != vk_code || alternative_switch_modifier_mask != modifiers_mask)
        {
            return 0;
        }
        instance->target_state->toggle_force_shown();
        return 0;
    };

    if (!_enabled)
    {
        Trace::EnableShortcutGuide(true);
        winkey_popup = std::make_unique<D2DOverlayWindow>(std::move(switcher));
        winkey_popup->apply_overlay_opacity(((float)overlayOpacity.value) / 100.0f);
        winkey_popup->set_theme(theme.value);
        target_state = std::make_unique<TargetState>(pressTime.value);
        winkey_popup->initialize();
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
        const bool hook_disabled = IsDebuggerPresent();
#else
        const bool hook_disabled = false;
#endif
        if (!hook_disabled)
        {
            hook_handle = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandle(NULL), NULL);
            if (!hook_handle)
            {
                MessageBoxW(NULL, L"Cannot install keyboard listener.", L"PowerToys - Shortcut Guide", MB_OK | MB_ICONERROR);
            }
        }
        RegisterHotKey(winkey_popup->get_window_handle(), alternative_switch_hotkey_id, alternative_switch_modifier_mask, alternative_switch_vk_code);
    }
    _enabled = true;
}

void OverlayWindow::disable(bool trace_event)
{
    if (_enabled)
    {
        _enabled = false;
        if (trace_event)
        {
            Trace::EnableShortcutGuide(false);
        }
        UnregisterHotKey(winkey_popup->get_window_handle(), alternative_switch_hotkey_id);
        winkey_popup->hide();
        target_state->exit();
        target_state.reset();
        winkey_popup.reset();
        if (hook_handle)
        {
            bool success = UnhookWindowsHookEx(hook_handle);
            if (success)
            {
                hook_handle = nullptr;
            }
        }
    }
}

void OverlayWindow::disable()
{
    this->disable(true);
}

bool OverlayWindow::is_enabled()
{
    return _enabled;
}

intptr_t OverlayWindow::signal_event(LowlevelKeyboardEvent* event)
{
    if (!_enabled)
    {
        return 0;
    }

    if (event->wParam == WM_KEYDOWN ||
        event->wParam == WM_SYSKEYDOWN ||
        event->wParam == WM_KEYUP ||
        event->wParam == WM_SYSKEYUP)
    {
        bool suppress = target_state->signal_event(event->lParam->vkCode,
                                                   event->wParam == WM_KEYDOWN || event->wParam == WM_SYSKEYDOWN);
        return suppress ? 1 : 0;
    }
    else
    {
        return 0;
    }
}

void OverlayWindow::on_held()
{
    auto filter = get_shortcutguide_filtered_window();
    winkey_popup->show(filter.hwnd, filter.snappable);
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

void OverlayWindow::destroy()
{
    this->disable(false);
    delete this;
    instance = nullptr;
}

bool OverlayWindow::overlay_visible() const
{
    return target_state->active();
}

void OverlayWindow::init_settings()
{
    try
    {
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(OverlayWindow::get_name());
        if (const auto val = settings.get_int_value(pressTime.name))
        {
            pressTime.value = *val;
        }
        if (const auto val = settings.get_int_value(overlayOpacity.name))
        {
            overlayOpacity.value = *val;
        }
        if (auto val = settings.get_string_value(theme.name))
        {
            theme.value = std::move(*val);
        }
    }
    catch (std::exception&)
    {
        // Error while loading from the settings file. Just let default values stay as they are.
    }
}
