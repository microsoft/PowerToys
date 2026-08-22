#include "pch.h"
#include <common/SettingsAPI/settings_objects.h>
#include "trace.h"
#include "InclusiveCrosshairs.h"
#include "common/utils/color.h"
#include <common/utils/EventWaiter.h>
#include <common/interop/shared_constants.h>
#include <thread>
#include <chrono>
#include <memory>
#include <algorithm>

extern void InclusiveCrosshairsRequestUpdatePosition();
extern void InclusiveCrosshairsEnsureOn();
extern void InclusiveCrosshairsEnsureOff();
extern void InclusiveCrosshairsSetExternalControl(bool enabled);
extern void InclusiveCrosshairsSetOrientation(CrosshairsOrientation orientation);
extern bool InclusiveCrosshairsIsEnabled();
extern void InclusiveCrosshairsSwitch();

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
    const wchar_t JSON_KEY_CROSSHAIRS_ORIENTATION[] = L"crosshairs_orientation";
    const wchar_t JSON_KEY_AUTO_ACTIVATE[] = L"auto_activate";
    const wchar_t JSON_KEY_GLIDE_TRAVEL_SPEED[] = L"gliding_travel_speed";
    const wchar_t JSON_KEY_GLIDE_DELAY_SPEED[] = L"gliding_delay_speed";
}

extern "C" IMAGE_DOS_HEADER __ImageBase;

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"MousePointerCrosshairs";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

class MousePointerCrosshairs; // fwd
static std::atomic<MousePointerCrosshairs*> g_instance{ nullptr }; // for hook callback

// Implement the PowerToy Module Interface and all the required methods.
class MousePointerCrosshairs
{
private:
    // Low-level keyboard hook (Escape to cancel gliding)
    HHOOK m_keyboardHook = nullptr;

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
        int xPosSnapshot{ 0 }; // xPos captured at end of horizontal scan

        // Fractional accumulators to spread movement across 10ms ticks
        double xFraction{ 0.0 };
        double yFraction{ 0.0 };

        // Speeds represent pixels per 200ms (min 5, max 60 enforced by UI/settings)
        int fastHSpeed{ 30 }; // pixels per base window
        int slowHSpeed{ 5 }; // pixels per base window
        int fastVSpeed{ 30 }; // pixels per base window
        int slowVSpeed{ 5 }; // pixels per base window
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

    // Event-driven trigger support
    EventWaiter m_triggerEventWaiter;

public:
    // Constructor
    MousePointerCrosshairs()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::mousePointerCrosshairsLoggerName);
        m_state = std::make_shared<State>();
        init_settings();
        g_instance.store(this, std::memory_order_release);
    };

    virtual void enable()
    {
        Trace::EnableMousePointerCrosshairs(true);
        std::thread([=]() { InclusiveCrosshairsMain(GetModuleHandle(nullptr), m_inclusiveCrosshairsSettings); }).detach();
    }

    virtual std::atomic<int>* getCurrentGlideState()
    {
        return &m_glideState;
    }

    // Disable the powertoy
    virtual void disable()
    {
        Trace::EnableMousePointerCrosshairs(false);
        UninstallKeyboardHook();
        StopXTimer();
        StopYTimer();
        m_glideState = 0;
        InclusiveCrosshairsDisable();

        m_triggerEventWaiter.stop();
    }

    // Cancel gliding with option to activate crosshairs in user's preferred orientation
    void CancelGliding(bool activateCrosshairs)
    {
        int state = m_glideState.load();
        if (state == 0)
        {
            return; // nothing to cancel
        }
        
        // Stop all gliding operations
        StopXTimer();
        StopYTimer();
        m_glideState = 0;
        UninstallKeyboardHook();
        
        // Reset crosshairs control and restore user settings
        InclusiveCrosshairsSetExternalControl(false);
        InclusiveCrosshairsSetOrientation(m_inclusiveCrosshairsSettings.crosshairsOrientation);
        
        if (activateCrosshairs)
        {
            // User is switching to crosshairs mode - enable with their settings
            InclusiveCrosshairsEnsureOn();
        }
        else
        {
            // User canceled (Escape) - turn off crosshairs completely
            InclusiveCrosshairsEnsureOff();
        }
        
        // Reset gliding state
        if (auto s = m_state)
        {
            s->xFraction = 0.0;
            s->yFraction = 0.0;
        }
        
        Logger::debug("Gliding cursor cancelled (activateCrosshairs={})", activateCrosshairs ? 1 : 0);
    }

    void HandleGlidingHotkey()
    {
        auto s = m_state;
        if (!s)
        {
            return;
        }
        
        int state = m_glideState.load();
        switch (state)
        {
        case 0: // Starting gliding
        {
            // Install keyboard hook for Escape cancellation
            InstallKeyboardHook();
            
            // Force crosshairs visible in BOTH orientation for gliding, regardless of user setting
            // Set external control before enabling to prevent internal movement hook from attaching
            InclusiveCrosshairsSetExternalControl(true);
            InclusiveCrosshairsSetOrientation(CrosshairsOrientation::Both);
            InclusiveCrosshairsEnsureOn(); // Always ensure they are visible

            // Initialize gliding state
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
        case 1: // Slow horizontal
            s->currentXSpeed = s->slowHSpeed;
            m_glideState = 2;
            break;
        case 2: // Switch to vertical fast
        {
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
        case 3: // Slow vertical
            s->currentYSpeed = s->slowVSpeed;
            m_glideState = 4;
            break;
        case 4: // Finalize (click and end)
        default:
        {
            // Complete the gliding sequence
            StopYTimer();
            m_glideState = 0;
            LeftClick();
            
            // Restore normal crosshairs operation and turn them off
            InclusiveCrosshairsSetExternalControl(false);
            InclusiveCrosshairsSetOrientation(m_inclusiveCrosshairsSettings.crosshairsOrientation);
            InclusiveCrosshairsEnsureOff();
            
            UninstallKeyboardHook();
            
            // Reset state
            if (auto sp = m_state)
            {
                sp->xFraction = 0.0;
                sp->yFraction = 0.0;
            }
            break;
        }
        }
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

    // Low-level keyboard hook for Escape cancellation
    static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode == HC_ACTION)
        {
            const KBDLLHOOKSTRUCT* kb = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
            if (kb && kb->vkCode == VK_ESCAPE && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                if (auto inst = g_instance.load(std::memory_order_acquire))
                {
                    if (inst->m_glideState.load() != 0)
                    {
                        inst->CancelGliding(false); // Escape cancels without activating crosshairs
                    }
                }
            }
        }
        return CallNextHookEx(nullptr, nCode, wParam, lParam);
    }

    void InstallKeyboardHook()
    {
        if (m_keyboardHook)
        {
            return; // already installed
        }
        m_keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandle(nullptr), 0);
        if (!m_keyboardHook)
        {
            Logger::error("Failed to install low-level keyboard hook for MousePointerCrosshairs (Escape cancel). GetLastError={}.", GetLastError());
        }
    }

    void UninstallKeyboardHook()
    {
        if (m_keyboardHook)
        {
            UnhookWindowsHookEx(m_keyboardHook);
            m_keyboardHook = nullptr;
        }
    }

    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(L"MousePointerCrosshairs");
            parse_settings(settings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to load the Mouse Pointer Crosshairs settings json from file.");
        }
    }

public:

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        // Refactored JSON parsing: uses inline try-catch blocks for each property for clarity and error handling
        auto settingsObject = settings.get_raw_json();
        InclusiveCrosshairsSettings inclusiveCrosshairsSettings;
        
        if (settingsObject.GetView().Size())
        {
            try
            {
                auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);

                // Parse individual properties with error handling and defaults
                try
                {
                    if (propertiesObject.HasKey(L"crosshairs_opacity"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"crosshairs_opacity");
                        if (propertyObj.HasKey(L"value"))
                        {
                            inclusiveCrosshairsSettings.crosshairsOpacity = static_cast<int>(propertyObj.GetNamedNumber(L"value"));
                        }
                    }
                }
                catch (...) { /* Use default value */ }

                try
                {
                    if (propertiesObject.HasKey(L"crosshairs_radius"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"crosshairs_radius");
                        if (propertyObj.HasKey(L"value"))
                        {
                            inclusiveCrosshairsSettings.crosshairsRadius = static_cast<int>(propertyObj.GetNamedNumber(L"value"));
                        }
                    }
                }
                catch (...) { /* Use default value */ }

                try
                {
                    if (propertiesObject.HasKey(L"crosshairs_thickness"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"crosshairs_thickness");
                        if (propertyObj.HasKey(L"value"))
                        {
                            inclusiveCrosshairsSettings.crosshairsThickness = static_cast<int>(propertyObj.GetNamedNumber(L"value"));
                        }
                    }
                }
                catch (...) { /* Use default value */ }

                try
                {
                    if (propertiesObject.HasKey(L"crosshairs_border_size"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"crosshairs_border_size");
                        if (propertyObj.HasKey(L"value"))
                        {
                            inclusiveCrosshairsSettings.crosshairsBorderSize = static_cast<int>(propertyObj.GetNamedNumber(L"value"));
                        }
                    }
                }
                catch (...) { /* Use default value */ }

                try
                {
                    if (propertiesObject.HasKey(L"crosshairs_fixed_length"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"crosshairs_fixed_length");
                        if (propertyObj.HasKey(L"value"))
                        {
                            inclusiveCrosshairsSettings.crosshairsFixedLength = static_cast<int>(propertyObj.GetNamedNumber(L"value"));
                        }
                    }
                }
                catch (...) { /* Use default value */ }

                try
                {
                    if (propertiesObject.HasKey(L"crosshairs_auto_hide"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"crosshairs_auto_hide");
                        if (propertyObj.HasKey(L"value"))
                        {
                            inclusiveCrosshairsSettings.crosshairsAutoHide = propertyObj.GetNamedBoolean(L"value");
                        }
                    }
                }
                catch (...) { /* Use default value */ }

                try
                {
                    if (propertiesObject.HasKey(L"crosshairs_is_fixed_length_enabled"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"crosshairs_is_fixed_length_enabled");
                        if (propertyObj.HasKey(L"value"))
                        {
                            inclusiveCrosshairsSettings.crosshairsIsFixedLengthEnabled = propertyObj.GetNamedBoolean(L"value");
                        }
                    }
                }
                catch (...) { /* Use default value */ }

                try
                {
                    if (propertiesObject.HasKey(L"auto_activate"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"auto_activate");
                        if (propertyObj.HasKey(L"value"))
                        {
                            inclusiveCrosshairsSettings.autoActivate = propertyObj.GetNamedBoolean(L"value");
                        }
                    }
                }
                catch (...) { /* Use default value */ }

                // Parse orientation with validation - this fixes the original issue!
                try
                {
                    if (propertiesObject.HasKey(L"crosshairs_orientation"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"crosshairs_orientation");
                        if (propertyObj.HasKey(L"value"))
                        {
                            int orientationValue = static_cast<int>(propertyObj.GetNamedNumber(L"value"));
                            if (orientationValue >= 0 && orientationValue <= 2)
                            {
                                inclusiveCrosshairsSettings.crosshairsOrientation = static_cast<CrosshairsOrientation>(orientationValue);
                            }
                        }
                    }
                }
                catch (...) { /* Use default value (Both = 0) */ }

                // Parse colors with validation
                try
                {
                    if (propertiesObject.HasKey(L"crosshairs_color"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"crosshairs_color");
                        if (propertyObj.HasKey(L"value"))
                        {
                            std::wstring crosshairsColorValue = std::wstring(propertyObj.GetNamedString(L"value").c_str());
                            uint8_t r, g, b;
                            if (checkValidRGB(crosshairsColorValue, &r, &g, &b))
                            {
                                inclusiveCrosshairsSettings.crosshairsColor = winrt::Windows::UI::ColorHelper::FromArgb(255, r, g, b);
                            }
                        }
                    }
                }
                catch (...) { /* Use default color */ }

                try
                {
                    if (propertiesObject.HasKey(L"crosshairs_border_color"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"crosshairs_border_color");
                        if (propertyObj.HasKey(L"value"))
                        {
                            std::wstring borderColorValue = std::wstring(propertyObj.GetNamedString(L"value").c_str());
                            uint8_t r, g, b;
                            if (checkValidRGB(borderColorValue, &r, &g, &b))
                            {
                                inclusiveCrosshairsSettings.crosshairsBorderColor = winrt::Windows::UI::ColorHelper::FromArgb(255, r, g, b);
                            }
                        }
                    }
                }
                catch (...) { /* Use default border color */ }

                // Parse speed settings with validation
                try
                {
                    if (propertiesObject.HasKey(L"gliding_travel_speed"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"gliding_travel_speed");
                        if (propertyObj.HasKey(L"value") && m_state)
                        {
                            int travelSpeedValue = static_cast<int>(propertyObj.GetNamedNumber(L"value"));
                            if (travelSpeedValue >= 5 && travelSpeedValue <= 60)
                            {
                                m_state->fastHSpeed = travelSpeedValue;
                                m_state->fastVSpeed = travelSpeedValue;
                            }
                            else
                            {
                                // Clamp to valid range
                                int clampedValue = travelSpeedValue;
                                if (clampedValue < 5) clampedValue = 5;
                                if (clampedValue > 60) clampedValue = 60;
                                m_state->fastHSpeed = clampedValue;
                                m_state->fastVSpeed = clampedValue;
                                Logger::warn("Travel speed value out of range, clamped to valid range");
                            }
                        }
                    }
                }
                catch (...) 
                { 
                    if (m_state)
                    {
                        m_state->fastHSpeed = 25;
                        m_state->fastVSpeed = 25;
                    }
                }

                try
                {
                    if (propertiesObject.HasKey(L"gliding_delay_speed"))
                    {
                        auto propertyObj = propertiesObject.GetNamedObject(L"gliding_delay_speed");
                        if (propertyObj.HasKey(L"value") && m_state)
                        {
                            int delaySpeedValue = static_cast<int>(propertyObj.GetNamedNumber(L"value"));
                            if (delaySpeedValue >= 5 && delaySpeedValue <= 60)
                            {
                                m_state->slowHSpeed = delaySpeedValue;
                                m_state->slowVSpeed = delaySpeedValue;
                            }
                            else
                            {
                                // Clamp to valid range
                                int clampedValue = delaySpeedValue;
                                if (clampedValue < 5) clampedValue = 5;
                                if (clampedValue > 60) clampedValue = 60;
                                m_state->slowHSpeed = clampedValue;
                                m_state->slowVSpeed = clampedValue;
                                Logger::warn("Delay speed value out of range, clamped to valid range");
                            }
                        }
                    }
                }
                catch (...) 
                { 
                    if (m_state)
                    {
                        m_state->slowHSpeed = 5;
                        m_state->slowVSpeed = 5;
                    }
                }
            }
            catch (...)
            {
                Logger::warn("Error parsing some MousePointerCrosshairs properties. Using defaults for failed properties.");
            }
        }
        else
        {
            Logger::info("Mouse Pointer Crosshairs settings are empty");
        }
        
        m_inclusiveCrosshairsSettings = inclusiveCrosshairsSettings;
    }
};

MousePointerCrosshairs* g_mousePointerCrosshairsModule;

EXTERN_C __declspec(dllexport) void DisableMousePointerCrosshairs()
{
    if (g_mousePointerCrosshairsModule == nullptr)
    {
        g_mousePointerCrosshairsModule = new MousePointerCrosshairs();
    }

    g_mousePointerCrosshairsModule->disable();
}

EXTERN_C __declspec(dllexport) void EnableMousePointerCrosshairs()
{
    if (g_mousePointerCrosshairsModule == nullptr)
    {
        g_mousePointerCrosshairsModule = new MousePointerCrosshairs();
    }
    g_mousePointerCrosshairsModule->enable();
}

EXTERN_C __declspec(dllexport) void OnMousePointerCrosshairsActivationShortcut()
{
    if (g_mousePointerCrosshairsModule == nullptr)
    {
        g_mousePointerCrosshairsModule = new MousePointerCrosshairs();
    }

    if (g_mousePointerCrosshairsModule->getCurrentGlideState()->load() != 0)
    {
        g_mousePointerCrosshairsModule->CancelGliding(true /*activateCrosshairs*/);
    }

    // Otherwise, normal crosshairs toggle
    InclusiveCrosshairsSwitch();
}

EXTERN_C __declspec(dllexport) void OnMousePointerCrosshairsGlidingCursorShortcut()
{
    if (g_mousePointerCrosshairsModule == nullptr)
    {
        g_mousePointerCrosshairsModule = new MousePointerCrosshairs();
    }

    g_mousePointerCrosshairsModule->HandleGlidingHotkey();
}

EXTERN_C __declspec(dllexport) void OnMousePointerCrosshairsSettingsChanged()
{
    if (g_mousePointerCrosshairsModule == nullptr)
    {
        g_mousePointerCrosshairsModule = new MousePointerCrosshairs();
    }
    PowerToysSettings::PowerToyValues settings =
        PowerToysSettings::PowerToyValues::load_from_settings_file(L"MousePointerCrosshairs");
    g_mousePointerCrosshairsModule->parse_settings(settings);
}
