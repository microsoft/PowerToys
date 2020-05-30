#include "pch.h"
#include "WindowMoveHandler.h"

#include <common/notifications.h>
#include <common/notifications/fancyzones_notifications.h>
#include <common/window_helpers.h>

#include "lib/Settings.h"
#include "lib/ZoneWindow.h"
#include "lib/util.h"
#include "VirtualDesktopUtils.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

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
    WindowMoveHandlerPrivate(const winrt::com_ptr<IFancyZonesSettings>& settings) :
        m_settings(settings)
    {};

    bool IsDragEnabled() const noexcept
    {
        return m_dragEnabled;
    }

    bool InMoveSize() const noexcept
    {
        return m_inMoveSize;
    }

    void MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;
    void MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;
    void MoveSizeEnd(HWND window, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;

    void MoveWindowIntoZoneByIndexSet(HWND window, HMONITOR monitor, const std::vector<int>& indexSet, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept;
    bool MoveWindowIntoZoneByDirection(HMONITOR monitor, HWND window, DWORD vkCode, bool cycle, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap);

private:
    void UpdateDragState(HWND window) noexcept;

private:
    winrt::com_ptr<IFancyZonesSettings> m_settings{};

    HWND m_windowMoveSize{}; // The window that is being moved/sized
    bool m_inMoveSize{}; // Whether or not a move/size operation is currently active
    winrt::com_ptr<IZoneWindow> m_zoneWindowMoveSize; // "Active" ZoneWindow, where the move/size is happening. Will update as drag moves between monitors.
    bool m_dragEnabled{}; // True if we should be showing zone hints while dragging
};


WindowMoveHandler::WindowMoveHandler(const winrt::com_ptr<IFancyZonesSettings>& settings) :
    pimpl(new WindowMoveHandlerPrivate(settings))
{
    
}

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

void WindowMoveHandler::MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    pimpl->MoveSizeStart(window, monitor, ptScreen, zoneWindowMap);
}

void WindowMoveHandler::MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    pimpl->MoveSizeUpdate(monitor, ptScreen, zoneWindowMap);
}

void WindowMoveHandler::MoveSizeEnd(HWND window, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    pimpl->MoveSizeEnd(window, ptScreen, zoneWindowMap);
}

void WindowMoveHandler::MoveWindowIntoZoneByIndexSet(HWND window, HMONITOR monitor, const std::vector<int>& indexSet, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    pimpl->MoveWindowIntoZoneByIndexSet(window, monitor, indexSet, zoneWindowMap);
}

bool WindowMoveHandler::MoveWindowIntoZoneByDirection(HMONITOR monitor, HWND window, DWORD vkCode, bool cycle, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap)
{
    return pimpl->MoveWindowIntoZoneByDirection(monitor, window, vkCode, cycle, zoneWindowMap);
}

void WindowMoveHandlerPrivate::MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
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

    // This updates m_dragEnabled depending on if the shift key is being held down.
    UpdateDragState(window);

    if (m_dragEnabled)
    {
        m_zoneWindowMoveSize = iter->second;
        m_zoneWindowMoveSize->MoveSizeEnter(window, m_dragEnabled);
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
        m_zoneWindowMoveSize->RestoreOriginalTransparency();
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

void WindowMoveHandlerPrivate::MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    if (!m_inMoveSize)
    {
        return;
    }

    // This updates m_dragEnabled depending on if the shift key is being held down.
    UpdateDragState(m_windowMoveSize);

    if (m_zoneWindowMoveSize)
    {
        // Update the ZoneWindow already handling move/size
        if (!m_dragEnabled)
        {
            // Drag got disabled, tell it to cancel and hide all windows
            m_zoneWindowMoveSize = nullptr;

            for (auto [keyMonitor, zoneWindow] : zoneWindowMap)
            {
                if (zoneWindow)
                {
                    zoneWindow->RestoreOriginalTransparency();
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
                    m_zoneWindowMoveSize->RestoreOriginalTransparency();

                    if (!m_settings->GetSettings()->showZonesOnAllMonitors)
                    {
                        m_zoneWindowMoveSize->HideZoneWindow();
                    }
                    m_zoneWindowMoveSize = iter->second;
                    m_zoneWindowMoveSize->MoveSizeEnter(m_windowMoveSize, m_zoneWindowMoveSize->IsDragEnabled());
                }

                for (auto [keyMonitor, zoneWindow] : zoneWindowMap)
                {
                    zoneWindow->MoveSizeUpdate(ptScreen, m_dragEnabled);
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

void WindowMoveHandlerPrivate::MoveSizeEnd(HWND window, POINT const& ptScreen, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    if (window != m_windowMoveSize && !IsInterestingWindow(window, m_settings->GetSettings()->excludedAppsArray))
    {
        return;
    }

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
        ::RemoveProp(window, MULTI_ZONE_STAMP);

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
                        JSONHelpers::FancyZonesDataInstance().RemoveAppLastZone(window, zoneWindowPtr->UniqueId(), guidString.get());
                    }
                }
            }
        }
    }

    // Also, hide all windows (regardless of settings)
    for (auto [keyMonitor, zoneWindow] : zoneWindowMap)
    {
        if (zoneWindow)
        {
            zoneWindow->HideZoneWindow();
        }
    }
}

void WindowMoveHandlerPrivate::MoveWindowIntoZoneByIndexSet(HWND window, HMONITOR monitor, const std::vector<int>& indexSet, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap) noexcept
{
    if (window != m_windowMoveSize)
    {
        const HMONITOR hm = (monitor != nullptr) ? monitor : MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
        if (hm)
        {
            auto zoneWindow = zoneWindowMap.find(hm);
            if (zoneWindow != zoneWindowMap.end())
            {
                const auto& zoneWindowPtr = zoneWindow->second;
                // Only process windows located on currently active work area.
                GUID windowDesktopId{};
                GUID zoneWindowDesktopId{};
                if (VirtualDesktopUtils::GetWindowDesktopId(window, &windowDesktopId) &&
                    VirtualDesktopUtils::GetZoneWindowDesktopId(zoneWindowPtr.get(), &zoneWindowDesktopId) &&
                    (windowDesktopId != zoneWindowDesktopId))
                {
                    return;
                }
                zoneWindowPtr->MoveWindowIntoZoneByIndexSet(window, indexSet);
            }
        }
    }
}

bool WindowMoveHandlerPrivate::MoveWindowIntoZoneByDirection(HMONITOR monitor, HWND window, DWORD vkCode, bool cycle, const std::map<HMONITOR, winrt::com_ptr<IZoneWindow>>& zoneWindowMap)
{
    auto iter = zoneWindowMap.find(monitor);
    if (iter != std::end(zoneWindowMap))
    {
        const auto& zoneWindowPtr = iter->second;
        return zoneWindowPtr->MoveWindowIntoZoneByDirection(window, vkCode, cycle);
    }
    return false;
}

void WindowMoveHandlerPrivate::UpdateDragState(HWND window) noexcept
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

    mouse &= m_settings->GetSettings()->mouseSwitch;

    if (m_settings->GetSettings()->shiftDrag)
    {
        m_dragEnabled = (shift | mouse);
    }
    else
    {
        m_dragEnabled = !(shift | mouse);
    }

    static bool warning_shown = false;
    if (!is_process_elevated() && IsProcessOfWindowElevated(window))
    {
        m_dragEnabled = false;
        if (!warning_shown && !is_cant_drag_elevated_warning_disabled())
        {
            std::vector<notifications::action_t> actions = {
                notifications::link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_LEARN_MORE), L"https://aka.ms/powertoysDetectedElevatedHelp" },
                notifications::link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_DIALOG_DONT_SHOW_AGAIN), L"powertoys://cant_drag_elevated_disable/" }
            };
            notifications::show_toast_with_activations(GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED), {}, std::move(actions));
            warning_shown = true;
        }
    }
}
