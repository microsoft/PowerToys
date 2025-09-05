#include "pch.h"
#include <common/SettingsAPI/settings_objects.h>
#include <interface/powertoy_module_interface.h>
#include "trace.h"
#include <atomic>
#include <thread>
#include <common/utils/logger_helper.h>
#include "DwellIndicator.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    // JSON configuration keys for settings persistence
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    const wchar_t JSON_KEY_DELAY_TIME_MS[] = L"delay_time_ms";
    const wchar_t JSON_KEY_SETTLE_TIME_SECONDS[] = L"settle_time_seconds";

    // Update interval for the visual indicator (in milliseconds)
    // 30ms gives ~33 FPS for smooth animation without excessive CPU usage
    constexpr DWORD kIndicatorUpdateIntervalMs = 30;
}

/**
 * @brief Send a left mouse click via Windows input system
 * 
 * Simulates a complete left click (down + up) at the current cursor position.
 * This is the core functionality that gets triggered after the dwell delay.
 */
static void SendLeftClick()
{
    INPUT inputs[2]{};
    
    // First input: Left mouse button down
    inputs[0].type = INPUT_MOUSE;
    inputs[0].mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
    
    // Second input: Left mouse button up
    inputs[1].type = INPUT_MOUSE;
    inputs[1].mi.dwFlags = MOUSEEVENTF_LEFTUP;
    
    // Send both inputs to simulate a complete click
    SendInput(2, inputs, sizeof(INPUT));
}

/**
 * @brief Main DwellCursor PowerToy module implementation
 * 
 * This class implements the dwell cursor functionality:
 * - Monitors mouse movement continuously
 * - Detects when mouse becomes stationary
 * - Shows visual countdown indicator
 * - Triggers left click after configured delay
 * - Provides hotkey toggle for enable/disable
 * 
 * State Management:
 * - m_enabled: Whether module is active (controlled by PowerToys settings)
 * - m_armed: Whether dwell clicking is currently armed (toggled by hotkey)
 * - firedForThisStationary: Prevents multiple clicks during one stationary period
 */
class DwellCursorModule : public PowertoyModuleIface
{
private:
    // Core module state - SINGLE DECLARATIONS ONLY
    bool m_enabled{ false };                    // Module enabled/disabled state
    Hotkey m_activationHotkey{};               // Hotkey for toggling armed state

    // Configuration settings - SINGLE DECLARATIONS ONLY
    std::atomic<int> m_delayMs{ 1000 };        // Dwell delay in milliseconds (500-10000ms)
    std::atomic<int> m_settleTimeSeconds{ 1 }; // Settle time in seconds (1-5s)

    // Runtime state management - SINGLE DECLARATIONS ONLY
    std::atomic<bool> m_armed{ true };         // Whether dwell clicking is armed
    std::atomic<bool> m_stop{ false };         // Signal to stop the worker thread
    std::thread m_worker;                       // Background thread for mouse monitoring

    // Visual feedback system
    std::unique_ptr<DwellIndicator> m_indicator;

    // Progress tracking - Use member variable instead of static
    float m_lastProgress{ -1.0f };             // Last progress value for change detection

    // Mouse movement sensitivity (pixels) - SINGLE DECLARATION ONLY
    // Movement within this threshold is considered "stationary"
    static constexpr int kMoveThresholdPx = 5;

public:
    /**
     * @brief Constructor - Initialize the DwellCursor module
     * 
     * Sets up logging, loads settings, configures default hotkey,
     * and creates the visual indicator instance.
     */
    DwellCursorModule()
    {
        // Initialize logging system for debugging and telemetry
        LoggerHelpers::init_logger(L"DwellCursor", L"ModuleInterface", "dwell-cursor");
        Logger::trace(L"DwellCursor: Constructor called");
        
        // Load saved settings from PowerToys configuration
        init_settings();
        
        // Set default hotkey if not configured: Win+Alt+D
        if (m_activationHotkey.key == 0)
        {
            m_activationHotkey.win = true;   // Windows key required
            m_activationHotkey.alt = true;   // Alt key required
            m_activationHotkey.key = 'D';    // D key
        }

        // Create visual indicator instance (but don't initialize yet)
        m_indicator = std::make_unique<DwellIndicator>();
        Logger::trace(L"DwellCursor: Constructor completed");
    }

    /**
     * @brief Destructor cleanup
     */
    virtual void destroy() override
    {
        disable();  // Stop all activity
        delete this;
    }

    // PowerToy identification methods
    virtual const wchar_t* get_name() override { return L"DwellCursor"; }
    virtual const wchar_t* get_key() override { return L"DwellCursor"; }

    /**
     * @brief Get module configuration for PowerToys settings UI
     */
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    /**
     * @brief Apply new configuration from PowerToys settings UI
     */
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            parse_settings(values);
        }
        catch (...)
        {
            // Ignore configuration errors to prevent crashes
        }
    }

    virtual void call_custom_action(const wchar_t* /*action*/) override {}

    /**
     * @brief Enable the DwellCursor module
     * 
     * This is called when:
     * 1. PowerToys starts up (if module is enabled in settings)
     * 2. User enables the module via PowerToys settings UI
     * 
     * Actions performed:
     * 1. Initialize visual indicator system
     * 2. Start background mouse monitoring thread
     * 3. Begin dwell detection
     */
    virtual void enable() override
    {
        if (m_enabled) 
        {
            Logger::trace(L"DwellCursor: Already enabled");
            return;
        }
        
        Logger::trace(L"DwellCursor: Enabling module");
        m_enabled = true;
        m_stop = false;

        // Initialize the visual indicator system (GDI+, window creation)
        if (m_indicator)
        {
            if (!m_indicator->Initialize())
            {
                Logger::trace(L"DwellCursor: Failed to initialize visual indicator");
                // Continue without visual indicator - core functionality still works
            }
            else
            {
                Logger::trace(L"DwellCursor: Visual indicator initialized successfully");
            }
        }
        else
        {
            Logger::trace(L"DwellCursor: No indicator instance available");
        }

        // Start the mouse monitoring thread
        m_worker = std::thread([this]() { this->RunLoop(); });
        Logger::trace(L"DwellCursor: Module enabled and worker thread started");
    }

    /**
     * @brief Disable the DwellCursor module
     * 
     * This is called when:
     * 1. PowerToys shuts down
     * 2. User disables the module via PowerToys settings UI
     * 
     * Actions performed:
     * 1. Stop mouse monitoring thread
     * 2. Hide any visible indicator
     * 3. Clean up visual indicator resources
     */
    virtual void disable() override
    {
        if (!m_enabled) 
        {
            Logger::trace(L"DwellCursor: Already disabled");
            return;
        }
        
        Logger::trace(L"DwellCursor: Disabling module");
        m_enabled = false;
        m_stop = true;
        
        // Wait for worker thread to finish
        if (m_worker.joinable()) m_worker.join();

        // Clean up visual indicator resources
        if (m_indicator)
        {
            m_indicator->Cleanup();
        }
        
        Logger::trace(L"DwellCursor: Module disabled");
    }

    virtual bool is_enabled() override { return m_enabled; }
    virtual bool is_enabled_by_default() const override { return false; }  // User must explicitly enable

    /**
     * @brief Report hotkeys to PowerToys for registration
     */
    virtual size_t get_hotkeys(Hotkey* buffer, size_t buffer_size) override
    {
        if (buffer && buffer_size >= 1)
        {
            buffer[0] = m_activationHotkey;
        }
        return 1;  // We have exactly one hotkey
    }

    /**
     * @brief Handle hotkey press events
     * 
     * The hotkey toggles the "armed" state:
     * - Armed: Dwell clicking is active, countdown indicator shows
     * - Disarmed: No dwell clicking, indicator hidden
     * 
     * This allows users to temporarily disable dwell clicking without
     * going into settings (e.g., when typing or doing precise work).
     * 
     * @param hotkeyId Index of the pressed hotkey (we only have one)
     * @return true if handled, false otherwise
     */
    virtual bool on_hotkey(size_t hotkeyId) override
    {
        // Handle our single registered hotkey
        if (hotkeyId == 0)
        {
            // Toggle armed state (enabled/disabled functionality)
            m_armed = !m_armed.load();

            // Hide indicator immediately when disarming or when module disabled
            if ((!m_armed || !m_enabled) && m_indicator)
            {
                m_indicator->Hide();
            }

            Logger::trace(L"DwellCursor: Hotkey pressed, armed={}, enabled={}", m_armed.load(), m_enabled);
            return true;
        }
        return false;
    }

private:
    /**
     * @brief Load settings from PowerToys configuration files
     */
    void init_settings()
    {
        try
        {
            PowerToysSettings::PowerToyValues settings = PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());
            parse_settings(settings);
        }
        catch (...)
        {
            // Use default settings if loading fails
        }
    }

    /**
     * @brief Parse and apply settings from JSON configuration
     * 
     * Extracts:
     * - Activation hotkey configuration
     * - Dwell delay time (with validation)
     */
    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        auto obj = settings.get_raw_json();
        if (!obj.GetView().Size()) return;
        
        // Parse hotkey configuration
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
            // Keep default hotkey if parsing fails
        }
        
        // Parse dwell delay setting
        try
        {
            auto jsonDelay = obj.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_DELAY_TIME_MS);
            int v = static_cast<int>(jsonDelay.GetNamedNumber(JSON_KEY_VALUE));
            
            // Validate delay range: 0.5 seconds to 10 seconds
            if (v < 500) v = 500;       // Minimum 0.5 seconds
            if (v > 10000) v = 10000;   // Maximum 10 seconds
            
            m_delayMs = v;
        }
        catch (...)
        {
            // Keep default delay if parsing fails
        }

        // Parse settle time setting
        try
        {
            auto jsonSettleTime = obj.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_SETTLE_TIME_SECONDS);
            int v = static_cast<int>(jsonSettleTime.GetNamedNumber(JSON_KEY_VALUE));
            
            // Validate settle time range: 1 to 5 seconds
            if (v < 1) v = 1;           // Minimum 1 second
            if (v > 5) v = 5;           // Maximum 5 seconds
            
            m_settleTimeSeconds = v;
        }
        catch (...)
        {
            // Keep default settle time if parsing fails
        }
    }

    /**
     * @brief Check if two points are within movement threshold
     * 
     * @param a First coordinate
     * @param b Second coordinate  
     * @param thr Threshold in pixels
     * @return true if coordinates are within threshold (considered "near")
     */
    static bool Near(int a, int b, int thr) { return (abs(a - b) <= thr); }

    /**
     * @brief Main mouse monitoring loop (runs in background thread)
     * 
     * This is the core logic that runs continuously while the module is enabled:
     * 
     * State Machine:
     * 1. Monitor mouse position every 50ms (20 Hz)
     * 2. If mouse moves > threshold: Reset timer, hide indicator
     * 3. If mouse stationary for SETTLE_TIME: Show indicator
     * 4. If mouse stationary for SETTLE_TIME + dwell delay: Send click
     * 
     * CRITICAL: This method handles ALL progress reset logic
     */
    void RunLoop()
    {
        constexpr DWORD ACTIVE_POLL_INTERVAL = 50;   // 50ms = 20 Hz monitoring when active
        constexpr DWORD INACTIVE_POLL_INTERVAL = 200; // 200ms when disabled
        
        // Initialize tracking variables
        POINT last{};                           // Last recorded mouse position
        GetCursorPos(&last);                   // Get initial position
        DWORD lastMove = GetTickCount();       // Time of last movement
        bool firedForThisStationary = false;   // Prevents multiple clicks during one stationary period
        bool indicatorShown = false;           // Current indicator visibility state
        DWORD lastIndicatorUpdate = 0;         // Last time we updated indicator progress

        Logger::trace(L"DwellCursor: RunLoop started with 50ms polling and {}s configurable settle time, enabled={}, armed={}", 
            m_settleTimeSeconds.load(), m_enabled, m_armed.load());

        // Main monitoring loop - continues until module shutdown
        while (!m_stop)
        {
            // Performance optimization: When module disabled, sleep longer and skip processing
            if (!m_enabled)
            {
                // Hide any visible indicator when disabled
                if (indicatorShown && m_indicator)
                {
                    m_indicator->Hide();
                    indicatorShown = false;
                    m_lastProgress = -1.0f;  // Reset progress tracking
                }
                Sleep(INACTIVE_POLL_INTERVAL);  // Sleep 200ms when disabled
                continue;
            }

            // Get current mouse position and time
            POINT p{};
            GetCursorPos(&p);
            DWORD currentTime = GetTickCount();

            // Check if mouse has moved beyond our threshold
            if (!Near(p.x, last.x, kMoveThresholdPx) || !Near(p.y, last.y, kMoveThresholdPx))
            {
                // MOUSE MOVEMENT DETECTED - RESET ALL STATE
                
                Logger::trace(L"DwellCursor: Mouse movement detected, resetting state");
                
                // Update tracking variables
                last = p;                           // Record new position
                lastMove = currentTime;             // Record movement time
                firedForThisStationary = false;     // Re-arm for next stationary period

                // CRITICAL: Hide indicator and reset progress immediately on movement
                if (indicatorShown && m_indicator)
                {
                    m_indicator->Hide();
                    indicatorShown = false;
                }
                // Reset progress tracking for next stationary period
                m_lastProgress = -1.0f;
            }
            else
            {
                // MOUSE IS STATIONARY - Process dwell logic
                
                // Check if we should process dwell logic
                if (m_enabled && m_armed && !firedForThisStationary)
                {
                    // Calculate how long mouse has been stationary
                    DWORD elapsed = currentTime - lastMove;
                    DWORD delayMs = static_cast<DWORD>(m_delayMs.load());
                    DWORD settleTimeMs = static_cast<DWORD>(m_settleTimeSeconds.load() * 1000); // Convert seconds to milliseconds
                    DWORD totalTimeRequired = settleTimeMs + delayMs;  // Settle time + dwell delay

                    if (elapsed >= totalTimeRequired)
                    {
                        // SETTLE TIME + DWELL DELAY COMPLETED - TRIGGER CLICK
                        
                        Logger::trace(L"DwellCursor: Triggering click after {}ms total ({}ms settle + {}ms dwell)", elapsed, settleTimeMs, delayMs);
                        
                        // Hide indicator before clicking
                        if (indicatorShown && m_indicator)
                        {
                            m_indicator->Hide();
                            indicatorShown = false;
                        }
                        
                        // Reset progress tracking
                        m_lastProgress = -1.0f;
                        
                        SendLeftClick();                    // Send the mouse click
                        firedForThisStationary = true;      // Prevent additional clicks
                    }
                    else if (elapsed >= settleTimeMs)
                    {
                        // SETTLE TIME COMPLETED - START/UPDATE COUNTDOWN INDICATOR
                        
                        DWORD dwellElapsed = elapsed - settleTimeMs;  // Time since settle completed
                        
                        // Show indicator if not already visible
                        if (!indicatorShown && m_indicator)
                        {
                            Logger::trace(L"DwellCursor: Settle time ({}ms) completed, showing NEW indicator at ({}, {}) - dwellElapsed={}ms, delayMs={}", 
                                settleTimeMs, p.x, p.y, dwellElapsed, delayMs);
                            
                            // CRITICAL: Force complete reset before showing
                            m_lastProgress = -1.0f;
                            
                            m_indicator->Show(p.x, p.y);  // This internally resets indicator progress to 0.0
                            indicatorShown = true;
                            lastIndicatorUpdate = currentTime; // Reset update timer when showing
                        }

                        // Update indicator progress ONLY at throttled intervals
                        if (indicatorShown && (currentTime - lastIndicatorUpdate >= ACTIVE_POLL_INTERVAL))
                        {
                            // Calculate progress as percentage: 0.0 = just started, 1.0 = almost complete
                            float newProgress = static_cast<float>(dwellElapsed) / static_cast<float>(delayMs);
                            if (newProgress > 1.0f) newProgress = 1.0f;  // Clamp to prevent over-draw
                            
                            // Only update if progress changed significantly (at least 3% or 0.03) OR forced reset
                            if (abs(newProgress - m_lastProgress) >= 0.03f || m_lastProgress < 0.0f)
                            {
                                Logger::trace(L"DwellCursor: Updating progress from {:.2f} to {:.2f} (dwellElapsed={}ms)", 
                                    m_lastProgress, newProgress, dwellElapsed);

                                if (m_indicator)
                                {
                                    m_indicator->UpdateProgress(newProgress);
                                }
                                m_lastProgress = newProgress;
                                lastIndicatorUpdate = currentTime;
                            }
                        }
                    }
                    // else: Still in settle time, do nothing (no indicator shown)
                }
                else if (indicatorShown && m_indicator)
                {
                    // STATIONARY BUT CONDITIONS NOT MET - HIDE INDICATOR
                    // This happens when: not enabled, not armed, or already fired
                    Logger::trace(L"DwellCursor: Hiding indicator - conditions not met (enabled={}, armed={}, fired={})", 
                        m_enabled, m_armed.load(), firedForThisStationary);
                    
                    m_indicator->Hide();
                    indicatorShown = false;
                    // Reset progress tracking
                    m_lastProgress = -1.0f;
                }
            }
            
            // Sleep appropriate interval based on activity (20 Hz when active)
            Sleep(ACTIVE_POLL_INTERVAL);
        }

        // THREAD SHUTDOWN CLEANUP
        Logger::trace(L"DwellCursor: RunLoop shutdown - cleaning up");
        
        // Hide indicator when stopping
        if (indicatorShown && m_indicator)
        {
            m_indicator->Hide();
        }
        
        // Final progress reset
        m_lastProgress = -1.0f;
        
        Logger::trace(L"DwellCursor: RunLoop ended");
    }
};

// ============================================================================
// DLL Entry Points
// ============================================================================

/**
 * @brief DLL entry point for Windows module loading
 */
BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        Trace::RegisterProvider();    // Initialize ETW tracing
        break;
    case DLL_PROCESS_DETACH:
        Trace::UnregisterProvider();  // Cleanup ETW tracing
        break;
    }
    return TRUE;
}

/**
 * @brief PowerToys module factory function
 * 
 * This is the entry point called by PowerToys runner to create an instance
 * of our module. The runner will call this once during startup.
 * 
 * @return New instance of DwellCursorModule
 */
extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new DwellCursorModule();
}
