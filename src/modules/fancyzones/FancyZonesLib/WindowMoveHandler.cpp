#include "pch.h"
#include "WindowMoveHandler.h"

#include <common/display/dpi_aware.h>
#include <common/notifications/notifications.h>
#include <common/notifications/dont_show_again.h>
#include <common/utils/elevation.h>
#include <common/utils/resources.h>

#include "FancyZonesData/AppZoneHistory.h"
#include "Settings.h"
#include "WorkArea.h"
#include <FancyZonesLib/FancyZonesWindowProcessing.h>
#include <FancyZonesLib/WindowUtils.h>

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

WindowMoveHandler::WindowMoveHandler(const std::function<void()>& keyUpdateCallback) :
    m_mouseState(false),
    m_mouseHook(std::bind(&WindowMoveHandler::OnMouseDown, this)),
    m_leftShiftKeyState(keyUpdateCallback),
    m_rightShiftKeyState(keyUpdateCallback),
    m_ctrlKeyState(keyUpdateCallback),
    m_keyUpdateCallback(keyUpdateCallback)
{
}

void WindowMoveHandler::MoveSizeStart(HWND window, HMONITOR monitor, POINT const& /*ptScreen*/, const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& workAreaMap) noexcept
{
    if (!FancyZonesWindowProcessing::IsProcessable(window))
    {
        return;
    }

    if (!FancyZonesWindowUtils::IsCandidateForZoning(window) || WindowMoveHandlerUtils::IsCursorTypeIndicatingSizeEvent())
    {
        return;
    }

    m_draggedWindowInfo.hasNoVisibleOwner = !FancyZonesWindowUtils::HasVisibleOwner(window);
    m_draggedWindowInfo.isStandardWindow = FancyZonesWindowUtils::IsStandardWindow(window) && (!FancyZonesWindowUtils::IsPopupWindow(window) || FancyZonesSettings::settings().allowSnapPopupWindows);
    m_inDragging = true;

    auto iter = workAreaMap.find(monitor);
    if (iter == end(workAreaMap))
    {
        return;
    }

    m_draggedWindow = window;

    if (FancyZonesSettings::settings().mouseSwitch)
    {
        m_mouseHook.enable();
    }

    m_leftShiftKeyState.enable();
    m_rightShiftKeyState.enable();
    m_ctrlKeyState.enable();

    // This updates m_dragEnabled depending on if the shift key is being held down
    UpdateDragState();

    // Notifies user if unable to drag elevated window
    WarnIfElevationIsRequired(window);

    if (m_dragEnabled)
    {
        m_draggedWindowWorkArea = iter->second;
        SetWindowTransparency(m_draggedWindow);
        m_draggedWindowWorkArea->MoveSizeEnter(m_draggedWindow);
        if (FancyZonesSettings::settings().showZonesOnAllMonitors)
        {
            for (const auto& [keyMonitor, workArea] : workAreaMap)
            {
                // Skip calling ShowZonesOverlay for iter->second (m_draggedWindowWorkArea) since it
                // was already called in MoveSizeEnter
                const bool moveSizeEnterCalled = workArea == m_draggedWindowWorkArea;
                if (workArea && !moveSizeEnterCalled)
                {
                    workArea->ShowZonesOverlay();
                }
            }
        }
    }
    else if (m_draggedWindowWorkArea)
    {
        ResetWindowTransparency();
        m_draggedWindowWorkArea = nullptr;
        for (const auto& [keyMonitor, workArea] : workAreaMap)
        {
            if (workArea)
            {
                workArea->HideZonesOverlay();
            }
        }
    }

    auto workArea = workAreaMap.find(monitor);
    if (workArea != workAreaMap.end())
    {
        const auto& layoutWindows = workArea->second->GetLayoutWindows();
        if (layoutWindows)
        {
            layoutWindows->Dismiss(window);
        }
    }
}

void WindowMoveHandler::MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& workAreaMap) noexcept
{
    if (!m_inDragging)
    {
        return;
    }

    // This updates m_dragEnabled depending on if the shift key is being held down.
    UpdateDragState();

    if (m_draggedWindowWorkArea)
    {
        // Update the WorkArea already handling move/size
        if (!m_dragEnabled)
        {
            // Drag got disabled, tell it to cancel and hide all windows
            m_draggedWindowWorkArea = nullptr;
            ResetWindowTransparency();

            for (auto [keyMonitor, workArea] : workAreaMap)
            {
                if (workArea)
                {
                    workArea->HideZonesOverlay();
                }
            }
        }
        else
        {
            auto iter = workAreaMap.find(monitor);
            if (iter != workAreaMap.end())
            {
                if (iter->second != m_draggedWindowWorkArea)
                {
                    // The drag has moved to a different monitor.
                    m_draggedWindowWorkArea->ClearSelectedZones();
                    if (!FancyZonesSettings::settings().showZonesOnAllMonitors)
                    {
                        m_draggedWindowWorkArea->HideZonesOverlay();
                    }

                    m_draggedWindowWorkArea = iter->second;
                    m_draggedWindowWorkArea->MoveSizeEnter(m_draggedWindow);
                }

                for (auto [keyMonitor, workArea] : workAreaMap)
                {
                    workArea->MoveSizeUpdate(ptScreen, m_dragEnabled, m_ctrlKeyState.state());
                }
            }
        }
    }
    else if (m_dragEnabled)
    {
        // We'll get here if the user presses/releases shift while dragging.
        // Restart the drag on the WorkArea that m_draggedWindow is on
        MoveSizeStart(m_draggedWindow, monitor, ptScreen, workAreaMap);

        // m_dragEnabled could get set to false if we're moving an elevated window.
        // In that case do not proceed.
        if (m_dragEnabled)
        {
            MoveSizeUpdate(monitor, ptScreen, workAreaMap);
        }
    }
}

void WindowMoveHandler::MoveSizeEnd(HWND window, const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& workAreaMap) noexcept
{
    if (window != m_draggedWindow)
    {
        return;
    }

    bool leftShiftPressed = m_leftShiftKeyState.state();
    bool rightShiftPressed = m_rightShiftKeyState.state();

    m_mouseHook.disable();
    m_leftShiftKeyState.disable();
    m_rightShiftKeyState.disable();
    m_ctrlKeyState.disable();

    if (m_draggedWindowWorkArea)
    {
        auto workArea = std::move(m_draggedWindowWorkArea);
        ResetWindowTransparency();

        bool hasNoVisibleOwner = !FancyZonesWindowUtils::HasVisibleOwner(window);
        bool isStandardWindow = FancyZonesWindowUtils::IsStandardWindow(window);

        if ((isStandardWindow == false && hasNoVisibleOwner == true &&
             m_draggedWindowInfo.isStandardWindow == true && m_draggedWindowInfo.hasNoVisibleOwner == true) ||
            FancyZonesWindowUtils::IsWindowMaximized(window))
        {
            // Abort the zoning, this is a Chromium based tab that is merged back with an existing window
            // or if the window is maximized by Windows when the cursor hits the screen top border
        }
        else
        {
            if (FancyZonesSettings::settings().shiftDrag)
            {
                if (leftShiftPressed)
                {
                    SwallowKey(VK_LSHIFT);
                }

                if (rightShiftPressed)
                {
                    SwallowKey(VK_RSHIFT);
                }
            }

            workArea->MoveSizeEnd(m_draggedWindow);
        }
    }
    else
    {
        if (FancyZonesSettings::settings().restoreSize)
        {
            if (WindowMoveHandlerUtils::IsCursorTypeIndicatingSizeEvent())
            {
                ::RemoveProp(window, ZonedWindowProperties::PropertyRestoreSizeID);
            }
            else if (!FancyZonesWindowUtils::IsWindowMaximized(window))
            {
                FancyZonesWindowUtils::RestoreWindowSize(window);
            }
        }

        FancyZonesWindowUtils::ResetRoundCornersPreference(window);

        auto monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
        if (monitor)
        {
            auto workArea = workAreaMap.find(monitor);
            if (workArea != workAreaMap.end())
            {
                const auto workAreaPtr = workArea->second;
                const auto& layout = workAreaPtr->GetLayout();
                if (layout)
                {
                    auto guidStr = FancyZonesUtils::GuidToString(layout->Id());
                    if (guidStr.has_value())
                    {
                        AppZoneHistory::instance().RemoveAppLastZone(window, workAreaPtr->UniqueId(), guidStr.value());
                    }
                }
            }
        }

        FancyZonesWindowProperties::RemoveZoneIndexProperty(window);
    }

    m_inDragging = false;
    m_dragEnabled = false;
    m_mouseState = false;
    m_draggedWindow = nullptr;

    // Also, hide all windows (regardless of settings)
    for (auto [keyMonitor, workArea] : workAreaMap)
    {
        if (workArea)
        {
            workArea->HideZonesOverlay();
        }
    }
}

void WindowMoveHandler::MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet, std::shared_ptr<WorkArea> workArea) noexcept
{
    if (window != m_draggedWindow)
    {
        workArea->MoveWindowIntoZoneByIndexSet(window, indexSet);
    }
}

bool WindowMoveHandler::MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle, std::shared_ptr<WorkArea> workArea) noexcept
{
    return workArea && workArea->MoveWindowIntoZoneByDirectionAndIndex(window, vkCode, cycle);
}

bool WindowMoveHandler::MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle, std::shared_ptr<WorkArea> workArea) noexcept
{
    return workArea && workArea->MoveWindowIntoZoneByDirectionAndPosition(window, vkCode, cycle);
}

bool WindowMoveHandler::ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode, std::shared_ptr<WorkArea> workArea) noexcept
{
    return workArea && workArea->ExtendWindowByDirectionAndPosition(window, vkCode);
}

void WindowMoveHandler::AssignWindowsToZones(const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& activeWorkAreas, bool updatePositions) noexcept
{
    for (const auto& window : VirtualDesktop::instance().GetWindowsFromCurrentDesktop())
    {
        auto zoneIndexSet = FancyZonesWindowProperties::RetrieveZoneIndexProperty(window);
        if (zoneIndexSet.size() == 0)
        {
            continue;
        }

        auto monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
        if (monitor && activeWorkAreas.contains(monitor))
        {
            activeWorkAreas.at(monitor)->MoveWindowIntoZoneByIndexSet(window, zoneIndexSet, updatePositions);
        }
    }
}

void WindowMoveHandler::WarnIfElevationIsRequired(HWND window) noexcept
{
    using namespace notifications;
    using namespace NonLocalizable;

    static bool warning_shown = false;
    if (!is_process_elevated() && FancyZonesWindowUtils::IsProcessOfWindowElevated(window))
    {
        m_dragEnabled = false;
        if (!warning_shown && !is_toast_disabled(CantDragElevatedDontShowAgainRegistryPath, CantDragElevatedDisableIntervalInDays))
        {
            std::vector<action_t> actions = {
                link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_LEARN_MORE), FancyZonesRunAsAdminInfoPage },
                link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_DIALOG_DONT_SHOW_AGAIN), ToastNotificationButtonUrl }
            };
            show_toast_with_activations(GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED),
                                        GET_RESOURCE_STRING(IDS_FANCYZONES),
                                        {},
                                        std::move(actions));
            warning_shown = true;
        }
    }
}

void WindowMoveHandler::UpdateDragState() noexcept
{
    if (FancyZonesSettings::settings().shiftDrag)
    {
        m_dragEnabled = ((m_leftShiftKeyState.state() || m_rightShiftKeyState.state()) ^ m_mouseState);
    }
    else
    {
        m_dragEnabled = !((m_leftShiftKeyState.state() || m_rightShiftKeyState.state()) ^ m_mouseState);
    }
}

void WindowMoveHandler::SetWindowTransparency(HWND window) noexcept
{
    if (FancyZonesSettings::settings().makeDraggedWindowTransparent)
    {
        m_windowTransparencyProperties.draggedWindowExstyle = GetWindowLong(window, GWL_EXSTYLE);

        SetWindowLong(window,
                      GWL_EXSTYLE,
                      m_windowTransparencyProperties.draggedWindowExstyle | WS_EX_LAYERED);

        if (!GetLayeredWindowAttributes(window, &m_windowTransparencyProperties.draggedWindowCrKey, &m_windowTransparencyProperties.draggedWindowInitialAlpha, &m_windowTransparencyProperties.draggedWindowDwFlags))
        {
            Logger::error(L"Window transparency: GetLayeredWindowAttributes failed, {}", get_last_error_or_default(GetLastError()));
            return;
        }

        m_windowTransparencyProperties.draggedWindow = window;

        if (!SetLayeredWindowAttributes(window, 0, (255 * 50) / 100, LWA_ALPHA))
        {
            Logger::error(L"Window transparency: SetLayeredWindowAttributes failed, {}", get_last_error_or_default(GetLastError()));
        }
    }
}

void WindowMoveHandler::ResetWindowTransparency() noexcept
{
    if (FancyZonesSettings::settings().makeDraggedWindowTransparent && m_windowTransparencyProperties.draggedWindow != nullptr)
    {
        if (!SetLayeredWindowAttributes(m_windowTransparencyProperties.draggedWindow, m_windowTransparencyProperties.draggedWindowCrKey, m_windowTransparencyProperties.draggedWindowInitialAlpha, m_windowTransparencyProperties.draggedWindowDwFlags))
        {
            Logger::error(L"Window transparency: SetLayeredWindowAttributes failed");
        }

        if (SetWindowLong(m_windowTransparencyProperties.draggedWindow, GWL_EXSTYLE, m_windowTransparencyProperties.draggedWindowExstyle) == 0)
        {
            Logger::error(L"Window transparency: SetWindowLong failed, {}", get_last_error_or_default(GetLastError()));
        }

        m_windowTransparencyProperties.draggedWindow = nullptr;
    }
}

void WindowMoveHandler::SwallowKey(const WORD key) noexcept
{
    INPUT inputKey[1] = {};
    inputKey[0].type = INPUT_KEYBOARD;
    inputKey[0].ki.wVk = key;
    inputKey[0].ki.dwFlags = KEYEVENTF_KEYUP;
    SendInput(1, inputKey, sizeof(INPUT));
}
