#include "pch.h"
#include "FancyZones.h"

#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>
#include <common/logger/call_tracer.h>
#include <common/utils/EventWaiter.h>
#include <common/utils/winapi_error.h>
#include <common/SettingsAPI/FileWatcher.h>

#include <FancyZonesLib/DraggingState.h>
#include <FancyZonesLib/EditorParameters.h>
#include <FancyZonesLib/FancyZonesData.h>
#include <FancyZonesLib/FancyZonesData/AppliedLayouts.h>
#include <FancyZonesLib/FancyZonesData/AppZoneHistory.h>
#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include <FancyZonesLib/FancyZonesData/DefaultLayouts.h>
#include <FancyZonesLib/FancyZonesData/LayoutHotkeys.h>
#include <FancyZonesLib/FancyZonesData/LayoutTemplates.h>
#include <FancyZonesLib/FancyZonesWindowProcessing.h>
#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>
#include <FancyZonesLib/MonitorUtils.h>
#include <FancyZonesLib/MonitorWorkAreaMap.h>
#include <FancyZonesLib/on_thread_executor.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/SettingsObserver.h>
#include <FancyZonesLib/trace.h>
#include <FancyZonesLib/WindowDrag.h>
#include <FancyZonesLib/WorkArea.h>

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
    FancyZones(HINSTANCE hinstance, std::function<void()> disableModuleCallbackFunction) noexcept :
        SettingsObserver({ SettingId::EditorHotkey, SettingId::PrevTabHotkey, SettingId::NextTabHotkey, SettingId::SpanZonesAcrossMonitors }),
        m_hinstance(hinstance),
        m_draggingState([this]() {
            PostMessageW(m_window, WM_PRIV_LOCATIONCHANGE, NULL, NULL);
        })
    {
        this->disableModuleCallback = std::move(disableModuleCallbackFunction);

        FancyZonesSettings::instance().LoadSettings();

        FancyZonesDataInstance().ReplaceZoneSettingsFileFromOlderVersions();
        LayoutTemplates::instance().LoadData();
        CustomLayouts::instance().LoadData();
        LayoutHotkeys::instance().LoadData();
        AppliedLayouts::instance().LoadData();
        AppZoneHistory::instance().LoadData();
        DefaultLayouts::instance().LoadData();
    }

    // IFancyZones
    IFACEMETHODIMP_(void)
    Run() noexcept;
    IFACEMETHODIMP_(void)
    Destroy() noexcept;

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

    void MoveSizeStart(HWND window, HMONITOR monitor);
    void MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen);
    void MoveSizeEnd();

    void WindowCreated(HWND window) noexcept;
    void ToggleEditor() noexcept;

    LRESULT WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;
    void OnDisplayChange(DisplayChangeType changeType) noexcept;
    void AddWorkArea(HMONITOR monitor, const FancyZonesDataTypes::WorkAreaId& id, bool updateWindowsPositions) noexcept;

protected:
    static LRESULT CALLBACK s_WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;

private:
    void UpdateWorkAreas(bool updateWindowPositions) noexcept;
    void CycleWindows(bool reverse) noexcept;
    bool OnSnapHotkeyBasedOnZoneNumber(HWND window, DWORD vkCode) noexcept;
    bool OnSnapHotkeyBasedOnPosition(HWND window, DWORD vkCode) noexcept;
    bool OnSnapHotkey(DWORD vkCode) noexcept;
    bool ProcessDirectedSnapHotkey(HWND window, DWORD vkCode, bool cycle, WorkArea* const workArea) noexcept;

    void RegisterVirtualDesktopUpdates() noexcept;

    void UpdateHotkey(int hotkeyId, const PowerToysSettings::HotkeyObject& hotkeyObject, bool enable) noexcept;

    std::pair<WorkArea*, ZoneIndexSet> GetAppZoneHistoryInfo(HWND window, HMONITOR monitor, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& workAreas) noexcept;
    void MoveWindowIntoZone(HWND window, WorkArea* const workArea, const ZoneIndexSet& zoneIndexSet) noexcept;
    bool MoveToAppLastZone(HWND window, HMONITOR active, HMONITOR primary) noexcept;
    
    void UpdateActiveLayouts() noexcept;
    bool ShouldProcessSnapHotkey(DWORD vkCode) noexcept;
    void ApplyQuickLayout(int key) noexcept;
    void FlashZones() noexcept;

    std::vector<std::pair<HMONITOR, RECT>> GetRawMonitorData() noexcept;
    std::vector<HMONITOR> GetMonitorsSorted() noexcept;
    HMONITOR WorkAreaKeyFromWindow(HWND window) noexcept;

    virtual void SettingsUpdate(SettingId type) override;

    const HINSTANCE m_hinstance{};

    HWND m_window{};
    std::unique_ptr<WindowDrag> m_windowDrag{};
    MonitorWorkAreaMap m_workAreaHandler;
    DraggingState m_draggingState;

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

    // Initialize COM. Needed for WMI monitor identifying
    HRESULT comInitHres = CoInitializeEx(0, COINIT_MULTITHREADED);
    if (FAILED(comInitHres))
    {
        Logger::error(L"Failed to initialize COM library. {}", get_last_error_or_default(comInitHres));
        return;
    }

    // Initialize security. Needed for WMI monitor identifying
    HRESULT comSecurityInitHres = CoInitializeSecurity(NULL, -1, NULL, NULL, RPC_C_AUTHN_LEVEL_DEFAULT, RPC_C_IMP_LEVEL_IMPERSONATE, NULL, EOAC_NONE, NULL);
    if (FAILED(comSecurityInitHres))
    {
        Logger::error(L"Failed to initialize security. {}", get_last_error_or_default(comSecurityInitHres));
        return;
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

    CoUninitialize();
}

// IFancyZonesCallback
IFACEMETHODIMP_(void)
FancyZones::VirtualDesktopChanged() noexcept
{
    // VirtualDesktopChanged is called from a reentrant WinHookProc function, therefore we must postpone the actual logic
    // until we're in FancyZones::WndProc, which is not reentrant.
    PostMessage(m_window, WM_PRIV_VD_SWITCH, 0, 0);
}

void FancyZones::MoveSizeStart(HWND window, HMONITOR monitor)
{
    m_windowDrag = WindowDrag::Create(window, m_workAreaHandler.GetAllWorkAreas());
    if (m_windowDrag)
    {
        if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
        {
            monitor = NULL;
        }

        m_draggingState.Enable();
        m_draggingState.UpdateDraggingState();
        m_windowDrag->MoveSizeStart(monitor, m_draggingState.IsDragging());
    }
}

void FancyZones::MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen)
{
    if (m_windowDrag)
    {
        if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
        {
            monitor = NULL;
        }

        m_draggingState.UpdateDraggingState();
        m_windowDrag->MoveSizeUpdate(monitor, ptScreen, m_draggingState.IsDragging(), m_draggingState.IsSelectManyZonesState());
    }
}

void FancyZones::MoveSizeEnd()
{
    if (m_windowDrag)
    {
        m_windowDrag->MoveSizeEnd();
        m_draggingState.Disable();
        m_windowDrag = nullptr;
    }
}

std::pair<WorkArea*, ZoneIndexSet> FancyZones::GetAppZoneHistoryInfo(HWND window, HMONITOR monitor, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& workAreas) noexcept
{
    for (const auto& [workAreaMonitor, workArea] : workAreas)
    {
        if (workAreaMonitor == monitor && workArea)
        {
            return std::pair<WorkArea*, ZoneIndexSet>{ workArea.get(), workArea->GetWindowZoneIndexes(window) };
        }
    }

    Logger::error(L"No work area for the currently active monitor.");
    return std::pair<WorkArea*, ZoneIndexSet>{ nullptr, {} };
}

void FancyZones::MoveWindowIntoZone(HWND window, WorkArea* const workArea, const ZoneIndexSet& zoneIndexSet) noexcept
{
    if (workArea)
    {
        Trace::FancyZones::SnapNewWindowIntoZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
        workArea->MoveWindowIntoZoneByIndexSet(window, zoneIndexSet);
        AppZoneHistory::instance().UpdateProcessIdToHandleMap(window, workArea->UniqueId());
    }
}

bool FancyZones::MoveToAppLastZone(HWND window, HMONITOR active, HMONITOR primary) noexcept
{
    const auto& workAreas = m_workAreaHandler.GetAllWorkAreas();
    if (workAreas.empty())
    {
        Logger::trace(L"No work area for the current desktop.");
        return false;
    }

    // Search application history on currently active monitor.
    auto appZoneHistoryInfo = GetAppZoneHistoryInfo(window, active, workAreas);

    // No application history on currently active monitor
    if (appZoneHistoryInfo.second.empty())
    {
        // Search application history on primary monitor.
        appZoneHistoryInfo = GetAppZoneHistoryInfo(window, primary, workAreas);
    }

    // No application history on currently active and primary monitors
    if (appZoneHistoryInfo.second.empty())
    {
        // Search application history on remaining monitors.
        appZoneHistoryInfo = GetAppZoneHistoryInfo(window, nullptr, workAreas);
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
    if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
    {
        if (moveToAppLastZone)
        {
            movedToAppLastZone = MoveToAppLastZone(window, nullptr, nullptr);
        }
    }
    else
    {
        if (moveToAppLastZone)
        {
            movedToAppLastZone = MoveToAppLastZone(window, active, primary);
        }
    }

    // Open on active monitor if window wasn't zoned
    if (openOnActiveMonitor && !movedToAppLastZone)
    {
        // window is recreated after switching virtual desktop
        // avoid moving already opened windows after switching vd
        bool isMoved = FancyZonesWindowProperties::RetrieveMovedOnOpeningProperty(window);
        if (!isMoved)
        {
            FancyZonesWindowProperties::StampMovedOnOpeningProperty(window);
            m_dpiUnawareThread.submit(OnThreadExecutor::task_t{ [&] { MonitorUtils::OpenWindowOnActiveMonitor(window, active); } }).wait();
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

        bool dragging = m_draggingState.IsDragging();
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

    if (m_draggingState.IsDragging() && shift)
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

    if (!EditorParameters::Save())
    {
        Logger::error(L"Failed to save editor startup parameters");
        return;
    }

    SHELLEXECUTEINFO sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
    sei.lpFile = NonLocalizable::FZEditorExecutablePath;
    sei.lpParameters = L"";
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
            CycleWindows(reverse);
        }
    }
    break;

    case WM_SETTINGCHANGE:
    {
        if (wparam == SPI_SETWORKAREA)
        {
            // Changes in taskbar position resulted in different size of work area.
            // Invalidate cached work-areas so they can be recreated with latest information.
            OnDisplayChange(DisplayChangeType::WorkArea);
        }
    }
    break;

    case WM_DISPLAYCHANGE:
    {
        // Display resolution changed. Invalidate cached work-areas so they can be recreated with latest information.
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
            // Clean up the event either way
            m_terminateEditorEvent.release();
        }
        else if (message == WM_PRIV_MOVESIZESTART)
        {
            auto hwnd = reinterpret_cast<HWND>(wparam);
            if (auto monitor = MonitorFromPoint(ptScreen, MONITOR_DEFAULTTONULL))
            {
                MoveSizeStart(hwnd, monitor);
            }
        }
        else if (message == WM_PRIV_MOVESIZEEND)
        {
            MoveSizeEnd();
        }
        else if (message == WM_PRIV_LOCATIONCHANGE)
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
            UpdateActiveLayouts();
        }
        else if (message == WM_PRIV_DEFAULT_LAYOUTS_FILE_UPDATE)
        {
            DefaultLayouts::instance().LoadData();
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

            // id format of applied-layouts and app-zone-history was changed in 0.60
            auto monitors = MonitorUtils::IdentifyMonitors();
            AppliedLayouts::instance().AdjustWorkAreaIds(monitors);
            AppZoneHistory::instance().AdjustWorkAreaIds(monitors);
        }
    }

    bool updateWindowsPositionsOnResolutionChange = FancyZonesSettings::settings().displayChange_moveWindows && changeType == DisplayChangeType::DisplayChange;
    bool updateWindowsPositionsOnStart = FancyZonesSettings::settings().zoneSetChange_moveWindows && changeType == DisplayChangeType::Initialization;
    UpdateWorkAreas(updateWindowsPositionsOnResolutionChange || updateWindowsPositionsOnStart);
}

void FancyZones::AddWorkArea(HMONITOR monitor, const FancyZonesDataTypes::WorkAreaId& id, bool updateWindowsPositions) noexcept
{
    wil::unique_cotaskmem_string virtualDesktopIdStr;
    if (!SUCCEEDED(StringFromCLSID(VirtualDesktop::instance().GetCurrentVirtualDesktopId(), &virtualDesktopIdStr)))
    {
        Logger::debug(L"Add new work area on virtual desktop {}", virtualDesktopIdStr.get());
    }

    FancyZonesUtils::Rect rect{};
    if (monitor)
    {
        rect = MonitorUtils::GetWorkAreaRect(monitor);
    }
    else
    {
        rect = FancyZonesUtils::GetAllMonitorsCombinedRect<&MONITORINFO::rcWork>();
    }
        
    auto workArea = WorkArea::Create(m_hinstance, id, m_workAreaHandler.GetParent(monitor), rect);
    if (workArea)
    {
        if (updateWindowsPositions)
        {
            workArea->UpdateWindowPositions();
        }

        m_workAreaHandler.AddWorkArea(monitor, std::move(workArea));
    }
}

LRESULT CALLBACK FancyZones::s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    auto thisRef = reinterpret_cast<FancyZones*>(GetWindowLongPtr(window, GWLP_USERDATA));
    if (!thisRef && (message == WM_CREATE))
    {
        const auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
        thisRef = static_cast<FancyZones*>(createStruct->lpCreateParams);
        SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
    }

    return thisRef ? thisRef->WndProc(window, message, wparam, lparam) :
                     DefWindowProc(window, message, wparam, lparam);
}

void FancyZones::UpdateWorkAreas(bool updateWindowPositions) noexcept
{
    m_workAreaHandler.SaveParentIds();
    m_workAreaHandler.Clear();

    if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
    {
        FancyZonesDataTypes::WorkAreaId workAreaId;
        workAreaId.virtualDesktopId = VirtualDesktop::instance().GetCurrentVirtualDesktopId();
        workAreaId.monitorId = { .deviceId = { .id = ZonedWindowProperties::MultiMonitorName, .instanceId = ZonedWindowProperties::MultiMonitorInstance } };

        AddWorkArea(nullptr, workAreaId, updateWindowPositions);
    }
    else
    {
        auto monitors = MonitorUtils::IdentifyMonitors();
        for (const auto& monitor : monitors)
        {
            FancyZonesDataTypes::WorkAreaId workAreaId;
            workAreaId.virtualDesktopId = VirtualDesktop::instance().GetCurrentVirtualDesktopId();
            workAreaId.monitorId = monitor;

            AddWorkArea(monitor.monitor, workAreaId, updateWindowPositions);
        }
    }
}

void FancyZones::CycleWindows(bool reverse) noexcept
{
    auto window = GetForegroundWindow();
    HMONITOR current = WorkAreaKeyFromWindow(window);

    auto workArea = m_workAreaHandler.GetWorkArea(current);
    if (workArea)
    {
        workArea->CycleWindows(window, reverse);
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
            auto workArea = m_workAreaHandler.GetWorkArea(*currMonitorInfo);
            if (workArea && workArea->MoveWindowIntoZoneByDirectionAndIndex(window, vkCode, false /* cycle through zones */))
            {
                // unassign from previous work area
                for (auto& [_, prevWorkArea] : m_workAreaHandler.GetAllWorkAreas())
                {
                    if (prevWorkArea && workArea != prevWorkArea.get())
                    {
                        prevWorkArea->UnsnapWindow(window);
                    }
                }
                
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
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
        auto workArea = m_workAreaHandler.GetWorkArea(current);
        // Single monitor environment, or combined multi-monitor environment.
        if (FancyZonesSettings::settings().restoreSize)
        {
            bool moved = workArea && workArea->MoveWindowIntoZoneByDirectionAndIndex(window, vkCode, false /* cycle through zones */);
            if (!moved)
            {
                FancyZonesWindowUtils::RestoreWindowOrigin(window);
                FancyZonesWindowUtils::RestoreWindowSize(window);
            }
            else if (workArea)
            {
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
            }
            return moved;
        }
        else
        {
            bool moved = workArea && workArea->MoveWindowIntoZoneByDirectionAndIndex(window, vkCode, true /* cycle through zones */);

            if (moved)
            {
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
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
        bool success = ProcessDirectedSnapHotkey(window, vkCode, false, m_workAreaHandler.GetWorkArea(current));
        if (success)
        {
            return true;
        }

        // If that didn't work, extract zones from all other monitors and target one of them
        std::vector<RECT> zoneRects;
        std::vector<std::pair<ZoneIndex, WorkArea*>> zoneRectsInfo;
        RECT currentMonitorRect{ .top = 0, .bottom = -1 };

        for (const auto& [monitor, monitorRect] : allMonitors)
        {
            if (monitor == current)
            {
                currentMonitorRect = monitorRect;
            }
            else
            {
                auto workArea = m_workAreaHandler.GetWorkArea(monitor);
                if (workArea)
                {
                    const auto& layout = workArea->GetLayout();
                    if (layout)
                    {
                        const auto& zones = layout->Zones();
                        for (const auto& [zoneId, zone] : zones)
                        {
                            RECT zoneRect = zone.GetZoneRect();

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
            if (workArea)
            {
                workArea->MoveWindowIntoZoneByIndexSet(window, { trueZoneIdx });
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
            }

            return true;
        }

        // We reached the end of all monitors.
        // Try again, cycling on all monitors.
        // First, add zones from the origin monitor to zoneRects
        // Sanity check: the current monitor is valid
        if (currentMonitorRect.top <= currentMonitorRect.bottom)
        {
            auto workArea = m_workAreaHandler.GetWorkArea(current);
            if (workArea)
            {
                const auto& layout = workArea->GetLayout();
                if (layout)
                {
                    const auto& zones = layout->Zones();
                    for (const auto& [zoneId, zone] : zones)
                    {
                        RECT zoneRect = zone.GetZoneRect();

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

            if (workArea)
            {
                workArea->MoveWindowIntoZoneByIndexSet(window, { trueZoneIdx });
                Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
            }
            
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
        return ProcessDirectedSnapHotkey(window, vkCode, true, m_workAreaHandler.GetWorkArea(current));
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

    return (vkCode == VK_LEFT || vkCode == VK_RIGHT) && OnSnapHotkeyBasedOnZoneNumber(window, vkCode);
}

bool FancyZones::ProcessDirectedSnapHotkey(HWND window, DWORD vkCode, bool cycle, WorkArea* const workArea) noexcept
{
    // Check whether Alt is used in the shortcut key combination
    if (GetAsyncKeyState(VK_MENU) & 0x8000)
    {
        bool result = workArea && workArea->ExtendWindowByDirectionAndPosition(window, vkCode);
        if (result)
        {
            Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
        }
        return result;
    }
    else
    {
        bool result = workArea && workArea->MoveWindowIntoZoneByDirectionAndPosition(window, vkCode, cycle);
        if (result)
        {
            Trace::FancyZones::KeyboardSnapWindowToZone(workArea->GetLayout().get(), workArea->GetLayoutWindows().get());
        }
        return result;
    }
}

void FancyZones::RegisterVirtualDesktopUpdates() noexcept
{
    auto guids = VirtualDesktop::instance().GetVirtualDesktopIdsFromRegistry();
    if (guids.has_value())
    {
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

void FancyZones::UpdateActiveLayouts() noexcept
{
    for (const auto& [_, workArea] : m_workAreaHandler.GetAllWorkAreas())
    {
        if (workArea)
        {
            workArea->UpdateActiveZoneSet();

            if (FancyZonesSettings::settings().zoneSetChange_moveWindows)
            {
                workArea->UpdateWindowPositions();
            }
        }
    }
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

        auto workArea = m_workAreaHandler.GetWorkArea(monitor);
        if (!workArea)
        {
            Logger::error(L"No work area for processing snap hotkey");
            return false;
        }

        const auto& layout = workArea->GetLayout();
        if (!layout)
        {
            Logger::error(L"No layout for processing snap hotkey");
            return false;
        }

        if (layout->Type() != FancyZonesDataTypes::ZoneSetLayoutType::Blank)
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

    auto workArea = m_workAreaHandler.GetWorkAreaFromCursor();
    if (workArea)
    {
        AppliedLayouts::instance().ApplyLayout(workArea->UniqueId(), layout.value());
        AppliedLayouts::instance().SaveData();
        UpdateActiveLayouts();
        FlashZones();
    }
}

void FancyZones::FlashZones() noexcept
{
    if (FancyZonesSettings::settings().flashZonesOnQuickSwitch && !m_draggingState.IsDragging())
    {
        for (const auto& [_, workArea] : m_workAreaHandler.GetAllWorkAreas())
        {
            if (workArea)
            {
                workArea->FlashZones();
            }
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
    const auto& activeWorkAreaMap = m_workAreaHandler.GetAllWorkAreas();
    for (const auto& [monitor, workArea] : activeWorkAreaMap)
    {
        if (workArea && workArea->GetLayout() != nullptr)
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
