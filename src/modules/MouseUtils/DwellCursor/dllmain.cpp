#include "pch.h"
#include <common/SettingsAPI/settings_objects.h>
#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include <atomic>
#include <thread>
#include <common/utils/logger_helper.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    const wchar_t JSON_KEY_DELAY_TIME_MS[] = L"delay_time_ms";
}

static void SendLeftClick()
{
    INPUT inputs[2]{};
    inputs[0].type = INPUT_MOUSE;
    inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
    inputs[1].type = INPUT_MOUSE;
    inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;
    SendInput(2, inputs, sizeof(INPUT));
}

class DwellCursorModule : public PowertoyModuleIface
{
private:
    bool m_enabled{ false };
    // Hotkey (legacy API)
    Hotkey m_activationHotkey{};

    // Settings
    std::atomic<int> m_delayMs{ 1000 }; // 0.5s-10s

    // Dwell state
    std::atomic<bool> m_armed{ true };
    std::atomic<bool> m_stop{ false };
    std::thread m_worker;

    static constexpr int kMoveThresholdPx = 2;

public:
    DwellCursorModule()
    {
        LoggerHelpers::init_logger(L"DwellCursor", L"ModuleInterface", "dwell-cursor");
        init_settings();
        // Default hotkey Win+Alt+D
        if (m_activationHotkey.key == 0)
        {
            m_activationHotkey.win = true;
            m_activationHotkey.alt = true;
            m_activationHotkey.key = 'D';
        }
    }

    virtual void destroy() override
    {
        disable();
        delete this;
    }

    virtual const wchar_t* get_name() override { return L"DwellCursor"; }
    virtual const wchar_t* get_key() override { return L"DwellCursor"; }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            parse_settings(values);
        }
        catch (...)
        {
        }
    }

    virtual void call_custom_action(const wchar_t* /*action*/) override {}

    virtual void enable() override
    {
        if (m_enabled) return;
        m_enabled = true;
        m_stop = false;
        m_worker = std::thread([this]() { this->RunLoop(); });
    }

    virtual void disable() override
    {
        if (!m_enabled) return;
        m_enabled = false;
        m_stop = true;
        if (m_worker.joinable()) m_worker.join();
    }

    virtual bool is_enabled() override { return m_enabled; }

    virtual bool is_enabled_by_default() const override { return false; }

    virtual size_t get_hotkeys(Hotkey* buffer, size_t buffer_size) override
    {
        if (buffer && buffer_size >= 1)
        {
            buffer[0] = m_activationHotkey;
        }
        return 1;
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (!m_enabled) return false;
        if (hotkeyId == 0)
        {
            // Toggle armed
            m_armed = !m_armed.load();
            return true;
        }
        return false;
    }

private:
    void init_settings()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings = PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());
            parse_settings(settings);
        }
        catch (...)
        {
        }
    }

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        auto obj = settings.get_raw_json();
        if (!obj.GetView().Size()) return;
        try
        {
            auto jsonHotkey = obj.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
            auto hk = PowerToysSettings::HotkeyObject::from_json(jsonHotkey);
            m_activationHotkey = {};
            m_activationHotkey.win = hk.win_pressed();
            m_activationHotkey.ctrl = hk.ctrl_pressed();
            m_activationHotkey.shift = hk.shift_pressed();
            m_activationHotkey.alt = hk.alt_pressed();
            m_activationHotkey.key = static_cast<unsigned char>(hk.get_code());
        }
        catch (...)
        {
        }
        try
        {
            auto jsonDelay = obj.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_DELAY_TIME_MS);
            int v = static_cast<int>(jsonDelay.GetNamedNumber(JSON_KEY_VALUE));
            if (v < 500) v = 500; if (v > 10000) v = 10000;
            m_delayMs = v;
        }
        catch (...)
        {
        }
    }

    static bool Near(int a, int b, int thr) { return (abs(a - b) <= thr); }

    void RunLoop()
    {
        POINT last{}; GetCursorPos(&last);
        DWORD lastMove = GetTickCount();
        bool firedForThisStationary = false;
        while (!m_stop)
        {
            POINT p{}; GetCursorPos(&p);
            if (!Near(p.x, last.x, kMoveThresholdPx) || !Near(p.y, last.y, kMoveThresholdPx))
            {
                last = p;
                lastMove = GetTickCount();
                firedForThisStationary = false; // re-arm on movement
            }
            else
            {
                if (m_armed && !firedForThisStationary)
                {
                    DWORD elapsed = GetTickCount() - lastMove;
                    if (elapsed >= static_cast<DWORD>(m_delayMs.load()))
                    {
                        SendLeftClick();
                        firedForThisStationary = true; // require movement before firing again
                    }
                }
            }
            Sleep(10);
        }
    }
};

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new DwellCursorModule();
}
