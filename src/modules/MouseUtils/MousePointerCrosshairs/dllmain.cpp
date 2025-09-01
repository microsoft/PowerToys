#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include "InclusiveCrosshairs.h"
#include "common/utils/color.h"
#include <atomic>
#include <thread>
#include <chrono>
#include <memory>

extern void InclusiveCrosshairsRequestUpdatePosition();
extern void InclusiveCrosshairsEnsureOn();
extern void InclusiveCrosshairsEnsureOff();
extern void InclusiveCrosshairsSetExternalControl(bool enabled);

// Non-Localizable strings
namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    const wchar_t JSON_KEY_GLIDING_ACTIVATION_SHORTCUT[] = L"gliding_cursor_activation_shortcut";
    const wchar_t JSON_KEY_CROSSHAIRS_COLOR[] = L"crosshairs_color";
    const wchar_t JSON_KEY_CROSSHAIRS_OPACITY[] = L"crosshairs_opacity";
    const wchar_t JSON_KEY_CROSSHAIRS_RADIUS[] = L"crosshairs_radius";
    const wchar_t JSON_KEY_CROSSHAIRS_THICKNESS[] = L"crosshairs_thickness";
    const wchar_t JSON_KEY_CROSSHAIRS_BORDER_COLOR[] = L"crosshairs_border_color";
    const wchar_t JSON_KEY_CROSSHAIRS_BORDER_SIZE[] = L"crosshairs_border_size";
    const wchar_t JSON_KEY_CROSSHAIRS_AUTO_HIDE[] = L"crosshairs_auto_hide";
    const wchar_t JSON_KEY_CROSSHAIRS_IS_FIXED_LENGTH_ENABLED[] = L"crosshairs_is_fixed_length_enabled";
    const wchar_t JSON_KEY_CROSSHAIRS_FIXED_LENGTH[] = L"crosshairs_fixed_length";
    const wchar_t JSON_KEY_AUTO_ACTIVATE[] = L"auto_activate";
    const wchar_t JSON_KEY_GLIDE_TRAVEL_SPEED[] = L"gliding_travel_speed";
    const wchar_t JSON_KEY_GLIDE_DELAY_SPEED[] = L"gliding_delay_speed";
}

extern "C" IMAGE_DOS_HEADER __ImageBase;

HMODULE m_hModule;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    m_hModule = hModule;
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();
        break;
    }
    return TRUE;
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"MousePointerCrosshairs";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

// Implement the PowerToy Module Interface and all the required methods.
class MousePointerCrosshairs : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;

    // Additional hotkeys (legacy API) to support multiple shortcuts
    Hotkey m_activationHotkey{};    // Crosshairs toggle
    Hotkey m_glidingHotkey{};       // Gliding cursor state machine

    // Shared state for worker threads (decoupled from this lifetime)
    struct State
    {
        std::atomic<bool> stopX{ false };
        std::atomic<bool> stopY{ false };

        // positions and speeds
        int currentXPos{ 0 };
        int currentYPos{ 0 };
        int currentXSpeed{ 0 }; // pixels per base window
        int currentYSpeed{ 0 }; // pixels per base window
        int xPosSnapshot{ 0 };  // xPos captured at end of horizontal scan

        // Fractional accumulators to spread movement across 10ms ticks
        double xFraction{ 0.0 };
        double yFraction{ 0.0 };

        // Speeds represent pixels per 200ms (min 5, max 60 enforced by UI/settings)
        int fastHSpeed{ 30 }; // pixels per base window
        int slowHSpeed{ 5 };  // pixels per base window
        int fastVSpeed{ 30 }; // pixels per base window
        int slowVSpeed{ 5 };  // pixels per base window
    };

    std::shared_ptr<State> m_state;

    // Worker threads
    std::thread m_xThread;
    std::thread m_yThread;

    // Gliding cursor state machine
    std::atomic<int> m_glideState{ 0 }; // 0..4 like the AHK script

    // Timer configuration: 10ms tick, speeds are defined per 200ms base window
    static constexpr int kTimerTickMs = 10;
    static constexpr int kBaseSpeedTickMs = 200; // mapping period for configured pixel counts

    // Mouse Pointer Crosshairs specific settings
    InclusiveCrosshairsSettings m_inclusiveCrosshairsSettings;

public:
    // Constructor
    MousePointerCrosshairs()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::mousePointerCrosshairsLoggerName);
        m_state = std::make_shared<State>();
        init_settings();
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        StopXTimer();
        StopYTimer();
        // Release shared state so worker threads (if any) exit when weak_ptr lock fails
        m_state.reset();
        delete this;
    }

    // Return the localized display name of the powertoy
    virtual const wchar_t* get_name() override
    {
        return MODULE_NAME;
    }

    // Return the non localized key of the powertoy, this will be cached by the runner
    virtual const wchar_t* get_key() override
    {
        return MODULE_NAME;
    }

    // Return the configured status for the gpo policy for the module
    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredMousePointerCrosshairsEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Signal from the Settings editor to call a custom action.
    // This can be used to spawn more complex editors.
    virtual void call_custom_action(const wchar_t* /*action*/) override {}

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            // Parse the input JSON string.
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

            parse_settings(values);

            InclusiveCrosshairsApplySettings(m_inclusiveCrosshairsSettings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to parse Mouse Pointer Crosshairs settings json.");
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        Trace::EnableMousePointerCrosshairs(true);
        std::thread([=]() { InclusiveCrosshairsMain(m_hModule, m_inclusiveCrosshairsSettings); }).detach();
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Trace::EnableMousePointerCrosshairs(false);
        StopXTimer();
        StopYTimer();
        m_glideState = 0;
        InclusiveCrosshairsDisable();
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Returns whether the PowerToys should be enabled by default
    virtual bool is_enabled_by_default() const override
    {
        return false;
    }

    // Legacy multi-hotkey support (like CropAndLock)
    virtual size_t get_hotkeys(Hotkey* buffer, size_t buffer_size) override
    {
        if (buffer && buffer_size >= 2)
        {
            buffer[0] = m_activationHotkey; // Crosshairs toggle
            buffer[1] = m_glidingHotkey;    // Gliding cursor toggle
        }
        return 2;
    }

    virtual bool on_hotkey(size_t hotkeyId) override
    {
        if (!m_enabled)
        {
            return false;
        }

        if (hotkeyId == 0)
        {
            InclusiveCrosshairsSwitch();
            return true;
        }
        if (hotkeyId == 1)
        {
            HandleGlidingHotkey();
            return true;
        }
        return false;
    }

private:
    static void LeftClick()
    {
        INPUT inputs[2]{};
        inputs[0].type = INPUT_MOUSE;
        inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
        inputs[1].type = INPUT_MOUSE;
        inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;
        SendInput(2, inputs, sizeof(INPUT));
    }

    // Stateless helpers operating on shared State
    static void PositionCursorX(const std::shared_ptr<State>& s)
    {
        int screenW = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int screenH = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        s->currentYPos = screenH / 2;

        // Distribute movement over 10ms ticks to match pixels-per-base-window speeds
        const double perTick = (static_cast<double>(s->currentXSpeed) * kTimerTickMs) / static_cast<double>(kBaseSpeedTickMs);
        s->xFraction += perTick;
        int step = static_cast<int>(s->xFraction);
        if (step > 0)
        {
            s->xFraction -= step;
            s->currentXPos += step;
        }

        s->xPosSnapshot = s->currentXPos;
        if (s->currentXPos >= screenW)
        {
            s->currentXPos = 0;
            s->currentXSpeed = s->fastHSpeed;
            s->xPosSnapshot = 0;
            s->xFraction = 0.0; // reset fractional remainder on wrap
        }
        SetCursorPos(s->currentXPos, s->currentYPos);
        // Ensure overlay crosshairs follow immediately
        InclusiveCrosshairsRequestUpdatePosition();
    }

    static void PositionCursorY(const std::shared_ptr<State>& s)
    {
        int screenH = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        // Keep X at snapshot
        // Use s->xPosSnapshot captured during X pass

        // Distribute movement over 10ms ticks to match pixels-per-base-window speeds
        const double perTick = (static_cast<double>(s->currentYSpeed) * kTimerTickMs) / static_cast<double>(kBaseSpeedTickMs);
        s->yFraction += perTick;
        int step = static_cast<int>(s->yFraction);
        if (step > 0)
        {
            s->yFraction -= step;
            s->currentYPos += step;
        }

        if (s->currentYPos >= screenH)
        {
            s->currentYPos = 0;
            s->currentYSpeed = s->fastVSpeed;
            s->yFraction = 0.0; // reset fractional remainder on wrap
        }
        SetCursorPos(s->xPosSnapshot, s->currentYPos);
        // Ensure overlay crosshairs follow immediately
        InclusiveCrosshairsRequestUpdatePosition();
    }

    void StartXTimer()
    {
        auto s = m_state;
        if (!s)
        {
            return;
        }
        s->stopX = false;
        std::weak_ptr<State> wp = s;
        m_xThread = std::thread([wp]() {
            while (true)
            {
                auto sp = wp.lock();
                if (!sp || sp->stopX.load())
                {
                    break;
                }
                PositionCursorX(sp);
                std::this_thread::sleep_for(std::chrono::milliseconds(kTimerTickMs));
            }
        });
    }

    void StopXTimer()
    {
        auto s = m_state;
        if (s)
        {
            s->stopX = true;
        }
        if (m_xThread.joinable())
        {
            m_xThread.join();
        }
    }

    void StartYTimer()
    {
        auto s = m_state;
        if (!s)
        {
            return;
        }
        s->stopY = false;
        std::weak_ptr<State> wp = s;
        m_yThread = std::thread([wp]() {
            while (true)
            {
                auto sp = wp.lock();
                if (!sp || sp->stopY.load())
                {
                    break;
                }
                PositionCursorY(sp);
                std::this_thread::sleep_for(std::chrono::milliseconds(kTimerTickMs));
            }
        });
    }

    void StopYTimer()
    {
        auto s = m_state;
        if (s)
        {
            s->stopY = true;
        }
        if (m_yThread.joinable())
        {
            m_yThread.join();
        }
    }

    void HandleGlidingHotkey()
    {
        auto s = m_state;
        if (!s)
        {
            return;
        }
        // Simulate the AHK state machine
        int state = m_glideState.load();
        switch (state)
        {
        case 0:
        {
            // Ensure crosshairs on (do not toggle off if already on)
            InclusiveCrosshairsEnsureOn();
            // Disable internal mouse hook so we control position updates explicitly
            InclusiveCrosshairsSetExternalControl(true);

            s->currentXPos = 0;
            s->currentXSpeed = s->fastHSpeed;
            s->xFraction = 0.0;
            s->yFraction = 0.0;
            int y = GetSystemMetrics(SM_CYVIRTUALSCREEN) / 2;
            SetCursorPos(0, y);
            InclusiveCrosshairsRequestUpdatePosition();
            m_glideState = 1;
            StartXTimer();
            break;
        }
        case 1:
        {
            // Slow horizontal
            s->currentXSpeed = s->slowHSpeed;
            m_glideState = 2;
            break;
        }
        case 2:
        {
            // Stop horizontal, start vertical (fast)
            StopXTimer();
            s->currentYSpeed = s->fastVSpeed;
            s->currentYPos = 0;
            s->yFraction = 0.0;
            SetCursorPos(s->xPosSnapshot, s->currentYPos);
            InclusiveCrosshairsRequestUpdatePosition();
            m_glideState = 3;
            StartYTimer();
            break;
        }
        case 3:
        {
            // Slow vertical
            s->currentYSpeed = s->slowVSpeed;
            m_glideState = 4;
            break;
        }
        case 4:
        default:
        {
            // Stop vertical, click, turn crosshairs off, re-enable internal tracking, reset state
            StopYTimer();
            m_glideState = 0;
            LeftClick();
            InclusiveCrosshairsEnsureOff();
            InclusiveCrosshairsSetExternalControl(false);
            s->xFraction = 0.0;
            s->yFraction = 0.0;
            break;
        }
        }
    }

    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(MousePointerCrosshairs::get_key());
            parse_settings(settings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to load the Mouse Pointer Crosshairs settings json from file.");
        }
    }

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        // TODO: refactor to use common/utils/json.h instead
        auto settingsObject = settings.get_raw_json();
        InclusiveCrosshairsSettings inclusiveCrosshairsSettings;
        if (settingsObject.GetView().Size())
        {
            try
            {
                // Parse primary activation HotKey (for centralized hook)
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                auto hotkey = PowerToysSettings::HotkeyObject::from_json(jsonPropertiesObject);

                // Map to legacy Hotkey for multi-hotkey API
                m_activationHotkey.win = hotkey.win_pressed();
                m_activationHotkey.ctrl = hotkey.ctrl_pressed();
                m_activationHotkey.shift = hotkey.shift_pressed();
                m_activationHotkey.alt = hotkey.alt_pressed();
                m_activationHotkey.key = static_cast<unsigned char>(hotkey.get_code());
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Mouse Pointer Crosshairs activation shortcut");
            }
            try
            {
                // Parse Gliding Cursor HotKey
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_GLIDING_ACTIVATION_SHORTCUT);
                auto hotkey = PowerToysSettings::HotkeyObject::from_json(jsonPropertiesObject);
                m_glidingHotkey.win = hotkey.win_pressed();
                m_glidingHotkey.ctrl = hotkey.ctrl_pressed();
                m_glidingHotkey.shift = hotkey.shift_pressed();
                m_glidingHotkey.alt = hotkey.alt_pressed();
                m_glidingHotkey.key = static_cast<unsigned char>(hotkey.get_code());
            }
            catch (...)
            {
                // note that this is also defined in src\settings-ui\Settings.UI.Library\MousePointerCrosshairsProperties.cs, DefaultGlidingCursorActivationShortcut
                // both need to be kept in sync!
                Logger::warn("Failed to initialize Gliding Cursor activation shortcut. Using default Win+Alt+.");
                m_glidingHotkey.win = true;
                m_glidingHotkey.alt = true;
                m_glidingHotkey.ctrl = false;
                m_glidingHotkey.shift = false;
                m_glidingHotkey.key = VK_OEM_PERIOD;
            }
            try
            {
                // Parse Opacity
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIRS_OPACITY);
                int value = static_cast<uint8_t>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 0)
                {
                    inclusiveCrosshairsSettings.crosshairsOpacity = value;
                }
                else
                {
                    throw std::runtime_error("Invalid Opacity value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Opacity from settings. Will use default value");
            }
            try
            {
                // Parse crosshairs color
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIRS_COLOR);
                auto crosshairsColor = (std::wstring)jsonPropertiesObject.GetNamedString(JSON_KEY_VALUE);
                uint8_t r, g, b;
                if (!checkValidRGB(crosshairsColor, &r, &g, &b))
                {
                    Logger::error("Crosshairs color RGB value is invalid. Will use default value");
                }
                else
                {
                    inclusiveCrosshairsSettings.crosshairsColor = winrt::Windows::UI::ColorHelper::FromArgb(255, r, g, b);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize crosshairs color from settings. Will use default value");
            }
            try
            {
                // Parse Radius
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIRS_RADIUS);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 0)
                {
                    inclusiveCrosshairsSettings.crosshairsRadius = value;
                }
                else
                {
                    throw std::runtime_error("Invalid Radius value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Radius from settings. Will use default value");
            }
            try
            {
                // Parse Thickness
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIRS_THICKNESS);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 0)
                {
                    inclusiveCrosshairsSettings.crosshairsThickness = value;
                }
                else
                {
                    throw std::runtime_error("Invalid Thickness value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize Thickness from settings. Will use default value");
            }
            try
            {
                // Parse crosshairs border color
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIRS_BORDER_COLOR);
                auto crosshairsBorderColor = (std::wstring)jsonPropertiesObject.GetNamedString(JSON_KEY_VALUE);
                uint8_t r, g, b;
                if (!checkValidRGB(crosshairsBorderColor, &r, &g, &b))
                {
                    Logger::error("Crosshairs border color RGB value is invalid. Will use default value");
                }
                else
                {
                    inclusiveCrosshairsSettings.crosshairsBorderColor = winrt::Windows::UI::ColorHelper::FromArgb(255, r, g, b);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize crosshairs border color from settings. Will use default value");
            }
            try
            {
                // Parse border size
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIRS_BORDER_SIZE);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 0)
                {
                    inclusiveCrosshairsSettings.crosshairsBorderSize = value;
                }
                else
                {
                    throw std::runtime_error("Invalid Border Color value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize border color from settings. Will use default value");
            }
            try
            {
                // Parse auto hide
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIRS_AUTO_HIDE);
                inclusiveCrosshairsSettings.crosshairsAutoHide = jsonPropertiesObject.GetNamedBoolean(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize auto hide from settings. Will use default value");
            }
            try
            {
                // Parse whether the fixed length is enabled
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIRS_IS_FIXED_LENGTH_ENABLED);
                bool value = jsonPropertiesObject.GetNamedBoolean(JSON_KEY_VALUE);
                inclusiveCrosshairsSettings.crosshairsIsFixedLengthEnabled = value;
            }
            catch (...)
            {
                Logger::warn("Failed to initialize fixed length enabled from settings. Will use default value");
            }
            try
            {
                // Parse fixed length
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_CROSSHAIRS_FIXED_LENGTH);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 0)
                {
                    inclusiveCrosshairsSettings.crosshairsFixedLength = value;
                }
                else
                {
                    throw std::runtime_error("Invalid Fixed Length value");
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize fixed length from settings. Will use default value");
            }
            try
            {
                // Parse auto activate
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_AUTO_ACTIVATE);
                inclusiveCrosshairsSettings.autoActivate = jsonPropertiesObject.GetNamedBoolean(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize auto activate from settings. Will use default value");
            }
            try
            {
                // Parse Travel speed (fast speed mapping)
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_GLIDE_TRAVEL_SPEED);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 5 && value <= 60)
                {
                    m_state->fastHSpeed = value;
                    m_state->fastVSpeed = value;
                }
                else if (value < 5)
                {
                    m_state->fastHSpeed = 5; m_state->fastVSpeed = 5;
                }
                else
                {
                    m_state->fastHSpeed = 60; m_state->fastVSpeed = 60;
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize gliding travel speed from settings. Using default 25.");
                if (m_state)
                {
                    m_state->fastHSpeed = 25;
                    m_state->fastVSpeed = 25;
                }
            }
            try
            {
                // Parse Delay speed (slow speed mapping)
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_GLIDE_DELAY_SPEED);
                int value = static_cast<int>(jsonPropertiesObject.GetNamedNumber(JSON_KEY_VALUE));
                if (value >= 5 && value <= 60)
                {
                    m_state->slowHSpeed = value;
                    m_state->slowVSpeed = value;
                }
                else if (value < 5)
                {
                    m_state->slowHSpeed = 5; m_state->slowVSpeed = 5;
                }
                else
                {
                    m_state->slowHSpeed = 60; m_state->slowVSpeed = 60;
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize gliding delay speed from settings. Using default 5.");
                if (m_state)
                {
                    m_state->slowHSpeed = 5;
                    m_state->slowVSpeed = 5;
                }
            }
        }
        else
        {
            Logger::info("Mouse Pointer Crosshairs settings are empty");
        }
        
        if (m_activationHotkey.key == 0)
        {
            m_activationHotkey.win = true;
            m_activationHotkey.alt = true;
            m_activationHotkey.ctrl = false;
            m_activationHotkey.shift = false;
            m_activationHotkey.key = 'P';
        }
        if (m_glidingHotkey.key == 0)
        {
            m_glidingHotkey.win = true;
            m_glidingHotkey.alt = true;
            m_glidingHotkey.ctrl = false;
            m_glidingHotkey.shift = false;
            m_glidingHotkey.key = VK_OEM_PERIOD;
        }
        m_inclusiveCrosshairsSettings = inclusiveCrosshairsSettings;
    }
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new MousePointerCrosshairs();
}