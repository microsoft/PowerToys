// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/logger_helper.h>
#include <common/logger/logger.h>
#include "trace.h"
#include "resource.h"
#include "CursorBrakeCore.h"
#include "ModuleConstants.h"

#include <ShellScalingApi.h>
#include <WtsApi32.h>
#include <dbt.h>

#include <algorithm>
#include <atomic>
#include <cmath>
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

    // Owned by the hook thread: the engine, the layout it reads, and the cursor between callbacks.
    cursorbrake::Engine m_engine;
    cursorbrake::Layout m_layout;
    cursorbrake::PointL m_lastPoint;
    bool m_remoteSession = false;

    // Hook thread + lifecycle.
    HHOOK m_mouseHook = nullptr;
    HWND m_messageWindow = nullptr;
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
    void RefreshSessionState();
    bool HandleMove(const MSLLHOOKSTRUCT& data);

    static BOOL CALLBACK AddMonitor(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data);
    static LRESULT CALLBACK MessageWindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam);

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

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(IDS_CURSORBRAKE_NAME);

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void call_custom_action(const wchar_t* /*action*/) override {}

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

    Logger::info(L"CursorBrake settings applied: push_distance {}, edge_filter {}, cross_modifier {}, disable_while_dragging {}, free_return {}.", m_pushDistance.load(), m_edgeFilter.load(), m_crossModifier.load(), m_disableWhileDragging.load(), m_freeReturn.load());
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
    RebuildLayout();
    RefreshSessionState();

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

    m_messageWindow = CreateWindowExW(WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, DISPLAY_CHANGE_WINDOW_CLASS, nullptr, WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, GetModuleHandle(nullptr), nullptr);
    if (!m_messageWindow)
    {
        Logger::error(L"Failed to create the CursorBrake display change window, error: {}. The layout will not follow display changes.", GetLastError());
        return;
    }

    DEV_BROADCAST_DEVICEINTERFACE filter = {};
    filter.dbcc_size = sizeof(DEV_BROADCAST_DEVICEINTERFACE);
    filter.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
    filter.dbcc_classguid = GUID_DEVINTERFACE_MONITOR;
    m_deviceNotify = RegisterDeviceNotificationW(m_messageWindow, &filter, DEVICE_NOTIFY_WINDOW_HANDLE);
    if (!m_deviceNotify)
    {
        Logger::warn(L"Failed to register CursorBrake for monitor device notifications, error: {}", GetLastError());
    }

    m_sessionNotify = WTSRegisterSessionNotification(m_messageWindow, NOTIFY_FOR_THIS_SESSION);
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
    if (m_sessionNotify)
    {
        WTSUnRegisterSessionNotification(m_messageWindow);
        m_sessionNotify = false;
    }
    if (m_messageWindow)
    {
        KillTimer(m_messageWindow, TIMER_REBUILD_LAYOUT);
        DestroyWindow(m_messageWindow);
        m_messageWindow = nullptr;
        UnregisterClassW(DISPLAY_CHANGE_WINDOW_CLASS, GetModuleHandle(nullptr));
    }
}

// Runs on the hook thread only (at start and from the display change window), so the hook reads
// m_layout without a lock.
void CursorBrake::RebuildLayout()
{
    cursorbrake::Layout layout;
    if (!EnumDisplayMonitors(nullptr, nullptr, AddMonitor, reinterpret_cast<LPARAM>(&layout)))
    {
        Logger::warn(L"EnumDisplayMonitors failed, error: {}. Keeping the previous CursorBrake layout.", GetLastError());
        return;
    }
    m_layout = std::move(layout);
    Logger::info(L"CursorBrake layout rebuilt with {} monitors.", m_layout.monitors.size());
    for (size_t i = 0; i < m_layout.monitors.size(); ++i)
    {
        const cursorbrake::Monitor& monitor = m_layout.monitors[i];
        Logger::info(L"CursorBrake monitor {}: left {} top {} right {} bottom {} dpi {}", i, monitor.rect.left, monitor.rect.top, monitor.rect.right, monitor.rect.bottom, monitor.dpi);
    }
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
    auto* layout = reinterpret_cast<cursorbrake::Layout*>(data);
    MONITORINFO info{};
    info.cbSize = sizeof(MONITORINFO);
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
    layout->monitors.push_back(cursorbrake::Monitor{ cursorbrake::RectL{ r.left, r.top, r.right, r.bottom }, dpiX });
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
            instance->RebuildLayout();
        }
        break;

    case WM_WTSSESSION_CHANGE:
        instance->RefreshSessionState();
        break;
    }

    return DefWindowProcW(hwnd, msg, wParam, lParam);
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

    if (treatment == cursorbrake::InputTreatment::Pass)
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
