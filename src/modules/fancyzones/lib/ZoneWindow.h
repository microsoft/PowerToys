#pragma once
#include "FancyZones.h"
#include "lib/ZoneSet.h"

namespace ZoneWindowUtils
{
    const std::wstring& GetActiveZoneSetTmpPath();
    const std::wstring& GetAppliedZoneSetTmpPath();
    const std::wstring& GetCustomZoneSetsTmpPath();
}

interface __declspec(uuid("{7F017528-8110-4FB3-BE41-F472969C2560}")) IZoneWindow : public IUnknown
{
    IFACEMETHOD(MoveSizeEnter)(HWND window, bool dragEnabled) = 0;
    IFACEMETHOD(MoveSizeUpdate)(POINT const& ptScreen, bool dragEnabled) = 0;
    IFACEMETHOD(MoveSizeEnd)(HWND window, POINT const& ptScreen) = 0;
    IFACEMETHOD(MoveSizeCancel)() = 0;
    IFACEMETHOD_(bool, IsDragEnabled)() = 0;
    IFACEMETHOD_(void, MoveWindowIntoZoneByIndex)(HWND window, int index) = 0;
    IFACEMETHOD_(void, MoveWindowIntoZoneByDirection)(HWND window, DWORD vkCode) = 0;
    IFACEMETHOD_(void, CycleActiveZoneSet)(DWORD vkCode) = 0;
    IFACEMETHOD_(void, SaveWindowProcessToZoneIndex)(HWND window) = 0;
    IFACEMETHOD_(std::wstring, UniqueId)() = 0;
    IFACEMETHOD_(std::wstring, WorkAreaKey)() = 0;
    IFACEMETHOD_(IZoneSet*, ActiveZoneSet)() = 0;
};

winrt::com_ptr<IZoneWindow> MakeZoneWindow(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor,
    std::wstring uniqueId, bool flashZones) noexcept;
