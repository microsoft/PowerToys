// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "WindowTabs.h"
#include "trace.h"
#include <cmath>
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
        Trace::RegisterProvider();
    if (reason == DLL_PROCESS_DETACH)
        Trace::UnregisterProvider();
    return TRUE;
}

class WindowTabsModule : public PowertoyModuleIface
{
    WindowTabs::Service m_service;
    bool m_enabled = false;
    Hotkey m_hotkeys[2];

    void Parse(PowerToysSettings::PowerToyValues& values)
    {
        m_service.SetRestore(values.get_bool_value(L"restore_session").value_or(true));
        const auto properties = values.get_raw_json().GetNamedObject(L"properties");
        const wchar_t* keys[] = { L"next_tab_shortcut", L"previous_tab_shortcut" };
        for (size_t index = 0; index < 2; ++index)
        {
            if (!properties.HasKey(keys[index]))
                continue;
            const auto object = properties.GetNamedObject(keys[index]);
            const auto key = object.GetNamedNumber(L"code");
            Hotkey hotkey{ object.GetNamedBoolean(L"win"), object.GetNamedBoolean(L"ctrl"), object.GetNamedBoolean(L"shift"), object.GetNamedBoolean(L"alt"), 0 };
            if (key >= 1 && key <= 255 && std::floor(key) == key && (hotkey.win || hotkey.ctrl || hotkey.shift || hotkey.alt))
            {
                hotkey.key = static_cast<unsigned char>(key);
                hotkey.id = static_cast<int>(index);
                m_hotkeys[index] = hotkey;
            }
        }
    }

public:
    WindowTabsModule()
    {
        LoggerHelpers::init_logger(L"WindowTabs", L"ModuleInterface", "WindowTabs");
        m_hotkeys[0] = { true, true, false, false, VK_NEXT, 0 };
        m_hotkeys[1] = { true, true, false, false, VK_PRIOR, 1 };
        try
        {
            auto settings = PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());
            Parse(settings);
        }
        catch (...)
        {
        } // First run uses defaults.
    }

    const wchar_t* get_name() override { return L"Window Tabs"; }
    const wchar_t* get_key() override { return L"WindowTabs"; }
    bool is_enabled_by_default() const override { return false; }
    bool is_enabled() override { return m_enabled; }
    void enable() override
    {
        if (m_enabled)
            return;
        m_enabled = m_service.Start(reinterpret_cast<HINSTANCE>(&__ImageBase));
        Trace::EnableWindowTabs(m_enabled);
    }
    void disable() override
    {
        m_enabled = false;
        m_service.Stop();
        Trace::EnableWindowTabs(false);
    }
    void destroy() override
    {
        disable();
        delete this;
    }
    bool get_config(wchar_t* buffer, int* size) override
    {
        PowerToysSettings::Settings settings(reinterpret_cast<HINSTANCE>(&__ImageBase), get_key());
        return settings.serialize_to_buffer(buffer, size);
    }
    void set_config(const wchar_t* config) override
    {
        try
        {
            auto values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            Parse(values);
            values.save_to_settings_file();
        }
        catch (...)
        {
            Logger::warn("Invalid Window Tabs settings.");
        }
    }
    size_t get_hotkeys(Hotkey* hotkeys, size_t size) override
    {
        if (hotkeys)
        {
            for (size_t index = 0; index < std::min(size, size_t{ 2 }); ++index)
                hotkeys[index] = m_hotkeys[index];
        }
        return 2;
    }
    bool on_hotkey(size_t index) override
    {
        return m_enabled && index < 2 && m_service.Cycle(index == 0);
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new WindowTabsModule();
}
