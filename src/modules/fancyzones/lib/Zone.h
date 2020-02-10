#pragma once

interface __declspec(uuid("{8228E934-B6EF-402A-9892-15A1441BF8B0}")) IZone : public IUnknown
{
    IFACEMETHOD_(RECT, GetZoneRect)() = 0;
    IFACEMETHOD_(bool, IsEmpty)() = 0;
    IFACEMETHOD_(bool, ContainsWindow)(HWND window) = 0;
    IFACEMETHOD_(void, AddWindowToZone)(HWND window, HWND zoneWindow, bool stampZone) = 0;
    IFACEMETHOD_(void, RemoveWindowFromZone)(HWND window, bool restoreSize) = 0;
    IFACEMETHOD_(void, SetId)(size_t id) = 0;
    IFACEMETHOD_(size_t, Id)() = 0;
};

winrt::com_ptr<IZone> MakeZone(const RECT& zoneRect) noexcept;
