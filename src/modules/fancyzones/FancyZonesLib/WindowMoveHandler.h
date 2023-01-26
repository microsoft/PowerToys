#pragma once

#include "FancyZonesWindowProperties.h"
#include "KeyState.h"
#include "SecondaryMouseButtonsHook.h"

#include <functional>

interface IFancyZonesSettings;
class WorkArea;

class WindowMoveHandler
{
public:
    WindowMoveHandler();

    void MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet, std::shared_ptr<WorkArea> workArea) noexcept;
    bool MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle, std::shared_ptr<WorkArea> workArea) noexcept;
    bool MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle, std::shared_ptr<WorkArea> workArea) noexcept;
    bool ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode, std::shared_ptr<WorkArea> workArea) noexcept;

    void AssignWindowsToZones(const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& activeWorkAreas, bool updatePositions) noexcept;
};
