// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/logger_helper.h>
#include <common/logger/logger.h>
#include "trace.h"
#include "Generated Files/resource.h"
#include "CursorBrakeCore.h"
#include "CursorBrakeEdges.h"
#include "CursorBrakeFullScreen.h"
#include "DetectedEdges.h"
#include "EdgeOverlay.h"
#include "EdgePublisher.h"
#include "EdgeSettings.h"
#include "FullScreenSettings.h"
#include "IdentityReader.h"
#include "ModuleConstants.h"

#include <common/utils/excluded_apps.h>
#include <common/utils/resources.h>

#include <ShellScalingApi.h>
#include <WtsApi32.h>
#include <dbt.h>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <mutex>
#include <optional>
#include <thread>

// CursorBrake
//
// Adds resistance at the edges between monitors. The cursor stops at an inner edge until the push
// past it adds up to a configurable distance, so a fast move toward something near the edge stops
// there and does not overshoot onto the next monitor. Outer edges and gaps are left to Windows.
//
// The hook lives on a dedicated thread with its own message pump, matching the other Mouse
// Utilities (see CursorWrap and MouseButtonLock). The decisions live in CursorBrakeCore.h
// (Win32-free and unit tested); this file is the Win32 adapter around it.

extern "C" IMAGE_DOS_HEADER __ImageBase;

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD ul_reason_for_call, LPVOID /*lpReserved*/)
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

// Non-localizable strings.
namespace
{
    const wchar_t JSON_KEY_PROPERTIES[] = L"properties";
    const wchar_t JSON_KEY_PUSH_DISTANCE[] = L"push_distance";
    const wchar_t JSON_KEY_EDGE_FILTER[] = L"edge_filter";
    const wchar_t JSON_KEY_DISABLE_WHILE_DRAGGING[] = L"disable_while_dragging";
    const wchar_t JSON_KEY_CROSS_MODIFIER[] = L"cross_modifier";
    const wchar_t JSON_KEY_FREE_RETURN[] = L"free_return";
    const wchar_t DISPLAY_CHANGE_WINDOW_CLASS[] = L"CursorBrakeDisplayChangeWindow";

    // Monitor device interface, for RegisterDeviceNotification.
    // {e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
    const GUID GUID_DEVINTERFACE_MONITOR = { 0xe6f07b5f, 0xee97, 0x4a90, { 0xb0, 0x76, 0x33, 0xf5, 0x7b, 0xf4, 0xea, 0xa7 } };

    // Display changes arrive in bursts while Windows reconfigures, so the layout is rebuilt once
    // they settle.
    constexpr UINT_PTR TIMER_REBUILD_LAYOUT = 1;
    constexpr UINT DEBOUNCE_DELAY_MS = 500;

    // A monitor whose identity cannot be read during a reconfiguration is read again, a few times,
    // a second apart.
    constexpr UINT_PTR TIMER_RETRY_IDENTITY = 2;
    constexpr UINT IDENTITY_RETRY_DELAY_MS = 1000;
    constexpr int MAX_IDENTITY_RETRIES = 3;

    // A UWP app's frame belongs to ApplicationFrameHost until the app's own window attaches to it,
    // which can come after the foreground event. The app in front is then judged once more, a
    // moment later, on a timer rather than by waiting beside the mouse hook.
    constexpr UINT_PTR TIMER_REJUDGE_FOREGROUND = 3;
    constexpr UINT REJUDGE_FOREGROUND_DELAY_MS = 200;
    const wchar_t APPLICATION_FRAME_HOST[] = L"\\APPLICATIONFRAMEHOST.EXE";

    // Posted to the display change window when set_config leaves new edge settings for the hook
    // thread.
    constexpr UINT WM_APP_EDGES_CHANGED = WM_APP + 1;

    // Posted to the display change window by the "show_edges" custom action from Settings.
    constexpr UINT WM_APP_SHOW_EDGES = WM_APP + 2;
    const wchar_t SHOW_EDGES_ACTION[] = L"show_edges";

    // Posted to the display change window by the identity reader when a read finishes.
    constexpr UINT WM_APP_IDENTITIES = WM_APP + 3;

    // Posted to the display change window when set_config leaves a new excluded apps list for the
    // hook thread.
    constexpr UINT WM_APP_EXCLUDED_APPS_CHANGED = WM_APP + 4;

    // Class names of the windows that draw the desktop.
    const wchar_t PROGMAN_CLASS[] = L"Progman";
    const wchar_t WORKERW_CLASS[] = L"WorkerW";

    std::wstring WithKey(UINT resourceId, const std::wstring& key)
    {
        return cursorbrake::ReplacePlaceholder(get_resource_string(resourceId, reinterpret_cast<HINSTANCE>(&__ImageBase), L""), key);
    }

    // The console session's ID, from "Detecting the Remote Desktop Services environment".
    const wchar_t TERMINAL_SERVER_KEY[] = L"SYSTEM\\CurrentControlSet\\Control\\Terminal Server\\";
    const wchar_t GLASS_SESSION_ID_VALUE[] = L"GlassSessionId";

    static_assert(cursorbrake::INJECTED_FLAG == LLMHF_INJECTED);
    static_assert(cursorbrake::LOWER_IL_INJECTED_FLAG == LLMHF_LOWER_IL_INJECTED);

    // SM_REMOTESESSION reports a RemoteFX vGPU session as local, so a process whose session is not
    // the console (glass) session counts as remote too.
    bool IsRemoteSession()
    {
        if (GetSystemMetrics(SM_REMOTESESSION) != 0)
        {
            return true;
        }

        DWORD glassSessionId = 0;
        DWORD size = sizeof(glassSessionId);
        if (RegGetValueW(HKEY_LOCAL_MACHINE, TERMINAL_SERVER_KEY, GLASS_SESSION_ID_VALUE, RRF_RT_REG_DWORD, nullptr, &glassSessionId, &size) != ERROR_SUCCESS)
        {
            return false;
        }

        DWORD currentSessionId = 0;
        if (!ProcessIdToSessionId(GetCurrentProcessId(), &currentSessionId))
        {
            return false;
        }
        return currentSessionId != glassSessionId;
    }

    // The uppercased image path of a process. Reading the name needs no right to read the
    // process's memory, which anti-cheat drivers watch for. Empty when the process refuses even
    // this, as a protected one can.
    std::wstring ProcessImagePath(DWORD processId)
    {
        const HANDLE process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
        if (!process)
        {
            return {};
        }
        std::wstring path(MAX_PATH, L'\0');
        DWORD length = static_cast<DWORD>(path.size());
        if (!QueryFullProcessImageNameW(process, 0, path.data(), &length))
        {
            length = 0;
        }
        CloseHandle(process);
        path.resize(length);
        CharUpperBuffW(path.data(), length);
        return path;
    }

    // The uppercased image path of the process that owns `window`. ApplicationFrameHost owns a UWP
    // app's frame, so for a frame the path is that of the first child window another process owns.
    // None of these calls sends a message.
    std::wstring WindowProcessPath(HWND window)
    {
        DWORD frameProcessId = 0;
        GetWindowThreadProcessId(window, &frameProcessId);
        const std::wstring path = ProcessImagePath(frameProcessId);
        if (!path.ends_with(APPLICATION_FRAME_HOST))
        {
            return path;
        }

        std::pair<DWORD, DWORD> processes{ frameProcessId, frameProcessId };
        EnumChildWindows(
            window,
            [](HWND child, LPARAM data) -> BOOL {
                auto* processes = reinterpret_cast<std::pair<DWORD, DWORD>*>(data);
                DWORD childProcessId = 0;
                GetWindowThreadProcessId(child, &childProcessId);
                if (childProcessId != processes->first)
                {
                    processes->second = childProcessId;
                    return FALSE;
                }
                return TRUE;
            },
            reinterpret_cast<LPARAM>(&processes));
        return processes.second != frameProcessId ? ProcessImagePath(processes.second) : path;
    }
}

// Forward declaration so the static hook and window procedures can reach the singleton instance.
class CursorBrake;
// Atomic because the static procedures (on the hook thread) read it while the ctor/destroy (on the
// runner thread) write it. destroy() unhooks and joins the hook thread before clearing this and
// deleting the object, so a non-null load in a procedure stays valid.
static std::atomic<CursorBrake*> g_instance{ nullptr };

// Implement the PowerToy Module Interface and all the required methods.
class CursorBrake : public PowertoyModuleIface
{
private:
    // The PowerToy enabled state (the whole module, driven by the runner). Atomic because the
    // runner's telemetry thread can call is_enabled() while enable()/disable() write it.
    std::atomic<bool> m_enabled{ false };

    // Settings, written by set_config on the runner thread and read on the hook thread. A move can
    // see old and new values mixed while an apply lands; the engine resets on any settings change,
    // so the mix costs at most one push.
    std::atomic<int> m_pushDistance{ cursorbrake::Settings{}.pushDistance };
    std::atomic<int> m_edgeFilter{ static_cast<int>(cursorbrake::Settings{}.edgeFilter) };
    std::atomic<bool> m_disableWhileDragging{ cursorbrake::Settings{}.disableWhileDragging };
    std::atomic<int> m_crossModifier{ static_cast<int>(cursorbrake::Settings{}.crossModifier) };
    std::atomic<bool> m_freeReturn{ cursorbrake::Settings{}.freeReturn };
    std::atomic<int> m_fullScreenBehavior{ static_cast<int>(cursorbrake::FullScreenBehavior::Same) };

    // Owned by the hook thread: the engine, the layout it reads, and the cursor between callbacks.
    cursorbrake::Engine m_engine;
    cursorbrake::Layout m_layout;
    cursorbrake::PointL m_lastPoint;
    bool m_remoteSession = false;

    // Per-edge settings. set_config parses them on the runner thread and leaves them in
    // m_pendingOverrides, which only m_overridesMutex guards. The hook thread takes them in a
    // window message and resolves them into m_layout.edges, so the hook itself never locks.
    std::mutex m_overridesMutex;
    std::optional<std::vector<cursorbrake::EdgeOverride>> m_pendingOverrides;

    // The excluded apps take the same route: set_config leaves them here, and the hook thread takes
    // them in a window message.
    std::mutex m_excludedAppsMutex;
    std::optional<std::vector<std::wstring>> m_pendingExcludedApps;

    // Owned by the hook thread: the excluded apps in effect, uppercased, whether one of them is in
    // front, the monitor holding a full-screen app as last worked out, and the foreground event
    // hook. The foreground is judged in the event callback, outside the mouse hook, because the
    // title fallback calls GetWindowTextW.
    std::vector<std::wstring> m_excludedApps;
    cursorbrake::ForegroundCache m_foreground;
    cursorbrake::FullScreenTracker m_fullScreen;
    HWINEVENTHOOK m_foregroundHook = nullptr;
    // Owned by the hook thread: the last window looked up as a possible PowerToys overlay, and the
    // answer. A window keeps its process, so each one opens its process once.
    HWND m_overlayCheckWindow = nullptr;
    bool m_overlayCheckResult = false;

    // Owned by the hook thread: the overrides in effect, and per monitor of m_layout its ID
    // (empty when unknown) and what Settings is told about it.
    std::vector<cursorbrake::EdgeOverride> m_overrides;
    std::vector<std::wstring> m_monitorIds;
    std::vector<cursorbrake::DetectedMonitor> m_detected;
    // The identified pairs sharing an edge at the last publish, stamped once more on the next one
    // so a pair that just came apart records when it was last together.
    std::vector<std::pair<std::wstring, std::wstring>> m_presentPairs;
    int m_identityRetries = 0;
    // Owned by the hook thread: each monitor's GDI device, by index of m_layout, and the number of
    // the layout identity reads are applied to. A rebuild and a display change each advance it.
    std::vector<std::wstring> m_devices;
    uint64_t m_generation = 0;
    cursorbrake::IdentityReader m_identityReader;
    cursorbrake::EdgePublisher m_publisher;
    cursorbrake::EdgeOverlay m_overlay;
    // Set by call_custom_action on the runner thread, read on the hook thread.
    cursorbrake::ShowGate m_showGate;

    // Hook thread + lifecycle. The display change window is atomic because set_config posts to
    // it from the runner thread.
    HHOOK m_mouseHook = nullptr;
    std::atomic<HWND> m_messageWindow{ nullptr };
    HDEVNOTIFY m_deviceNotify = nullptr;
    bool m_sessionNotify = false;
    HANDLE m_terminateEvent = nullptr;
    std::thread m_hookThread;
    std::atomic<bool> m_listening{ false };

    void init_settings();
    void parse_settings(PowerToysSettings::PowerToyValues& settings);
    cursorbrake::Settings SettingsSnapshot() const;

    void HookThreadMain();
    void RegisterForDisplayChanges();
    void UnregisterDisplayChanges();
    void RebuildLayout();
    void ApplyIdentities();
    bool TakePendingOverrides();
    bool TakePendingExcludedApps();
    void AdoptForeground(HWND window, cursorbrake::ForegroundReport report);
    std::optional<size_t> FullScreenMonitor();
    void ResolveOverrides();
    void PublishEdges();
    void ShowEdges();
    void RefreshSessionState();
    bool HandleMove(const MSLLHOOKSTRUCT& data);

    static BOOL CALLBACK AddMonitor(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data);
    static LRESULT CALLBACK MessageWindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam);
    static void CALLBACK ForegroundProc(HWINEVENTHOOK hook, DWORD event, HWND hwnd, LONG idObject, LONG idChild, DWORD thread, DWORD time);

public:
    CursorBrake()
    {
        LoggerHelpers::init_logger(cursorbrake::MODULE_NAME, L"ModuleInterface", LogSettings::cursorBrakeLoggerName);
        init_settings();
        g_instance.store(this);
    }

    virtual void destroy() override
    {
        // Ensure the hook thread is torn down before deletion.
        disable();
        g_instance.store(nullptr);
        delete this;
    }

    virtual const wchar_t* get_name() override
    {
        return cursorbrake::MODULE_NAME;
    }

    virtual const wchar_t* get_key() override
    {
        return cursorbrake::MODULE_NAME;
    }

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override
    {
        return powertoys_gpo::getConfiguredCursorBrakeEnabledValue();
    }

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(IDS_CURSORBRAKE_NAME);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void call_custom_action(const wchar_t* action) override
    {
        try
        {
            PowerToysSettings::CustomActionObject actionObject = PowerToysSettings::CustomActionObject::from_json_string(action);
            if (actionObject.get_name() == SHOW_EDGES_ACTION)
            {
                // The overlay is drawn from the hook thread's layout, so the request goes there.
                // The gate lets through only a message this action asked for.
                if (const HWND window = m_messageWindow.load())
                {
                    m_showGate.Request();
                    PostMessageW(window, WM_APP_SHOW_EDGES, 0, 0);
                }
            }
        }
        catch (...)
        {
            Logger::error(L"Failed to parse a CursorBrake custom action.");
        }
    }

    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            parse_settings(values);
        }
        catch (...)
        {
            // catch(...) because the JSON accessors throw winrt::hresult_error, which does not
            // derive from std::exception; a malformed payload must not escape and abort apply.
            Logger::error("Invalid json when trying to parse CursorBrake settings json.");
        }
    }

    virtual void enable() override
    {
        m_enabled = true;
        Trace::EnableCursorBrake(true);

        if (m_listening)
        {
            return;
        }

        m_terminateEvent = CreateEventW(nullptr, FALSE, FALSE, nullptr);
        if (m_terminateEvent == nullptr)
        {
            // Without the terminate event the hook thread's wait would spin at 100% CPU; don't start it.
            Logger::error(L"Failed to create CursorBrake terminate event, error: {}. Hook not started.", GetLastError());
            return;
        }
        m_listening = true;
        m_hookThread = std::thread([this]() { HookThreadMain(); });
    }

    virtual void disable() override
    {
        m_enabled = false;
        Trace::EnableCursorBrake(false);

        if (!m_listening)
        {
            return;
        }

        m_listening = false;
        if (m_terminateEvent)
        {
            SetEvent(m_terminateEvent);
        }
        if (m_hookThread.joinable())
        {
            m_hookThread.join();
        }
        m_overlay.Close();
        if (m_terminateEvent)
        {
            CloseHandle(m_terminateEvent);
            m_terminateEvent = nullptr;
        }
    }

    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    virtual bool is_enabled_by_default() const override
    {
        return false;
    }
};

void CursorBrake::init_settings()
{
    try
    {
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(CursorBrake::get_key());
        parse_settings(settings);
    }
    catch (...)
    {
        // catch(...) so winrt::hresult_error from the JSON accessors can't escape the constructor;
        // keep the defaults on any parse error.
        Logger::error("Invalid json when trying to load the CursorBrake settings json from file.");
    }
}

void CursorBrake::parse_settings(PowerToysSettings::PowerToyValues& settings)
{
    // The constructor reads the PowerToys settings folder, which holds no CursorBrake file until
    // the settings UI first saves one, so an empty object here is normal on a first run. Later
    // settings arrive through set_config: from the settings UI on a save, or from ModuleLoader at
    // start.
    auto settingsObject = settings.get_raw_json();
    if (!settingsObject.GetView().Size() || !settingsObject.HasKey(JSON_KEY_PROPERTIES))
    {
        Logger::info("CursorBrake settings carry no properties; keeping the current values.");
        return;
    }

    auto properties = settingsObject.GetNamedObject(JSON_KEY_PROPERTIES);

    // GetNamedNumber yields a double. A non-finite value is rejected outright, because NaN slips
    // past range comparisons and its cast to int is undefined.
    auto readNumber = [&](const wchar_t* key) -> std::optional<double> {
        if (!properties.HasKey(key))
        {
            return std::nullopt;
        }
        try
        {
            const double raw = properties.GetNamedObject(key).GetNamedNumber(cursorbrake::JSON_KEY_VALUE);
            if (std::isfinite(raw))
            {
                return raw;
            }
            Logger::warn(L"Ignoring non-finite CursorBrake setting; keeping previous value.");
        }
        catch (...)
        {
            Logger::warn(L"Failed to read CursorBrake number setting; keeping previous value.");
        }
        return std::nullopt;
    };

    // A hand-edited file can carry any distance, so it is clamped before the cast.
    if (const std::optional<double> raw = readNumber(JSON_KEY_PUSH_DISTANCE))
    {
        m_pushDistance.store(cursorbrake::ClampPushDistance(*raw));
    }

    // A choice outside the known values has no meaning to clamp toward, so it is ignored.
    auto readChoice = [&](const wchar_t* key, std::atomic<int>& target, int count) {
        if (const std::optional<double> raw = readNumber(key))
        {
            if (const std::optional<int> choice = cursorbrake::ChoiceOf<int>(*raw, count))
            {
                target.store(*choice);
            }
            else
            {
                Logger::warn(L"Ignoring out-of-range CursorBrake choice setting; keeping previous value.");
            }
        }
    };
    readChoice(JSON_KEY_EDGE_FILTER, m_edgeFilter, cursorbrake::EDGE_FILTER_COUNT);
    readChoice(JSON_KEY_CROSS_MODIFIER, m_crossModifier, cursorbrake::CROSS_MODIFIER_COUNT);

    try
    {
        if (const std::optional<cursorbrake::FullScreenBehavior> behavior = cursorbrake::ReadFullScreenBehavior(properties))
        {
            m_fullScreenBehavior.store(static_cast<int>(*behavior));
        }
    }
    catch (...)
    {
        Logger::warn(L"Failed to read the CursorBrake fullscreen_behavior setting; keeping previous value.");
    }

    if (properties.HasKey(JSON_KEY_FREE_RETURN))
    {
        try
        {
            m_freeReturn.store(properties.GetNamedObject(JSON_KEY_FREE_RETURN).GetNamedBoolean(cursorbrake::JSON_KEY_VALUE));
        }
        catch (...)
        {
            Logger::warn(L"Failed to read the CursorBrake free_return setting; keeping previous value.");
        }
    }

    if (properties.HasKey(JSON_KEY_DISABLE_WHILE_DRAGGING))
    {
        try
        {
            m_disableWhileDragging.store(properties.GetNamedObject(JSON_KEY_DISABLE_WHILE_DRAGGING).GetNamedBoolean(cursorbrake::JSON_KEY_VALUE));
        }
        catch (...)
        {
            Logger::warn(L"Failed to read CursorBrake bool setting; keeping previous value.");
        }
    }

    try
    {
        if (std::optional<std::vector<cursorbrake::EdgeOverride>> overrides = cursorbrake::ReadEdgesSetting(properties))
        {
            Logger::info(L"CursorBrake edge settings read: {} entries.", overrides->size());
            {
                std::lock_guard lock(m_overridesMutex);
                m_pendingOverrides = std::move(*overrides);
            }
            // Before the hook thread creates its window, the pending entries wait for it to start.
            // A post to a window whose thread is stopping is lost, and the next start takes them.
            if (const HWND window = m_messageWindow.load())
            {
                PostMessageW(window, WM_APP_EDGES_CHANGED, 0, 0);
            }
        }
        else
        {
            Logger::warn(L"Failed to read the CursorBrake edges setting; keeping the previous entries.");
        }
    }
    catch (...)
    {
        Logger::warn(L"Failed to read the CursorBrake edges setting; keeping the previous entries.");
    }

    try
    {
        if (std::optional<std::vector<std::wstring>> apps = cursorbrake::ReadExcludedApps(properties))
        {
            Logger::info(L"CursorBrake excluded apps read: {} entries.", apps->size());
            {
                std::lock_guard lock(m_excludedAppsMutex);
                m_pendingExcludedApps = std::move(*apps);
            }
            if (const HWND window = m_messageWindow.load())
            {
                PostMessageW(window, WM_APP_EXCLUDED_APPS_CHANGED, 0, 0);
            }
        }
    }
    catch (...)
    {
        Logger::warn(L"Failed to read the CursorBrake excluded_apps setting; keeping the previous list.");
    }

    Logger::info(L"CursorBrake settings applied: push_distance {}, edge_filter {}, cross_modifier {}, disable_while_dragging {}, free_return {}, fullscreen_behavior {}.", m_pushDistance.load(), m_edgeFilter.load(), m_crossModifier.load(), m_disableWhileDragging.load(), m_freeReturn.load(), m_fullScreenBehavior.load());
}

cursorbrake::Settings CursorBrake::SettingsSnapshot() const
{
    cursorbrake::Settings s;
    s.pushDistance = m_pushDistance.load();
    s.edgeFilter = static_cast<cursorbrake::EdgeFilter>(m_edgeFilter.load());
    s.disableWhileDragging = m_disableWhileDragging.load();
    s.crossModifier = static_cast<cursorbrake::CrossModifier>(m_crossModifier.load());
    s.freeReturn = m_freeReturn.load();
    return s;
}

void CursorBrake::HookThreadMain()
{
    // The hook reports physical coordinates, and per-monitor awareness on this thread makes the
    // monitor rects match them. GetCursorPos inside a low-level hook follows the host process's
    // awareness instead, so the host must be per-monitor aware; PowerToys.exe and ModuleLoader.exe
    // both declare PerMonitorV2.
    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    // WH_MOUSE_LL callbacks are delivered to the thread that installed the hook, so this
    // thread needs a message queue and must pump messages while the hook is active.
    MSG msg;
    PeekMessage(&msg, nullptr, WM_USER, WM_USER, PM_NOREMOVE);

    // A held push or free return from an earlier session means nothing now.
    m_engine = cursorbrake::Engine{};
    m_remoteSession = false;
    POINT cursor{};
    m_lastPoint = GetCursorPos(&cursor) ? cursorbrake::PointL{ cursor.x, cursor.y } : cursorbrake::PointL{};

    RegisterForDisplayChanges();
    TakePendingOverrides();
    TakePendingExcludedApps();
    m_identityRetries = 0;
    RebuildLayout();
    RefreshSessionState();

    // The app already in front when the hook starts is judged now, not at the next foreground
    // change, so an excluded app in front at start stands CursorBrake down from the first move.
    m_foreground = cursorbrake::ForegroundCache{};
    m_fullScreen.Reset();
    m_overlayCheckWindow = nullptr;
    m_overlayCheckResult = false;
    m_foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, nullptr, ForegroundProc, 0, 0, WINEVENT_OUTOFCONTEXT);
    if (!m_foregroundHook)
    {
        Logger::error(L"Failed to install the CursorBrake foreground event hook, error: {}. Excluded apps apply only to the app in front at start and after a settings change.", GetLastError());
    }
    AdoptForeground(GetForegroundWindow(), cursorbrake::ForegroundReport::Query);

    m_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(nullptr), 0);
    if (!m_mouseHook)
    {
        Logger::error(L"Failed to install CursorBrake mouse hook, error: {}", GetLastError());
    }
    else
    {
        Logger::info(L"CursorBrake hook installed.");
    }

    HANDLE handles[1] = { m_terminateEvent };
    while (m_listening)
    {
        DWORD res = MsgWaitForMultipleObjects(1, handles, FALSE, INFINITE, QS_ALLINPUT);
        if (!m_listening || res == WAIT_OBJECT_0)
        {
            break;
        }
        if (res == WAIT_FAILED)
        {
            // Bail rather than spin if the wait ever fails.
            Logger::error(L"CursorBrake wait failed, error: {}. Stopping hook thread.", GetLastError());
            break;
        }

        while (PeekMessage(&msg, nullptr, 0, 0, PM_REMOVE))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

    if (m_mouseHook)
    {
        UnhookWindowsHookEx(m_mouseHook);
        m_mouseHook = nullptr;
    }
    if (m_foregroundHook)
    {
        UnhookWinEvent(m_foregroundHook);
        m_foregroundHook = nullptr;
    }
    UnregisterDisplayChanges();
}

void CursorBrake::RegisterForDisplayChanges()
{
    // Message-only windows (HWND_MESSAGE parent) do not receive the WM_DISPLAYCHANGE,
    // WM_SETTINGCHANGE or WM_DEVICECHANGE broadcasts, so this is a hidden top-level window.
    WNDCLASSEXW wc = { sizeof(WNDCLASSEXW) };
    wc.lpfnWndProc = MessageWindowProc;
    wc.hInstance = GetModuleHandle(nullptr);
    wc.lpszClassName = DISPLAY_CHANGE_WINDOW_CLASS;
    RegisterClassExW(&wc);

    const HWND window = CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, DISPLAY_CHANGE_WINDOW_CLASS, nullptr, WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, GetModuleHandle(nullptr), nullptr);
    if (!window)
    {
        Logger::error(L"Failed to create the CursorBrake display change window, error: {}. The layout will not follow display changes, and edge settings apply only when the module starts.", GetLastError());
        return;
    }
    m_messageWindow.store(window);

    DEV_BROADCAST_DEVICEINTERFACE filter = {};
    filter.dbcc_size = sizeof(DEV_BROADCAST_DEVICEINTERFACE);
    filter.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
    filter.dbcc_classguid = GUID_DEVINTERFACE_MONITOR;
    m_deviceNotify = RegisterDeviceNotificationW(window, &filter, DEVICE_NOTIFY_WINDOW_HANDLE);
    if (!m_deviceNotify)
    {
        Logger::warn(L"Failed to register CursorBrake for monitor device notifications, error: {}", GetLastError());
    }

    m_sessionNotify = WTSRegisterSessionNotification(window, NOTIFY_FOR_THIS_SESSION);
    if (!m_sessionNotify)
    {
        Logger::warn(L"Failed to register CursorBrake for session change notifications, error: {}. A later switch between local and remote will go unnoticed.", GetLastError());
    }
}

void CursorBrake::UnregisterDisplayChanges()
{
    if (m_deviceNotify)
    {
        UnregisterDeviceNotification(m_deviceNotify);
        m_deviceNotify = nullptr;
    }
    // No read may post to the window once it is gone.
    m_identityReader.Cancel();
    const HWND window = m_messageWindow.exchange(nullptr);
    if (m_sessionNotify)
    {
        WTSUnRegisterSessionNotification(window);
        m_sessionNotify = false;
    }
    if (window)
    {
        KillTimer(window, TIMER_REBUILD_LAYOUT);
        KillTimer(window, TIMER_RETRY_IDENTITY);
        KillTimer(window, TIMER_REJUDGE_FOREGROUND);
        DestroyWindow(window);
        UnregisterClassW(DISPLAY_CHANGE_WINDOW_CLASS, GetModuleHandle(nullptr));
    }
}

namespace
{
    // What EnumDisplayMonitors reports, by monitor: the engine's geometry, plus the handle that
    // matches it to its identity, its GDI device that matches it to its display path, and whether
    // it is the main display.
    struct MonitorEnumeration
    {
        std::vector<cursorbrake::Monitor> monitors;
        std::vector<HMONITOR> handles;
        std::vector<std::wstring> devices;
        std::vector<bool> primary;
    };
}

// Runs on the hook thread only (at start and from the display change window), so the hook reads
// m_layout without a lock. The geometry comes from EnumDisplayMonitors on this per-monitor-aware
// thread, so its rects are physical, and takes effect at once. The identities, names and numbers
// are read on the identity reader's thread, because those reads can stall during a display change,
// and ApplyIdentities takes them when they arrive. Until then a monitor that kept its GDI device
// and rect keeps what was read for it, and any other monitor has no ID: its edges brake at Default
// and no override reaches them.
void CursorBrake::RebuildLayout()
{
    MonitorEnumeration enumeration;
    if (!EnumDisplayMonitors(nullptr, nullptr, AddMonitor, reinterpret_cast<LPARAM>(&enumeration)))
    {
        Logger::warn(L"EnumDisplayMonitors failed, error: {}. Keeping the previous CursorBrake layout.", GetLastError());
        return;
    }

    std::vector<cursorbrake::RectL> rects;
    for (const cursorbrake::Monitor& monitor : enumeration.monitors)
    {
        rects.push_back(monitor.rect);
    }
    std::vector<cursorbrake::RectL> previousRects;
    for (const cursorbrake::Monitor& monitor : m_layout.monitors)
    {
        previousRects.push_back(monitor.rect);
    }
    const std::vector<std::optional<size_t>> previous = cursorbrake::MatchPrevious(enumeration.devices, rects, m_devices, previousRects);

    std::vector<std::wstring> ids(enumeration.monitors.size());
    std::vector<cursorbrake::DetectedMonitor> detected;
    for (size_t i = 0; i < enumeration.monitors.size(); ++i)
    {
        const cursorbrake::Monitor& monitor = enumeration.monitors[i];
        cursorbrake::DetectedMonitor entry{ L"", L"", monitor.rect, monitor.dpi, enumeration.primary[i], static_cast<int>(i) + 1, false };
        if (previous[i] && *previous[i] < m_detected.size() && *previous[i] < m_monitorIds.size())
        {
            const cursorbrake::DetectedMonitor& kept = m_detected[*previous[i]];
            ids[i] = m_monitorIds[*previous[i]];
            entry.id = kept.id;
            entry.name = kept.name;
            entry.number = kept.number;
            entry.builtIn = kept.builtIn;
        }
        detected.push_back(std::move(entry));
    }

    m_layout.monitors = std::move(enumeration.monitors);
    m_devices = enumeration.devices;
    m_monitorIds = std::move(ids);
    m_detected = std::move(detected);
    Logger::info(L"CursorBrake layout rebuilt with {} monitors.", m_layout.monitors.size());
    ResolveOverrides();

    ++m_generation;
    if (m_identityReader.Request(m_generation, std::move(enumeration.handles), std::move(enumeration.devices), m_messageWindow.load(), WM_APP_IDENTITIES))
    {
        // Without a work item or a window, the reader ran the read on this thread and posted
        // nothing.
        ApplyIdentities();
    }
}

// Runs on the hook thread when the identity reader finishes. A read for an older layout is
// dropped, since a read or a rebuild for the current one follows it. A read that failed, or that
// saw other monitors than the layout holds, leaves the identities as they are and runs again.
void CursorBrake::ApplyIdentities()
{
    const std::optional<cursorbrake::Identities> result = m_identityReader.Take();
    if (!result)
    {
        return;
    }

    const HWND window = m_messageWindow.load();
    const auto retry = [&] {
        if (window && m_identityRetries < MAX_IDENTITY_RETRIES)
        {
            ++m_identityRetries;
            SetTimer(window, TIMER_RETRY_IDENTITY, IDENTITY_RETRY_DELAY_MS, nullptr);
        }
    };
    std::vector<cursorbrake::RectL> rects;
    for (const cursorbrake::Monitor& monitor : m_layout.monitors)
    {
        rects.push_back(monitor.rect);
    }
    switch (cursorbrake::JudgeIdentities(*result, m_generation, m_devices, rects))
    {
    case cursorbrake::IdentityReadUse::Drop:
        return;
    case cursorbrake::IdentityReadUse::Retry:
        Logger::warn(L"CursorBrake could not read the identities of the current monitors; the monitors keep the ones they have.");
        retry();
        return;
    case cursorbrake::IdentityReadUse::Apply:
        break;
    }

    for (size_t i = 0; i < m_layout.monitors.size(); ++i)
    {
        cursorbrake::DetectedMonitor& entry = m_detected[i];
        entry.id = result->ids[i];
        entry.name = result->names[i];
        entry.builtIn = result->builtIn[i];
        entry.number = result->numbers[i];
        m_monitorIds[i] = result->ids[i];

        // The EDID code only: the instance identifies the machine's ports, so it stays out of logs.
        const std::wstring_view id = entry.id;
        const size_t codeStart = id.find(L'#');
        const size_t codeEnd = codeStart == std::wstring_view::npos ? codeStart : id.find(L'#', codeStart + 1);
        const std::wstring_view model = codeEnd == std::wstring_view::npos ? std::wstring_view{ L"unknown" } : id.substr(codeStart + 1, codeEnd - codeStart - 1);
        const cursorbrake::RectL& rect = entry.rect;
        Logger::info(L"CursorBrake monitor {}: number {} left {} top {} right {} bottom {} dpi {} model {}{}{}", i, entry.number, rect.left, rect.top, rect.right, rect.bottom, entry.dpi, model, entry.builtIn ? L", built-in" : L"", entry.id.empty() ? L", unidentified" : L"");
    }
    ResolveOverrides();
    PublishEdges();

    const bool identified = std::none_of(m_monitorIds.begin(), m_monitorIds.end(), [](const std::wstring& id) { return id.empty(); });
    if (!result->complete || !identified)
    {
        retry();
    }
}

// Runs on the hook thread. Moves the entries set_config left, if any, into m_overrides and says
// whether there were any.
bool CursorBrake::TakePendingOverrides()
{
    std::optional<std::vector<cursorbrake::EdgeOverride>> pending;
    {
        std::lock_guard lock(m_overridesMutex);
        pending.swap(m_pendingOverrides);
    }
    if (!pending)
    {
        return false;
    }
    m_overrides = std::move(*pending);
    return true;
}

// Runs on the hook thread. Moves the excluded apps set_config left, if any, into m_excludedApps and
// says whether there were any.
bool CursorBrake::TakePendingExcludedApps()
{
    std::optional<std::vector<std::wstring>> pending;
    {
        std::lock_guard lock(m_excludedAppsMutex);
        pending.swap(m_pendingExcludedApps);
    }
    if (!pending)
    {
        return false;
    }
    m_excludedApps = std::move(*pending);
    return true;
}

// Runs on the hook thread, outside the mouse hook: at start, from the foreground event hook, after
// a settings or session change, and for a second look at a UWP app.
void CursorBrake::AdoptForeground(HWND window, cursorbrake::ForegroundReport report)
{
    bool frameHost = false;
    m_foreground.Adopt(reinterpret_cast<uintptr_t>(window), report, !m_excludedApps.empty(), [&](uintptr_t value) {
        const HWND hwnd = reinterpret_cast<HWND>(value);
        const std::wstring path = WindowProcessPath(hwnd);
        frameHost = path.ends_with(APPLICATION_FRAME_HOST);

        // GetWindowTextW sends WM_GETTEXT to a window of this process, whose thread can be the
        // runner's, waiting in disable() for this thread to end, so only the path is matched.
        DWORD processId = 0;
        GetWindowThreadProcessId(hwnd, &processId);
        if (processId == GetCurrentProcessId())
        {
            return find_app_name_in_path(path, m_excludedApps);
        }
        return check_excluded_app(hwnd, path, m_excludedApps);
    });

    // Only a foreground event asks for the second look, so the look itself never asks again.
    const HWND messageWindow = m_messageWindow.load();
    if (frameHost && report == cursorbrake::ForegroundReport::Event && messageWindow)
    {
        SetTimer(messageWindow, TIMER_REJUDGE_FOREGROUND, REJUDGE_FOREGROUND_DELAY_MS, nullptr);
    }
}

// Runs inside the mouse hook, only for a move that proposes a crossing. None of these calls sends
// a message, so a hung foreground window cannot stall the hook. The rects are physical, as this
// thread is per-monitor aware.
std::optional<size_t> CursorBrake::FullScreenMonitor()
{
    const HWND window = GetForegroundWindow();
    RECT windowRect{};
    if (!window || !GetWindowRect(window, &windowRect))
    {
        return std::nullopt;
    }
    const HMONITOR monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
    MONITORINFO info{};
    info.cbSize = sizeof(MONITORINFO);
    if (!monitor || !GetMonitorInfoW(monitor, &info))
    {
        return std::nullopt;
    }

    wchar_t className[16]{};
    const int classLength = GetClassNameW(window, className, ARRAYSIZE(className));
    const std::wstring_view windowClass{ className, classLength > 0 ? static_cast<size_t>(classLength) : 0 };

    cursorbrake::ForegroundWindow foreground;
    foreground.rect = cursorbrake::RectL{ windowRect.left, windowRect.top, windowRect.right, windowRect.bottom };
    foreground.monitor = cursorbrake::RectL{ info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.right, info.rcMonitor.bottom };
    foreground.hasCaption = (GetWindowLongPtrW(window, GWL_STYLE) & WS_CAPTION) == WS_CAPTION;
    foreground.isShellOrDesktop = window == GetShellWindow() || window == GetDesktopWindow() || windowClass == PROGMAN_CLASS || windowClass == WORKERW_CLASS;
    foreground.isToolWindow = (GetWindowLongPtrW(window, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0;

    return cursorbrake::FullScreenMonitorOf(m_layout, foreground, [&] {
        if (window != m_overlayCheckWindow)
        {
            m_overlayCheckWindow = window;
            m_overlayCheckResult = cursorbrake::IsPowerToysOverlay(WindowProcessPath(window));
        }
        return m_overlayCheckResult;
    });
}

// Runs on the hook thread. A change to m_layout.edges resets the engine's push on the next move,
// like any layout change.
void CursorBrake::ResolveOverrides()
{
    m_layout.edges = cursorbrake::ResolveEdges(m_layout, m_monitorIds, m_overrides);
    Logger::info(L"CursorBrake edge settings: {} of {} entries apply to the current monitors.", m_layout.edges.size(), m_overrides.size());
}

// Runs on the hook thread. The overlay draws on its own thread from a copy of what it needs.
void CursorBrake::ShowEdges()
{
    const cursorbrake::Settings settings = SettingsSnapshot();
    const std::wstring key = get_resource_string(settings.crossModifier == cursorbrake::CrossModifier::Shift ? IDS_CURSORBRAKE_KEY_SHIFT : IDS_CURSORBRAKE_KEY_CTRL, reinterpret_cast<HINSTANCE>(&__ImageBase), L"");

    const std::vector<size_t> trapped = cursorbrake::TrappedMonitors(m_layout, settings);

    cursorbrake::OverlayContent content;
    content.wallLabel = WithKey(IDS_CURSORBRAKE_OVERLAY_WALL, key);
    content.leaveLabel = WithKey(IDS_CURSORBRAKE_OVERLAY_LEAVE, key);
    content.offLabel = get_resource_string(IDS_CURSORBRAKE_OVERLAY_OFF, reinterpret_cast<HINSTANCE>(&__ImageBase), L"");
    content.pixelsFormat = get_resource_string(IDS_CURSORBRAKE_OVERLAY_PIXELS, reinterpret_cast<HINSTANCE>(&__ImageBase), L"");
    for (size_t i = 0; i < m_layout.monitors.size(); ++i)
    {
        const cursorbrake::Monitor& monitor = m_layout.monitors[i];
        const bool isTrapped = std::find(trapped.begin(), trapped.end(), i) != trapped.end();
        const int number = i < m_detected.size() ? m_detected[i].number : static_cast<int>(i) + 1;
        content.monitors.push_back(cursorbrake::OverlayMonitor{ monitor.rect, monitor.dpi, number, isTrapped });
    }
    for (const cursorbrake::InnerEdge& edge : cursorbrake::InnerEdges(m_layout))
    {
        for (const auto [from, to] : { std::pair{ edge.first, edge.second }, std::pair{ edge.second, edge.first } })
        {
            const cursorbrake::Monitor& source = m_layout.monitors[from];
            const long thickness = cursorbrake::Scaled(cursorbrake::EdgeOverlay::STRIP_THICKNESS, source.dpi);
            const bool towardPositive = edge.sideBySide ? source.rect.right == edge.at : source.rect.bottom == edge.at;
            content.directions.push_back(cursorbrake::OverlayDirection{ from, cursorbrake::DirectionStrip(source.rect, edge, thickness), edge.sideBySide, towardPositive ? 1 : -1, cursorbrake::RuleFor(m_layout, settings, from, to, edge.sideBySide) });
        }
    }

    Logger::info(L"CursorBrake shows {} edges on {} displays.", content.directions.size() / 2, content.monitors.size());
    m_overlay.Show(std::move(content));
}

// Runs on the hook thread; the write happens on the thread pool.
void CursorBrake::PublishEdges()
{
    std::vector<std::pair<std::wstring, std::wstring>> current = cursorbrake::PresentPairs(m_layout, m_monitorIds);
    std::vector<std::pair<std::wstring, std::wstring>> present = m_presentPairs;
    present.insert(present.end(), current.begin(), current.end());
    m_presentPairs = std::move(current);
    m_publisher.Publish(m_detected, std::move(present), cursorbrake::NamedPairs(m_overrides));
}

// Runs on the hook thread only (at start and on WM_WTSSESSION_CHANGE), so the hook reads
// m_remoteSession without a lock. A session can switch between console and remote while the module
// runs, and nothing held before a switch applies after it.
void CursorBrake::RefreshSessionState()
{
    const bool remote = IsRemoteSession();
    if (remote != m_remoteSession)
    {
        m_remoteSession = remote;
        m_engine = cursorbrake::Engine{};
    }
    Logger::info(remote ? L"CursorBrake stands down: the session is remote." : L"CursorBrake is active: the session is local.");
}

BOOL CALLBACK CursorBrake::AddMonitor(HMONITOR monitor, HDC /*hdc*/, LPRECT /*rect*/, LPARAM data)
{
    auto* enumeration = reinterpret_cast<MonitorEnumeration*>(data);
    MONITORINFOEXW info{};
    info.cbSize = sizeof(MONITORINFOEXW);
    if (!GetMonitorInfoW(monitor, &info))
    {
        return TRUE;
    }

    UINT dpiX = cursorbrake::BASE_DPI;
    UINT dpiY = cursorbrake::BASE_DPI;
    if (FAILED(GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, &dpiX, &dpiY)))
    {
        dpiX = cursorbrake::BASE_DPI;
    }

    const RECT& r = info.rcMonitor;
    enumeration->monitors.push_back(cursorbrake::Monitor{ cursorbrake::RectL{ r.left, r.top, r.right, r.bottom }, dpiX });
    enumeration->handles.push_back(monitor);
    enumeration->devices.push_back(info.szDevice);
    enumeration->primary.push_back((info.dwFlags & MONITORINFOF_PRIMARY) != 0);
    return TRUE;
}

LRESULT CALLBACK CursorBrake::MessageWindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    CursorBrake* instance = g_instance.load();
    if (!instance)
    {
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    switch (msg)
    {
    case WM_DISPLAYCHANGE:
        // A read still running was asked for the layout this change replaces, so its result is
        // dropped rather than applied in the debounce before the rebuild.
        ++instance->m_generation;
        SetTimer(hwnd, TIMER_REBUILD_LAYOUT, DEBOUNCE_DELAY_MS, nullptr);
        break;

    case WM_SETTINGCHANGE:
        if (wParam == SPI_SETWORKAREA)
        {
            SetTimer(hwnd, TIMER_REBUILD_LAYOUT, DEBOUNCE_DELAY_MS, nullptr);
        }
        break;

    case WM_DEVICECHANGE:
        if (wParam == DBT_DEVNODES_CHANGED)
        {
            SetTimer(hwnd, TIMER_REBUILD_LAYOUT, DEBOUNCE_DELAY_MS, nullptr);
            return TRUE;
        }
        break;

    case WM_TIMER:
        if (wParam == TIMER_REBUILD_LAYOUT)
        {
            KillTimer(hwnd, TIMER_REBUILD_LAYOUT);
            instance->m_identityRetries = 0;
            instance->RebuildLayout();
        }
        else if (wParam == TIMER_RETRY_IDENTITY)
        {
            KillTimer(hwnd, TIMER_RETRY_IDENTITY);
            instance->RebuildLayout();
        }
        else if (wParam == TIMER_REJUDGE_FOREGROUND)
        {
            KillTimer(hwnd, TIMER_REJUDGE_FOREGROUND);
            instance->AdoptForeground(GetForegroundWindow(), cursorbrake::ForegroundReport::Query);
        }
        break;

    case WM_APP_EDGES_CHANGED:
        if (instance->TakePendingOverrides())
        {
            instance->ResolveOverrides();
            instance->PublishEdges();
        }
        return 0;

    case WM_APP_SHOW_EDGES:
        if (instance->m_showGate.Accept(std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now().time_since_epoch()).count()))
        {
            instance->ShowEdges();
        }
        else
        {
            Logger::info(L"CursorBrake ignored a request to show the edges that Settings did not make, or that came within a second of the last.");
        }
        return 0;

    case WM_APP_IDENTITIES:
        instance->ApplyIdentities();
        return 0;

    case WM_APP_EXCLUDED_APPS_CHANGED:
        if (instance->TakePendingExcludedApps())
        {
            Logger::info(L"CursorBrake excluded apps applied: {} entries.", instance->m_excludedApps.size());
            instance->AdoptForeground(GetForegroundWindow(), cursorbrake::ForegroundReport::Query);
        }
        return 0;

    case WM_WTSSESSION_CHANGE:
        instance->RefreshSessionState();
        instance->AdoptForeground(GetForegroundWindow(), cursorbrake::ForegroundReport::Query);
        break;
    }

    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

void CALLBACK CursorBrake::ForegroundProc(HWINEVENTHOOK /*hook*/, DWORD /*event*/, HWND hwnd, LONG /*idObject*/, LONG /*idChild*/, DWORD /*thread*/, DWORD /*time*/)
{
    if (CursorBrake* instance = g_instance.load())
    {
        instance->AdoptForeground(hwnd, cursorbrake::ForegroundReport::Event);
    }
}

LRESULT CALLBACK CursorBrake::MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    // Load the singleton once into a local; see the g_instance declaration for why this is safe.
    CursorBrake* instance = g_instance.load();
    if (nCode == HC_ACTION && wParam == WM_MOUSEMOVE && instance)
    {
        if (instance->HandleMove(*reinterpret_cast<const MSLLHOOKSTRUCT*>(lParam)))
        {
            return 1; // Suppress: the cursor stays where SetCursorPos put it.
        }
    }
    return CallNextHookEx(nullptr, nCode, wParam, lParam);
}

// Runs inside the low-level hook, so it must stay fast and must not log.
bool CursorBrake::HandleMove(const MSLLHOOKSTRUCT& data)
{
    const cursorbrake::PointL proposed{ data.pt.x, data.pt.y };

    const cursorbrake::InputTreatment treatment = cursorbrake::ClassifyInput(data.flags, static_cast<uint64_t>(data.dwExtraInfo), m_remoteSession);

    // Inside the hook the cursor has not moved yet, so GetCursorPos is the current point. A reading
    // equal to the proposed point is a zero-length move, which the engine allows. Only when the
    // call fails does the last decided point stand in, because that point misses every SetCursorPos
    // from other modules, such as CursorWrap's wrap.
    cursorbrake::MoveEvent move;
    POINT cursor{};
    move.current = GetCursorPos(&cursor) ? cursorbrake::PointL{ cursor.x, cursor.y } : m_lastPoint;
    move.proposed = proposed;

    // An excluded app in front stands CursorBrake down, walls included. It takes the path of
    // absolute input, which the engine never holds and which resets it.
    bool pass = treatment == cursorbrake::InputTreatment::Pass || m_foreground.Excluded();

    // The foreground is checked for a full-screen app only on a move that proposes a crossing.
    // Every other move carries the last answer, so the engine sees a change on the first crossing
    // after it. No resistance passes a move leaving the full-screen app's monitor, and Wall names
    // that monitor to the engine.
    const auto behavior = static_cast<cursorbrake::FullScreenBehavior>(m_fullScreenBehavior.load());
    if (!pass && behavior != cursorbrake::FullScreenBehavior::Same)
    {
        const std::optional<size_t> source = cursorbrake::MonitorAt(m_layout, move.current);
        const bool crossing = cursorbrake::MonitorAt(m_layout, proposed) != source;
        const std::optional<size_t> fullScreen = m_fullScreen.Observe(crossing, [this] { return FullScreenMonitor(); });
        const cursorbrake::FullScreenTreatment fullScreenTreatment = cursorbrake::TreatFullScreen(behavior, fullScreen, source);
        pass = fullScreenTreatment.pass;
        move.wallMonitor = fullScreenTreatment.wallMonitor;
    }

    if (pass)
    {
        move.input = cursorbrake::InputKind::Absolute;
        (void)m_engine.OnMove(move, m_layout, SettingsSnapshot());
        m_lastPoint = cursorbrake::ClampToClip(proposed, m_engine.Clip());
        return false;
    }

    move.input = cursorbrake::InputKind::Relative;
    const int modifierKey = m_crossModifier.load() == static_cast<int>(cursorbrake::CrossModifier::Shift) ? VK_SHIFT : VK_CONTROL;
    move.crossModifierHeld = (GetAsyncKeyState(modifierKey) & 0x8000) != 0;
    move.buttonDown = ((GetAsyncKeyState(VK_LBUTTON) | GetAsyncKeyState(VK_RBUTTON) | GetAsyncKeyState(VK_MBUTTON) | GetAsyncKeyState(VK_XBUTTON1) | GetAsyncKeyState(VK_XBUTTON2)) & 0x8000) != 0;

    // The hook sees the point before Windows clamps it into a clip, such as a full-screen game's,
    // so the engine needs the clip to judge where the move lands. Like GetCursorPos, it reports
    // physical pixels in the per-monitor-aware host. When the call fails, the move carries no clip
    // and the engine keeps the last one.
    RECT clip{};
    if (GetClipCursor(&clip))
    {
        move.clip = cursorbrake::RectL{ clip.left, clip.top, clip.right, clip.bottom };
    }

    const cursorbrake::Decision decision = m_engine.OnMove(move, m_layout, SettingsSnapshot());
    // An Allow carries the point the hook saw; Windows puts the cursor where the clip clamps it.
    m_lastPoint = decision.kind == cursorbrake::DecisionKind::Allow ? cursorbrake::ClampToClip(decision.point, m_engine.Clip()) : decision.point;
    if (decision.kind != cursorbrake::DecisionKind::Hold)
    {
        return false;
    }

    // SetCursorPos produces no low-level hook event, so nothing from this comes back through the hook.
    SetCursorPos(decision.point.x, decision.point.y);
    return true;
}

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new CursorBrake();
}
