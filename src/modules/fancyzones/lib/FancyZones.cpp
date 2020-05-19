#include "pch.h"

#include <common/dpi_aware.h>
#include <common/on_thread_executor.h>
#include <common/window_helpers.h>

#include <common/notifications.h>
#include <common/notifications/fancyzones_notifications.h>
#include <common/window_helpers.h>

#include "FancyZones.h"
#include "lib/Settings.h"
#include "lib/ZoneWindow.h"
#include "lib/JsonHelpers.h"
#include "lib/ZoneSet.h"
#include "lib/WindowMoveHandler.h"
#include "lib/FancyZonesWinHookEventIDs.h"
#include "lib/util.h"
#include "trace.h"
#include "VirtualDesktopUtils.h"

#include <interface/win_hook_event_data.h>
#include <common\notifications.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;


enum class DisplayChangeType
{
    WorkArea,
    DisplayChange,
    VirtualDesktop,
    Initialization
};

namespace std
{
    template<>
    struct hash<GUID>
    {
        size_t operator()(const GUID& Value) const
        {
            RPC_STATUS status = RPC_S_OK;
            return ::UuidHash(&const_cast<GUID&>(Value), &status);
        }
    };
}

struct FancyZones : public winrt::implements<FancyZones, IFancyZones, IFancyZonesCallback, IZoneWindowHost>
{
public:
    FancyZones(HINSTANCE hinstance, const winrt::com_ptr<IFancyZonesSettings>& settings) noexcept :
        m_hinstance(hinstance),
        m_settings(settings),
        m_windowMoveHandler(settings)
    {
        m_settings->SetCallback(this);
    }

    // IFancyZones
    IFACEMETHODIMP_(void)
    Run() noexcept;
    IFACEMETHODIMP_(void)
    Destroy() noexcept;

    // IFancyZonesCallback
    IFACEMETHODIMP_(bool)
    InMoveSize() noexcept
    {
        std::shared_lock readLock(m_lock);
        return m_windowMoveHandler.InMoveSize();
    }

    void MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen) noexcept
    {
        std::unique_lock writeLock(m_lock);
        m_windowMoveHandler.MoveSizeStart(window, monitor, ptScreen, m_zoneWindowMap);
    }
    
    void MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen) noexcept
    {
        std::unique_lock writeLock(m_lock);
        m_windowMoveHandler.MoveSizeUpdate(monitor, ptScreen, m_zoneWindowMap);
    }
    
    void MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept
    {
        std::unique_lock writeLock(m_lock);
        m_windowMoveHandler.MoveSizeEnd(window, ptScreen, m_zoneWindowMap);
    }
    IFACEMETHODIMP_(void)
    HandleWinHookEvent(const WinHookEvent* data) noexcept
    {
        const auto wparam = reinterpret_cast<WPARAM>(data->hwnd);
        const LONG lparam = 0;
        std::shared_lock readLock(m_lock);
        switch (data->event)
        {
        case EVENT_SYSTEM_MOVESIZESTART:
            PostMessageW(m_window, WM_PRIV_MOVESIZESTART, wparam, lparam);
            break;
        case EVENT_SYSTEM_MOVESIZEEND:
            PostMessageW(m_window, WM_PRIV_MOVESIZEEND, wparam, lparam);
            break;
        case EVENT_OBJECT_LOCATIONCHANGE:
            PostMessageW(m_window, WM_PRIV_LOCATIONCHANGE, wparam, lparam);
            break;
        case EVENT_OBJECT_NAMECHANGE:
            PostMessageW(m_window, WM_PRIV_NAMECHANGE, wparam, lparam);
            break;

        case EVENT_OBJECT_UNCLOAKED:
        case EVENT_OBJECT_SHOW:
        case EVENT_OBJECT_CREATE:
            if (data->idObject == OBJID_WINDOW)
            {
                PostMessageW(m_window, WM_PRIV_WINDOWCREATED, wparam, lparam);
            }
            break;
        }
    }

    IFACEMETHODIMP_(void)
    VirtualDesktopChanged() noexcept;
    IFACEMETHODIMP_(void)
    VirtualDesktopInitialize() noexcept;
    IFACEMETHODIMP_(bool)
    OnKeyDown(PKBDLLHOOKSTRUCT info) noexcept;
    IFACEMETHODIMP_(void)
    ToggleEditor() noexcept;
    IFACEMETHODIMP_(void)
    SettingsChanged() noexcept;
    IFACEMETHODIMP_(void)
    UpdateHotKeys() noexcept;
    
    void WindowCreated(HWND window) noexcept;

    // IZoneWindowHost
    IFACEMETHODIMP_(void)
    MoveWindowsOnActiveZoneSetChange() noexcept;
    IFACEMETHODIMP_(COLORREF)
    GetZoneColor() noexcept
    {
        // Skip the leading # and convert to long
        const auto color = m_settings->GetSettings()->zoneColor;
        const auto tmp = std::stol(color.substr(1), nullptr, 16);
        const auto nR = (tmp & 0xFF0000) >> 16;
        const auto nG = (tmp & 0xFF00) >> 8;
        const auto nB = (tmp & 0xFF);
        return RGB(nR, nG, nB);
    }
    IFACEMETHODIMP_(COLORREF)
    GetZoneBorderColor() noexcept
    {
        // Skip the leading # and convert to long
        const auto color = m_settings->GetSettings()->zoneBorderColor;
        const auto tmp = std::stol(color.substr(1), nullptr, 16);
        const auto nR = (tmp & 0xFF0000) >> 16;
        const auto nG = (tmp & 0xFF00) >> 8;
        const auto nB = (tmp & 0xFF);
        return RGB(nR, nG, nB);
    }
    IFACEMETHODIMP_(COLORREF)
    GetZoneHighlightColor() noexcept
    {
        // Skip the leading # and convert to long
        const auto color = m_settings->GetSettings()->zoneHightlightColor;
        const auto tmp = std::stol(color.substr(1), nullptr, 16);
        const auto nR = (tmp & 0xFF0000) >> 16;
        const auto nG = (tmp & 0xFF00) >> 8;
        const auto nB = (tmp & 0xFF);
        return RGB(nR, nG, nB);
    }
    IFACEMETHODIMP_(IZoneWindow*)
    GetParentZoneWindow(HMONITOR monitor) noexcept
    {
        //NOTE: as public method it's unsafe without lock, but it's called from AddZoneWindow through making ZoneWindow that causes deadlock
        //TODO: needs refactoring
        auto it = m_zoneWindowMap.find(monitor);
        if (it != m_zoneWindowMap.end())
        {
            return it->second.get();
        }
        return nullptr;
    }
    IFACEMETHODIMP_(int)
    GetZoneHighlightOpacity() noexcept
    {
        return m_settings->GetSettings()->zoneHighlightOpacity;
    }

    IFACEMETHODIMP_(bool)
    isMakeDraggedWindowTransparentActive() noexcept
    {
        return m_settings->GetSettings()->makeDraggedWindowTransparent;
    }

    LRESULT WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;
    void OnDisplayChange(DisplayChangeType changeType) noexcept;
    void AddZoneWindow(HMONITOR monitor, PCWSTR deviceId) noexcept;

protected:
    static LRESULT CALLBACK s_WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;

private:
    struct require_read_lock
    {
        template<typename T>
        require_read_lock(const std::shared_lock<T>& lock)
        {
            lock;
        }

        template<typename T>
        require_read_lock(const std::unique_lock<T>& lock)
        {
            lock;
        }
    };

    struct require_write_lock
    {
        template<typename T>
        require_write_lock(const std::unique_lock<T>& lock)
        {
            lock;
        }
    };

    void UpdateZoneWindows() noexcept;
    void UpdateWindowsPositions() noexcept;
    void CycleActiveZoneSet(DWORD vkCode) noexcept;
    bool OnSnapHotkey(DWORD vkCode) noexcept;

    void RegisterVirtualDesktopUpdates(std::vector<GUID>& ids) noexcept;
    void RegisterNewWorkArea(GUID virtualDesktopId, HMONITOR monitor) noexcept;
    bool IsNewWorkArea(GUID virtualDesktopId, HMONITOR monitor) noexcept;

    void OnEditorExitEvent() noexcept;

    std::vector<std::pair<HMONITOR, RECT>> GetRawMonitorData() noexcept;
    std::vector<HMONITOR> GetMonitorsSorted() noexcept;

    const HINSTANCE m_hinstance{};

    mutable std::shared_mutex m_lock;
    HWND m_window{};
    WindowMoveHandler m_windowMoveHandler;

    std::map<HMONITOR, winrt::com_ptr<IZoneWindow>> m_zoneWindowMap; // Map of monitor to ZoneWindow (one per monitor)
    winrt::com_ptr<IFancyZonesSettings> m_settings{};
    GUID m_currentVirtualDesktopId{}; // UUID of the current virtual desktop. Is GUID_NULL until first VD switch per session.
    std::unordered_map<GUID, std::vector<HMONITOR>> m_processedWorkAreas; // Work area is defined by monitor and virtual desktop id.
    wil::unique_handle m_terminateEditorEvent; // Handle of FancyZonesEditor.exe we launch and wait on
    wil::unique_handle m_terminateVirtualDesktopTrackerEvent;

    OnThreadExecutor m_dpiUnawareThread;
    OnThreadExecutor m_virtualDesktopTrackerThread;

    static UINT WM_PRIV_VD_INIT; // Scheduled when FancyZones is initialized
    static UINT WM_PRIV_VD_SWITCH; // Scheduled when virtual desktop switch occurs
    static UINT WM_PRIV_VD_UPDATE; // Scheduled on virtual desktops update (creation/deletion)
    static UINT WM_PRIV_EDITOR; // Scheduled when the editor exits

    static UINT WM_PRIV_LOWLEVELKB; // Scheduled when we receive a key down press

    // Did we terminate the editor or was it closed cleanly?
    enum class EditorExitKind : byte
    {
        Exit,
        Terminate
    };
};

UINT FancyZones::WM_PRIV_VD_INIT = RegisterWindowMessage(L"{469818a8-00fa-4069-b867-a1da484fcd9a}");
UINT FancyZones::WM_PRIV_VD_SWITCH = RegisterWindowMessage(L"{128c2cb0-6bdf-493e-abbe-f8705e04aa95}");
UINT FancyZones::WM_PRIV_VD_UPDATE = RegisterWindowMessage(L"{b8b72b46-f42f-4c26-9e20-29336cf2f22e}");
UINT FancyZones::WM_PRIV_EDITOR = RegisterWindowMessage(L"{87543824-7080-4e91-9d9c-0404642fc7b6}");
UINT FancyZones::WM_PRIV_LOWLEVELKB = RegisterWindowMessage(L"{763c03a3-03d9-4cde-8d71-f0358b0b4b52}");

// IFancyZones
IFACEMETHODIMP_(void)
FancyZones::Run() noexcept
{
    std::unique_lock writeLock(m_lock);

    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = s_WndProc;
    wcex.hInstance = m_hinstance;
    wcex.lpszClassName = L"SuperFancyZones";
    RegisterClassExW(&wcex);

    BufferedPaintInit();

    m_window = CreateWindowExW(WS_EX_TOOLWINDOW, L"SuperFancyZones", L"", WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, m_hinstance, this);
    if (!m_window)
        return;

    RegisterHotKey(m_window, 1, m_settings->GetSettings()->editorHotkey.get_modifiers(), m_settings->GetSettings()->editorHotkey.get_code());



    VirtualDesktopInitialize();

    m_dpiUnawareThread.submit(OnThreadExecutor::task_t{ [] {
                          SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_UNAWARE);
                          SetThreadDpiHostingBehavior(DPI_HOSTING_BEHAVIOR_MIXED);
                      } })
        .wait();

    m_terminateVirtualDesktopTrackerEvent.reset(CreateEvent(nullptr, FALSE, FALSE, nullptr));
    m_virtualDesktopTrackerThread.submit(OnThreadExecutor::task_t{ [&] { VirtualDesktopUtils::HandleVirtualDesktopUpdates(m_window, WM_PRIV_VD_UPDATE, m_terminateVirtualDesktopTrackerEvent.get()); } });
}

// IFancyZones
IFACEMETHODIMP_(void)
FancyZones::Destroy() noexcept
{
    std::unique_lock writeLock(m_lock);
    m_zoneWindowMap.clear();
    BufferedPaintUnInit();
    if (m_window)
    {
        DestroyWindow(m_window);
        m_window = nullptr;
    }
    if (m_terminateVirtualDesktopTrackerEvent)
    {
        SetEvent(m_terminateVirtualDesktopTrackerEvent.get());
    }
}

// IFancyZonesCallback
IFACEMETHODIMP_(void)
FancyZones::VirtualDesktopChanged() noexcept
{
    // VirtualDesktopChanged is called from a reentrant WinHookProc function, therefore we must postpone the actual logic
    // until we're in FancyZones::WndProc, which is not reentrant.
    std::shared_lock readLock(m_lock);
    PostMessage(m_window, WM_PRIV_VD_SWITCH, 0, 0);
}

// IFancyZonesCallback
IFACEMETHODIMP_(void)
FancyZones::VirtualDesktopInitialize() noexcept
{
    PostMessage(m_window, WM_PRIV_VD_INIT, 0, 0);
}

// IFancyZonesCallback
IFACEMETHODIMP_(void)
FancyZones::WindowCreated(HWND window) noexcept
{
    std::shared_lock readLock(m_lock);
    if (m_settings->GetSettings()->appLastZone_moveWindows && IsInterestingWindow(window, m_settings->GetSettings()->excludedAppsArray))
    {
        for (const auto& [monitor, zoneWindow] : m_zoneWindowMap)
        {
            // WindowCreated is also invoked when a virtual desktop switch occurs, we need a way
            // to figure out when that happens to avoid moving windows that should not be moved.
            GUID windowDesktopId{};
            GUID zoneWindowDesktopId{};
            if (VirtualDesktopUtils::GetWindowDesktopId(window, &windowDesktopId) &&
                VirtualDesktopUtils::GetZoneWindowDesktopId(zoneWindow.get(), &zoneWindowDesktopId) &&
                (windowDesktopId != zoneWindowDesktopId))
            {
                return;
            }
            const auto activeZoneSet = zoneWindow->ActiveZoneSet();
            if (activeZoneSet)
            {
                const auto& fancyZonesData = JSONHelpers::FancyZonesDataInstance();

                wil::unique_cotaskmem_string guidString;
                if (SUCCEEDED(StringFromCLSID(activeZoneSet->Id(), &guidString)))
                {
                    int zoneIndex = fancyZonesData.GetAppLastZoneIndex(window, zoneWindow->UniqueId(), guidString.get());
                    if (zoneIndex != -1)
                    {
                        m_windowMoveHandler.MoveWindowIntoZoneByIndex(window, monitor, zoneIndex, m_zoneWindowMap);
                        break;
                    }
                }
            }
        }
    }
}

// IFancyZonesCallback
IFACEMETHODIMP_(bool)
FancyZones::OnKeyDown(PKBDLLHOOKSTRUCT info) noexcept
{
    // Return true to swallow the keyboard event
    bool const shift = GetAsyncKeyState(VK_SHIFT) & 0x8000;
    bool const win = GetAsyncKeyState(VK_LWIN) & 0x8000 || GetAsyncKeyState(VK_RWIN) & 0x8000;
    if (win && !shift)
    {
        bool const ctrl = GetAsyncKeyState(VK_CONTROL) & 0x8000;
        if (ctrl)
        {
            // Temporarily disable Win+Ctrl+Number functionality
            //if ((info->vkCode >= '0') && (info->vkCode <= '9'))
            //{
            //    // Win+Ctrl+Number will cycle through ZoneSets
            //    Trace::FancyZones::OnKeyDown(info->vkCode, win, ctrl, false /*inMoveSize*/);
            //    CycleActiveZoneSet(info->vkCode);
            //    return true;
            //}
        }
        else if ((info->vkCode == VK_RIGHT) || (info->vkCode == VK_LEFT))
        {
            if (m_settings->GetSettings()->overrideSnapHotkeys)
            {
                Trace::FancyZones::OnKeyDown(info->vkCode, win, ctrl, false /*inMoveSize*/);
                // Win+Left, Win+Right will cycle through Zones in the active ZoneSet when WM_PRIV_LOWLEVELKB's handled
                PostMessageW(m_window, WM_PRIV_LOWLEVELKB, 0, info->vkCode);
                return true;
            }
        }
    }
    // Temporarily disable Win+Ctrl+Number functionality
    //else if (m_inMoveSize && (info->vkCode >= '0') && (info->vkCode <= '9'))
    //{
    //    // This allows you to cycle through ZoneSets while dragging a window
    //    Trace::FancyZones::OnKeyDown(info->vkCode, win, false /*control*/, true /*inMoveSize*/);
    //    CycleActiveZoneSet(info->vkCode);
    //    return false;
    //}

    if (m_windowMoveHandler.IsDragEnabled() && shift)
    {
        return true;
    }
    return false;
}

void FancyZones::UpdateHotKeys() noexcept
{
    auto& fancyZonesData = JSONHelpers::FancyZonesDataInstance();
    auto fancyZoneDataJson = fancyZonesData.GetPersistFancyZonesJSON();
    json::JsonArray customZonesJson = fancyZoneDataJson.GetNamedArray(L"custom-zone-sets");

    uint32_t size = customZonesJson.Size();
    for (uint32_t i = 0; i < size; ++i)
    {
        json::JsonObject customZoneJson = customZonesJson.GetObjectAt(i);
        const int eventid = static_cast<int>(customZoneJson.GetNamedNumber(L"eventid"));
        const int keyid = static_cast<int>(customZoneJson.GetNamedNumber(L"keyid"));
        UnregisterHotKey(m_window, eventid);
        // TODO: allow the user to set their own hotkeys and not hardcode it to alt + letter/number
        RegisterHotKey(m_window, eventid, MOD_ALT | MOD_NOREPEAT, keyid + 48);
    }

}

// IFancyZonesCallback
void FancyZones::ToggleEditor() noexcept
{
    {
        std::shared_lock readLock(m_lock);
        if (m_terminateEditorEvent)
        {
            SetEvent(m_terminateEditorEvent.get());
            return;
        }
    }

    {
        std::unique_lock writeLock(m_lock);
        m_terminateEditorEvent.reset(CreateEvent(nullptr, true, false, nullptr));
    }

    UpdateHotKeys(); // TODO: call the update hotkey function on zone-settings.json change event
    HMONITOR monitor{};
    HWND foregroundWindow{};

    const bool use_cursorpos_editor_startupscreen = m_settings->GetSettings()->use_cursorpos_editor_startupscreen;
    POINT currentCursorPos{};
    if (use_cursorpos_editor_startupscreen)
    {
        GetCursorPos(&currentCursorPos);
        monitor = MonitorFromPoint(currentCursorPos, MONITOR_DEFAULTTOPRIMARY);
    }
    else
    {
        foregroundWindow = GetForegroundWindow();
        monitor = MonitorFromWindow(foregroundWindow, MONITOR_DEFAULTTOPRIMARY);
    }

    if (!monitor)
    {
        return;
    }

    std::shared_lock readLock(m_lock);
    auto iter = m_zoneWindowMap.find(monitor);
    if (iter == m_zoneWindowMap.end())
    {
        return;
    }

    MONITORINFOEX mi;
    mi.cbSize = sizeof(mi);

    m_dpiUnawareThread.submit(OnThreadExecutor::task_t{ [&] {
                          GetMonitorInfo(monitor, &mi);
                      } })
        .wait();

    auto zoneWindow = iter->second;

    const auto& fancyZonesData = JSONHelpers::FancyZonesDataInstance();
    fancyZonesData.CustomZoneSetsToJsonFile(ZoneWindowUtils::GetCustomZoneSetsTmpPath());

    // Do not scale window params by the dpi, that will be done in the editor - see LayoutModel.Apply
    const auto taskbar_x_offset = mi.rcWork.left - mi.rcMonitor.left;
    const auto taskbar_y_offset = mi.rcWork.top - mi.rcMonitor.top;
    const auto x = mi.rcMonitor.left + taskbar_x_offset;
    const auto y = mi.rcMonitor.top + taskbar_y_offset;
    const auto width = mi.rcWork.right - mi.rcWork.left;
    const auto height = mi.rcWork.bottom - mi.rcWork.top;
    const std::wstring editorLocation =
        std::to_wstring(x) + L"_" +
        std::to_wstring(y) + L"_" +
        std::to_wstring(width) + L"_" +
        std::to_wstring(height);

    const auto deviceInfo = fancyZonesData.FindDeviceInfo(zoneWindow->UniqueId());
    if (!deviceInfo.has_value())
    {
        return;
    }

    JSONHelpers::DeviceInfoJSON deviceInfoJson{ zoneWindow->UniqueId(), *deviceInfo };
    fancyZonesData.SerializeDeviceInfoToTmpFile(deviceInfoJson, ZoneWindowUtils::GetActiveZoneSetTmpPath());

    const std::wstring params =
        /*1*/ std::to_wstring(reinterpret_cast<UINT_PTR>(monitor)) + L" " +
        /*2*/ editorLocation + L" " +
        /*3*/ zoneWindow->WorkAreaKey() + L" " +
        /*4*/ L"\"" + ZoneWindowUtils::GetActiveZoneSetTmpPath() + L"\" " +
        /*5*/ L"\"" + ZoneWindowUtils::GetAppliedZoneSetTmpPath() + L"\" " +
        /*6*/ L"\"" + ZoneWindowUtils::GetCustomZoneSetsTmpPath() + L"\"";

    SHELLEXECUTEINFO sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
    sei.lpFile = L"modules\\FancyZones\\FancyZonesEditor.exe";
    sei.lpParameters = params.c_str();
    sei.nShow = SW_SHOWNORMAL;
    ShellExecuteEx(&sei);
    Trace::FancyZones::EditorLaunched(1);

    // Launch the editor on a background thread
    // Wait for the editor's process to exit
    // Post back to the main thread to update
    std::thread waitForEditorThread([window = m_window, processHandle = sei.hProcess, terminateEditorEvent = m_terminateEditorEvent.get()]() {
        HANDLE waitEvents[2] = { processHandle, terminateEditorEvent };
        auto result = WaitForMultipleObjects(2, waitEvents, false, INFINITE);
        if (result == WAIT_OBJECT_0 + 0)
        {
            // Editor exited
            // Update any changes it may have made
            PostMessage(window, WM_PRIV_EDITOR, 0, static_cast<LPARAM>(EditorExitKind::Exit));
        }
        else if (result == WAIT_OBJECT_0 + 1)
        {
            // User hit Win+~ while editor is already running
            // Shut it down
            TerminateProcess(processHandle, 2);
            PostMessage(window, WM_PRIV_EDITOR, 0, static_cast<LPARAM>(EditorExitKind::Terminate));
        }
        CloseHandle(processHandle);
    });

    waitForEditorThread.detach();
}

void FancyZones::SettingsChanged() noexcept
{
    std::shared_lock readLock(m_lock);
    // Update the hotkey
    UnregisterHotKey(m_window, 1);
    RegisterHotKey(m_window, 1, m_settings->GetSettings()->editorHotkey.get_modifiers(), m_settings->GetSettings()->editorHotkey.get_code());
}

// IZoneWindowHost
IFACEMETHODIMP_(void)
FancyZones::MoveWindowsOnActiveZoneSetChange() noexcept
{
    if (m_settings->GetSettings()->zoneSetChange_moveWindows)
    {
        UpdateWindowsPositions();
    }
}

LRESULT FancyZones::WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_HOTKEY:
    {
        if (wparam == 1)
        {
            ToggleEditor();
        }

        // Check for custom zone hotkeys
        auto& fancyZonesData = JSONHelpers::FancyZonesDataInstance();
        auto fancyZoneDataJson = fancyZonesData.GetPersistFancyZonesJSON();
        json::JsonArray customZonesJson = fancyZoneDataJson.GetNamedArray(L"custom-zone-sets");
        uint32_t size = customZonesJson.Size();

        for (int i = 0; i < size ; i++)
        {

                    json::JsonObject customZoneJson = customZonesJson.GetObjectAt(i);
                    const std::wstring newLayout = static_cast<std::wstring>(customZoneJson.GetNamedString(L"uuid"));
                    const int refWidth = static_cast<int>(customZoneJson.GetNamedObject(L"info").GetNamedNumber(L"ref-width"));
                    const int refHeight = static_cast<int>(customZoneJson.GetNamedObject(L"info").GetNamedNumber(L"ref-height"));
                    const int eventID = static_cast<int>(customZoneJson.GetNamedNumber(L"eventid"));

                    if (wparam == eventID)
                    {
                        // Grab the monitor
                        POINT currentCursorPos{};
                        HMONITOR monitor{};
                        GetCursorPos(&currentCursorPos);
                        monitor = MonitorFromPoint(currentCursorPos, MONITOR_DEFAULTTOPRIMARY);

                        // Grab actual monitor height/width
                        MONITORINFOEX mi;
                        mi.cbSize = sizeof(mi);

                        m_dpiUnawareThread.submit(OnThreadExecutor::task_t{ [&] {
                                              GetMonitorInfo(monitor, &mi);
                                          } })
                            .wait();

                        const auto actualWidth = mi.rcWork.right - mi.rcWork.left;
                        const auto actualHeight = mi.rcWork.bottom - mi.rcWork.top;

                        // Send notification if monitor is diffrent
                        if (actualHeight != refHeight || actualWidth != refWidth)
                        {
                            // TODO: update resource strings to show respective messages
                            std::vector<notifications::action_t> actions = {
                                notifications::link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_LEARN_MORE), L"TODO" },
                                notifications::link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_DIALOG_DONT_SHOW_AGAIN), L"TODO" }
                            };
                            notifications::show_toast_with_activations(L"WARNING: the fancy zone you have chosen may not be applied as you intended. Resolutions do not match!", {}, std::move(actions));
                        }

                        // Apply the desired layout
                        auto iter = m_zoneWindowMap.find(monitor);
                        auto zoneWindow = iter->second;
                        auto deviceInfo = fancyZonesData.FindDeviceInfo(zoneWindow->UniqueId());
                        JSONHelpers::ZoneSetData newZoneData{ newLayout, JSONHelpers::ZoneSetLayoutType::Custom };
                        deviceInfo->activeZoneSet = newZoneData;
                        JSONHelpers::DeviceInfoJSON deviceInfoJson{ zoneWindow->UniqueId(), *deviceInfo };

                        fancyZonesData.SerializeDeviceInfoToTmpFile(deviceInfoJson, ZoneWindowUtils::GetActiveZoneSetTmpPath());
                        fancyZonesData.SetActiveZoneSet(zoneWindow->UniqueId(), newZoneData);
                        fancyZonesData.SaveFancyZonesData();
                        OnEditorExitEvent();
                    }
        }
    }
    break;

    case WM_SETTINGCHANGE:
    {
        if (wparam == SPI_SETWORKAREA)
        {
            OnDisplayChange(DisplayChangeType::WorkArea);
        }
    }
    break;

    case WM_DISPLAYCHANGE:
    {
        OnDisplayChange(DisplayChangeType::DisplayChange);
    }
    break;

    default:
    {
        POINT ptScreen;
        GetPhysicalCursorPos(&ptScreen);

        if (message == WM_PRIV_LOWLEVELKB)
        {
            OnSnapHotkey(static_cast<DWORD>(lparam));
        }
        else if (message == WM_PRIV_VD_INIT)
        {
            OnDisplayChange(DisplayChangeType::Initialization);
        }
        else if (message == WM_PRIV_VD_SWITCH)
        {
            OnDisplayChange(DisplayChangeType::VirtualDesktop);
        }
        else if (message == WM_PRIV_VD_UPDATE)
        {
            std::vector<GUID> ids{};
            if (VirtualDesktopUtils::GetVirtualDesktopIds(ids))
            {
                RegisterVirtualDesktopUpdates(ids);
            }
        }
        else if (message == WM_PRIV_EDITOR)
        {
            if (lparam == static_cast<LPARAM>(EditorExitKind::Exit))
            {
                OnEditorExitEvent();
            }

            {
                // Clean up the event either way
                std::unique_lock writeLock(m_lock);
                m_terminateEditorEvent.release();
            }
        }
        else if (message == WM_PRIV_MOVESIZESTART)
        {
            auto hwnd = reinterpret_cast<HWND>(wparam);
            if (auto monitor = MonitorFromPoint(ptScreen, MONITOR_DEFAULTTONULL))
            {
                MoveSizeStart(hwnd, monitor, ptScreen);
            }
        }
        else if (message == WM_PRIV_MOVESIZEEND)
        {
            auto hwnd = reinterpret_cast<HWND>(wparam);
            MoveSizeEnd(hwnd, ptScreen);
        }
        else if (message == WM_PRIV_LOCATIONCHANGE && InMoveSize())
        {
            if (auto monitor = MonitorFromPoint(ptScreen, MONITOR_DEFAULTTONULL))
            {
                MoveSizeUpdate(monitor, ptScreen);
            }
        }
        else if (message == WM_PRIV_WINDOWCREATED)
        {
            auto hwnd = reinterpret_cast<HWND>(wparam);
            WindowCreated(hwnd);
        }
        else
        {
            return DefWindowProc(window, message, wparam, lparam);
        }
    }
    break;
    }
    return 0;
}

void FancyZones::OnDisplayChange(DisplayChangeType changeType) noexcept
{
    if (changeType == DisplayChangeType::VirtualDesktop ||
        changeType == DisplayChangeType::Initialization)
    {
        GUID currentVirtualDesktopId{};
        if (VirtualDesktopUtils::GetCurrentVirtualDesktopId(&currentVirtualDesktopId))
        {
            m_currentVirtualDesktopId = currentVirtualDesktopId;
        }
        if (changeType == DisplayChangeType::Initialization)
        {
            std::vector<std::wstring> ids{};
            if (VirtualDesktopUtils::GetVirtualDesktopIds(ids) && !ids.empty())
            {
                JSONHelpers::FancyZonesDataInstance().UpdatePrimaryDesktopData(ids[0]);
                JSONHelpers::FancyZonesDataInstance().RemoveDeletedDesktops(ids);
            }
        }
    }

    UpdateZoneWindows();

    if ((changeType == DisplayChangeType::WorkArea) || (changeType == DisplayChangeType::DisplayChange))
    {
        if (m_settings->GetSettings()->displayChange_moveWindows)
        {
            UpdateWindowsPositions();
        }
    }
    else if (changeType == DisplayChangeType::VirtualDesktop)
    {
        if (m_settings->GetSettings()->virtualDesktopChange_moveWindows)
        {
            UpdateWindowsPositions();
        }
    }
}

void FancyZones::AddZoneWindow(HMONITOR monitor, PCWSTR deviceId) noexcept
{
    std::unique_lock writeLock(m_lock);
    wil::unique_cotaskmem_string virtualDesktopId;
    if (SUCCEEDED_LOG(StringFromCLSID(m_currentVirtualDesktopId, &virtualDesktopId)))
    {
        std::wstring uniqueId = ZoneWindowUtils::GenerateUniqueId(monitor, deviceId, virtualDesktopId.get());
        JSONHelpers::FancyZonesDataInstance().SetActiveDeviceId(uniqueId);

        const bool newWorkArea = IsNewWorkArea(m_currentVirtualDesktopId, monitor);
        // "Turning FLASHING_ZONE option off"
        //const bool flash = m_settings->GetSettings()->zoneSetChange_flashZones && newWorkArea;
        const bool flash = false;

        auto zoneWindow = MakeZoneWindow(this, m_hinstance, monitor, uniqueId, flash, newWorkArea);
        if (zoneWindow)
        {
            m_zoneWindowMap[monitor] = std::move(zoneWindow);
        }

        if (newWorkArea)
        {
            RegisterNewWorkArea(m_currentVirtualDesktopId, monitor);
            JSONHelpers::FancyZonesDataInstance().SaveFancyZonesData();
        }
    }
}

LRESULT CALLBACK FancyZones::s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    auto thisRef = reinterpret_cast<FancyZones*>(GetWindowLongPtr(window, GWLP_USERDATA));
    if (!thisRef && (message == WM_CREATE))
    {
        const auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
        thisRef = reinterpret_cast<FancyZones*>(createStruct->lpCreateParams);
        SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
    }

    return thisRef ? thisRef->WndProc(window, message, wparam, lparam) :
                     DefWindowProc(window, message, wparam, lparam);
}

void FancyZones::UpdateZoneWindows() noexcept
{
    auto callback = [](HMONITOR monitor, HDC, RECT*, LPARAM data) -> BOOL {
        MONITORINFOEX mi;
        mi.cbSize = sizeof(mi);
        if (GetMonitorInfo(monitor, &mi))
        {
            DISPLAY_DEVICE displayDevice = { sizeof(displayDevice) };
            PCWSTR deviceId = nullptr;

            bool validMonitor = true;
            if (EnumDisplayDevices(mi.szDevice, 0, &displayDevice, 1))
            {
                if (WI_IsFlagSet(displayDevice.StateFlags, DISPLAY_DEVICE_MIRRORING_DRIVER))
                {
                    validMonitor = FALSE;
                }
                else if (displayDevice.DeviceID[0] != L'\0')
                {
                    deviceId = displayDevice.DeviceID;
                }
            }

            if (validMonitor)
            {
                if (!deviceId)
                {
                    deviceId = GetSystemMetrics(SM_REMOTESESSION) ?
                                   L"\\\\?\\DISPLAY#REMOTEDISPLAY#" :
                                   L"\\\\?\\DISPLAY#LOCALDISPLAY#";
                }

                auto strongThis = reinterpret_cast<FancyZones*>(data);
                strongThis->AddZoneWindow(monitor, deviceId);
            }
        }
        return TRUE;
    };

    EnumDisplayMonitors(nullptr, nullptr, callback, reinterpret_cast<LPARAM>(this));
}

void FancyZones::UpdateWindowsPositions() noexcept
{
    auto callback = [](HWND window, LPARAM data) -> BOOL {
        int i = static_cast<int>(reinterpret_cast<UINT_PTR>(::GetProp(window, ZONE_STAMP)));
        if (i != 0)
        {
            // i is off by 1 since 0 is special.
            auto strongThis = reinterpret_cast<FancyZones*>(data);
            std::unique_lock writeLock(strongThis->m_lock);
            strongThis->m_windowMoveHandler.MoveWindowIntoZoneByIndex(window, nullptr, i - 1, strongThis->m_zoneWindowMap);
        }
        return TRUE;
    };
    EnumWindows(callback, reinterpret_cast<LPARAM>(this));
}

void FancyZones::CycleActiveZoneSet(DWORD vkCode) noexcept
{
    auto window = GetForegroundWindow();
    if (IsInterestingWindow(window, m_settings->GetSettings()->excludedAppsArray))
    {
        const HMONITOR monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
        if (monitor)
        {
            std::shared_lock readLock(m_lock);

            auto iter = m_zoneWindowMap.find(monitor);
            if (iter != m_zoneWindowMap.end())
            {
                const auto& zoneWindowPtr = iter->second;
                zoneWindowPtr->CycleActiveZoneSet(vkCode);
            }
        }
    }
}

bool FancyZones::OnSnapHotkey(DWORD vkCode) noexcept
{
    auto window = GetForegroundWindow();
    if (IsInterestingWindow(window, m_settings->GetSettings()->excludedAppsArray))
    {
        const HMONITOR current = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
        if (current)
        {
            std::vector<HMONITOR> monitorInfo = GetMonitorsSorted();
            if (monitorInfo.size() > 1 && m_settings->GetSettings()->moveWindowAcrossMonitors)
            {
                // Multi monitor environment.
                auto currMonitorInfo = std::find(std::begin(monitorInfo), std::end(monitorInfo), current);
                do
                {
                    std::unique_lock writeLock(m_lock);
                    if (m_windowMoveHandler.MoveWindowIntoZoneByDirection(*currMonitorInfo, window, vkCode, false /* cycle through zones */, m_zoneWindowMap))
                    {
                        return true;
                    }
                    // We iterated through all zones in current monitor zone layout, move on to next one (or previous depending on direction).
                    if (vkCode == VK_RIGHT)
                    {
                        currMonitorInfo = std::next(currMonitorInfo);
                        if (currMonitorInfo == std::end(monitorInfo))
                        {
                            currMonitorInfo = std::begin(monitorInfo);
                        }
                    }
                    else if (vkCode == VK_LEFT)
                    {
                        if (currMonitorInfo == std::begin(monitorInfo))
                        {
                            currMonitorInfo = std::end(monitorInfo);
                        }
                        currMonitorInfo = std::prev(currMonitorInfo);
                    }
                } while (*currMonitorInfo != current);
            }
            else
            {
                // Single monitor environment.
                std::unique_lock writeLock(m_lock);
                return m_windowMoveHandler.MoveWindowIntoZoneByDirection(current, window, vkCode, true /* cycle through zones */, m_zoneWindowMap);
            }
        }
    }
    return false;
}

void FancyZones::RegisterVirtualDesktopUpdates(std::vector<GUID>& ids) noexcept
{
    std::unordered_set<GUID> activeVirtualDesktops(std::begin(ids), std::end(ids));
    std::unique_lock writeLock(m_lock);
    bool modified{ false };
    for (auto it = std::begin(m_processedWorkAreas); it != std::end(m_processedWorkAreas);)
    {
        auto iter = activeVirtualDesktops.find(it->first);
        if (iter == activeVirtualDesktops.end())
        {
            // if we couldn't find the GUID in currentVirtualDesktopIds, we must remove it from both m_processedWorkAreas and deviceInfoMap
            wil::unique_cotaskmem_string virtualDesktopId;
            if (SUCCEEDED_LOG(StringFromCLSID(it->first, &virtualDesktopId)))
            {
                modified |= JSONHelpers::FancyZonesDataInstance().RemoveDevicesByVirtualDesktopId(virtualDesktopId.get());
            }
            it = m_processedWorkAreas.erase(it);
        }
        else
        {
            activeVirtualDesktops.erase(it->first); // virtual desktop already in map, skip it
            ++it;
        }
    }
    if (modified)
    {
        JSONHelpers::FancyZonesDataInstance().SaveFancyZonesData();
    }
    // register new virtual desktops, if any
    for (const auto& id : activeVirtualDesktops)
    {
        m_processedWorkAreas[id] = std::vector<HMONITOR>();
    }
}

void FancyZones::RegisterNewWorkArea(GUID virtualDesktopId, HMONITOR monitor) noexcept
{
    if (!m_processedWorkAreas.contains(virtualDesktopId))
    {
        m_processedWorkAreas[virtualDesktopId] = { monitor };
    }
    else
    {
        m_processedWorkAreas[virtualDesktopId].push_back(monitor);
    }
}

bool FancyZones::IsNewWorkArea(GUID virtualDesktopId, HMONITOR monitor) noexcept
{
    auto it = m_processedWorkAreas.find(virtualDesktopId);
    if (it != m_processedWorkAreas.end())
    {
        // virtual desktop exists, check if it's processed on given monitor
        return std::find(it->second.begin(), it->second.end(), monitor) == it->second.end();
    }
    return true;
}

void FancyZones::OnEditorExitEvent() noexcept
{
    // Colect information about changes in zone layout after editor exited.
    JSONHelpers::FancyZonesDataInstance().ParseDeviceInfoFromTmpFile(ZoneWindowUtils::GetActiveZoneSetTmpPath());
    JSONHelpers::FancyZonesDataInstance().ParseDeletedCustomZoneSetsFromTmpFile(ZoneWindowUtils::GetCustomZoneSetsTmpPath());
    JSONHelpers::FancyZonesDataInstance().ParseCustomZoneSetFromTmpFile(ZoneWindowUtils::GetAppliedZoneSetTmpPath());
    JSONHelpers::FancyZonesDataInstance().SaveFancyZonesData();
    // Update zone sets for currently active work areas.
    for (auto& [monitor, zoneWindow] : m_zoneWindowMap)
    {
        zoneWindow->UpdateActiveZoneSet();
    }
    if (m_settings->GetSettings()->zoneSetChange_moveWindows)
    {
        UpdateWindowsPositions();
    }
}

std::vector<HMONITOR> FancyZones::GetMonitorsSorted() noexcept
{
    std::shared_lock readLock(m_lock);

    auto monitorInfo = GetRawMonitorData();
    OrderMonitors(monitorInfo);
    std::vector<HMONITOR> output;
    std::transform(std::begin(monitorInfo), std::end(monitorInfo), std::back_inserter(output), [](const auto& info) { return info.first; });
    return output;
}

std::vector<std::pair<HMONITOR, RECT>> FancyZones::GetRawMonitorData() noexcept
{
    std::shared_lock readLock(m_lock);

    std::vector<std::pair<HMONITOR, RECT>> monitorInfo;
    for (const auto& [monitor, window] : m_zoneWindowMap)
    {
        if (window->ActiveZoneSet() != nullptr)
        {
            MONITORINFOEX mi;
            mi.cbSize = sizeof(mi);
            GetMonitorInfo(monitor, &mi);
            monitorInfo.push_back({ monitor, mi.rcMonitor });
        }
    }
    return monitorInfo;
}

winrt::com_ptr<IFancyZones> MakeFancyZones(HINSTANCE hinstance, const winrt::com_ptr<IFancyZonesSettings>& settings) noexcept
{
    if (!settings)
    {
        return nullptr;
    }

    return winrt::make_self<FancyZones>(hinstance, settings);
}
