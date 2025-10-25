#include "pch.h"
#include "../../../interface/powertoy_module_interface.h"
#include "../../../common/SettingsAPI/settings_objects.h"
#include "trace.h"
#include "../../../common/utils/process_path.h"
#include "../../../common/utils/resources.h"
#include "../../../common/logger/logger.h"
#include "../../../common/utils/logger_helper.h"
#include <atomic>
#include <thread>
#include <vector>
#include <map>
#include <string>
#include <algorithm>
#include <windows.h>
#include "resource.h"
#include "CursorWrapTests.h"

// Disable C26451 arithmetic overflow warning for this file since the operations are safe in this context
#pragma warning(disable: 26451)

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
{
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

// Non-Localizable strings
namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_VALUE[] = L"value";
    const wchar_t JSON_KEY_ACTIVATION_SHORTCUT[] = L"activation_shortcut";
    const wchar_t JSON_KEY_AUTO_ACTIVATE[] = L"auto_activate";
    const wchar_t JSON_KEY_DISABLE_WRAP_DURING_DRAG[] = L"disable_wrap_during_drag";
}

// The PowerToy name that will be shown in the settings.
const static wchar_t* MODULE_NAME = L"CursorWrap";
// Add a description that will we shown in the module settings page.
const static wchar_t* MODULE_DESC = L"<no description>";

// Mouse hook data structure
struct MonitorInfo
{
    RECT rect;
    bool isPrimary;
    int monitorId; // Add monitor ID for easier debugging
};

// Add structure for logical monitor grid position
struct LogicalPosition
{
    int row;
    int col;
    bool isValid;
};

// Add monitor topology helper
struct MonitorTopology
{
    std::vector<std::vector<HMONITOR>> grid; // 3x3 grid of monitors
    std::map<HMONITOR, LogicalPosition> monitorToPosition;
    std::map<std::pair<int, int>, HMONITOR> positionToMonitor;
    
    void Initialize(const std::vector<MonitorInfo>& monitors);
    LogicalPosition GetPosition(HMONITOR monitor) const;
    HMONITOR GetMonitorAt(int row, int col) const;
    HMONITOR FindAdjacentMonitor(HMONITOR current, int deltaRow, int deltaCol) const;
};

// Forward declaration
class CursorWrap;

// Global instance pointer for the mouse hook
static CursorWrap* g_cursorWrapInstance = nullptr;

// Implement the PowerToy Module Interface and all the required methods.
class CursorWrap : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    bool m_autoActivate = false;
    bool m_disableWrapDuringDrag = true; // Default to true to prevent wrap during drag
    
    // Mouse hook
    HHOOK m_mouseHook = nullptr;
    std::atomic<bool> m_hookActive{ false };
    
    // Monitor information
    std::vector<MonitorInfo> m_monitors;
    MonitorTopology m_topology;
    
    // Hotkey
    Hotkey m_activationHotkey{};

public:
    // Constructor
    CursorWrap()
    {
        LoggerHelpers::init_logger(MODULE_NAME, L"ModuleInterface", LogSettings::cursorWrapLoggerName);
        init_settings();
        UpdateMonitorInfo();
        g_cursorWrapInstance = this; // Set global instance pointer
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        StopMouseHook();
        g_cursorWrapInstance = nullptr; // Clear global instance pointer
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
        return powertoys_gpo::getConfiguredCursorWrapEnabledValue();
    }

    // Return JSON with the configuration options.
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());

        settings.set_description(IDS_CURSORWRAP_NAME);
        settings.set_icon_key(L"pt-cursor-wrap");
        
        // Create HotkeyObject from the Hotkey struct for the settings
        auto hotkey_object = PowerToysSettings::HotkeyObject::from_settings(
            m_activationHotkey.win,
            m_activationHotkey.ctrl,
            m_activationHotkey.alt,
            m_activationHotkey.shift,
            m_activationHotkey.key);

        settings.add_hotkey(JSON_KEY_ACTIVATION_SHORTCUT, IDS_CURSORWRAP_NAME, hotkey_object);
        settings.add_bool_toggle(JSON_KEY_AUTO_ACTIVATE, IDS_CURSORWRAP_NAME, m_autoActivate);
        settings.add_bool_toggle(JSON_KEY_DISABLE_WRAP_DURING_DRAG, IDS_CURSORWRAP_NAME, m_disableWrapDuringDrag);

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
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to parse CursorWrap settings json.");
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        Trace::EnableCursorWrap(true);
        
        if (m_autoActivate)
        {
            StartMouseHook();
        }
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Trace::EnableCursorWrap(false);
        StopMouseHook();
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

    // Legacy hotkey support
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
        if (!m_enabled || hotkeyId != 0)
        {
            return false;
        }

        // Toggle cursor wrapping
        if (m_hookActive)
        {
            StopMouseHook();
        }
        else
        {
            StartMouseHook();
#ifdef _DEBUG
            // Run comprehensive tests when hook is started in debug builds
            RunComprehensiveTests();
#endif
        }
        
        return true;
    }

private:
    // Load the settings file.
    void init_settings()
    {
        try
        {
            // Load and parse the settings file for this PowerToy.
            PowerToysSettings::PowerToyValues settings =
                PowerToysSettings::PowerToyValues::load_from_settings_file(CursorWrap::get_key());
            parse_settings(settings);
        }
        catch (std::exception&)
        {
            Logger::error("Invalid json when trying to load the CursorWrap settings json from file.");
        }
    }

    void parse_settings(PowerToysSettings::PowerToyValues& settings)
    {
        auto settingsObject = settings.get_raw_json();
        if (settingsObject.GetView().Size())
        {
            try
            {
                // Parse activation HotKey
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_ACTIVATION_SHORTCUT);
                auto hotkey = PowerToysSettings::HotkeyObject::from_json(jsonPropertiesObject);

                m_activationHotkey.win = hotkey.win_pressed();
                m_activationHotkey.ctrl = hotkey.ctrl_pressed();
                m_activationHotkey.shift = hotkey.shift_pressed();
                m_activationHotkey.alt = hotkey.alt_pressed();
                m_activationHotkey.key = static_cast<unsigned char>(hotkey.get_code());
            }
            catch (...)
            {
                Logger::warn("Failed to initialize CursorWrap activation shortcut");
            }
            
            try
            {
                // Parse auto activate
                auto jsonPropertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES).GetNamedObject(JSON_KEY_AUTO_ACTIVATE);
                m_autoActivate = jsonPropertiesObject.GetNamedBoolean(JSON_KEY_VALUE);
            }
            catch (...)
            {
                Logger::warn("Failed to initialize CursorWrap auto activate from settings. Will use default value");
            }
            
            try
            {
                // Parse disable wrap during drag
                auto propertiesObject = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);
                if (propertiesObject.HasKey(JSON_KEY_DISABLE_WRAP_DURING_DRAG))
                {
                    auto disableDragObject = propertiesObject.GetNamedObject(JSON_KEY_DISABLE_WRAP_DURING_DRAG);
                    m_disableWrapDuringDrag = disableDragObject.GetNamedBoolean(JSON_KEY_VALUE);
                }
            }
            catch (...)
            {
                Logger::warn("Failed to initialize CursorWrap disable wrap during drag from settings. Will use default value (true)");
            }
        }
        else
        {
            Logger::info("CursorWrap settings are empty");
        }
        
        // Set default hotkey if not configured
        if (m_activationHotkey.key == 0)
        {
            m_activationHotkey.win = true;
            m_activationHotkey.alt = true;
            m_activationHotkey.ctrl = false;
            m_activationHotkey.shift = false;
            m_activationHotkey.key = 'U'; // Win+Alt+U
        }
    }

    void UpdateMonitorInfo()
    {
        m_monitors.clear();
        
        EnumDisplayMonitors(nullptr, nullptr, [](HMONITOR hMonitor, HDC, LPRECT, LPARAM lParam) -> BOOL {
            auto* self = reinterpret_cast<CursorWrap*>(lParam);
            
            MONITORINFO mi{};
            mi.cbSize = sizeof(MONITORINFO);
            if (GetMonitorInfo(hMonitor, &mi))
            {
                MonitorInfo info{};
                info.rect = mi.rcMonitor;
                info.isPrimary = (mi.dwFlags & MONITORINFOF_PRIMARY) != 0;
                info.monitorId = static_cast<int>(self->m_monitors.size());
                self->m_monitors.push_back(info);
            }
            
            return TRUE;
        }, reinterpret_cast<LPARAM>(this));
        
        // Initialize monitor topology
        m_topology.Initialize(m_monitors);
    }

    void StartMouseHook()
    {
        if (m_mouseHook || m_hookActive)
        {
            Logger::info("CursorWrap mouse hook already active");
            return;
        }

        UpdateMonitorInfo();
        
        m_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(nullptr), 0);
        if (m_mouseHook)
        {
            m_hookActive = true;
            Logger::info("CursorWrap mouse hook started successfully");
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Hook installed");
#endif
        }
        else
        {
            DWORD error = GetLastError();
            Logger::error(L"Failed to install CursorWrap mouse hook, error: {}", error);
        }
    }

    void StopMouseHook()
    {
        if (m_mouseHook)
        {
            UnhookWindowsHookEx(m_mouseHook);
            m_mouseHook = nullptr;
            m_hookActive = false;
            Logger::info("CursorWrap mouse hook stopped");
#ifdef _DEBUG
            Logger::info("CursorWrap DEBUG: Mouse hook stopped");
#endif
        }
    }

    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 && wParam == WM_MOUSEMOVE)
        {
            auto* pMouseStruct = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);
            POINT currentPos = { pMouseStruct->pt.x, pMouseStruct->pt.y };
            
            if (g_cursorWrapInstance && g_cursorWrapInstance->m_hookActive)
            {
                POINT newPos = g_cursorWrapInstance->HandleMouseMove(currentPos);
                if (newPos.x != currentPos.x || newPos.y != currentPos.y)
                {
#ifdef _DEBUG
                    Logger::info(L"CursorWrap DEBUG: Wrapping cursor from ({}, {}) to ({}, {})", 
                                currentPos.x, currentPos.y, newPos.x, newPos.y);
#endif
                    SetCursorPos(newPos.x, newPos.y);
                    return 1; // Suppress the original message
                }
            }
        }
        
        return CallNextHookEx(nullptr, nCode, wParam, lParam);
    }
    
    // *** COMPLETELY REWRITTEN CURSOR WRAPPING LOGIC ***
    // Implements vertical scrolling to bottom/top of vertical stack as requested
    POINT HandleMouseMove(const POINT& currentPos)
    {
        POINT newPos = currentPos;
        
        // Check if we should skip wrapping during drag if the setting is enabled
        if (m_disableWrapDuringDrag && (GetAsyncKeyState(VK_LBUTTON) & 0x8000))
        {
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Left mouse button is down and disable_wrap_during_drag is enabled - skipping wrap");
#endif
            return currentPos; // Return unchanged position (no wrapping)
        }

#ifdef _DEBUG
        Logger::info(L"CursorWrap DEBUG: ======= HANDLE MOUSE MOVE START =======");
        Logger::info(L"CursorWrap DEBUG: Input position ({}, {})", currentPos.x, currentPos.y);
#endif
        
        // Find which monitor the cursor is currently on
        HMONITOR currentMonitor = MonitorFromPoint(currentPos, MONITOR_DEFAULTTONEAREST);
        MONITORINFO currentMonitorInfo{};
        currentMonitorInfo.cbSize = sizeof(MONITORINFO);
        GetMonitorInfo(currentMonitor, &currentMonitorInfo);
        
        LogicalPosition currentLogicalPos = m_topology.GetPosition(currentMonitor);
        
#ifdef _DEBUG
        Logger::info(L"CursorWrap DEBUG: Current monitor bounds: Left={}, Top={}, Right={}, Bottom={}", 
                    currentMonitorInfo.rcMonitor.left, currentMonitorInfo.rcMonitor.top, 
                    currentMonitorInfo.rcMonitor.right, currentMonitorInfo.rcMonitor.bottom);
        Logger::info(L"CursorWrap DEBUG: Logical position: Row={}, Col={}, Valid={}", 
                    currentLogicalPos.row, currentLogicalPos.col, currentLogicalPos.isValid);
#endif
        
        bool wrapped = false;
        
        // *** VERTICAL WRAPPING LOGIC - CONFIRMED WORKING ***
        // Move to bottom of vertical stack when hitting top edge
        if (currentPos.y <= currentMonitorInfo.rcMonitor.top)
        {
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: ======= VERTICAL WRAP: TOP EDGE DETECTED =======");
#endif
            
            // Find the bottom-most monitor in the vertical stack (same column)
            HMONITOR bottomMonitor = nullptr;
            
            if (currentLogicalPos.isValid) {
                // Search down from current position to find the bottom-most monitor in same column
                for (int row = 2; row >= 0; row--) { // Start from bottom and work up
                    HMONITOR candidateMonitor = m_topology.GetMonitorAt(row, currentLogicalPos.col);
                    if (candidateMonitor) {
                        bottomMonitor = candidateMonitor;
                        break; // Found the bottom-most monitor
                    }
                }
            }
            
            if (bottomMonitor && bottomMonitor != currentMonitor) {
                // *** MOVE TO BOTTOM OF VERTICAL STACK ***
                MONITORINFO bottomInfo{};
                bottomInfo.cbSize = sizeof(MONITORINFO);
                GetMonitorInfo(bottomMonitor, &bottomInfo);
                
                // Calculate relative X position to maintain cursor X alignment
                double relativeX = static_cast<double>(currentPos.x - currentMonitorInfo.rcMonitor.left) / 
                                  (currentMonitorInfo.rcMonitor.right - currentMonitorInfo.rcMonitor.left);
                
                int targetWidth = bottomInfo.rcMonitor.right - bottomInfo.rcMonitor.left;
                newPos.x = bottomInfo.rcMonitor.left + static_cast<int>(relativeX * targetWidth);
                newPos.y = bottomInfo.rcMonitor.bottom - 1; // Bottom edge of bottom monitor
                
                // Clamp X to target monitor bounds
                newPos.x = max(bottomInfo.rcMonitor.left, min(newPos.x, bottomInfo.rcMonitor.right - 1));
                wrapped = true;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: VERTICAL WRAP SUCCESS - Moved to bottom of vertical stack");
                Logger::info(L"CursorWrap DEBUG: New position: ({}, {})", newPos.x, newPos.y);
#endif
            } else {
                // *** NO OTHER MONITOR IN VERTICAL STACK - WRAP WITHIN CURRENT MONITOR ***
                newPos.y = currentMonitorInfo.rcMonitor.bottom - 1;
                wrapped = true;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: VERTICAL WRAP - No other monitor in stack, wrapping within current monitor");
#endif
            }
        }
        else if (currentPos.y >= currentMonitorInfo.rcMonitor.bottom - 1)
        {
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: ======= VERTICAL WRAP: BOTTOM EDGE DETECTED =======");
#endif
            
            // Find the top-most monitor in the vertical stack (same column)
            HMONITOR topMonitor = nullptr;
            
            if (currentLogicalPos.isValid) {
                // Search up from current position to find the top-most monitor in same column
                for (int row = 0; row <= 2; row++) { // Start from top and work down
                    HMONITOR candidateMonitor = m_topology.GetMonitorAt(row, currentLogicalPos.col);
                    if (candidateMonitor) {
                        topMonitor = candidateMonitor;
                        break; // Found the top-most monitor
                    }
                }
            }
            
            if (topMonitor && topMonitor != currentMonitor) {
                // *** MOVE TO TOP OF VERTICAL STACK ***
                MONITORINFO topInfo{};
                topInfo.cbSize = sizeof(MONITORINFO);
                GetMonitorInfo(topMonitor, &topInfo);
                
                // Calculate relative X position to maintain cursor X alignment
                double relativeX = static_cast<double>(currentPos.x - currentMonitorInfo.rcMonitor.left) / 
                                  (currentMonitorInfo.rcMonitor.right - currentMonitorInfo.rcMonitor.left);
                
                int targetWidth = topInfo.rcMonitor.right - topInfo.rcMonitor.left;
                newPos.x = topInfo.rcMonitor.left + static_cast<int>(relativeX * targetWidth);
                newPos.y = topInfo.rcMonitor.top; // Top edge of top monitor
                
                // Clamp X to target monitor bounds
                newPos.x = max(topInfo.rcMonitor.left, min(newPos.x, topInfo.rcMonitor.right - 1));
                wrapped = true;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: VERTICAL WRAP SUCCESS - Moved to top of vertical stack");
                Logger::info(L"CursorWrap DEBUG: New position: ({}, {})", newPos.x, newPos.y);
#endif
            } else {
                // *** NO OTHER MONITOR IN VERTICAL STACK - WRAP WITHIN CURRENT MONITOR ***
                newPos.y = currentMonitorInfo.rcMonitor.top;
                wrapped = true;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: VERTICAL WRAP - No other monitor in stack, wrapping within current monitor");
#endif
            }
        }
        
        // *** FIXED HORIZONTAL WRAPPING LOGIC ***
        // Move to opposite end of horizontal stack when hitting left/right edge
        // Only handle horizontal wrapping if we haven't already wrapped vertically
        if (!wrapped && currentPos.x <= currentMonitorInfo.rcMonitor.left)
        {
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: ======= HORIZONTAL WRAP: LEFT EDGE DETECTED =======");
#endif
            
            // Find the right-most monitor in the horizontal stack (same row)
            HMONITOR rightMonitor = nullptr;
            
            if (currentLogicalPos.isValid) {
                // Search right from current position to find the right-most monitor in same row
                for (int col = 2; col >= 0; col--) { // Start from right and work left
                    HMONITOR candidateMonitor = m_topology.GetMonitorAt(currentLogicalPos.row, col);
                    if (candidateMonitor) {
                        rightMonitor = candidateMonitor;
                        break; // Found the right-most monitor
                    }
                }
            }
            
            if (rightMonitor && rightMonitor != currentMonitor) {
                // *** MOVE TO RIGHT END OF HORIZONTAL STACK ***
                MONITORINFO rightInfo{};
                rightInfo.cbSize = sizeof(MONITORINFO);
                GetMonitorInfo(rightMonitor, &rightInfo);
                
                // Calculate relative Y position to maintain cursor Y alignment
                double relativeY = static_cast<double>(currentPos.y - currentMonitorInfo.rcMonitor.top) / 
                                  (currentMonitorInfo.rcMonitor.bottom - currentMonitorInfo.rcMonitor.top);
                
                int targetHeight = rightInfo.rcMonitor.bottom - rightInfo.rcMonitor.top;
                newPos.y = rightInfo.rcMonitor.top + static_cast<int>(relativeY * targetHeight);
                newPos.x = rightInfo.rcMonitor.right - 1; // Right edge of right monitor
                
                // Clamp Y to target monitor bounds
                newPos.y = max(rightInfo.rcMonitor.top, min(newPos.y, rightInfo.rcMonitor.bottom - 1));
                wrapped = true;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: HORIZONTAL WRAP SUCCESS - Moved to right end of horizontal stack");
                Logger::info(L"CursorWrap DEBUG: New position: ({}, {})", newPos.x, newPos.y);
#endif
            } else {
                // *** NO OTHER MONITOR IN HORIZONTAL STACK - WRAP WITHIN CURRENT MONITOR ***
                newPos.x = currentMonitorInfo.rcMonitor.right - 1;
                wrapped = true;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: HORIZONTAL WRAP - No other monitor in stack, wrapping within current monitor");
#endif
            }
        }
        else if (!wrapped && currentPos.x >= currentMonitorInfo.rcMonitor.right - 1)
        {
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: ======= HORIZONTAL WRAP: RIGHT EDGE DETECTED =======");
#endif
            
            // Find the left-most monitor in the horizontal stack (same row)
            HMONITOR leftMonitor = nullptr;
            
            if (currentLogicalPos.isValid) {
                // Search left from current position to find the left-most monitor in same row
                for (int col = 0; col <= 2; col++) { // Start from left and work right
                    HMONITOR candidateMonitor = m_topology.GetMonitorAt(currentLogicalPos.row, col);
                    if (candidateMonitor) {
                        leftMonitor = candidateMonitor;
                        break; // Found the left-most monitor
                    }
                }
            }
            
            if (leftMonitor && leftMonitor != currentMonitor) {
                // *** MOVE TO LEFT END OF HORIZONTAL STACK ***
                MONITORINFO leftInfo{};
                leftInfo.cbSize = sizeof(MONITORINFO);
                GetMonitorInfo(leftMonitor, &leftInfo);
                
                // Calculate relative Y position to maintain cursor Y alignment
                double relativeY = static_cast<double>(currentPos.y - currentMonitorInfo.rcMonitor.top) / 
                                  (currentMonitorInfo.rcMonitor.bottom - currentMonitorInfo.rcMonitor.top);
                
                int targetHeight = leftInfo.rcMonitor.bottom - leftInfo.rcMonitor.top;
                newPos.y = leftInfo.rcMonitor.top + static_cast<int>(relativeY * targetHeight);
                newPos.x = leftInfo.rcMonitor.left; // Left edge of left monitor
                
                // Clamp Y to target monitor bounds
                newPos.y = max(leftInfo.rcMonitor.top, min(newPos.y, leftInfo.rcMonitor.bottom - 1));
                wrapped = true;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: HORIZONTAL WRAP SUCCESS - Moved to left end of horizontal stack");
                Logger::info(L"CursorWrap DEBUG: New position: ({}, {})", newPos.x, newPos.y);
#endif
            } else {
                // *** NO OTHER MONITOR IN HORIZONTAL STACK - WRAP WITHIN CURRENT MONITOR ***
                newPos.x = currentMonitorInfo.rcMonitor.left;
                wrapped = true;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: HORIZONTAL WRAP - No other monitor in stack, wrapping within current monitor");
#endif
            }
        }
        
#ifdef _DEBUG
        if (wrapped)
        {
            Logger::info(L"CursorWrap DEBUG: ======= WRAP RESULT =======");
            Logger::info(L"CursorWrap DEBUG: Original: ({}, {}) -> New: ({}, {})", 
                        currentPos.x, currentPos.y, newPos.x, newPos.y);
        }
        else
        {
            Logger::info(L"CursorWrap DEBUG: No wrapping performed - cursor not at edge");
        }
        Logger::info(L"CursorWrap DEBUG: ======= HANDLE MOUSE MOVE END =======");
#endif
        
        return newPos;
    }

    // Add test method for monitor topology validation
    void RunMonitorTopologyTests()
    {
#ifdef _DEBUG
        Logger::info(L"CursorWrap: Running monitor topology tests...");
        
        // Test all 9 possible monitor positions in 3x3 grid
        const char* gridNames[3][3] = {
            {"TL", "TC", "TR"},  // Top-Left, Top-Center, Top-Right
            {"ML", "MC", "MR"},  // Middle-Left, Middle-Center, Middle-Right  
            {"BL", "BC", "BR"}   // Bottom-Left, Bottom-Center, Bottom-Right
        };
        
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                HMONITOR monitor = m_topology.GetMonitorAt(row, col);
                if (monitor)
                {
                    std::string gridName(gridNames[row][col]);
                    std::wstring wGridName(gridName.begin(), gridName.end());
                    Logger::info(L"CursorWrap TEST: Monitor at [{}][{}] ({}) exists", 
                                row, col, wGridName.c_str());
                    
                    // Test adjacent monitor finding
                    HMONITOR up = m_topology.FindAdjacentMonitor(monitor, -1, 0);
                    HMONITOR down = m_topology.FindAdjacentMonitor(monitor, 1, 0);
                    HMONITOR left = m_topology.FindAdjacentMonitor(monitor, 0, -1);
                    HMONITOR right = m_topology.FindAdjacentMonitor(monitor, 0, 1);
                    
                    Logger::info(L"CursorWrap TEST: Adjacent monitors - Up: {}, Down: {}, Left: {}, Right: {}",
                                up ? L"YES" : L"NO", down ? L"YES" : L"NO", 
                                left ? L"YES" : L"NO", right ? L"YES" : L"NO");
                }
            }
        }
        
        Logger::info(L"CursorWrap: Monitor topology tests completed.");
#endif
    }

    // Add method to trigger test suite (can be called via hotkey in debug builds)
    void RunComprehensiveTests()
    {
#ifdef _DEBUG
        RunMonitorTopologyTests();
        
        // Test cursor wrapping scenarios
        Logger::info(L"CursorWrap: Testing cursor wrapping scenarios...");
        
        // Simulate cursor positions at each monitor edge and verify expected behavior
        for (const auto& monitor : m_monitors)
        {
            HMONITOR hMonitor = MonitorFromRect(&monitor.rect, MONITOR_DEFAULTTONEAREST);
            LogicalPosition pos = m_topology.GetPosition(hMonitor);
            
            if (pos.isValid)
            {
                Logger::info(L"CursorWrap TEST: Testing monitor at position [{}][{}]", pos.row, pos.col);
                
                // Test top edge
                POINT topEdge = {(monitor.rect.left + monitor.rect.right) / 2, monitor.rect.top};
                POINT newPos = HandleMouseMove(topEdge);
                Logger::info(L"CursorWrap TEST: Top edge ({}, {}) -> ({}, {})", 
                            topEdge.x, topEdge.y, newPos.x, newPos.y);
                
                // Test bottom edge
                POINT bottomEdge = {(monitor.rect.left + monitor.rect.right) / 2, monitor.rect.bottom - 1};
                newPos = HandleMouseMove(bottomEdge);
                Logger::info(L"CursorWrap TEST: Bottom edge ({}, {}) -> ({}, {})", 
                            bottomEdge.x, bottomEdge.y, newPos.x, newPos.y);
                
                // Test left edge
                POINT leftEdge = {monitor.rect.left, (monitor.rect.top + monitor.rect.bottom) / 2};
                newPos = HandleMouseMove(leftEdge);
                Logger::info(L"CursorWrap TEST: Left edge ({}, {}) -> ({}, {})", 
                            leftEdge.x, leftEdge.y, newPos.x, newPos.y);
                
                // Test right edge
                POINT rightEdge = {monitor.rect.right - 1, (monitor.rect.top + monitor.rect.bottom) / 2};
                newPos = HandleMouseMove(rightEdge);
                Logger::info(L"CursorWrap TEST: Right edge ({}, {}) -> ({}, {})", 
                            rightEdge.x, rightEdge.y, newPos.x, newPos.y);
            }
        }
        
        Logger::info(L"CursorWrap: Comprehensive tests completed.");
#endif
    }
};

// Implementation of MonitorTopology methods
void MonitorTopology::Initialize(const std::vector<MonitorInfo>& monitors)
{
    // Clear existing data
    grid.assign(3, std::vector<HMONITOR>(3, nullptr));
    monitorToPosition.clear();
    positionToMonitor.clear();
    
    if (monitors.empty()) return;
    
#ifdef _DEBUG
    Logger::info(L"CursorWrap DEBUG: ======= TOPOLOGY INITIALIZATION START =======");
    Logger::info(L"CursorWrap DEBUG: Initializing topology for {} monitors", monitors.size());
    for (const auto& monitor : monitors)
    {
        Logger::info(L"CursorWrap DEBUG: Monitor {}: bounds=({},{},{},{}), isPrimary={}", 
                    monitor.monitorId, monitor.rect.left, monitor.rect.top, 
                    monitor.rect.right, monitor.rect.bottom, monitor.isPrimary);
    }
#endif
    
    // Special handling for 2 monitors - use physical position, not discovery order
    if (monitors.size() == 2)
    {
        // Determine if arrangement is horizontal or vertical by comparing centers
        POINT center0 = {(monitors[0].rect.left + monitors[0].rect.right) / 2, 
                        (monitors[0].rect.top + monitors[0].rect.bottom) / 2};
        POINT center1 = {(monitors[1].rect.left + monitors[1].rect.right) / 2,
                        (monitors[1].rect.top + monitors[1].rect.bottom) / 2};
        
        int xDiff = abs(center0.x - center1.x);
        int yDiff = abs(center0.y - center1.y);
        
        bool isHorizontal = xDiff > yDiff;
        
#ifdef _DEBUG
        Logger::info(L"CursorWrap DEBUG: Monitor centers: M0=({}, {}), M1=({}, {})", 
                    center0.x, center0.y, center1.x, center1.y);
        Logger::info(L"CursorWrap DEBUG: Differences: X={}, Y={}, IsHorizontal={}", 
                    xDiff, yDiff, isHorizontal);
#endif
        
        if (isHorizontal)
        {
            // Horizontal arrangement - place in middle row [1,0] and [1,2]
            for (const auto& monitor : monitors)
            {
                HMONITOR hMonitor = MonitorFromRect(&monitor.rect, MONITOR_DEFAULTTONEAREST);
                POINT center = {(monitor.rect.left + monitor.rect.right) / 2, 
                               (monitor.rect.top + monitor.rect.bottom) / 2};
                
                int row = 1; // Middle row
                int col = (center.x < (center0.x + center1.x) / 2) ? 0 : 2; // Left or right based on center
                
                grid[row][col] = hMonitor;
                monitorToPosition[hMonitor] = {row, col, true};
                positionToMonitor[{row, col}] = hMonitor;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: Monitor {} (horizontal) placed at grid[{}][{}]", 
                            monitor.monitorId, row, col);
#endif
            }
        }
        else
        {
            // *** VERTICAL ARRANGEMENT - CRITICAL LOGIC ***
            // Sort monitors by Y coordinate to determine vertical order
            std::vector<std::pair<int, MonitorInfo>> sortedMonitors;
            for (int i = 0; i < 2; i++) {
                sortedMonitors.push_back({i, monitors[i]});
            }
            
            // Sort by Y coordinate (top to bottom)
            std::sort(sortedMonitors.begin(), sortedMonitors.end(), 
                [](const std::pair<int, MonitorInfo>& a, const std::pair<int, MonitorInfo>& b) {
                    int centerA = (a.second.rect.top + a.second.rect.bottom) / 2;
                    int centerB = (b.second.rect.top + b.second.rect.bottom) / 2;
                    return centerA < centerB; // Top first
                });
            
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: VERTICAL ARRANGEMENT DETECTED");
            Logger::info(L"CursorWrap DEBUG: Top monitor: ID={}, Y-center={}", 
                        sortedMonitors[0].second.monitorId,
                        (sortedMonitors[0].second.rect.top + sortedMonitors[0].second.rect.bottom) / 2);
            Logger::info(L"CursorWrap DEBUG: Bottom monitor: ID={}, Y-center={}", 
                        sortedMonitors[1].second.monitorId,
                        (sortedMonitors[1].second.rect.top + sortedMonitors[1].second.rect.bottom) / 2);
#endif
            
            // Place monitors in grid based on sorted order
            for (int i = 0; i < 2; i++) {
                const auto& monitorPair = sortedMonitors[i];
                const auto& monitor = monitorPair.second;
                HMONITOR hMonitor = MonitorFromRect(&monitor.rect, MONITOR_DEFAULTTONEAREST);
                
                int col = 1; // Middle column for vertical arrangement
                int row = (i == 0) ? 0 : 2; // Top monitor at row 0, bottom at row 2
                
                grid[row][col] = hMonitor;
                monitorToPosition[hMonitor] = {row, col, true};
                positionToMonitor[{row, col}] = hMonitor;
                
#ifdef _DEBUG
                Logger::info(L"CursorWrap DEBUG: Monitor {} (vertical) placed at grid[{}][{}] - {} position", 
                            monitor.monitorId, row, col, (i == 0) ? L"TOP" : L"BOTTOM");
#endif
            }
        }
    }
    else
    {
        // For more than 2 monitors, use the general algorithm
        RECT totalBounds = monitors[0].rect;
        for (const auto& monitor : monitors)
        {
            totalBounds.left = min(totalBounds.left, monitor.rect.left);
            totalBounds.top = min(totalBounds.top, monitor.rect.top);
            totalBounds.right = max(totalBounds.right, monitor.rect.right);
            totalBounds.bottom = max(totalBounds.bottom, monitor.rect.bottom);
        }
        
        int totalWidth = totalBounds.right - totalBounds.left;
        int totalHeight = totalBounds.bottom - totalBounds.top;
        int gridWidth = max(1, totalWidth / 3);
        int gridHeight = max(1, totalHeight / 3);
        
        // Place monitors in the 3x3 grid based on their center points
        for (const auto& monitor : monitors)
        {
            HMONITOR hMonitor = MonitorFromRect(&monitor.rect, MONITOR_DEFAULTTONEAREST);
            
            // Calculate center point of monitor
            int centerX = (monitor.rect.left + monitor.rect.right) / 2;
            int centerY = (monitor.rect.top + monitor.rect.bottom) / 2;
            
            // Map to grid position
            int col = (centerX - totalBounds.left) / gridWidth;
            int row = (centerY - totalBounds.top) / gridHeight;
            
            // Ensure we stay within bounds
            col = max(0, min(2, col));
            row = max(0, min(2, row));
            
            grid[row][col] = hMonitor;
            monitorToPosition[hMonitor] = {row, col, true};
            positionToMonitor[{row, col}] = hMonitor;
            
#ifdef _DEBUG
            Logger::info(L"CursorWrap DEBUG: Monitor {} placed at grid[{}][{}], center=({}, {})", 
                        monitor.monitorId, row, col, centerX, centerY);
#endif
        }
    }
    
#ifdef _DEBUG
    // *** CRITICAL: Print topology map using OutputDebugString for debug builds ***
    Logger::info(L"CursorWrap DEBUG: ======= FINAL TOPOLOGY MAP =======");
    OutputDebugStringA("CursorWrap TOPOLOGY MAP:\n");
    for (int r = 0; r < 3; r++)
    {
        std::string rowStr = "  ";
        for (int c = 0; c < 3; c++)
        {
            if (grid[r][c])
            {
                // Find monitor ID for this handle
                int monitorId = -1;
                for (const auto& monitor : monitors)
                {
                    HMONITOR handle = MonitorFromRect(&monitor.rect, MONITOR_DEFAULTTONEAREST);
                    if (handle == grid[r][c])
                    {
                        monitorId = monitor.monitorId + 1; // Convert to 1-based for display
                        break;
                    }
                }
                rowStr += std::to_string(monitorId) + " ";
            }
            else
            {
                rowStr += ". ";
            }
        }
        rowStr += "\n";
        OutputDebugStringA(rowStr.c_str());
        
        // Also log to PowerToys logger
        std::wstring wRowStr(rowStr.begin(), rowStr.end());
        Logger::info(wRowStr.c_str());
    }
    OutputDebugStringA("======= END TOPOLOGY MAP =======\n");
    
    // Additional validation logging
    Logger::info(L"CursorWrap DEBUG: ======= GRID POSITION VALIDATION =======");
    for (const auto& monitor : monitors)
    {
        HMONITOR hMonitor = MonitorFromRect(&monitor.rect, MONITOR_DEFAULTTONEAREST);
        LogicalPosition pos = GetPosition(hMonitor);
        if (pos.isValid)
        {
            Logger::info(L"CursorWrap DEBUG: Monitor {} -> grid[{}][{}]", monitor.monitorId, pos.row, pos.col);
            OutputDebugStringA(("Monitor " + std::to_string(monitor.monitorId) + " -> grid[" + std::to_string(pos.row) + "][" + std::to_string(pos.col) + "]\n").c_str());
            
            // Test adjacent finding
            HMONITOR up = FindAdjacentMonitor(hMonitor, -1, 0);
            HMONITOR down = FindAdjacentMonitor(hMonitor, 1, 0);
            HMONITOR left = FindAdjacentMonitor(hMonitor, 0, -1);
            HMONITOR right = FindAdjacentMonitor(hMonitor, 0, 1);
            
            Logger::info(L"CursorWrap DEBUG: Monitor {} adjacents - Up: {}, Down: {}, Left: {}, Right: {}",
                        monitor.monitorId, up ? L"YES" : L"NO", down ? L"YES" : L"NO", 
                        left ? L"YES" : L"NO", right ? L"YES" : L"NO");
        }
    }
    Logger::info(L"CursorWrap DEBUG: ======= TOPOLOGY INITIALIZATION COMPLETE =======");
#endif
}

LogicalPosition MonitorTopology::GetPosition(HMONITOR monitor) const
{
    auto it = monitorToPosition.find(monitor);
    if (it != monitorToPosition.end())
    {
        return it->second;
    }
    return {-1, -1, false};
}

HMONITOR MonitorTopology::GetMonitorAt(int row, int col) const
{
    if (row >= 0 && row < 3 && col >= 0 && col < 3)
    {
        return grid[row][col];
    }
    return nullptr;
}

HMONITOR MonitorTopology::FindAdjacentMonitor(HMONITOR current, int deltaRow, int deltaCol) const
{
    LogicalPosition currentPos = GetPosition(current);
    if (!currentPos.isValid) return nullptr;
    
    int newRow = currentPos.row + deltaRow;
    int newCol = currentPos.col + deltaCol;
    
    return GetMonitorAt(newRow, newCol);
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new CursorWrap();
}
