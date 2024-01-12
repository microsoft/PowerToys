#include "pch.h"
#include "WindowMouseSnap.h"

#include <FancyZonesLib/FancyZonesData/AppZoneHistory.h>
#include <FancyZonesLib/FancyZonesWindowProcessing.h>
#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/WindowUtils.h>
#include <FancyZonesLib/WorkArea.h>

#include <FancyZonesLib/trace.h>

#include <common/utils/elevation.h>
#include <common/notifications/NotificationUtil.h>

WindowMouseSnap::WindowMouseSnap(HWND window, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas) :
    m_window(window),
    m_activeWorkAreas(activeWorkAreas),
    m_currentWorkArea(nullptr),
    m_snappingMode(false)
{
    m_windowProperties.hasNoVisibleOwner = !FancyZonesWindowUtils::HasVisibleOwner(m_window);
}

WindowMouseSnap::~WindowMouseSnap()
{
    ResetWindowTransparency();
}

std::unique_ptr<WindowMouseSnap> WindowMouseSnap::Create(HWND window, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas)
{
    if (FancyZonesWindowUtils::IsCursorTypeIndicatingSizeEvent() || !FancyZonesWindowProcessing::IsProcessable(window))
    {
        return nullptr;
    }

    if (!is_process_elevated() && IsProcessOfWindowElevated(window))
    {
        // Notifies user if unable to drag elevated window
        notifications::WarnIfElevationIsRequired(GET_RESOURCE_STRING(IDS_FANCYZONES), GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED), GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_LEARN_MORE), GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_DIALOG_DONT_SHOW_AGAIN));
        return nullptr;
    }

    return std::unique_ptr<WindowMouseSnap>(new WindowMouseSnap(window, activeWorkAreas));
}

bool WindowMouseSnap::MoveSizeStart(HMONITOR monitor, bool isSnapping)
{
    auto iter = m_activeWorkAreas.find(monitor);
    if (iter == end(m_activeWorkAreas))
    {
        return false;
    }

    m_currentWorkArea = iter->second.get();

    SwitchSnappingMode(isSnapping);

    if (m_currentWorkArea)
    {
        m_currentWorkArea->Unsnap(m_window);
    }
    
    return true;
}

void WindowMouseSnap::MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, bool isSnapping, bool isSelectManyZonesState)
{
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
                    m_currentWorkArea->HideZones();
                }
                else
                {
                    m_currentWorkArea->ShowZones({}, m_window);
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
                m_currentWorkArea->ShowZones(m_highlightedZones.Zones(), m_window);
            }
        }
    }

    SwitchSnappingMode(isSnapping);
}

void WindowMouseSnap::MoveSizeEnd()
{
    if (m_snappingMode)
    {
        if (FancyZonesWindowUtils::IsWindowMaximized(m_window))
        {
            // Abort the zoning, this is a Chromium based tab that is merged back with an existing window
            // or if the window is maximized by Windows when the cursor hits the screen top border
        }
        else if (m_currentWorkArea)
        {
            m_currentWorkArea->Snap(m_window, m_highlightedZones.Zones());
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

void WindowMouseSnap::SwitchSnappingMode(bool isSnapping)
{
    if (!m_snappingMode && isSnapping) // turn on
    {
        m_highlightedZones.Reset();
        SetWindowTransparency();

        if (FancyZonesSettings::settings().showZonesOnAllMonitors)
        {
            for (const auto& [_, workArea] : m_activeWorkAreas)
            {
                if (workArea)
                {
                    workArea->ShowZones({}, m_window);
                }
            }
        }
        else if (m_currentWorkArea)
        {
            m_currentWorkArea->ShowZones({}, m_window);
        }

        if (m_currentWorkArea)
        {
            m_currentWorkArea->Unsnap(m_window);
            Trace::WorkArea::MoveOrResizeStarted(m_currentWorkArea->GetLayout().get(), m_currentWorkArea->GetLayoutWindows());
        }
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
                workArea->HideZones();
            }
        }

        if (m_currentWorkArea)
        {
            Trace::WorkArea::MoveOrResizeEnd(m_currentWorkArea->GetLayout().get(), m_currentWorkArea->GetLayoutWindows());
        }
    }

    m_snappingMode = isSnapping;
}

void WindowMouseSnap::SetWindowTransparency()
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

void WindowMouseSnap::ResetWindowTransparency()
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
