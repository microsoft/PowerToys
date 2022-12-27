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

WindowDrag::WindowDrag(HWND window, const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& activeWorkAreas) :
    m_window(window),
    m_activeWorkAreas(activeWorkAreas),
    m_transparencySet(false)
{
    m_windowProperties.hasNoVisibleOwner = !FancyZonesWindowUtils::HasVisibleOwner(m_window);
    m_windowProperties.isStandardWindow = FancyZonesWindowUtils::IsStandardWindow(m_window) &&
                                          (!FancyZonesWindowUtils::IsPopupWindow(m_window) || FancyZonesSettings::settings().allowSnapPopupWindows);
}

WindowDrag::~WindowDrag()
{
    ResetWindowTransparency();
}

std::unique_ptr<WindowDrag> WindowDrag::Create(HWND window, const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& activeWorkAreas)
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

bool WindowDrag::MoveSizeStart(HMONITOR monitor, bool isDragging)
{
    auto iter = m_activeWorkAreas.find(monitor);
    if (iter == end(m_activeWorkAreas))
    {
        return false;
    }

    if (isDragging)
    {
        m_currentWorkArea = iter->second;
        SetWindowTransparency();
        m_currentWorkArea->MoveSizeEnter();
        if (FancyZonesSettings::settings().showZonesOnAllMonitors)
        {
            for (const auto& [keyMonitor, workArea] : m_activeWorkAreas)
            {
                // Skip calling ShowZonesOverlay for iter->second (m_draggedWindowWorkArea) since it
                // was already called in MoveSizeEnter
                const bool moveSizeEnterCalled = workArea == m_currentWorkArea;
                if (workArea && !moveSizeEnterCalled)
                {
                    workArea->ShowZonesOverlay();
                }
            }
        }
    }
    else if (m_currentWorkArea)
    {
        m_currentWorkArea = nullptr;
        for (const auto& [keyMonitor, workArea] : m_activeWorkAreas)
        {
            if (workArea)
            {
                workArea->HideZonesOverlay();
            }
        }
    }

    auto workArea = m_activeWorkAreas.find(monitor);
    if (workArea != m_activeWorkAreas.end())
    {
        workArea->second->UnsnapWindow(m_window);
    }

    return true;
}

void WindowDrag::MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, bool isDragging, bool isSelectManyZonesState)
{
    if (isDragging)
    {
        SetWindowTransparency();
    }
    else
    {
        ResetWindowTransparency();
    }

    if (m_currentWorkArea)
    {
        // Update the WorkArea already handling move/size
        if (!isDragging)
        {
            // Drag got disabled, tell it to cancel and hide all windows
            m_currentWorkArea = nullptr;
            
            for (auto& [_, workArea] : m_activeWorkAreas)
            {
                if (workArea)
                {
                    workArea->HideZonesOverlay();
                }
            }
        }
        else
        {
            auto iter = m_activeWorkAreas.find(monitor);
            if (iter != m_activeWorkAreas.end())
            {
                if (iter->second != m_currentWorkArea)
                {
                    // The drag has moved to a different monitor.
                    m_currentWorkArea->ClearSelectedZones();
                    if (!FancyZonesSettings::settings().showZonesOnAllMonitors)
                    {
                        m_currentWorkArea->HideZonesOverlay();
                    }

                    m_currentWorkArea = iter->second;
                    m_currentWorkArea->MoveSizeEnter();
                }

                for (auto& [_, workArea] : m_activeWorkAreas)
                {
                    workArea->MoveSizeUpdate(ptScreen, isDragging, isSelectManyZonesState);
                }
            }
        }
    }
    else if (isDragging)
    {
        // We'll get here if the user presses/releases shift while dragging.
        // Restart the drag on the WorkArea that m_draggedWindow is on
        MoveSizeStart(monitor, isDragging);
        MoveSizeUpdate(monitor, ptScreen, isDragging, isSelectManyZonesState);
    }
}

void WindowDrag::MoveSizeEnd()
{
    if (m_currentWorkArea)
    {
        auto workArea = std::move(m_currentWorkArea);
        
        const bool hasNoVisibleOwner = !FancyZonesWindowUtils::HasVisibleOwner(m_window);
        const bool isStandardWindow = FancyZonesWindowUtils::IsStandardWindow(m_window);

        if ((isStandardWindow == false && hasNoVisibleOwner == true &&
             m_windowProperties.isStandardWindow == true && m_windowProperties.hasNoVisibleOwner == true) ||
            FancyZonesWindowUtils::IsWindowMaximized(m_window))
        {
            // Abort the zoning, this is a Chromium based tab that is merged back with an existing window
            // or if the window is maximized by Windows when the cursor hits the screen top border
        }
        else
        {
            workArea->MoveSizeEnd(m_window);
        }
    }
    else
    {
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

        FancyZonesWindowUtils::ResetRoundCornersPreference(m_window);

        auto monitor = MonitorFromWindow(m_window, MONITOR_DEFAULTTONULL);
        if (monitor)
        {
            auto workArea = m_activeWorkAreas.find(monitor);
            if (workArea != m_activeWorkAreas.end())
            {
                const auto workAreaPtr = workArea->second;
                const auto& layout = workAreaPtr->GetLayout();
                if (layout)
                {
                    auto guidStr = FancyZonesUtils::GuidToString(layout->Id());
                    if (guidStr.has_value())
                    {
                        AppZoneHistory::instance().RemoveAppLastZone(m_window, workAreaPtr->UniqueId(), guidStr.value());
                    }
                }

                workAreaPtr->UnsnapWindow(m_window);
            }
        }

        FancyZonesWindowProperties::RemoveZoneIndexProperty(m_window);
    }

    // Also, hide all windows (regardless of settings)
    for (auto& [_, workArea] : m_activeWorkAreas)
    {
        if (workArea)
        {
            workArea->HideZonesOverlay();
        }
    }
}

void WindowDrag::SetWindowTransparency() noexcept
{
    if (m_transparencySet)
    {
        return;
    }
    
    if (FancyZonesSettings::settings().makeDraggedWindowTransparent)
    {
        try
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
            }
        }
        catch(const std::exception&)
        {
            Logger::error("Window transparency: exception");
        }
    }

    m_transparencySet = true;
}

void WindowDrag::ResetWindowTransparency() noexcept
{
    if (!m_transparencySet)
    {
        return;
    }

    if (FancyZonesSettings::settings().makeDraggedWindowTransparent)
    {
        try
        {
            if (!SetLayeredWindowAttributes(m_window, m_windowProperties.crKey, m_windowProperties.alpha, m_windowProperties.dwFlags))
            {
                Logger::error(L"Window transparency: SetLayeredWindowAttributes failed");
            }

            if (SetWindowLong(m_window, GWL_EXSTYLE, m_windowProperties.exstyle) == 0)
            {
                Logger::error(L"Window transparency: SetWindowLong failed, {}", get_last_error_or_default(GetLastError()));
            }
        }
        catch (const std::exception&)
        {
            Logger::error("Window transparency: exception");
        }
    }

    m_transparencySet = false;
}
