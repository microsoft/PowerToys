#include "pch.h"
#include "WindowDrag.h"

#include <FancyZonesLib/FancyZonesData/AppZoneHistory.h>
#include <FancyZonesLib/FancyZonesWindowProcessing.h>
#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/NotificationUtil.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/WindowUtils.h>
#include <FancyZonesLib/WorkArea.h>

#include <FancyZonesLib/trace.h>

#include <common/utils/elevation.h>

WindowDrag::WindowDrag(HWND window, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas) :
    m_window(window),
    m_activeWorkAreas(activeWorkAreas),
    m_currentWorkArea(nullptr),
    m_snappingMode(false)
{
    m_windowProperties.hasNoVisibleOwner = !FancyZonesWindowUtils::HasVisibleOwner(m_window);
    m_windowProperties.isStandardWindow = FancyZonesWindowUtils::IsStandardWindow(m_window) &&
                                          (!FancyZonesWindowUtils::IsPopupWindow(m_window) || FancyZonesSettings::settings().allowSnapPopupWindows);
}

WindowDrag::~WindowDrag()
{
    ResetWindowTransparency();
}

std::unique_ptr<WindowDrag> WindowDrag::Create(HWND window, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas)
{
    if (!FancyZonesWindowProcessing::IsProcessable(window) ||
        !FancyZonesWindowUtils::IsCandidateForZoning(window) ||
        FancyZonesWindowUtils::IsCursorTypeIndicatingSizeEvent())
    {
        return nullptr;
    }

    if (!is_process_elevated() && FancyZonesWindowUtils::IsProcessOfWindowElevated(window))
    {
        // Notifies user if unable to drag elevated window
        FancyZonesNotifications::WarnIfElevationIsRequired();
        return nullptr;
    }

    return std::unique_ptr<WindowDrag>(new WindowDrag(window, activeWorkAreas));
}

bool WindowDrag::MoveSizeStart(HMONITOR monitor, bool isSnapping)
{
    auto iter = m_activeWorkAreas.find(monitor);
    if (iter == end(m_activeWorkAreas))
    {
        return false;
    }

    m_currentWorkArea = iter->second.get();
    if (!m_currentWorkArea)
    {
        return false;
    }

    m_currentWorkArea->UnsnapWindow(m_window);
    SwitchSnappingMode(isSnapping);
    
    return true;
}

void WindowDrag::MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, bool isSnapping, bool isSelectManyZonesState)
{
    SwitchSnappingMode(isSnapping);

    auto iter = m_activeWorkAreas.find(monitor);
    if (isSnapping && iter != m_activeWorkAreas.end())
    {
        // The drag has moved to a different monitor.
        // Change work area
        if (iter->second.get() != m_currentWorkArea)
        {
            m_highlightedZones.Reset();

            if (m_currentWorkArea)
            {
                if (!FancyZonesSettings::settings().showZonesOnAllMonitors)
                {
                    m_currentWorkArea->HideZonesOverlay();
                }
                else
                {
                    m_currentWorkArea->ShowZonesOverlay({}, m_window);
                }
            }
            
            m_currentWorkArea = iter->second.get();
        }

        if (m_currentWorkArea)
        {
            POINT ptClient = ptScreen;
            MapWindowPoints(nullptr, m_currentWorkArea->GetWorkAreaWindow(), &ptClient, 1);
            const bool redraw = m_highlightedZones.Update(m_currentWorkArea->GetLayout().get(), ptClient, isSelectManyZonesState);
            if (redraw)
            {
                m_currentWorkArea->ShowZonesOverlay(m_highlightedZones.Zones(), m_window);
            }
        }
    }
}

void WindowDrag::MoveSizeEnd()
{
    if (m_snappingMode)
    {   
        const bool hasNoVisibleOwner = !FancyZonesWindowUtils::HasVisibleOwner(m_window);
        const bool isStandardWindow = FancyZonesWindowUtils::IsStandardWindow(m_window);

        if ((isStandardWindow == false && hasNoVisibleOwner == true &&
             m_windowProperties.isStandardWindow == true && m_windowProperties.hasNoVisibleOwner == true) ||
             FancyZonesWindowUtils::IsWindowMaximized(m_window))
        {
            // Abort the zoning, this is a Chromium based tab that is merged back with an existing window
            // or if the window is maximized by Windows when the cursor hits the screen top border
        }
        else if (m_currentWorkArea)
        {
            m_currentWorkArea->MoveWindowIntoZoneByIndexSet(m_window, m_highlightedZones.Zones());
        }
    }
    else
    {
        FancyZonesWindowUtils::ResetRoundCornersPreference(m_window);
        if (FancyZonesSettings::settings().restoreSize)
        {
            if (FancyZonesWindowUtils::IsCursorTypeIndicatingSizeEvent())
            {
                ::RemoveProp(m_window, ZonedWindowProperties::PropertyRestoreSizeID);
            }
            else if (!FancyZonesWindowUtils::IsWindowMaximized(m_window))
            {
                FancyZonesWindowUtils::RestoreWindowSize(m_window);
            }
        }
    }

    SwitchSnappingMode(false);
}

void WindowDrag::SwitchSnappingMode(bool isSnapping)
{
    if (!m_currentWorkArea)
    {
        return;
    }

    if (!m_snappingMode && isSnapping) // turn on
    {
        SetWindowTransparency();

        // init active layout
        m_currentWorkArea->ShowZonesOverlay(m_highlightedZones.Zones(), m_window);

        // init layouts on other monitors
        if (FancyZonesSettings::settings().showZonesOnAllMonitors)
        {
            for (const auto& [_, workArea] : m_activeWorkAreas)
            {
                if (workArea && workArea.get() != m_currentWorkArea)
                {
                    workArea->ShowZonesOverlay({}, m_window);
                }
            }
        }

        Trace::WorkArea::MoveOrResizeStarted(m_currentWorkArea->GetLayout().get(), m_currentWorkArea->GetLayoutWindows().get());
    }
    else if (m_snappingMode && !isSnapping) // turn off
    {
        ResetWindowTransparency();
        m_highlightedZones.Reset();

        // Hide all layouts (regardless of settings)
        for (auto& [_, workArea] : m_activeWorkAreas)
        {
            if (workArea)
            {
                workArea->HideZonesOverlay();
            }
        }

        Trace::WorkArea::MoveOrResizeEnd(m_currentWorkArea->GetLayout().get(), m_currentWorkArea->GetLayoutWindows().get());
    }

    m_snappingMode = isSnapping;
}

void WindowDrag::SetWindowTransparency()
{
    if (FancyZonesSettings::settings().makeDraggedWindowTransparent)
    {
        m_windowProperties.exstyle = GetWindowLong(m_window, GWL_EXSTYLE);

        SetWindowLong(m_window, GWL_EXSTYLE, m_windowProperties.exstyle | WS_EX_LAYERED);

        if (!GetLayeredWindowAttributes(m_window, &m_windowProperties.crKey, &m_windowProperties.alpha, &m_windowProperties.dwFlags))
        {
            Logger::error(L"Window transparency: GetLayeredWindowAttributes failed, {}", get_last_error_or_default(GetLastError()));
            return;
        }

        if (!SetLayeredWindowAttributes(m_window, 0, (255 * 50) / 100, LWA_ALPHA))
        {
            Logger::error(L"Window transparency: SetLayeredWindowAttributes failed, {}", get_last_error_or_default(GetLastError()));
            return;
        }

        m_windowProperties.transparencySet = true;
    }
}

void WindowDrag::ResetWindowTransparency()
{
    if (FancyZonesSettings::settings().makeDraggedWindowTransparent && m_windowProperties.transparencySet)
    {
        bool reset = true;
        if (!SetLayeredWindowAttributes(m_window, m_windowProperties.crKey, m_windowProperties.alpha, m_windowProperties.dwFlags))
        {
            Logger::error(L"Window transparency: SetLayeredWindowAttributes failed, {}", get_last_error_or_default(GetLastError()));
            reset = false;
        }

        if (SetWindowLong(m_window, GWL_EXSTYLE, m_windowProperties.exstyle) == 0)
        {
            Logger::error(L"Window transparency: SetWindowLong failed, {}", get_last_error_or_default(GetLastError()));
            reset = false;
        }

        m_windowProperties.transparencySet = !reset;
    }
}
