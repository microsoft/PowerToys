#include "pch.h"
#include "WindowMoveHandler.h"

#include <common/notifications.h>
#include <common/notifications/fancyzones_notifications.h>
#include <common/window_helpers.h>
#include <common/dpi_aware.h>

#include "lib/Settings.h"
#include "lib/ZoneWindow.h"
#include "lib/util.h"
#include "VirtualDesktopUtils.h"
#include "lib/SecondaryMouseButtonsHook.h"
#include "lib/GenericKeyHook.h"
#include "lib/FancyZonesData.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t FancyZonesRunAsAdminInfoPage[] = L"https://aka.ms/powertoysDetectedElevatedHelp";
    const wchar_t ToastNotificationButtonUrl[] = L"powertoys://cant_drag_elevated_disable/";
}

namespace WindowMoveHandlerUtils
{
    bool IsCursorTypeIndicatingSizeEvent()
    {
        CURSORINFO cursorInfo = { 0 };
        cursorInfo.cbSize = sizeof(cursorInfo);

        if (::GetCursorInfo(&cursorInfo))
        {
            if (::LoadCursor(NULL, IDC_SIZENS) == cursorInfo.hCursor)
            {
                return true;
            }
            if (::LoadCursor(NULL, IDC_SIZEWE) == cursorInfo.hCursor)
            {
                return true;
            }
            if (::LoadCursor(NULL, IDC_SIZENESW) == cursorInfo.hCursor)
            {
                return true;
            }
            if (::LoadCursor(NULL, IDC_SIZENWSE) == cursorInfo.hCursor)
            {
                return true;
            }
        }
        return false;
    }
}

class WindowMoveHandlerPrivate
{
public:
    WindowMoveHandlerPrivate(const winrt::com_ptr<IFancyZonesSettings>& settings, SecondaryMouseButtonsHook* mouseHook, ShiftKeyHook* shiftHook, CtrlKeyHook* ctrlHook) :
        m_settings(settings),
        m_mouseHook(mouseHook),
        m_shiftHook(shiftHook),
        m_ctrlHook(ctrlHook)
    {
    }

    bool IsDragEnabled() const noexcept
    {
        return m_dragEnabled;
    }

    bool InMoveSize() const noexcept
    {
        return m_inMoveSize;
    }

    void OnMouseDown() noexcept;
    void OnShiftChangeState(bool state) noexcept;
    void OnCtrlChangeState(bool state) noexcept;

    void MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;
    void MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;
    void MoveSizeEnd(HWND window, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;

    void MoveWindowIntoZoneByIndexSet(HWND window, const std::vector<int>& indexSet, winrt::com_ptr<IZoneWindow> zoneWindow) noexcept;
    bool MoveWindowIntoZoneByDirection(HWND window, DWORD vkCode, bool cycle, winrt::com_ptr<IZoneWindow> zoneWindow);

private:
    void WarnIfElevationIsRequired(HWND window) noexcept;
    void UpdateDragState() noexcept;

    void SetWindowTransparency(HWND window) noexcept;
    void ResetWindowTransparency() noexcept;

private:
    winrt::com_ptr<IFancyZonesSettings> m_settings{};
    SecondaryMouseButtonsHook* m_mouseHook{};
    ShiftKeyHook* m_shiftHook{};
    CtrlKeyHook* m_ctrlHook{};

    HWND m_windowMoveSize{}; // The window that is being moved/sized
    bool m_inMoveSize{}; // Whether or not a move/size operation is currently active
    winrt::com_ptr<IZoneWindow> m_zoneWindowMoveSize; // "Active" ZoneWindow, where the move/size is happening. Will update as drag moves between monitors.
    bool m_dragEnabled{}; // True if we should be showing zone hints while dragging
    bool m_secondaryMouseButtonState{}; // True when secondary mouse button was clicked after window was moved
    bool m_shiftKeyState{}; // True when shift key was pressed after window was moved
    bool m_ctrlKeyState{}; // True when ctrl key was pressed after window was moved

    struct WindowTransparencyProperties
    {
        HWND draggedWindow = nullptr;
        long draggedWindowExstyle = 0;
        COLORREF draggedWindowCrKey = RGB(0, 0, 0);
        DWORD draggedWindowDwFlags = 0;
        BYTE draggedWindowInitialAlpha = 0;
    } m_windowTransparencyProperties;
};

WindowMoveHandler::WindowMoveHandler(const winrt::com_ptr<IFancyZonesSettings>& settings, SecondaryMouseButtonsHook* mouseHook, ShiftKeyHook* shiftHook, CtrlKeyHook* ctrlHook) :
    pimpl(new WindowMoveHandlerPrivate(settings, mouseHook, shiftHook, ctrlHook)) {}

WindowMoveHandler::~WindowMoveHandler()
{
    delete pimpl;
}

bool WindowMoveHandler::InMoveSize() const noexcept
{
    return pimpl->InMoveSize();
}

bool WindowMoveHandler::IsDragEnabled() const noexcept
{
    return pimpl->IsDragEnabled();
}

void WindowMoveHandler::OnMouseDown() noexcept
{
    pimpl->OnMouseDown();
}

void WindowMoveHandler::OnShiftChangeState(bool state) noexcept
{
    pimpl->OnShiftChangeState(state);
}

void WindowMoveHandler::OnCtrlChangeState(bool state) noexcept
{
    pimpl->OnCtrlChangeState(state);
}

void WindowMoveHandler::MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    pimpl->MoveSizeStart(window, monitor, ptScreen, zoneWindowMap);
}

void WindowMoveHandler::MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    pimpl->MoveSizeUpdate(monitor, ptScreen, zoneWindowMap);
}

void WindowMoveHandler::MoveSizeEnd(HWND window, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    pimpl->MoveSizeEnd(window, ptScreen, zoneWindowMap);
}

void WindowMoveHandler::MoveWindowIntoZoneByIndexSet(HWND window, const std::vector<int>& indexSet, winrt::com_ptr<IZoneWindow> zoneWindow) noexcept
{
    pimpl->MoveWindowIntoZoneByIndexSet(window, indexSet, zoneWindow);
}

bool WindowMoveHandler::MoveWindowIntoZoneByDirection(HWND window, DWORD vkCode, bool cycle, winrt::com_ptr<IZoneWindow> zoneWindow)
{
    return pimpl->MoveWindowIntoZoneByDirection(window, vkCode, cycle, zoneWindow);
}

void WindowMoveHandlerPrivate::OnMouseDown() noexcept
{
    m_secondaryMouseButtonState = !m_secondaryMouseButtonState;
}

void WindowMoveHandlerPrivate::OnShiftChangeState(bool state) noexcept
{
    m_shiftKeyState = state;
}

void WindowMoveHandlerPrivate::OnCtrlChangeState(bool state) noexcept
{
    m_ctrlKeyState = state;
}

void WindowMoveHandlerPrivate::MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    if (!IsInterestingWindow(window, m_settings->GetSettings()->excludedAppsArray) || WindowMoveHandlerUtils::IsCursorTypeIndicatingSizeEvent())
    {
        return;
    }

    m_inMoveSize = true;

    auto iter = zoneWindowMap.find(monitor);
    if (iter == end(zoneWindowMap))
    {
        return;
    }

    m_windowMoveSize = window;

    if (m_settings->GetSettings()->mouseSwitch)
    {
        m_mouseHook->enable();
    }

    m_shiftHook->enable();
    m_ctrlHook->enable();

    // This updates m_dragEnabled depending on if the shift key is being held down
    UpdateDragState();

    // Notifies user if unable to drag elevated window
    WarnIfElevationIsRequired(window);

    if (m_dragEnabled)
    {
        m_zoneWindowMoveSize = iter->second;
        SetWindowTransparency(m_windowMoveSize);
        m_zoneWindowMoveSize->MoveSizeEnter(m_windowMoveSize);
        if (m_settings->GetSettings()->showZonesOnAllMonitors)
        {
            for (auto [keyMonitor, zoneWindow] : zoneWindowMap)
            {
                // Skip calling ShowZoneWindow for iter->second (m_zoneWindowMoveSize) since it
                // was already called in MoveSizeEnter
                const bool moveSizeEnterCalled = zoneWindow == m_zoneWindowMoveSize;
                if (zoneWindow && !moveSizeEnterCalled)
                {
                    zoneWindow->ShowZoneWindow();
                }
            }
        }
    }
    else if (m_zoneWindowMoveSize)
    {
        ResetWindowTransparency();
        m_zoneWindowMoveSize = nullptr;
        for (auto [keyMonitor, zoneWindow] : zoneWindowMap)
        {
            if (zoneWindow)
            {
                zoneWindow->HideZoneWindow();
            }
        }
    }
}

void WindowMoveHandlerPrivate::MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    if (!m_inMoveSize)
    {
        return;
    }

    // This updates m_dragEnabled depending on if the shift key is being held down.
    UpdateDragState();

    if (m_zoneWindowMoveSize)
    {
        // Update the ZoneWindow already handling move/size
        if (!m_dragEnabled)
        {
            // Drag got disabled, tell it to cancel and hide all windows
            m_zoneWindowMoveSize = nullptr;
            ResetWindowTransparency();

            for (auto [keyMonitor, zoneWindow] : zoneWindowMap)
            {
                if (zoneWindow)
                {
                    zoneWindow->HideZoneWindow();
                }
            }
        }
        else
        {
            auto iter = zoneWindowMap.find(monitor);
            if (iter != zoneWindowMap.end())
            {
                if (iter->second != m_zoneWindowMoveSize)
                {
                    // The drag has moved to a different monitor.
                    m_zoneWindowMoveSize->ClearSelectedZones();
                    if (!m_settings->GetSettings()->showZonesOnAllMonitors)
                    {
                        m_zoneWindowMoveSize->HideZoneWindow();
                    }

                    m_zoneWindowMoveSize = iter->second;
                    m_zoneWindowMoveSize->MoveSizeEnter(m_windowMoveSize);
                }

                for (auto [keyMonitor, zoneWindow] : zoneWindowMap)
                {
                    zoneWindow->MoveSizeUpdate(ptScreen, m_dragEnabled, m_ctrlKeyState);
                }
            }
        }
    }
    else if (m_dragEnabled)
    {
        // We'll get here if the user presses/releases shift while dragging.
        // Restart the drag on the ZoneWindow that m_windowMoveSize is on
        MoveSizeStart(m_windowMoveSize, monitor, ptScreen, zoneWindowMap);
        MoveSizeUpdate(monitor, ptScreen, zoneWindowMap);
    }
}

void WindowMoveHandlerPrivate::MoveSizeEnd(HWND window, POINT const& ptScreen, const std::unordered_map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    if (window != m_windowMoveSize && !IsInterestingWindow(window, m_settings->GetSettings()->excludedAppsArray))
    {
        return;
    }

    m_mouseHook->disable();
    m_shiftHook->disable();
    m_ctrlHook->disable();

    if (m_zoneWindowMoveSize)
    {
        auto zoneWindow = std::move(m_zoneWindowMoveSize);
        ResetWindowTransparency();
        zoneWindow->MoveSizeEnd(m_windowMoveSize, ptScreen);
    }
    else
    {
        if (m_settings->GetSettings()->restoreSize)
        {
            if (WindowMoveHandlerUtils::IsCursorTypeIndicatingSizeEvent())
            {
                ::RemoveProp(window, ZonedWindowProperties::PropertyRestoreSizeID);
            }
            else if (!IsWindowMaximized(window))
            {
                RestoreWindowSize(window);
            }
        }

        auto monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
        if (monitor)
        {
            auto zoneWindow = zoneWindowMap.find(monitor);
            if (zoneWindow != zoneWindowMap.end())
            {
                const auto zoneWindowPtr = zoneWindow->second;
                const auto activeZoneSet = zoneWindowPtr->ActiveZoneSet();
                if (activeZoneSet)
                {
                    wil::unique_cotaskmem_string guidString;
                    if (SUCCEEDED_LOG(StringFromCLSID(activeZoneSet->Id(), &guidString)))
                    {
                        FancyZonesDataInstance().RemoveAppLastZone(window, zoneWindowPtr->UniqueId(), guidString.get());
                    }
                }
            }
        }
        ::RemoveProp(window, ZonedWindowProperties::PropertyMultipleZoneID);
    }
    
    m_inMoveSize = false;
    m_dragEnabled = false;
    m_secondaryMouseButtonState = false;
    m_windowMoveSize = nullptr;

    // Also, hide all windows (regardless of settings)
    for (auto [keyMonitor, zoneWindow] : zoneWindowMap)
    {
        if (zoneWindow)
        {
            zoneWindow->HideZoneWindow();
        }
    }
}

void WindowMoveHandlerPrivate::MoveWindowIntoZoneByIndexSet(HWND window, const std::vector<int>& indexSet, winrt::com_ptr<IZoneWindow> zoneWindow) noexcept
{
    if (window != m_windowMoveSize)
    {
        zoneWindow->MoveWindowIntoZoneByIndexSet(window, indexSet);
    }
}

bool WindowMoveHandlerPrivate::MoveWindowIntoZoneByDirection(HWND window, DWORD vkCode, bool cycle, winrt::com_ptr<IZoneWindow> zoneWindow)
{
    return zoneWindow && zoneWindow->MoveWindowIntoZoneByDirection(window, vkCode, cycle);
}

void WindowMoveHandlerPrivate::WarnIfElevationIsRequired(HWND window) noexcept
{
    static bool warning_shown = false;
    if (!is_process_elevated() && IsProcessOfWindowElevated(window))
    {
        m_dragEnabled = false;
        if (!warning_shown && !is_cant_drag_elevated_warning_disabled())
        {
            std::vector<notifications::action_t> actions = {
                notifications::link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_LEARN_MORE), NonLocalizable::FancyZonesRunAsAdminInfoPage },
                notifications::link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_DIALOG_DONT_SHOW_AGAIN), NonLocalizable::ToastNotificationButtonUrl }
            };
            notifications::show_toast_with_activations(GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED),
                                                       GET_RESOURCE_STRING(IDS_FANCYZONES),
                                                       {},
                                                       std::move(actions));
            warning_shown = true;
        }
    }
}

void WindowMoveHandlerPrivate::UpdateDragState() noexcept
{
    if (m_settings->GetSettings()->shiftDrag)
    {
        m_dragEnabled = (m_shiftKeyState ^ m_secondaryMouseButtonState);
    }
    else
    {
        m_dragEnabled = !(m_shiftKeyState ^ m_secondaryMouseButtonState);
    }
}

void WindowMoveHandlerPrivate::SetWindowTransparency(HWND window) noexcept
{
    if (m_settings->GetSettings()->makeDraggedWindowTransparent)
    {
        m_windowTransparencyProperties.draggedWindowExstyle = GetWindowLong(window, GWL_EXSTYLE);

        m_windowTransparencyProperties.draggedWindow = window;
        SetWindowLong(window,
                      GWL_EXSTYLE,
                      m_windowTransparencyProperties.draggedWindowExstyle | WS_EX_LAYERED);

        GetLayeredWindowAttributes(window, &m_windowTransparencyProperties.draggedWindowCrKey, &m_windowTransparencyProperties.draggedWindowInitialAlpha, &m_windowTransparencyProperties.draggedWindowDwFlags);

        SetLayeredWindowAttributes(window, 0, (255 * 50) / 100, LWA_ALPHA);
    }
}

void WindowMoveHandlerPrivate::ResetWindowTransparency() noexcept
{
    if (m_settings->GetSettings()->makeDraggedWindowTransparent && m_windowTransparencyProperties.draggedWindow != nullptr)
    {
        SetLayeredWindowAttributes(m_windowTransparencyProperties.draggedWindow, m_windowTransparencyProperties.draggedWindowCrKey, m_windowTransparencyProperties.draggedWindowInitialAlpha, m_windowTransparencyProperties.draggedWindowDwFlags);
        SetWindowLong(m_windowTransparencyProperties.draggedWindow, GWL_EXSTYLE, m_windowTransparencyProperties.draggedWindowExstyle);
        m_windowTransparencyProperties.draggedWindow = nullptr;
    }
}
