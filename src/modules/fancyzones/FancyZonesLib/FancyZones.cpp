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
#include <FancyZonesLib/FancyZonesData/LastUsedVirtualDesktop.h>
#include <FancyZonesLib/FancyZonesData/LayoutHotkeys.h>
#include <FancyZonesLib/FancyZonesData/LayoutTemplates.h>
#include <FancyZonesLib/FancyZonesWindowProcessing.h>
#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>
#include <FancyZonesLib/KeyboardInput.h>
#include <FancyZonesLib/MonitorUtils.h>
#include <FancyZonesLib/on_thread_executor.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/SettingsObserver.h>
#include <FancyZonesLib/trace.h>
#include <FancyZonesLib/VirtualDesktop.h>
#include <FancyZonesLib/WindowKeyboardSnap.h>
#include <FancyZonesLib/WindowMouseSnap.h>
#include <FancyZonesLib/WindowUtils.h>
#include <FancyZonesLib/WorkArea.h>
#include <FancyZonesLib/WorkAreaConfiguration.h>

enum class DisplayChangeType
{
    WorkArea,
    DisplayChange,
    VirtualDesktop,
    Initialization
};

constexpr wchar_t* DisplayChangeTypeName (const DisplayChangeType type){
    switch (type)
    {
    case DisplayChangeType::WorkArea:
        return L"WorkArea";
    case DisplayChangeType::DisplayChange:
        return L"DisplayChange";
    case DisplayChangeType::VirtualDesktop:
        return L"VirtualDesktop";
    case DisplayChangeType::Initialization:
        return L"Initialization";
    default:
        return L"";
    }
}

// Non-localizable strings
namespace NonLocalizable
{
    const wchar_t ToolWindowClassName[] = L"SuperFancyZones";
    const wchar_t FZEditorExecutablePath[] = L"PowerToys.FancyZonesEditor.exe";
}

struct FancyZones : public winrt::implements<FancyZones, IFancyZones, IFancyZonesCallback>, public SettingsObserver
{
public:
    FancyZones(HINSTANCE hinstance, std::function<void()> disableModuleCallbackFunction) noexcept :
        SettingsObserver({ SettingId::EditorHotkey, SettingId::WindowSwitching, SettingId::PrevTabHotkey, SettingId::NextTabHotkey, SettingId::SpanZonesAcrossMonitors }),
        m_hinstance(hinstance),
        m_draggingState([this]() {
            PostMessageW(m_window, WM_PRIV_LOCATIONCHANGE, NULL, NULL);
        })
    {
        if (!SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_NORMAL))
        {
            Logger::warn("Failed to set main thread priority");
        }

        this->disableModuleCallback = std::move(disableModuleCallbackFunction);

        FancyZonesSettings::instance().LoadSettings();

        FancyZonesDataInstance().ReplaceZoneSettingsFileFromOlderVersions();
        LayoutTemplates::instance().LoadData();
        CustomLayouts::instance().LoadData();
        LayoutHotkeys::instance().LoadData();
        AppliedLayouts::instance().LoadData();
        AppZoneHistory::instance().LoadData();
        DefaultLayouts::instance().LoadData();
        LastUsedVirtualDesktop::instance().LoadData();
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
    void OnKeyboardInput(WPARAM flags, HRAWINPUT hInput) noexcept;
    void OnDisplayChange(DisplayChangeType changeType) noexcept;
    bool AddWorkArea(HMONITOR monitor, const FancyZonesDataTypes::WorkAreaId& id, const FancyZonesUtils::Rect& rect) noexcept;

protected:
    static LRESULT CALLBACK s_WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;

private:
    void UpdateWorkAreas(bool updateWindowPositions) noexcept;
    bool ShouldWorkAreasBeRecreated(const std::vector<FancyZonesDataTypes::MonitorId>& monitors, const GUID& virtualDesktop, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& workAreas) noexcept;
    void CycleWindows(bool reverse) noexcept;

    void SyncVirtualDesktops() noexcept;

    void UpdateHotkey(int hotkeyId, const PowerToysSettings::HotkeyObject& hotkeyObject, bool enable) noexcept;
    
    bool MoveToAppLastZone(HWND window, HMONITOR monitor, GUID currentVirtualDesktop) noexcept;

    void RefreshLayouts() noexcept;
    bool ShouldProcessSnapHotkey(DWORD vkCode) noexcept;
    void ApplyQuickLayout(int key) noexcept;
    void FlashZones() noexcept;

    HMONITOR WorkAreaKeyFromWindow(HWND window) noexcept;

    virtual void SettingsUpdate(SettingId type) override;

    const HINSTANCE m_hinstance{};

    HWND m_window{};
    std::unique_ptr<WindowMouseSnap> m_windowMouseSnapper{};
    WindowKeyboardSnap m_windowKeyboardSnapper{};
    WorkAreaConfiguration m_workAreaConfiguration;
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
        Logger::critical(L"Failed to create FancyZones window");
        return;
    }

    if (!KeyboardInput::Initialize(m_window))
    {
        Logger::critical(L"Failed to register raw input device");
        return;
    }

    UpdateHotkey(static_cast<int>(HotkeyId::Editor), FancyZonesSettings::settings().editorHotkey, true);
    UpdateHotkey(static_cast<int>(HotkeyId::PrevTab), FancyZonesSettings::settings().prevTabHotkey, FancyZonesSettings::settings().windowSwitching);
    UpdateHotkey(static_cast<int>(HotkeyId::NextTab), FancyZonesSettings::settings().nextTabHotkey, FancyZonesSettings::settings().windowSwitching);

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

    SyncVirtualDesktops();

    // id format of applied-layouts and app-zone-history was changed in 0.60
    auto monitors = MonitorUtils::IdentifyMonitors();
    AppliedLayouts::instance().AdjustWorkAreaIds(monitors);
    AppZoneHistory::instance().AdjustWorkAreaIds(monitors);

    PostMessage(m_window, WM_PRIV_INIT, 0, 0);
}

// IFancyZones
IFACEMETHODIMP_(void)
FancyZones::Destroy() noexcept
{
    m_workAreaConfiguration.Clear();
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
    m_windowMouseSnapper = WindowMouseSnap::Create(window, m_workAreaConfiguration.GetAllWorkAreas());
    if (m_windowMouseSnapper)
    {
        if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
        {
            monitor = NULL;
        }

        m_draggingState.Enable();
        m_draggingState.UpdateDraggingState();
        m_windowMouseSnapper->MoveSizeStart(monitor, m_draggingState.IsDragging());
    }
}

void FancyZones::MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen)
{
    if (m_windowMouseSnapper)
    {
        if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
        {
            monitor = NULL;
        }

        m_draggingState.UpdateDraggingState();
        m_windowMouseSnapper->MoveSizeUpdate(monitor, ptScreen, m_draggingState.IsDragging(), m_draggingState.IsSelectManyZonesState());
    }
}

void FancyZones::MoveSizeEnd()
{
    if (m_windowMouseSnapper)
    {
        m_windowMouseSnapper->MoveSizeEnd();
        m_draggingState.Disable();
        m_windowMouseSnapper = nullptr;
    }
}

bool FancyZones::MoveToAppLastZone(HWND window, HMONITOR monitor, GUID currentVirtualDesktop) noexcept
{
    const auto& workAreas = m_workAreaConfiguration.GetAllWorkAreas();
    WorkArea* workArea{ nullptr };
    ZoneIndexSet indexes{};

    if (monitor)
    {    
        if (workAreas.contains(monitor))
        {
            workArea = workAreas.at(monitor).get();
            if (workArea && workArea->UniqueId().virtualDesktopId == currentVirtualDesktop)
            {
                indexes = AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workArea->UniqueId(), workArea->GetLayoutId());
            }
        }
        else
        {
            Logger::error(L"Unable to find work area for requested monitor on the active virtual desktop");
        }
    }
    else
    {
        for (const auto& [_, secondaryWorkArea] : workAreas)
        {
            if (secondaryWorkArea && secondaryWorkArea->UniqueId().virtualDesktopId == currentVirtualDesktop)
            {
                indexes = AppZoneHistory::instance().GetAppLastZoneIndexSet(window, secondaryWorkArea->UniqueId(), secondaryWorkArea->GetLayoutId());
                workArea = secondaryWorkArea.get();
                if (!indexes.empty())
                {
                    break;
                }
            }
        }
    }
    
    if (!indexes.empty() && workArea)
    {
        Trace::FancyZones::SnapNewWindowIntoZone(workArea->GetLayout().get(), workArea->GetLayoutWindows());
        workArea->Snap(window, indexes);

        return true;
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

    HMONITOR primary = MonitorFromWindow(nullptr, MONITOR_DEFAULTTOPRIMARY);
    HMONITOR active = primary;

    POINT cursorPosition{};
    if (GetCursorPos(&cursorPosition))
    {
        active = MonitorFromPoint(cursorPosition, MONITOR_DEFAULTTOPRIMARY);
    }

    bool windowMovedToZone = false;
    auto currentVirtualDesktop = VirtualDesktop::instance().GetCurrentVirtualDesktopIdFromRegistry();
    if (moveToAppLastZone)
    {
        if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
        {
            windowMovedToZone = MoveToAppLastZone(window, nullptr, currentVirtualDesktop);
        }
        else
        {
            // Search application history on currently active monitor.
            windowMovedToZone = MoveToAppLastZone(window, active, currentVirtualDesktop);

            if (!windowMovedToZone && primary != active)
            {
                // Search application history on primary monitor.
                windowMovedToZone = MoveToAppLastZone(window, primary, currentVirtualDesktop);
            }

            if (!windowMovedToZone)
            {
                // Search application history on remaining monitors.
                windowMovedToZone = MoveToAppLastZone(window, nullptr, currentVirtualDesktop);
            }
        }
    }
    

    // Open on active monitor if window wasn't zoned
    if (openOnActiveMonitor && !windowMovedToZone)
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

    if (!EditorParameters::Save(m_workAreaConfiguration, m_dpiUnawareThread))
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

    case WM_INPUT:
    {
        OnKeyboardInput(wparam, reinterpret_cast<HRAWINPUT>(lparam));
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
            // We already checked in ShouldProcessSnapHotkey whether the foreground window is a candidate for zoning
            auto foregroundWindow = GetForegroundWindow();

            HMONITOR monitor{ nullptr };
            if (!FancyZonesSettings::settings().spanZonesAcrossMonitors)
            {
                monitor = MonitorFromWindow(foregroundWindow, MONITOR_DEFAULTTONULL);
            }

            if (FancyZonesSettings::settings().moveWindowsBasedOnPosition)
            {
                auto monitors = FancyZonesUtils::GetAllMonitorRects<&MONITORINFOEX::rcWork>();
                RECT windowRect;
                if (GetWindowRect(foregroundWindow, &windowRect))
                {
                    // Check whether Alt is used in the shortcut key combination
                    if (GetAsyncKeyState(VK_MENU) & 0x8000)
                    {
                        m_windowKeyboardSnapper.Extend(foregroundWindow, windowRect, monitor, static_cast<DWORD>(lparam), m_workAreaConfiguration.GetAllWorkAreas());
                    }
                    else
                    {
                        m_windowKeyboardSnapper.Snap(foregroundWindow, windowRect, monitor, static_cast<DWORD>(lparam), m_workAreaConfiguration.GetAllWorkAreas(), monitors);
                    }
                }
                else
                {
                    Logger::error("Error snapping window by keyboard shortcut: failed to get window rect");
                }
            }
            else
            {
                m_windowKeyboardSnapper.Snap(foregroundWindow, monitor, static_cast<DWORD>(lparam), m_workAreaConfiguration.GetAllWorkAreas(), FancyZonesUtils::GetMonitorsOrdered());
            }
        }
        else if (message == WM_PRIV_INIT)
        {
            OnDisplayChange(DisplayChangeType::Initialization);
        }
        else if (message == WM_PRIV_VD_SWITCH)
        {
            OnDisplayChange(DisplayChangeType::VirtualDesktop);
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
                MoveSizeUpdate(monitor, ptScreen);
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
            RefreshLayouts();
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

void FancyZones::OnKeyboardInput(WPARAM /*flags*/, HRAWINPUT hInput) noexcept
{
    auto input = KeyboardInput::OnKeyboardInput(hInput);
    if (!input.has_value())
    {
        return;
    }

    switch (input.value().vkKey)
    {
    case VK_SHIFT:
        {
            m_draggingState.SetShiftState(input.value().pressed);
        }
        break;
    default:
        break;
    }
}

void FancyZones::OnDisplayChange(DisplayChangeType changeType) noexcept
{
    Logger::info(L"Display changed, type: {}", DisplayChangeTypeName(changeType));

    bool updateWindowsPositions = false;

    switch (changeType)
    {
    case DisplayChangeType::WorkArea: // WorkArea size changed
    case DisplayChangeType::DisplayChange: // Resolution changed or display added
        updateWindowsPositions = FancyZonesSettings::settings().displayOrWorkAreaChange_moveWindows;
        break;
    case DisplayChangeType::VirtualDesktop: // Switched virtual desktop
        SyncVirtualDesktops();
        break;
    case DisplayChangeType::Initialization: // Initialization
        updateWindowsPositions = FancyZonesSettings::settings().zoneSetChange_moveWindows;
        break;
    default:
        break;
    }

    UpdateWorkAreas(updateWindowsPositions);
}

bool FancyZones::AddWorkArea(HMONITOR monitor, const FancyZonesDataTypes::WorkAreaId& id, const FancyZonesUtils::Rect& rect) noexcept
{
    auto virtualDesktopIdStr = FancyZonesUtils::GuidToString(id.virtualDesktopId);
    if (virtualDesktopIdStr)
    {
        Logger::debug(L"Add new work area on virtual desktop {}", virtualDesktopIdStr.value());
    }

    auto parentWorkAreaId = id;
    parentWorkAreaId.virtualDesktopId = LastUsedVirtualDesktop::instance().GetId();

    auto workArea = WorkArea::Create(m_hinstance, id, parentWorkAreaId, rect);
    if (!workArea)
    {
        Logger::error(L"Failed to create work area {}", id.toString());
        return false;
    }
    
    m_workAreaConfiguration.AddWorkArea(monitor, std::move(workArea));
    return true;
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
    Logger::debug(L"Update work areas, update windows positions: {}", updateWindowPositions);

    auto currentVirtualDesktop = VirtualDesktop::instance().GetCurrentVirtualDesktopIdFromRegistry();

    if (FancyZonesSettings::settings().spanZonesAcrossMonitors)
    {
        std::vector<FancyZonesDataTypes::MonitorId> monitors = { FancyZonesDataTypes::MonitorId{ .monitor = nullptr, .deviceId = { .id = ZonedWindowProperties::MultiMonitorName, .instanceId = ZonedWindowProperties::MultiMonitorInstance } } };
        if (ShouldWorkAreasBeRecreated(monitors, currentVirtualDesktop, m_workAreaConfiguration.GetAllWorkAreas()))
        {
            m_workAreaConfiguration.Clear();

            FancyZonesDataTypes::WorkAreaId workAreaId;
            workAreaId.virtualDesktopId = currentVirtualDesktop;
            workAreaId.monitorId = { .deviceId = { .id = ZonedWindowProperties::MultiMonitorName, .instanceId = ZonedWindowProperties::MultiMonitorInstance } };

            AddWorkArea(nullptr, workAreaId, FancyZonesUtils::GetAllMonitorsCombinedRect<&MONITORINFO::rcWork>());
        }
    }
    else
    {
        auto monitors = MonitorUtils::IdentifyMonitors();
        const auto& workAreas = m_workAreaConfiguration.GetAllWorkAreas();
        
        if (ShouldWorkAreasBeRecreated(monitors, currentVirtualDesktop, workAreas))
        {
            m_workAreaConfiguration.Clear();
            for (const auto& monitor : monitors)
            {
                FancyZonesDataTypes::WorkAreaId workAreaId;
                workAreaId.virtualDesktopId = currentVirtualDesktop;
                workAreaId.monitorId = monitor;

                AddWorkArea(monitor.monitor, workAreaId, MonitorUtils::GetWorkAreaRect(monitor.monitor));
            }
        }
    }

    // init previously snapped windows
    std::unordered_map<HWND, ZoneIndexSet> windowsToSnap{};
    for (const auto& window : VirtualDesktop::instance().GetWindowsFromCurrentDesktop())
    {
        auto indexes = FancyZonesWindowProperties::RetrieveZoneIndexProperty(window);
        if (indexes.size() == 0)
        {
            continue;
        }

        windowsToSnap.insert({ window, indexes });
    }

    if (FancyZonesSettings::settings().spanZonesAcrossMonitors) // one work area across monitors
    {
        const auto workArea = m_workAreaConfiguration.GetWorkArea(nullptr);
        if (workArea)
        {
            for (const auto& [window, zones] : windowsToSnap)
            {
                workArea->Snap(window, zones, false);
            }
        }
    }
    else
    {
        // first, snap windows to the monitor where they're placed
        for (auto iter = windowsToSnap.begin(); iter != windowsToSnap.end();)
        {
            const auto window = iter->first;
            const auto zones = iter->second;
            const auto monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
            const auto workAreaForMonitor = m_workAreaConfiguration.GetWorkArea(monitor);
            if (workAreaForMonitor && AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workAreaForMonitor->UniqueId(), workAreaForMonitor->GetLayoutId()) == zones)
            {
                workAreaForMonitor->Snap(window, zones, false);
                iter = windowsToSnap.erase(iter);
            }
            else
            {
                ++iter;
            }
        }

        // snap rest of the windows to other work areas (in case they were moved after the monitor unplug)
        for (const auto& [window, zones] : windowsToSnap)
        {
            for (const auto& [_, workArea] : m_workAreaConfiguration.GetAllWorkAreas())
            {
                const auto savedIndexes = AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workArea->UniqueId(), workArea->GetLayoutId());
                if (savedIndexes == zones)
                {
                    workArea->Snap(window, zones, false);
                }
            }
        }
    }

    if (updateWindowPositions)
    {
        for (const auto& [_, workArea] : m_workAreaConfiguration.GetAllWorkAreas())
        {
            if (workArea)
            {
                workArea->UpdateWindowPositions();
            }
        }
    }
}

bool FancyZones::ShouldWorkAreasBeRecreated(const std::vector<FancyZonesDataTypes::MonitorId>& monitors, const GUID& virtualDesktop, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& workAreas) noexcept
{
    if (monitors.size() != workAreas.size())
    {
        Logger::trace(L"Monitor was added or removed");
        return true;
    }

    for (const auto& monitor : monitors)
    {
        auto iter = workAreas.find(monitor.monitor);
        if (iter == workAreas.end())
        {
            Logger::trace(L"WorkArea not found");
            return true;
        }

        if (iter->second->UniqueId().monitorId.deviceId != monitor.deviceId)
        {
            Logger::trace(L"DeviceId changed");
            return true;
        }

        if (iter->second->UniqueId().monitorId.serialNumber != monitor.serialNumber)
        {
            Logger::trace(L"Serial number changed");
            return true;
        }

        if (iter->second->UniqueId().virtualDesktopId != virtualDesktop)
        {
            Logger::trace(L"Virtual desktop changed");
            return true;
        }

        const auto rect = monitor.monitor ? MonitorUtils::GetWorkAreaRect(monitor.monitor) : FancyZonesUtils::Rect(FancyZonesUtils::GetMonitorsCombinedRect<&MONITORINFOEX::rcWork>(FancyZonesUtils::GetAllMonitorRects<&MONITORINFOEX::rcWork>()));
        if (iter->second->GetWorkAreaRect() != rect)
        {
            Logger::trace(L"WorkArea size changed");
            return true;
        }
    }

    return false;
}

void FancyZones::CycleWindows(bool reverse) noexcept
{
    auto window = GetForegroundWindow();
    HMONITOR current = WorkAreaKeyFromWindow(window);

    auto workArea = m_workAreaConfiguration.GetWorkArea(current);
    if (workArea)
    {
        workArea->CycleWindows(window, reverse);
    }
}

void FancyZones::SyncVirtualDesktops() noexcept
{
    // Explorer persists current virtual desktop identifier to registry on a per session basis,
    // but only after first virtual desktop switch happens. If the user hasn't switched virtual
    // desktops in this session value in registry will be empty and we will use default GUID in
    // that case (00000000-0000-0000-0000-000000000000).

    auto lastUsed = LastUsedVirtualDesktop::instance().GetId();
    auto current = VirtualDesktop::instance().GetCurrentVirtualDesktopIdFromRegistry();
    auto guids = VirtualDesktop::instance().GetVirtualDesktopIdsFromRegistry();

    if (current != lastUsed)
    {
        LastUsedVirtualDesktop::instance().SetId(current);
        LastUsedVirtualDesktop::instance().SaveData();
    }

    AppliedLayouts::instance().SyncVirtualDesktops(current, lastUsed, guids);
    AppZoneHistory::instance().SyncVirtualDesktops(current, lastUsed, guids);
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
    case SettingId::WindowSwitching:
    {
        UpdateHotkey(static_cast<int>(HotkeyId::PrevTab), FancyZonesSettings::settings().prevTabHotkey, FancyZonesSettings::settings().windowSwitching);
        UpdateHotkey(static_cast<int>(HotkeyId::NextTab), FancyZonesSettings::settings().nextTabHotkey, FancyZonesSettings::settings().windowSwitching);
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
        m_workAreaConfiguration.Clear();
        PostMessageW(m_window, WM_PRIV_INIT, NULL, NULL);
    }
    break;
    default:
        break;
    }
}

void FancyZones::RefreshLayouts() noexcept
{
    for (const auto& [_, workArea] : m_workAreaConfiguration.GetAllWorkAreas())
    {
        if (workArea)
        {
            workArea->InitLayout();

            if (FancyZonesSettings::settings().zoneSetChange_moveWindows)
            {
                workArea->UpdateWindowPositions();
            }
        }
    }
}

bool FancyZones::ShouldProcessSnapHotkey(DWORD vkCode) noexcept
{
    if (!FancyZonesSettings::settings().overrideSnapHotkeys)
    {
        return false;
    }

    auto window = GetForegroundWindow();
    if (!FancyZonesWindowProcessing::IsProcessable(window))
    {
        return false;
    }

    HMONITOR monitor = WorkAreaKeyFromWindow(window);

    auto workArea = m_workAreaConfiguration.GetWorkArea(monitor);
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

    if (layout->Zones().size() > 0)
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

    auto workArea = m_workAreaConfiguration.GetWorkAreaFromCursor();
    if (workArea)
    {
        if (AppliedLayouts::instance().ApplyLayout(workArea->UniqueId(), layout.value()))
        {
            RefreshLayouts();
            FlashZones();
            AppliedLayouts::instance().SaveData();
        }
    }
}

void FancyZones::FlashZones() noexcept
{
    if (FancyZonesSettings::settings().flashZonesOnQuickSwitch && !m_draggingState.IsDragging())
    {
        for (const auto& [_, workArea] : m_workAreaConfiguration.GetAllWorkAreas())
        {
            if (workArea)
            {
                workArea->FlashZones();
            }
        }
    }
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
