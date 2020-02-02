#include "pch.h"
#include "common/dpi_aware.h"
#include "common/on_thread_executor.h"

#include "FancyZones.h"
#include "lib/Settings.h"
#include "lib/ZoneWindow.h"
#include "lib/RegistryHelpers.h"
#include "lib/JsonHelpers.h"
#include "lib/ZoneSet.h"
#include "trace.h"

#include <functional>
#include <common/common.h>
#include <lib\util.h>

enum class DisplayChangeType
{
    WorkArea,
    DisplayChange,
    VirtualDesktop,
    Editor,
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
        m_settings(settings)
    {
        m_settings->SetCallback(this);
    }

    // IFancyZones
    IFACEMETHODIMP_(void) Run() noexcept;
    IFACEMETHODIMP_(void) Destroy() noexcept;

    // IFancyZonesCallback
    IFACEMETHODIMP_(bool) InMoveSize() noexcept
    {
        std::shared_lock readLock(m_lock);
        return m_inMoveSize;
    }
    IFACEMETHODIMP_(void) MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen) noexcept;
    IFACEMETHODIMP_(void) MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen) noexcept;
    IFACEMETHODIMP_(void) MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept;
    IFACEMETHODIMP_(void) VirtualDesktopChanged() noexcept;
    IFACEMETHODIMP_(void) VirtualDesktopInitialize() noexcept;
    IFACEMETHODIMP_(void) WindowCreated(HWND window) noexcept;
    IFACEMETHODIMP_(bool) OnKeyDown(PKBDLLHOOKSTRUCT info) noexcept;
    IFACEMETHODIMP_(void) ToggleEditor() noexcept;
    IFACEMETHODIMP_(void) SettingsChanged() noexcept;

    // IZoneWindowHost
    IFACEMETHODIMP_(void) MoveWindowsOnActiveZoneSetChange() noexcept;
    IFACEMETHODIMP_(COLORREF) GetZoneHighlightColor() noexcept
    {
        // Skip the leading # and convert to long
        const auto color = m_settings->GetSettings().zoneHightlightColor;
        const auto tmp = std::stol(color.substr(1), nullptr, 16);
        const auto nR = (tmp & 0xFF0000) >> 16;
        const auto nG = (tmp & 0xFF00) >> 8;
        const auto nB = (tmp & 0xFF);
        return RGB(nR, nG, nB);
    }
    IFACEMETHODIMP_(IZoneSet*) GetCurrentMonitorZoneSet(HMONITOR monitor) noexcept
    {
        //NOTE: as public method it's unsafe without lock, but it's called from AddZoneWindow through making ZoneWindow that causes deadlock
        //TODO: needs refactoring
        if (auto it = m_zoneWindowMap.find(monitor); it != m_zoneWindowMap.end())
        {
            return it->second->ActiveZoneSet();
        }
        return nullptr;
    }
    IFACEMETHODIMP_(int) GetZoneHighlightOpacity() noexcept
    {
        return m_settings->GetSettings().zoneHighlightOpacity;
    }

    LRESULT WndProc(HWND, UINT, WPARAM, LPARAM) noexcept;
    void OnDisplayChange(DisplayChangeType changeType) noexcept;
    void AddZoneWindow(HMONITOR monitor, PCWSTR deviceId) noexcept;
    void MoveWindowIntoZoneByIndex(HWND window, int index) noexcept;

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

    bool IsInterestingWindow(HWND window) noexcept;
    void UpdateZoneWindows() noexcept;
    void MoveWindowsOnDisplayChange() noexcept;
    void UpdateDragState(require_write_lock) noexcept;
    void CycleActiveZoneSet(DWORD vkCode) noexcept;
    bool OnSnapHotkey(DWORD vkCode) noexcept;
    void MoveSizeStartInternal(HWND window, HMONITOR monitor, POINT const& ptScreen, require_write_lock) noexcept;
    void MoveSizeEndInternal(HWND window, POINT const& ptScreen, require_write_lock) noexcept;
    void MoveSizeUpdateInternal(HMONITOR monitor, POINT const& ptScreen, require_write_lock) noexcept;
    void HandleVirtualDesktopUpdates(HANDLE fancyZonesDestroyedEvent) noexcept;

    const HINSTANCE m_hinstance{};

    HKEY m_virtualDesktopsRegKey{ nullptr };

    mutable std::shared_mutex m_lock;
    HWND m_window{};
    HWND m_windowMoveSize{}; // The window that is being moved/sized
    bool m_inMoveSize{}; // Whether or not a move/size operation is currently active
    bool m_dragEnabled{}; // True if we should be showing zone hints while dragging
    std::map<HMONITOR, winrt::com_ptr<IZoneWindow>> m_zoneWindowMap; // Map of monitor to ZoneWindow (one per monitor)
    winrt::com_ptr<IZoneWindow> m_zoneWindowMoveSize; // "Active" ZoneWindow, where the move/size is happening. Will update as drag moves between monitors.
    winrt::com_ptr<IFancyZonesSettings> m_settings{};
    GUID m_currentVirtualDesktopId{}; // UUID of the current virtual desktop. Is GUID_NULL until first VD switch per session.
    std::unordered_map<GUID, bool> m_virtualDesktopIds;
    wil::unique_handle m_terminateEditorEvent; // Handle of FancyZonesEditor.exe we launch and wait on
    wil::unique_handle m_terminateVirtualDesktopTrackerEvent;

    OnThreadExecutor m_dpiUnawareThread;
    OnThreadExecutor m_virtualDesktopTrackerThread;

    static UINT WM_PRIV_VDCHANGED; // Message to get back on to the UI thread when virtual desktop changes
    static UINT WM_PRIV_VDINIT; // Message to get back to the UI thread when FancyZones are initialized
    static UINT WM_PRIV_EDITOR; // Message to get back on to the UI thread when the editor exits

    // Did we terminate the editor or was it closed cleanly?
    enum class EditorExitKind : byte
    {
        Exit,
        Terminate
    };
};

UINT FancyZones::WM_PRIV_VDCHANGED = RegisterWindowMessage(L"{128c2cb0-6bdf-493e-abbe-f8705e04aa95}");
UINT FancyZones::WM_PRIV_VDINIT = RegisterWindowMessage(L"{469818a8-00fa-4069-b867-a1da484fcd9a}");
UINT FancyZones::WM_PRIV_EDITOR = RegisterWindowMessage(L"{87543824-7080-4e91-9d9c-0404642fc7b6}");

// IFancyZones
IFACEMETHODIMP_(void) FancyZones::Run() noexcept
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

    RegisterHotKey(m_window, 1, m_settings->GetSettings().editorHotkey.get_modifiers(), m_settings->GetSettings().editorHotkey.get_code());

    VirtualDesktopInitialize();

    m_dpiUnawareThread.submit(OnThreadExecutor::task_t{ [] {
                          SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_UNAWARE);
                          SetThreadDpiHostingBehavior(DPI_HOSTING_BEHAVIOR_MIXED);
                      } })
        .wait();

    if (RegOpenKeyEx(HKEY_CURRENT_USER, L"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops", 0, KEY_ALL_ACCESS, &m_virtualDesktopsRegKey) == ERROR_SUCCESS)
    {
        m_terminateVirtualDesktopTrackerEvent.reset(CreateEvent(nullptr, FALSE, FALSE, nullptr));
        m_virtualDesktopTrackerThread.submit(
            OnThreadExecutor::task_t{ std::bind(&FancyZones::HandleVirtualDesktopUpdates, this, m_terminateVirtualDesktopTrackerEvent.get()) });
    }
}

// IFancyZones
IFACEMETHODIMP_(void) FancyZones::Destroy() noexcept
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
    if (m_virtualDesktopsRegKey)
    {
        RegCloseKey(m_virtualDesktopsRegKey);
        m_virtualDesktopsRegKey = nullptr;
    }
}

// IFancyZonesCallback
IFACEMETHODIMP_(void) FancyZones::MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen) noexcept
{
    if (IsInterestingWindow(window))
    {
        std::unique_lock writeLock(m_lock);
        MoveSizeStartInternal(window, monitor, ptScreen, writeLock);
    }
}

// IFancyZonesCallback
IFACEMETHODIMP_(void) FancyZones::MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen) noexcept
{
    std::unique_lock writeLock(m_lock);
    MoveSizeUpdateInternal(monitor, ptScreen, writeLock);
}

// IFancyZonesCallback
IFACEMETHODIMP_(void) FancyZones::MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept
{
    if (window == m_windowMoveSize || IsInterestingWindow(window))
    {
        std::unique_lock writeLock(m_lock);
        MoveSizeEndInternal(window, ptScreen, writeLock);
    }
}

// IFancyZonesCallback
IFACEMETHODIMP_(void) FancyZones::VirtualDesktopChanged() noexcept
{
    // VirtualDesktopChanged is called from another thread but results in new windows being created.
    // Jump over to the UI thread to handle it.
    std::shared_lock readLock(m_lock);
    PostMessage(m_window, WM_PRIV_VDCHANGED, 0, 0);
}

// IFancyZonesCallback
IFACEMETHODIMP_(void) FancyZones::VirtualDesktopInitialize() noexcept
{
    PostMessage(m_window, WM_PRIV_VDINIT, 0, 0);
}

// IFancyZonesCallback
IFACEMETHODIMP_(void) FancyZones::WindowCreated(HWND window) noexcept
{
    if (m_settings->GetSettings().appLastZone_moveWindows && IsInterestingWindow(window))
    {
        auto processPath = get_process_path(window);
        if (!processPath.empty())
        {
            int zoneIndex = JSONHelpers::FancyZonesDataInstance().GetAppLastZone(window, processPath.data());

            if (zoneIndex != -1)
            {
                MoveWindowIntoZoneByIndex(window, zoneIndex);
            }
        }
    }
}

// IFancyZonesCallback
IFACEMETHODIMP_(bool) FancyZones::OnKeyDown(PKBDLLHOOKSTRUCT info) noexcept
{
    // Return true to swallow the keyboard event
    bool const shift = GetAsyncKeyState(VK_SHIFT) & 0x8000;
    bool const win = GetAsyncKeyState(VK_LWIN) & 0x8000;
    if (win && !shift)
    {
        bool const ctrl = GetAsyncKeyState(VK_CONTROL) & 0x8000;
        if (ctrl)
        {
            if ((info->vkCode >= '0') && (info->vkCode <= '9'))
            {
                // Win+Ctrl+Number will cycle through ZoneSets
                Trace::FancyZones::OnKeyDown(info->vkCode, win, ctrl, false /*inMoveSize*/);
                CycleActiveZoneSet(info->vkCode);
                return true;
            }
        }
        else if ((info->vkCode == VK_RIGHT) || (info->vkCode == VK_LEFT))
        {
            if (m_settings->GetSettings().overrideSnapHotkeys)
            {
                // Win+Left, Win+Right will cycle through Zones in the active ZoneSet
                Trace::FancyZones::OnKeyDown(info->vkCode, win, ctrl, false /*inMoveSize*/);
                return OnSnapHotkey(info->vkCode);
            }
        }
    }
    else if (m_inMoveSize && (info->vkCode >= '0') && (info->vkCode <= '9'))
    {
        // This allows you to cycle through ZoneSets while dragging a window
        Trace::FancyZones::OnKeyDown(info->vkCode, win, false /*control*/, true /*inMoveSize*/);
        CycleActiveZoneSet(info->vkCode);
        return false;
    }
    return false;
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

    HMONITOR monitor{};
    HWND foregroundWindow{};

    UINT dpi_x = DPIAware::DEFAULT_DPI;
    UINT dpi_y = DPIAware::DEFAULT_DPI;

    const bool use_cursorpos_editor_startupscreen = m_settings->GetSettings().use_cursorpos_editor_startupscreen;
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

    if (use_cursorpos_editor_startupscreen)
    {
        DPIAware::GetScreenDPIForPoint(currentCursorPos, dpi_x, dpi_y);
    }
    else
    {
        DPIAware::GetScreenDPIForWindow(foregroundWindow, dpi_x, dpi_y);
    }

    auto zoneWindow = iter->second;

    JSONHelpers::FancyZonesDataInstance().CustomZoneSetsToJsonFile(ZoneWindowUtils::GetCustomZoneSetsTmpPath());

    const auto taskbar_x_offset = MulDiv(mi.rcWork.left - mi.rcMonitor.left, DPIAware::DEFAULT_DPI, dpi_x);
    const auto taskbar_y_offset = MulDiv(mi.rcWork.top - mi.rcMonitor.top, DPIAware::DEFAULT_DPI, dpi_y);

    // Do not scale window params by the dpi, that will be done in the editor - see LayoutModel.Apply
    const auto x = mi.rcMonitor.left + taskbar_x_offset;
    const auto y = mi.rcMonitor.top + taskbar_y_offset;
    const auto width = mi.rcWork.right - mi.rcWork.left;
    const auto height = mi.rcWork.bottom - mi.rcWork.top;
    const std::wstring editorLocation =
        std::to_wstring(x) + L"_" +
        std::to_wstring(y) + L"_" +
        std::to_wstring(width) + L"_" +
        std::to_wstring(height);

    const auto& deviceInfo = JSONHelpers::FancyZonesDataInstance().GetDeviceInfoMap().at(zoneWindow->UniqueId());

    JSONHelpers::DeviceInfoJSON deviceInfoJson{ zoneWindow->UniqueId(), deviceInfo };
    JSONHelpers::FancyZonesDataInstance().SerializeDeviceInfoToTmpFile(deviceInfoJson, ZoneWindowUtils::GetActiveZoneSetTmpPath());

    const std::wstring params =
        /*1*/ std::to_wstring(reinterpret_cast<UINT_PTR>(monitor)) + L" " +
        /*2*/ editorLocation + L" " +
        /*3*/ zoneWindow->WorkAreaKey() + L" " +
        /*4*/ ZoneWindowUtils::GetActiveZoneSetTmpPath() + L" " +
        /*5*/ ZoneWindowUtils::GetAppliedZoneSetTmpPath() + L" " +
        /*6*/ ZoneWindowUtils::GetCustomZoneSetsTmpPath();

    SHELLEXECUTEINFO sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
    sei.lpFile = L"modules\\FancyZonesEditor.exe";
    sei.lpParameters = params.c_str();
    sei.nShow = SW_SHOWNORMAL;
    ShellExecuteEx(&sei);

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
    RegisterHotKey(m_window, 1, m_settings->GetSettings().editorHotkey.get_modifiers(), m_settings->GetSettings().editorHotkey.get_code());
}

// IZoneWindowHost
IFACEMETHODIMP_(void) FancyZones::MoveWindowsOnActiveZoneSetChange() noexcept
{
    if (m_settings->GetSettings().zoneSetChange_moveWindows)
    {
        MoveWindowsOnDisplayChange();
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
        if (message == WM_PRIV_VDCHANGED)
        {
            OnDisplayChange(DisplayChangeType::VirtualDesktop);
        }
        else if (message == WM_PRIV_VDINIT)
        {
            OnDisplayChange(DisplayChangeType::Initialization);
        }
        else if (message == WM_PRIV_EDITOR)
        {
            if (lparam == static_cast<LPARAM>(EditorExitKind::Exit))
            {
                // Don't reload settings if we terminated the editor
                OnDisplayChange(DisplayChangeType::Editor);
            }

            {
                // Clean up the event either way
                std::unique_lock writeLock(m_lock);
                m_terminateEditorEvent.release();
            }
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
        // Explorer persists this value to the registry on a per session basis but only after
        // the first virtual desktop switch happens. If the user hasn't switched virtual desktops in this session
        // then this value will be empty. This means loading the first virtual desktop's configuration can be
        // funky the first time we load up at boot since the user will not have switched virtual desktops yet.
        GUID currentVirtualDesktopId{};
        if (SUCCEEDED(RegistryHelpers::GetCurrentVirtualDesktop(&currentVirtualDesktopId)))
        {
            std::unique_lock writeLock(m_lock); 
            m_currentVirtualDesktopId = currentVirtualDesktopId;
        }
        else
        {
            // TODO: Use the previous "Desktop 1" fallback
            // Need to maintain a map of desktop name to virtual desktop uuid
        }
    }

    UpdateZoneWindows();

    if ((changeType == DisplayChangeType::WorkArea) || (changeType == DisplayChangeType::DisplayChange))
    {
        if (m_settings->GetSettings().displayChange_moveWindows)
        {
            MoveWindowsOnDisplayChange();
        }
    }
    else if (changeType == DisplayChangeType::VirtualDesktop)
    {
        if (m_settings->GetSettings().virtualDesktopChange_moveWindows)
        {
            MoveWindowsOnDisplayChange();
        }
    }
    else if (changeType == DisplayChangeType::Editor)
    {
        if (m_settings->GetSettings().zoneSetChange_moveWindows)
        {
            MoveWindowsOnDisplayChange();
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
        bool newVirtualDesktop = true;
        if (auto it = m_virtualDesktopIds.find(m_currentVirtualDesktopId); it != end(m_virtualDesktopIds))
        {
            newVirtualDesktop = it->second;
            JSONHelpers::FancyZonesDataInstance().SetActiveDeviceId(uniqueId);
        }
        const bool flash = m_settings->GetSettings().zoneSetChange_flashZones && newVirtualDesktop;

        if (auto zoneWindow = MakeZoneWindow(this, m_hinstance, monitor, uniqueId, flash))
        {
            m_zoneWindowMap[monitor] = std::move(zoneWindow);
        }
        m_virtualDesktopIds[m_currentVirtualDesktopId] = false;
    }
}

void FancyZones::MoveWindowIntoZoneByIndex(HWND window, int index) noexcept
{
    std::shared_lock readLock(m_lock);
    if (window != m_windowMoveSize)
    {
        if (const HMONITOR monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL))
        {
            auto iter = m_zoneWindowMap.find(monitor);
            if (iter != m_zoneWindowMap.end())
            {
                iter->second->MoveWindowIntoZoneByIndex(window, index);
            }
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

bool FancyZones::IsInterestingWindow(HWND window) noexcept
{
    auto filtered = get_fancyzones_filtered_window(window);
    if (!filtered.zonable)
    {
        return false;
    }
    // Filter out user specified apps
    CharUpperBuffW(filtered.process_path.data(), (DWORD)filtered.process_path.length());
    if (m_settings)
    {
        for (const auto& excluded : m_settings->GetSettings().excludedAppsArray)
        {
            if (filtered.process_path.find(excluded) != std::wstring::npos)
            {
                return false;
            }
        }
    }
    return true;
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

void FancyZones::MoveWindowsOnDisplayChange() noexcept
{
    auto callback = [](HWND window, LPARAM data) -> BOOL {
        int i = static_cast<int>(reinterpret_cast<UINT_PTR>(::GetProp(window, ZONE_STAMP)));
        if (i != 0)
        {
            // i is off by 1 since 0 is special.
            auto strongThis = reinterpret_cast<FancyZones*>(data);
            strongThis->MoveWindowIntoZoneByIndex(window, i - 1);
        }
        return TRUE;
    };
    EnumWindows(callback, reinterpret_cast<LPARAM>(this));
}

void FancyZones::UpdateDragState(require_write_lock) noexcept
{
    const bool shift = GetAsyncKeyState(VK_SHIFT) & 0x8000;
    const bool mouseL = GetAsyncKeyState(VK_LBUTTON) & 0x8000;
    const bool mouseR = GetAsyncKeyState(VK_RBUTTON) & 0x8000;
    const bool mouseM = GetAsyncKeyState(VK_MBUTTON) & 0x8000;
    const bool mouseX1 = GetAsyncKeyState(VK_XBUTTON1) & 0x8000;
    const bool mouseX2 = GetAsyncKeyState(VK_XBUTTON2) & 0x8000;

    // Note, Middle, X1 and X2 can also be used in addition to R/L
    bool mouse = mouseM | mouseX1 | mouseX2;
    // If the user has swapped their Right and Left Buttons, use the "Right" equivalent
    if (GetSystemMetrics(SM_SWAPBUTTON))
    {
        mouse |= mouseL;
    }
    else
    {
        mouse |= mouseR;
    }

    if (m_settings->GetSettings().shiftDrag)
    {
        m_dragEnabled = (shift | mouse);
    }
    else
    {
        m_dragEnabled = !(shift | mouse);
    }
}

void FancyZones::CycleActiveZoneSet(DWORD vkCode) noexcept
{
    auto window = GetForegroundWindow();
    if (IsInterestingWindow(window))
    {
        if (const HMONITOR monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL))
        {
            std::shared_lock readLock(m_lock);
            auto iter = m_zoneWindowMap.find(monitor);
            if (iter != m_zoneWindowMap.end())
            {
                iter->second->CycleActiveZoneSet(vkCode);
            }
        }
    }
}

bool FancyZones::OnSnapHotkey(DWORD vkCode) noexcept
{
    auto window = GetForegroundWindow();
    if (IsInterestingWindow(window))
    {
        if (const HMONITOR monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL))
        {
            std::shared_lock readLock(m_lock);
            auto iter = m_zoneWindowMap.find(monitor);
            if (iter != m_zoneWindowMap.end())
            {
                iter->second->MoveWindowIntoZoneByDirection(window, vkCode);
                return true;
            }
        }
    }
    return false;
}

void FancyZones::MoveSizeStartInternal(HWND window, HMONITOR monitor, POINT const& ptScreen, require_write_lock writeLock) noexcept
{
    // Only enter move/size if the cursor is inside the window rect by a certain padding.
    // This prevents resize from triggering zones.
    RECT windowRect{};
    ::GetWindowRect(window, &windowRect);

    const auto padding_x = 8;
    const auto padding_y = 6;
    windowRect.top += padding_y;
    windowRect.left += padding_x;
    windowRect.right -= padding_x;
    windowRect.bottom -= padding_y;

    if (PtInRect(&windowRect, ptScreen) == FALSE)
    {
        return;
    }

    m_inMoveSize = true;

    auto iter = m_zoneWindowMap.find(monitor);
    if (iter == end(m_zoneWindowMap))
    {
        return;
    }

    m_windowMoveSize = window;

    // This updates m_dragEnabled depending on if the shift key is being held down.
    UpdateDragState(writeLock);

    if (m_dragEnabled)
    {
        m_zoneWindowMoveSize = iter->second;
        m_zoneWindowMoveSize->MoveSizeEnter(window, m_dragEnabled);
    }
    else if (m_zoneWindowMoveSize)
    {
        m_zoneWindowMoveSize->MoveSizeCancel();
        m_zoneWindowMoveSize = nullptr;
    }
}

void FancyZones::MoveSizeEndInternal(HWND window, POINT const& ptScreen, require_write_lock) noexcept
{
    m_inMoveSize = false;
    m_dragEnabled = false;
    m_windowMoveSize = nullptr;
    if (m_zoneWindowMoveSize)
    {
        auto zoneWindow = std::move(m_zoneWindowMoveSize);
        zoneWindow->MoveSizeEnd(window, ptScreen);
    }
    else
    {
        ::RemoveProp(window, ZONE_STAMP);

        auto processPath = get_process_path(window);
        if (!processPath.empty())
        {
            JSONHelpers::FancyZonesDataInstance().SetAppLastZone(window, processPath.data(), -1);
        }
    }
}

void FancyZones::MoveSizeUpdateInternal(HMONITOR monitor, POINT const& ptScreen, require_write_lock writeLock) noexcept
{
    if (m_inMoveSize)
    {
        // This updates m_dragEnabled depending on if the shift key is being held down.
        UpdateDragState(writeLock);

        if (m_zoneWindowMoveSize)
        {
            // Update the ZoneWindow already handling move/size
            if (!m_dragEnabled)
            {
                // Drag got disabled, tell it to cancel and clear out m_zoneWindowMoveSize
                auto zoneWindow = std::move(m_zoneWindowMoveSize);
                zoneWindow->MoveSizeCancel();
            }
            else
            {
                auto iter = m_zoneWindowMap.find(monitor);
                if (iter != m_zoneWindowMap.end())
                {
                    if (iter->second != m_zoneWindowMoveSize)
                    {
                        // The drag has moved to a different monitor.
                        auto const isDragEnabled = m_zoneWindowMoveSize->IsDragEnabled();
                        m_zoneWindowMoveSize->MoveSizeCancel();
                        m_zoneWindowMoveSize = iter->second;
                        m_zoneWindowMoveSize->MoveSizeEnter(m_windowMoveSize, isDragEnabled);
                    }
                    m_zoneWindowMoveSize->MoveSizeUpdate(ptScreen, m_dragEnabled);
                }
            }
        }
        else if (m_dragEnabled)
        {
            // We'll get here if the user presses/releases shift while dragging.
            // Restart the drag on the ZoneWindow that m_windowMoveSize is on
            MoveSizeStartInternal(m_windowMoveSize, monitor, ptScreen, writeLock);
            MoveSizeUpdateInternal(monitor, ptScreen, writeLock);
        }
    }
}

void FancyZones::HandleVirtualDesktopUpdates(HANDLE fancyZonesDestroyedEvent) noexcept
{
    HANDLE regKeyEvent = CreateEvent(nullptr, FALSE, FALSE, nullptr);
    HANDLE events[2] = { regKeyEvent, fancyZonesDestroyedEvent };
    while (1)
    {
        if (RegNotifyChangeKeyValue(HKEY_CURRENT_USER, TRUE, REG_NOTIFY_CHANGE_LAST_SET, regKeyEvent, TRUE) != ERROR_SUCCESS)
        {
            return;
        }
        if (WaitForMultipleObjects(2, events, FALSE, INFINITE) != (WAIT_OBJECT_0 + 0))
        {
            // if fancyZonesDestroyedEvent is signalized or WaitForMultipleObjects failed, terminate thread execution
            return;
        }
        DWORD bufferCapacity;
        const WCHAR* key = L"VirtualDesktopIDs";
        // request regkey binary buffer capacity only
        if (RegQueryValueExW(m_virtualDesktopsRegKey, key, 0, nullptr, nullptr, &bufferCapacity) != ERROR_SUCCESS)
        {
            return;
        }
        std::unique_ptr<BYTE[]> buffer = std::make_unique<BYTE[]>(bufferCapacity);
        // request regkey binary content
        if (RegQueryValueExW(m_virtualDesktopsRegKey, key, 0, nullptr, buffer.get(), &bufferCapacity) != ERROR_SUCCESS)
        {
            return;
        }
        const int guidSize = sizeof(GUID);
        std::unordered_map<GUID, bool> temp;
        temp.reserve(bufferCapacity / guidSize);
        for (size_t i = 0; i < bufferCapacity; i += guidSize)
        {
            GUID* guid = reinterpret_cast<GUID*>(buffer.get() + i);
            temp[*guid] = true;
        }
        std::unique_lock writeLock(m_lock);
        for (auto it = begin(m_virtualDesktopIds); it != end(m_virtualDesktopIds);)
        {
            if (auto iter = temp.find(it->first); iter == temp.end())
            {
                it = m_virtualDesktopIds.erase(it); // virtual desktop closed, remove it from map
            }
            else
            {
                temp.erase(it->first); // virtual desktop already in map, skip it
                ++it;
            }
        }
        // register new virtual desktops, if any
        m_virtualDesktopIds.insert(begin(temp), end(temp));
    }
}

winrt::com_ptr<IFancyZones> MakeFancyZones(HINSTANCE hinstance, const winrt::com_ptr<IFancyZonesSettings>& settings) noexcept
{
    if (!settings)
    {
        return nullptr;
    }

    return winrt::make_self<FancyZones>(hinstance, settings);
}