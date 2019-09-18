#pragma once
#include "FancyZones.h"

interface __declspec(uuid("{7F017528-8110-4FB3-BE41-F472969C2560}")) IZoneWindow : public IUnknown
{
    IFACEMETHOD(ShowZoneWindow)(bool activate, bool fadeIn) = 0;
    IFACEMETHOD(HideZoneWindow)() = 0;
    IFACEMETHOD(MoveSizeEnter)(HWND window, bool dragEnabled) = 0;
    IFACEMETHOD(MoveSizeUpdate)(POINT const& ptScreen, bool dragEnabled) = 0;
    IFACEMETHOD(MoveSizeEnd)(HWND window, POINT const& ptScreen) = 0;
    IFACEMETHOD(MoveSizeCancel)() = 0;
    IFACEMETHOD_(bool, IsDragEnabled)() = 0;
    IFACEMETHOD_(void, MoveWindowIntoZoneByIndex)(HWND window, int index) = 0;
    IFACEMETHOD_(void, MoveWindowIntoZoneByDirection)(HWND window, DWORD vkCode) = 0;
    IFACEMETHOD_(void, CycleActiveZoneSet)(DWORD vkCode) = 0;
    IFACEMETHOD_(void, SaveWindowProcessToZoneIndex)(HWND window) = 0;
    IFACEMETHOD_(std::wstring, DeviceId)() = 0;
    IFACEMETHOD_(std::wstring, UniqueId)() = 0;
    IFACEMETHOD_(std::wstring, WorkAreaKey)() = 0;
    IFACEMETHOD_(IZoneSet*, ActiveZoneSet)() = 0;
};

winrt::com_ptr<IZoneWindow> MakeZoneWindow(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor,
    PCWSTR deviceId, PCWSTR virtualDesktopId, bool flashZones) noexcept;
