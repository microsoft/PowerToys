#include "pch.h"
#include "FancyZones.h"

#include <common/display/dpi_aware.h>
#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>
#include <common/logger/call_tracer.h>
#include <common/utils/EventWaiter.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>
#include <common/utils/window.h>
#include <common/SettingsAPI/FileWatcher.h>

#include <FancyZonesLib/FancyZonesData.h>
#include <FancyZonesLib/FancyZonesData/AppliedLayouts.h>
#include <FancyZonesLib/FancyZonesData/AppZoneHistory.h>
#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include <FancyZonesLib/FancyZonesData/LayoutHotkeys.h>
#include <FancyZonesLib/FancyZonesData/LayoutTemplates.h>
#include <FancyZonesLib/FancyZonesWindowProcessing.h>
#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>
#include <FancyZonesLib/MonitorUtils.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/SettingsObserver.h>
#include <FancyZonesLib/ZoneSet.h>
#include <FancyZonesLib/WorkArea.h>
#include <FancyZonesLib/WindowMoveHandler.h>
#include <FancyZonesLib/WindowUtils.h>
#include <FancyZonesLib/util.h>

#include "on_thread_executor.h"
#include "trace.h"
#include "VirtualDesktop.h"
#include "MonitorWorkAreaHandler.h"
#include "util.h"

#include <FancyZonesLib/SecondaryMouseButtonsHook.h>

enum class DisplayChangeType
{
    WorkArea,
    DisplayChange,
    VirtualDesktop,
    Initialization
};

// Non-localizable strings
namespace NonLocalizable
{
    const wchar_t ToolWindowClassName[] = L"SuperFancyZones";
    const wchar_t FZEditorExecutablePath[] = L"modules\\FancyZones\\PowerToys.FancyZonesEditor.exe";
}

struct FancyZones : public winrt::implements<FancyZones, IFancyZones, IFancyZonesCallback>, public SettingsObserver
{
public:
    FancyZones(HINSTANCE hinstance, std::function<void()> disableModuleCallback) noexcept :
        SettingsObserver({ SettingId::EditorHotkey, SettingId::PrevTabHotkey, SettingId::NextTabHotkey, SettingId::SpanZonesAcrossMonitors }),
        m_hinstance(hinstance),
        m_windowMoveHandler([this]() {
            PostMessageW(m_window, WM_PRIV_LOCATIONCHANGE, NULL, NULL);
        })
    {
        this->disableModuleCallback = std::move(disableModuleCallback);

        FancyZonesSettings::instance().LoadSettings();

        FancyZonesDataInstance().ReplaceZoneSettingsFileFromOlderVersions();
        LayoutTemplates::instance().LoadData();
        CustomLayouts::instance().LoadData();
        LayoutHotkeys::instance().LoadData();
        AppliedLayouts::instance().LoadData();
        AppZoneHistory::instance().LoadData();
    }

    // IFancyZones
    IFACEMETHODIMP_(void)
    Run() noexcept;
    IFACEMETHODIMP_(void)
    Destroy() noexcept;

    void MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen) noexcept
    {
        if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
        {
            monitor = NULL;
        }

        m_windowMoveHandler.MoveSizeStart(window, monitor, ptScreen, m_workAreaHandler.GetWorkAreasByDesktopId(VirtualDesktop::instance().GetCurrentVirtualDesktopId()));
    }

    void MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen) noexcept
    {
        if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
        {
            monitor = NULL;
        }
        m_windowMoveHandler.MoveSizeUpdate(monitor, ptScreen, m_workAreaHandler.GetWorkAreasByDesktopId(VirtualDesktop::instance().GetCurrentVirtualDesktopId()));
    }

    void MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept
    {
        m_windowMoveHandler.MoveSizeEnd(window, ptScreen, m_workAreaHandler.GetWorkAreasByDesktopId(VirtualDesktop::instance().GetCurrentVirtualDesktopId()));
    }

    IFACEMETHODIMP_(void)
    HandleWinHookEvent(const WinHookEvent* data) noexcept
    {
        const auto wparam = reinterpret_cast<WPARAM>(data->hwnd);
        const LONG lparam = 0;
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
    IFACEMETHODIMP_(bool)
    OnKeyDown(PKBDLLHOOKSTRUCT info) noexcept;

    void WindowCreated(HWND window) noexcept;
    void ToggleEditor() noexcept;

    LRESULT WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;
    void OnDisplayChange(DisplayChangeType changeType) noexcept;
    void AddWorkArea(HMONITOR monitor, const std::wstring& deviceId) noexcept;

protected:
    static LRESULT CALLBACK s_WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;

private:
    void UpdateWorkAreas() noexcept;
    void UpdateWindowsPositions(bool suppressMove = false) noexcept;
    void CycleTabs(bool reverse) noexcept;
    bool OnSnapHotkeyBasedOnZoneNumber(HWND window, DWORD vkCode) noexcept;
    bool OnSnapHotkeyBasedOnPosition(HWND window, DWORD vkCode) noexcept;
    bool OnSnapHotkey(DWORD vkCode) noexcept;
    bool ProcessDirectedSnapHotkey(HWND window, DWORD vkCode, bool cycle, winrt::com_ptr<IWorkArea> workArea) noexcept;

    void RegisterVirtualDesktopUpdates() noexcept;

    void UpdateHotkey(int hotkeyId, const PowerToysSettings::HotkeyObject& hotkeyObject, bool enable) noexcept;
    
    std::pair<winrt::com_ptr<IWorkArea>, ZoneIndexSet> GetAppZoneHistoryInfo(HWND window, HMONITOR monitor, const std::unordered_map<HMONITOR, winrt::com_ptr<IWorkArea>>& workAreaMap) noexcept;
    void MoveWindowIntoZone(HWND window, winrt::com_ptr<IWorkArea> workArea, const ZoneIndexSet& zoneIndexSet) noexcept;
    bool MoveToAppLastZone(HWND window, HMONITOR active, HMONITOR primary) noexcept;

    void OnEditorExitEvent() noexcept;
    void UpdateZoneSets() noexcept;
    bool ShouldProcessSnapHotkey(DWORD vkCode) noexcept;
    void ApplyQuickLayout(int key) noexcept;
    void FlashZones() noexcept;

    std::vector<std::pair<HMONITOR, RECT>> GetRawMonitorData() noexcept;
    std::vector<HMONITOR> GetMonitorsSorted() noexcept;
    HMONITOR WorkAreaKeyFromWindow(HWND window) noexcept;

    virtual void SettingsUpdate(SettingId type) override;

    const HINSTANCE m_hinstance{};

    HWND m_window{};
    WindowMoveHandler m_windowMoveHandler;
    MonitorWorkAreaHandler m_workAreaHandler;

    wil::unique_handle m_terminateEditorEvent; // Handle of FancyZonesEditor.exe we launch and wait on

    OnThreadExecutor m_dpiUnawareThread;

    EventWaiter m_toggleEditorEventWaiter;

    // If non-recoverable error occurs, trigger disabling of entire FancyZones.
    static std::function<void()> disableModuleCallback;

    // Did we terminate the editor or was it closed cleanly?
    enum class EditorExitKind : byte
    {
        Exit,
        Terminate
    };

    // IDs used to register hot keys (keyboard shortcuts).
    enum class HotkeyId : int
    {
        Editor = 1,
        NextTab = 2,
        PrevTab = 3,
    };
};

std::function<void()> FancyZones::disableModuleCallback = {};

// IFancyZones
IFACEMETHODIMP_(void)
FancyZones::Run() noexcept
{
    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = s_WndProc;
    wcex.hInstance = m_hinstance;
    wcex.lpszClassName = NonLocalizable::ToolWindowClassName;
    RegisterClassExW(&wcex);

    BufferedPaintInit();

    m_window = CreateWindowExW(WS_EX_TOOLWINDOW, NonLocalizable::ToolWindowClassName, L"", WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, m_hinstance, this);
    if (!m_window)
    {
        Logger::error(L"Failed to create FancyZones window");
        return;
    }

    if (!RegisterHotKey(m_window, static_cast<int>(HotkeyId::Editor), FancyZonesSettings::settings().editorHotkey.get_modifiers(), FancyZonesSettings::settings().editorHotkey.get_code()))
    {
        Logger::error(L"Failed to register hotkey: {}", get_last_error_or_default(GetLastError()));
    }

    if (FancyZonesSettings::settings().windowSwitching)
    {
        if (!RegisterHotKey(m_window, static_cast<int>(HotkeyId::NextTab), FancyZonesSettings::settings().nextTabHotkey.get_modifiers(), FancyZonesSettings::settings().nextTabHotkey.get_code()))
        {
            Logger::error(L"Failed to register hotkey: {}", get_last_error_or_default(GetLastError()));
        }

        if (!RegisterHotKey(m_window, static_cast<int>(HotkeyId::PrevTab), FancyZonesSettings::settings().prevTabHotkey.get_modifiers(), FancyZonesSettings::settings().prevTabHotkey.get_code()))
        {
            Logger::error(L"Failed to register hotkey: {}", get_last_error_or_default(GetLastError()));
        }
    }

    m_dpiUnawareThread.submit(OnThreadExecutor::task_t{ [] {
                          SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_UNAWARE);
                          SetThreadDpiHostingBehavior(DPI_HOSTING_BEHAVIOR_MIXED);
                      } })
        .wait();

    m_toggleEditorEventWaiter = EventWaiter(CommonSharedConstants::FANCY_ZONES_EDITOR_TOGGLE_EVENT, [&](int err) {
        if (err == ERROR_SUCCESS)
        {
            Logger::trace(L"{} event was signaled", CommonSharedConstants::FANCY_ZONES_EDITOR_TOGGLE_EVENT);
            PostMessage(m_window, WM_HOTKEY, 1, 0);
        }
    });

    PostMessage(m_window, WM_PRIV_VD_INIT, 0, 0);
}

// IFancyZones
IFACEMETHODIMP_(void)
FancyZones::Destroy() noexcept
{
    m_workAreaHandler.Clear();
    BufferedPaintUnInit();
    if (m_window)
    {
        DestroyWindow(m_window);
        m_window = nullptr;
    }
}

// IFancyZonesCallback
IFACEMETHODIMP_(void)
FancyZones::VirtualDesktopChanged() noexcept
{
    // VirtualDesktopChanged is called from a reentrant WinHookProc function, therefore we must postpone the actual logic
    // until we're in FancyZones::WndProc, which is not reentrant.
    PostMessage(m_window, WM_PRIV_VD_SWITCH, 0, 0);
}

std::pair<winrt::com_ptr<IWorkArea>, ZoneIndexSet> FancyZones::GetAppZoneHistoryInfo(HWND window, HMONITOR monitor, const std::unordered_map<HMONITOR, winrt::com_ptr<IWorkArea>>& workAreaMap) noexcept
{
    if (monitor)
    {
        if (workAreaMap.contains(monitor))
        {
            auto workArea = workAreaMap.at(monitor);
            return std::pair<winrt::com_ptr<IWorkArea>, ZoneIndexSet>{ workArea, workArea->GetWindowZoneIndexes(window) };
        }
        else
        {
            Logger::debug(L"No work area for the currently active monitor.");
        }
    }
    else
    {
        for (const auto& [monitor, workArea] : workAreaMap)
        {
            auto zoneIndexSet = workArea->GetWindowZoneIndexes(window);
            if (!zoneIndexSet.empty())
            {
                return std::pair<winrt::com_ptr<IWorkArea>, ZoneIndexSet>{ workArea, zoneIndexSet };
            }
        }
    }
    
    return std::pair<winrt::com_ptr<IWorkArea>, ZoneIndexSet>{ nullptr, {} };
}

void FancyZones::MoveWindowIntoZone(HWND window, winrt::com_ptr<IWorkArea> workArea, const ZoneIndexSet& zoneIndexSet) noexcept
{
    if (workArea)
    {
        Trace::FancyZones::SnapNewWindowIntoZone(workArea->ZoneSet());
    }
    m_windowMoveHandler.MoveWindowIntoZoneByIndexSet(window, zoneIndexSet, workArea);
    AppZoneHistory::instance().UpdateProcessIdToHandleMap(window, workArea->UniqueId());
}

bool FancyZones::MoveToAppLastZone(HWND window, HMONITOR active, HMONITOR primary) noexcept
{
    auto workAreaMap = m_workAreaHandler.GetWorkAreasByDesktopId(VirtualDesktop::instance().GetCurrentVirtualDesktopId());
    if (workAreaMap.empty())
    {
        Logger::trace(L"No work area for the current desktop.");
        return false;
    }

    // Search application history on currently active monitor.
    std::pair<winrt::com_ptr<IWorkArea>, ZoneIndexSet> appZoneHistoryInfo = GetAppZoneHistoryInfo(window, active, workAreaMap);

    // No application history on currently active monitor
    if (appZoneHistoryInfo.second.empty())
    {
        // Search application history on primary monitor.
        appZoneHistoryInfo = GetAppZoneHistoryInfo(window, primary, workAreaMap);
    }

    // No application history on currently active and primary monitors
    if (appZoneHistoryInfo.second.empty())
    {
        // Search application history on remaining monitors.
        appZoneHistoryInfo = GetAppZoneHistoryInfo(window, nullptr, workAreaMap);
    }

    if (!appZoneHistoryInfo.second.empty())
    {
        MoveWindowIntoZone(window, appZoneHistoryInfo.first, appZoneHistoryInfo.second);
        return true;
    }
    else
    {
        Logger::trace(L"App zone history is empty for the processing window on a current virtual desktop");
    }

    return false;
}

void FancyZones::WindowCreated(HWND window) noexcept
{
    const bool moveToAppLastZone = FancyZonesSettings::settings().appLastZone_moveWindows;
    const bool openOnActiveMonitor = FancyZonesSettings::settings().openWindowOnActiveMonitor;
    if (!moveToAppLastZone && !openOnActiveMonitor)
    {
        // Nothing to do here then.
        return;
    }

    if (!FancyZonesWindowProcessing::IsProcessable(window))
    {
        return;
    }

    // Avoid already stamped (zoned) windows
    const bool isZoned = !FancyZonesWindowProperties::RetrieveZoneIndexProperty(window).empty();
    if (isZoned)
    {
        return;
    }

    const bool isCandidateForLastKnownZone = FancyZonesWindowUtils::IsCandidateForZoning(window);
    if (!isCandidateForLastKnownZone)
    {
        return;
    }
    

    HMONITOR primary = MonitorFromWindow(nullptr, MONITOR_DEFAULTTOPRIMARY);
    HMONITOR active = primary;

    POINT cursorPosition{};
    if (GetCursorPos(&cursorPosition))
    {
        active = MonitorFromPoint(cursorPosition, MONITOR_DEFAULTTOPRIMARY);
    }

    bool movedToAppLastZone = false;
    if (moveToAppLastZone)
    {
        movedToAppLastZone = MoveToAppLastZone(window, active, primary);
    }

    // Open on active monitor if window wasn't zoned
    if (openOnActiveMonitor && !movedToAppLastZone)
    {
        m_dpiUnawareThread.submit(OnThreadExecutor::task_t{ [&] { MonitorUtils::OpenWindowOnActiveMonitor(window, active); } }).wait();
    }
}

// IFancyZonesCallback
IFACEMETHODIMP_(bool)
FancyZones::OnKeyDown(PKBDLLHOOKSTRUCT info) noexcept
{
    // Return true to swallow the keyboard event
    bool const shift = GetAsyncKeyState(VK_SHIFT) & 0x8000;
    bool const win = GetAsyncKeyState(VK_LWIN) & 0x8000 || GetAsyncKeyState(VK_RWIN) & 0x8000;
    bool const alt = GetAsyncKeyState(VK_MENU) & 0x8000;
    bool const ctrl = GetAsyncKeyState(VK_CONTROL) & 0x8000;
    if ((win && !shift && !ctrl) || (win && ctrl && alt))
    {
        if ((info->vkCode == VK_RIGHT) || (info->vkCode == VK_LEFT) || (info->vkCode == VK_UP) || (info->vkCode == VK_DOWN))
        {
            if (ShouldProcessSnapHotkey(info->vkCode))
            {
                Trace::FancyZones::OnKeyDown(info->vkCode, win, ctrl, false /*inMoveSize*/);
                // Win+Left, Win+Right will cycle through Zones in the active ZoneSet when WM_PRIV_SNAP_HOTKEY's handled
                PostMessageW(m_window, WM_PRIV_SNAP_HOTKEY, 0, info->vkCode);
                return true;
            }
        }
    }

    if (FancyZonesSettings::settings().quickLayoutSwitch)
    {
        int digitPressed = -1;
        if ('0' <= info->vkCode && info->vkCode <= '9')
        {
            digitPressed = info->vkCode - '0';
        }
        else if (VK_NUMPAD0 <= info->vkCode && info->vkCode <= VK_NUMPAD9)
        {
            digitPressed = info->vkCode - VK_NUMPAD0;
        }

        bool dragging = m_windowMoveHandler.InDragging();
        bool changeLayoutWhileNotDragging = !dragging && !shift && win && ctrl && alt && digitPressed != -1;
        bool changeLayoutWhileDragging = dragging && digitPressed != -1;

        if (changeLayoutWhileNotDragging || changeLayoutWhileDragging)
        {
            auto layoutId = LayoutHotkeys::instance().GetLayoutId(digitPressed);
            if (layoutId.has_value())
            {
                PostMessageW(m_window, WM_PRIV_QUICK_LAYOUT_KEY, 0, static_cast<LPARAM>(digitPressed));
                Trace::FancyZones::QuickLayoutSwitched(changeLayoutWhileNotDragging);
                return true;
            }
        }
    }

    if (m_windowMoveHandler.IsDragEnabled() && shift)
    {
        return true;
    }
    return false;
}

void FancyZones::ToggleEditor() noexcept
{
    _TRACER_;
    
    if (m_terminateEditorEvent)
    {
        SetEvent(m_terminateEditorEvent.get());
        return;
    }

    m_terminateEditorEvent.reset(CreateEvent(nullptr, true, false, nullptr));

    HMONITOR targetMonitor{};

    const bool use_cursorpos_editor_startupscreen = FancyZonesSettings::settings().use_cursorpos_editor_startupscreen;
    if (use_cursorpos_editor_startupscreen)
    {
        POINT currentCursorPos{};
        GetCursorPos(&currentCursorPos);
        targetMonitor = MonitorFromPoint(currentCursorPos, MONITOR_DEFAULTTOPRIMARY);
    }
    else
    {
        targetMonitor = MonitorFromWindow(GetForegroundWindow(), MONITOR_DEFAULTTOPRIMARY);
    }

    if (!targetMonitor)
    {
        return;
    }

    wil::unique_cotaskmem_string virtualDesktopId;
    if (!SUCCEEDED(StringFromCLSID(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), &virtualDesktopId)))
    {
        return;
    }

    /*
    * Divider: /
    * Parts:
    * (1) Process id
    * (2) Span zones across monitors
    * (3) Monitor id where the Editor should be opened
    * (4) Monitors count
    *
    * Data for each monitor:
    * (5) Monitor id
    * (6) DPI
    * (7) work area left
    * (8) work area top
    * (9) work area width
    * (10) work area height
    * ...
    */
    std::wstring params;
    const std::wstring divider = L"/";
    params += std::to_wstring(GetCurrentProcessId()) + divider; /* Process id */
    const bool spanZonesAcrossMonitors = FancyZonesSettings::settings().spanZonesAcrossMonitors;
    params += std::to_wstring(spanZonesAcrossMonitors) + divider; /* Span zones */
    std::vector<std::pair<HMONITOR, MONITORINFOEX>> allMonitors;

    m_dpiUnawareThread.submit(OnThreadExecutor::task_t{ [&] {
        allMonitors = FancyZonesUtils::GetAllMonitorInfo<&MONITORINFOEX::rcWork>();
    } }).wait();

    if (spanZonesAcrossMonitors)
    {
        params += FancyZonesUtils::GenerateUniqueIdAllMonitorsArea(virtualDesktopId.get()) + divider; /* Monitor id where the Editor should be opened */
    }

    // device id map
    std::unordered_map<std::wstring, DWORD> displayDeviceIdxMap;

    bool showDpiWarning = false;
    int prevDpi = -1;
    std::wstring monitorsDataStr;

    for (auto& monitorData : allMonitors)
    {
        HMONITOR monitor = monitorData.first;
        auto monitorInfo = monitorData.second;

        std::wstring deviceId = FancyZonesUtils::GetDisplayDeviceId(monitorInfo.szDevice, displayDeviceIdxMap);
        std::wstring monitorId = FancyZonesUtils::GenerateUniqueId(monitor, deviceId, virtualDesktopId.get());

        if (monitor == targetMonitor && !spanZonesAcrossMonitors)
        {
            params += monitorId + divider; /* Monitor id where the Editor should be opened */
        }
        
        UINT dpi = 0;
        if (DPIAware::GetScreenDPIForMonitor(monitor, dpi) != S_OK)
        {
            continue;
        }
        
        if (spanZonesAcrossMonitors && prevDpi != -1 && prevDpi != dpi)
        {
            showDpiWarning = true;
        }

        monitorsDataStr += std::move(monitorId) + divider; /* Monitor id */
        monitorsDataStr += std::to_wstring(dpi) + divider; /* DPI */
        monitorsDataStr += std::to_wstring(monitorInfo.rcWork.left) + divider; /* Top coordinate */
        monitorsDataStr += std::to_wstring(monitorInfo.rcWork.top) + divider; /* Left coordinate */
        monitorsDataStr += std::to_wstring(monitorInfo.rcWork.right - monitorInfo.rcWork.left) + divider; /* Width */
        monitorsDataStr += std::to_wstring(monitorInfo.rcWork.bottom - monitorInfo.rcWork.top) + divider; /* Height */
    }

    params += std::to_wstring(allMonitors.size()) + divider; /* Monitors count */
    params += monitorsDataStr;

    FancyZonesDataInstance().SaveFancyZonesEditorParameters(spanZonesAcrossMonitors, virtualDesktopId.get(), targetMonitor, allMonitors); /* Write parameters to json file */

    if (showDpiWarning)
    {
        // We must show the message box in a separate thread, since this code is called from a low-level
        // keyboard hook callback, and launching messageboxes from it has unexpected side effects
        //std::thread{ [] {
        //    MessageBoxW(nullptr,
        //                GET_RESOURCE_STRING(IDS_SPAN_ACROSS_ZONES_WARNING).c_str(),
        //                GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str(),
        //                MB_OK | MB_ICONWARNING);
        //} }.detach();
    }

    SHELLEXECUTEINFO sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
    sei.lpFile = NonLocalizable::FZEditorExecutablePath;
    sei.lpParameters = params.c_str();
    sei.nShow = SW_SHOWDEFAULT;
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


LRESULT FancyZones::WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_HOTKEY:
    {
        if (wparam == static_cast<WPARAM>(HotkeyId::Editor))
        {
            ToggleEditor();
        }
        else if (wparam == static_cast<WPARAM>(HotkeyId::NextTab) || wparam == static_cast<WPARAM>(HotkeyId::PrevTab))
        {
            bool reverse = wparam == static_cast<WPARAM>(HotkeyId::PrevTab);
            CycleTabs(reverse);
        }
    }
    break;

    case WM_SETTINGCHANGE:
    {
        if (wparam == SPI_SETWORKAREA)
        {
            // Changes in taskbar position resulted in different size of work area.
            // Invalidate cached work-areas so they can be recreated with latest information.
            m_workAreaHandler.Clear();
            OnDisplayChange(DisplayChangeType::WorkArea);
        }
    }
    break;

    case WM_DISPLAYCHANGE:
    {
        // Display resolution changed. Invalidate cached work-areas so they can be recreated with latest information.
        m_workAreaHandler.Clear();
        OnDisplayChange(DisplayChangeType::DisplayChange);
    }
    break;

    default:
    {
        POINT ptScreen;
        GetPhysicalCursorPos(&ptScreen);

        if (message == WM_PRIV_SNAP_HOTKEY)
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
            OnDisplayChange(DisplayChangeType::Initialization);
        }
        else if (message == WM_PRIV_EDITOR)
        {
            if (lparam == static_cast<LPARAM>(EditorExitKind::Exit))
            {
                OnEditorExitEvent();
            }

            {
                // Clean up the event either way
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
        else if (message == WM_PRIV_LOCATIONCHANGE)
        {
            if (m_windowMoveHandler.InDragging())
            {
                if (auto monitor = MonitorFromPoint(ptScreen, MONITOR_DEFAULTTONULL))
                {
                    MoveSizeUpdate(monitor, ptScreen);
                }
            }
        }
        else if (message == WM_PRIV_WINDOWCREATED)
        {
            auto hwnd = reinterpret_cast<HWND>(wparam);
            WindowCreated(hwnd);
        }
        else if (message == WM_PRIV_LAYOUT_HOTKEYS_FILE_UPDATE)
        {
            LayoutHotkeys::instance().LoadData();
        }
        else if (message == WM_PRIV_LAYOUT_TEMPLATES_FILE_UPDATE)
        {
            LayoutTemplates::instance().LoadData();
        }
        else if (message == WM_PRIV_CUSTOM_LAYOUTS_FILE_UPDATE)
        {
            CustomLayouts::instance().LoadData();
        }
        else if (message == WM_PRIV_APPLIED_LAYOUTS_FILE_UPDATE)
        {
            AppliedLayouts::instance().LoadData();
            UpdateZoneSets();
        }
        else if (message == WM_PRIV_QUICK_LAYOUT_KEY)
        {
            ApplyQuickLayout(static_cast<int>(lparam));
        }
        else if (message == WM_PRIV_SETTINGS_CHANGED)
        {
            FancyZonesSettings::instance().LoadSettings();
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
    Logger::info(L"Display changed, type: {}", changeType);

    if (changeType == DisplayChangeType::VirtualDesktop ||
        changeType == DisplayChangeType::Initialization)
    {
        VirtualDesktop::instance().UpdateVirtualDesktopId();

        if (changeType == DisplayChangeType::Initialization)
        {
            RegisterVirtualDesktopUpdates();
        }
    }

    UpdateWorkAreas();

    if ((changeType == DisplayChangeType::WorkArea) || (changeType == DisplayChangeType::DisplayChange))
    {
        if (FancyZonesSettings::settings().displayChange_moveWindows)
        {
            UpdateWindowsPositions();
        }
    }
}

void FancyZones::AddWorkArea(HMONITOR monitor, const std::wstring& deviceId) noexcept
{
    if (m_workAreaHandler.IsNewWorkArea(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), monitor))
    {
        wil::unique_cotaskmem_string virtualDesktopIdStr;
        if (!SUCCEEDED(StringFromCLSID(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), &virtualDesktopIdStr)))
        {
            Logger::debug(L"Add new work area on virtual desktop {}", virtualDesktopIdStr.get());
        }
        
        FancyZonesDataTypes::DeviceIdData uniqueId;
        uniqueId.virtualDesktopId = VirtualDesktop::instance().GetCurrentVirtualDesktopId();

        if (monitor)
        {
            uniqueId.deviceName = FancyZonesUtils::TrimDeviceId(deviceId);
        }
        else
        {
            uniqueId.deviceName = ZonedWindowProperties::MultiMonitorDeviceID;
        }

        FancyZonesDataTypes::DeviceIdData parentId{};
        auto parentArea = m_workAreaHandler.GetWorkArea(VirtualDesktop::instance().GetPreviousVirtualDesktopId(), monitor);
        if (parentArea)
        {
            parentId = parentArea->UniqueId();
        }

        auto workArea = MakeWorkArea(m_hinstance, monitor, uniqueId, parentId);
        if (workArea)
        {
            m_workAreaHandler.AddWorkArea(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), monitor, workArea);
            AppliedLayouts::instance().SaveData();
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

void FancyZones::UpdateWorkAreas() noexcept
{
    if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
    {
        AddWorkArea(nullptr, {});
    }
    else
    {
        // Mapping between display device name and device index (operating system identifies each display device with an index value).
        std::unordered_map<std::wstring, DWORD> displayDeviceIdxMap;
        struct capture
        {
            FancyZones* fancyZones;
            std::unordered_map<std::wstring, DWORD>* displayDeviceIdx;
        };

        auto callback = [](HMONITOR monitor, HDC, RECT*, LPARAM data) -> BOOL {
            capture* params = reinterpret_cast<capture*>(data);
            MONITORINFOEX mi{ { .cbSize = sizeof(mi) } };
            if (GetMonitorInfoW(monitor, &mi))
            {
                auto& displayDeviceIdxMap = *(params->displayDeviceIdx);
                FancyZones* fancyZones = params->fancyZones;

                std::wstring deviceId = FancyZonesUtils::GetDisplayDeviceId(mi.szDevice, displayDeviceIdxMap);
                fancyZones->AddWorkArea(monitor, deviceId);
            }
            return TRUE;
        };

        capture capture{ this, &displayDeviceIdxMap };
        EnumDisplayMonitors(nullptr, nullptr, callback, reinterpret_cast<LPARAM>(&capture));
    }
}

void FancyZones::UpdateWindowsPositions(bool suppressMove) noexcept
{
    for (const auto [window, desktopId] : VirtualDesktop::instance().GetWindowsRelatedToDesktops())
    {
        auto zoneIndexSet = FancyZonesWindowProperties::RetrieveZoneIndexProperty(window);
        auto workArea = m_workAreaHandler.GetWorkArea(window, desktopId);
        if (workArea)
        {
            m_windowMoveHandler.MoveWindowIntoZoneByIndexSet(window, zoneIndexSet, workArea, suppressMove);
        }
    }
}

void FancyZones::CycleTabs(bool reverse) noexcept
{
    auto window = GetForegroundWindow();
    HMONITOR current = WorkAreaKeyFromWindow(window);

    auto workArea = m_workAreaHandler.GetWorkArea(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), current);
    if (workArea)
    {
        workArea->CycleTabs(window, reverse);
    }
}

bool FancyZones::OnSnapHotkeyBasedOnZoneNumber(HWND window, DWORD vkCode) noexcept
{
    HMONITOR current = WorkAreaKeyFromWindow(window);

    std::vector<HMONITOR> monitorInfo = GetMonitorsSorted();
    if (current && monitorInfo.size() > 1 && FancyZonesSettings::settings().moveWindowAcrossMonitors)
    {
        // Multi monitor environment.
        auto currMonitorInfo = std::find(std::begin(monitorInfo), std::end(monitorInfo), current);
        do
        {
            auto workArea = m_workAreaHandler.GetWorkArea(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), *currMonitorInfo);
            if (m_windowMoveHandler.MoveWindowIntoZoneByDirectionAndIndex(window, vkCode, false /* cycle through zones */, workArea))
            {
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->ZoneSet());
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
        auto workArea = m_workAreaHandler.GetWorkArea(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), current);
        // Single monitor environment, or combined multi-monitor environment.
        if (FancyZonesSettings::settings().restoreSize)
        {
            bool moved = m_windowMoveHandler.MoveWindowIntoZoneByDirectionAndIndex(window, vkCode, false /* cycle through zones */, workArea);
            if (!moved)
            {
                FancyZonesWindowUtils::RestoreWindowOrigin(window);
                FancyZonesWindowUtils::RestoreWindowSize(window);
            }
            else
            {
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->ZoneSet());
            }
            return moved;
        }
        else
        {
            bool moved = m_windowMoveHandler.MoveWindowIntoZoneByDirectionAndIndex(window, vkCode, true /* cycle through zones */, workArea);
            if (moved)
            {
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->ZoneSet());
            }
            return moved;
        }
    }

    return false;
}

bool FancyZones::OnSnapHotkeyBasedOnPosition(HWND window, DWORD vkCode) noexcept
{
    HMONITOR current = WorkAreaKeyFromWindow(window);

    auto allMonitors = FancyZonesUtils::GetAllMonitorRects<&MONITORINFOEX::rcWork>();

    if (current && allMonitors.size() > 1 && FancyZonesSettings::settings().moveWindowAcrossMonitors)
    {
        // Multi monitor environment.
        // First, try to stay on the same monitor
        bool success = ProcessDirectedSnapHotkey(window, vkCode, false, m_workAreaHandler.GetWorkArea(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), current));
        if (success)
        {
            return true;
        }

        // If that didn't work, extract zones from all other monitors and target one of them
        std::vector<RECT> zoneRects;
        std::vector<std::pair<ZoneIndex, winrt::com_ptr<IWorkArea>>> zoneRectsInfo;
        RECT currentMonitorRect{ .top = 0, .bottom = -1 };

        for (const auto& [monitor, monitorRect] : allMonitors)
        {
            if (monitor == current)
            {
                currentMonitorRect = monitorRect;
            }
            else
            {
                auto workArea = m_workAreaHandler.GetWorkArea(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), monitor);
                if (workArea)
                {
                    auto zoneSet = workArea->ZoneSet();
                    if (zoneSet)
                    {
                        const auto zones = zoneSet->GetZones();
                        for (const auto& [zoneId, zone] : zones)
                        {
                            RECT zoneRect = zone->GetZoneRect();

                            zoneRect.left += monitorRect.left;
                            zoneRect.right += monitorRect.left;
                            zoneRect.top += monitorRect.top;
                            zoneRect.bottom += monitorRect.top;

                            zoneRects.emplace_back(zoneRect);
                            zoneRectsInfo.emplace_back(zoneId, workArea);
                        }
                    }
                }
            }
        }

        // Ensure we can get the windowRect, if not, just quit
        RECT windowRect;
        if (!GetWindowRect(window, &windowRect))
        {
            return false;
        }

        auto chosenIdx = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);

        if (chosenIdx < zoneRects.size())
        {
            // Moving to another monitor succeeded
            const auto& [trueZoneIdx, workArea] = zoneRectsInfo[chosenIdx];
            m_windowMoveHandler.MoveWindowIntoZoneByIndexSet(window, { trueZoneIdx }, workArea);
            Trace::FancyZones::KeyboardSnapWindowToZone(workArea->ZoneSet());
            return true;
        }

        // We reached the end of all monitors.
        // Try again, cycling on all monitors.
        // First, add zones from the origin monitor to zoneRects
        // Sanity check: the current monitor is valid
        if (currentMonitorRect.top <= currentMonitorRect.bottom)
        {
            auto workArea = m_workAreaHandler.GetWorkArea(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), current);
            if (workArea)
            {
                auto zoneSet = workArea->ZoneSet();
                if (zoneSet)
                {
                    const auto zones = zoneSet->GetZones();
                    for (const auto& [zoneId, zone] : zones)
                    {
                        RECT zoneRect = zone->GetZoneRect();

                        zoneRect.left += currentMonitorRect.left;
                        zoneRect.right += currentMonitorRect.left;
                        zoneRect.top += currentMonitorRect.top;
                        zoneRect.bottom += currentMonitorRect.top;

                        zoneRects.emplace_back(zoneRect);
                        zoneRectsInfo.emplace_back(zoneId, workArea);
                    }
                }
            }
        }
        else
        {
            return false;
        }

        RECT combinedRect = FancyZonesUtils::GetAllMonitorsCombinedRect<&MONITORINFOEX::rcWork>();
        windowRect = FancyZonesUtils::PrepareRectForCycling(windowRect, combinedRect, vkCode);
        chosenIdx = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);
        if (chosenIdx < zoneRects.size())
        {
            // Moving to another monitor succeeded
            const auto& [trueZoneIdx, workArea] = zoneRectsInfo[chosenIdx];
            m_windowMoveHandler.MoveWindowIntoZoneByIndexSet(window, { trueZoneIdx }, workArea);
            Trace::FancyZones::KeyboardSnapWindowToZone(workArea->ZoneSet());
            return true;
        }
        else
        {
            // Giving up
            return false;
        }
    }
    else
    {
        // Single monitor environment, or combined multi-monitor environment.
        return ProcessDirectedSnapHotkey(window, vkCode, true, m_workAreaHandler.GetWorkArea(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), current));
    }
}

bool FancyZones::OnSnapHotkey(DWORD vkCode) noexcept
{
    // We already checked in ShouldProcessSnapHotkey whether the foreground window is a candidate for zoning
    auto window = GetForegroundWindow();
    if (FancyZonesSettings::settings().moveWindowsBasedOnPosition)
    {
        return OnSnapHotkeyBasedOnPosition(window, vkCode);
    }
    else
    {
        return (vkCode == VK_LEFT || vkCode == VK_RIGHT) && OnSnapHotkeyBasedOnZoneNumber(window, vkCode);
    }
    return false;
}

bool FancyZones::ProcessDirectedSnapHotkey(HWND window, DWORD vkCode, bool cycle, winrt::com_ptr<IWorkArea> workArea) noexcept
{
    // Check whether Alt is used in the shortcut key combination
    if (GetAsyncKeyState(VK_MENU) & 0x8000)
    {
        bool result = m_windowMoveHandler.ExtendWindowByDirectionAndPosition(window, vkCode, workArea);
        if (result) 
        {
            Trace::FancyZones::KeyboardSnapWindowToZone(workArea->ZoneSet());
        }
        return result;
    }
    else
    {
        bool result = m_windowMoveHandler.MoveWindowIntoZoneByDirectionAndPosition(window, vkCode, cycle, workArea);
        if (result)
        {
            Trace::FancyZones::KeyboardSnapWindowToZone(workArea->ZoneSet());
        }
        return result;
    }
}

void FancyZones::RegisterVirtualDesktopUpdates() noexcept
{
    auto guids = VirtualDesktop::instance().GetVirtualDesktopIdsFromRegistry();
    if (guids.has_value())
    {
        m_workAreaHandler.RegisterUpdates(*guids);
        AppZoneHistory::instance().RemoveDeletedVirtualDesktops(*guids);
        AppliedLayouts::instance().RemoveDeletedVirtualDesktops(*guids);
    }

    AppZoneHistory::instance().SyncVirtualDesktops();
    AppliedLayouts::instance().SyncVirtualDesktops();
}

void FancyZones::UpdateHotkey(int hotkeyId, const PowerToysSettings::HotkeyObject& hotkeyObject, bool enable) noexcept
{
    if (!m_window)
    {
        return;
    }

    UnregisterHotKey(m_window, hotkeyId);

    if (!enable)
    {
        return;
    }

    auto modifiers = hotkeyObject.get_modifiers();
    auto code = hotkeyObject.get_code();
    auto result = RegisterHotKey(m_window, hotkeyId, modifiers, code);

    if (!result)
    {
        Logger::error(L"Failed to register hotkey: {}", get_last_error_or_default(GetLastError()));
    }
}

void FancyZones::SettingsUpdate(SettingId id)
{
    switch (id)
    {
    case SettingId::EditorHotkey:
    {
        UpdateHotkey(static_cast<int>(HotkeyId::Editor), FancyZonesSettings::settings().editorHotkey, true);
    }
    break;
    case SettingId::PrevTabHotkey:
    {
        UpdateHotkey(static_cast<int>(HotkeyId::PrevTab), FancyZonesSettings::settings().prevTabHotkey, FancyZonesSettings::settings().windowSwitching);
    }
    break;
    case SettingId::NextTabHotkey:
    {
        UpdateHotkey(static_cast<int>(HotkeyId::NextTab), FancyZonesSettings::settings().nextTabHotkey, FancyZonesSettings::settings().windowSwitching);
    }
    break;
    case SettingId::SpanZonesAcrossMonitors:
    {
        m_workAreaHandler.Clear();
        PostMessageW(m_window, WM_PRIV_VD_INIT, NULL, NULL);
    }
    break;
    default:
        break;
    }
}

void FancyZones::OnEditorExitEvent() noexcept
{
    // Collect information about changes in zone layout after editor exited.
    UpdateZoneSets();
}

void FancyZones::UpdateZoneSets() noexcept
{
    for (auto workArea : m_workAreaHandler.GetAllWorkAreas())
    {
        workArea->UpdateActiveZoneSet();
    }

    auto moveWindows = FancyZonesSettings::settings().zoneSetChange_moveWindows;
    UpdateWindowsPositions(!moveWindows);
}

bool FancyZones::ShouldProcessSnapHotkey(DWORD vkCode) noexcept
{
    auto window = GetForegroundWindow();
    
    if (!FancyZonesWindowProcessing::IsProcessable(window))
    {
        return false;
    }
    
    if (FancyZonesSettings::settings().overrideSnapHotkeys && FancyZonesWindowUtils::IsCandidateForZoning(window))
    {
        HMONITOR monitor = WorkAreaKeyFromWindow(window);

        auto workArea = m_workAreaHandler.GetWorkArea(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), monitor);
        if (workArea && workArea->ZoneSet() && workArea->ZoneSet()->LayoutType() != FancyZonesDataTypes::ZoneSetLayoutType::Blank)
        {
            if (vkCode == VK_UP || vkCode == VK_DOWN)
            {
                return FancyZonesSettings::settings().moveWindowsBasedOnPosition;
            }
            else
            {
                return true;
            }
        }
    }
    return false;
}

void FancyZones::ApplyQuickLayout(int key) noexcept
{
    auto layoutId = LayoutHotkeys::instance().GetLayoutId(key);
    if (!layoutId)
    {
        return;
    }

    // Find a custom zone set with this uuid and apply it
    auto layout = CustomLayouts::instance().GetLayout(layoutId.value());
    if (!layout)
    {
        return;
    }

    auto workArea = m_workAreaHandler.GetWorkAreaFromCursor(VirtualDesktop::instance().GetCurrentVirtualDesktopId());
    AppliedLayouts::instance().ApplyLayout(workArea->UniqueId(), layout.value());
    AppliedLayouts::instance().SaveData();
    UpdateZoneSets();
    FlashZones();
}

void FancyZones::FlashZones() noexcept
{
    if (FancyZonesSettings::settings().flashZonesOnQuickSwitch && !m_windowMoveHandler.IsDragEnabled())
    {
        for (auto [monitor, workArea] : m_workAreaHandler.GetWorkAreasByDesktopId(VirtualDesktop::instance().GetCurrentVirtualDesktopId()))
        {
            workArea->FlashZones();
        }
    }
}

std::vector<HMONITOR> FancyZones::GetMonitorsSorted() noexcept
{
    auto monitorInfo = GetRawMonitorData();
    FancyZonesUtils::OrderMonitors(monitorInfo);
    std::vector<HMONITOR> output;
    std::transform(std::begin(monitorInfo), std::end(monitorInfo), std::back_inserter(output), [](const auto& info) { return info.first; });
    return output;
}

std::vector<std::pair<HMONITOR, RECT>> FancyZones::GetRawMonitorData() noexcept
{
    std::vector<std::pair<HMONITOR, RECT>> monitorInfo;
    const auto& activeWorkAreaMap = m_workAreaHandler.GetWorkAreasByDesktopId(VirtualDesktop::instance().GetCurrentVirtualDesktopId());
    for (const auto& [monitor, workArea] : activeWorkAreaMap)
    {
        if (workArea->ZoneSet() != nullptr)
        {
            MONITORINFOEX mi;
            mi.cbSize = sizeof(mi);
            GetMonitorInfo(monitor, &mi);
            monitorInfo.push_back({ monitor, mi.rcMonitor });
        }
    }
    return monitorInfo;
}

HMONITOR FancyZones::WorkAreaKeyFromWindow(HWND window) noexcept
{
    if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
    {
        return NULL;
    }
    else
    {
        return MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
    }
}

winrt::com_ptr<IFancyZones> MakeFancyZones(HINSTANCE hinstance, std::function<void()> disableCallback) noexcept
{
    return winrt::make_self<FancyZones>(hinstance, disableCallback);
}
