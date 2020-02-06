#pragma once

interface IZoneWindow;
interface IFancyZonesSettings;
interface IZoneSet;

interface __declspec(uuid("{50D3F0F5-736E-4186-BDF4-3D6BEE150C3A}")) IFancyZones : public IUnknown
{
    IFACEMETHOD_(void, Run)() = 0;
    IFACEMETHOD_(void, Destroy)() = 0;
};

interface __declspec(uuid("{2CB37E8F-87E6-4AEC-B4B2-E0FDC873343F}")) IFancyZonesCallback : public IUnknown
{
    IFACEMETHOD_(bool, InMoveSize)() = 0;
    IFACEMETHOD_(void, MoveSizeStart)(HWND window, HMONITOR monitor, POINT const& ptScreen) = 0;
    IFACEMETHOD_(void, MoveSizeUpdate)(HMONITOR monitor, POINT const& ptScreen) = 0;
    IFACEMETHOD_(void, MoveSizeEnd)(HWND window, POINT const& ptScreen) = 0;
    IFACEMETHOD_(void, VirtualDesktopChanged)() = 0;
    IFACEMETHOD_(void, WindowCreated)(HWND window) = 0;
    IFACEMETHOD_(bool, OnKeyDown)(PKBDLLHOOKSTRUCT info) = 0;
    IFACEMETHOD_(void, ToggleEditor)() = 0;
    IFACEMETHOD_(void, SettingsChanged)() = 0;
};

interface __declspec(uuid("{5C8D99D6-34B2-4F4A-A8E5-7483F6869775}")) IZoneWindowHost : public IUnknown
{
    IFACEMETHOD_(void, MoveWindowsOnActiveZoneSetChange)() = 0;
    IFACEMETHOD_(COLORREF, GetZoneHighlightColor)() = 0;
    IFACEMETHOD_(IZoneWindow*, GetParentZoneWindow) (HMONITOR monitor) = 0;
    IFACEMETHOD_(int, GetZoneHighlightOpacity)() = 0;
};

winrt::com_ptr<IFancyZones> MakeFancyZones(HINSTANCE hinstance, const winrt::com_ptr<IFancyZonesSettings>& settings) noexcept;
