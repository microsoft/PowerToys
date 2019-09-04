#pragma once

#include "Zone.h"

enum class ZoneSetLayout
{
    Grid,
    Row,
    Focus,
    Custom
};

interface __declspec(uuid("{E4839EB7-669D-49CF-84A9-71A2DFD851A3}")) IZoneSet : public IUnknown
{
    IFACEMETHOD_(GUID, Id)() = 0;
    IFACEMETHOD_(WORD, LayoutId)() = 0;
    IFACEMETHOD(AddZone)(winrt::com_ptr<IZone> zone, bool front) = 0;
    IFACEMETHOD(RemoveZone)(winrt::com_ptr<IZone> zone) = 0;
    IFACEMETHOD_(winrt::com_ptr<IZone>, ZoneFromPoint)(POINT pt) = 0;
    IFACEMETHOD_(winrt::com_ptr<IZone>, ZoneFromWindow)(HWND window) = 0;
    IFACEMETHOD_(int, GetZoneIndexFromWindow)(HWND window) = 0;
    IFACEMETHOD_(std::vector<winrt::com_ptr<IZone>>, GetZones)() = 0;
    IFACEMETHOD_(ZoneSetLayout, GetLayout)() = 0;
    IFACEMETHOD_(int, GetInnerPadding)() = 0;
    IFACEMETHOD_(winrt::com_ptr<IZoneSet>, MakeCustomClone)() = 0;
    IFACEMETHOD_(void, Save)() = 0;
    IFACEMETHOD_(void, MoveZoneToFront)(winrt::com_ptr<IZone> zone) = 0;
    IFACEMETHOD_(void, MoveZoneToBack)(winrt::com_ptr<IZone> zone) = 0;
    IFACEMETHOD_(void, MoveWindowIntoZoneByIndex)(HWND window, HWND zoneWindow, int index) = 0;
    IFACEMETHOD_(void, MoveWindowIntoZoneByDirection)(HWND window, HWND zoneWindow, DWORD vkCode) = 0;
    IFACEMETHOD_(void, MoveSizeEnd)(HWND window, HWND zoneWindow, POINT ptClient) = 0;
};

#define VERSION_PERSISTEDDATA 0x0000F00D
struct ZoneSetPersistedData
{
    DWORD Version{VERSION_PERSISTEDDATA};
    WORD LayoutId{};
    DWORD ZoneCount{};
    ZoneSetLayout Layout{};
    DWORD PaddingInner{};
    DWORD PaddingOuter{};
    RECT Zones[40]{};
};

struct ZoneSetConfig
{
    ZoneSetConfig(
        GUID id,
        WORD layoutId,
        HMONITOR monitor,
        PCWSTR resolutionKey,
        ZoneSetLayout layout,
        int zoneCount,
        int paddingOuter,
        int paddingInner) noexcept :
            Id(id),
            LayoutId(layoutId),
            Monitor(monitor),
            ResolutionKey(resolutionKey),
            Layout(layout),
            ZoneCount(zoneCount),
            PaddingOuter(paddingOuter),
            PaddingInner(paddingInner)
    {
    }

    GUID Id{};
    WORD LayoutId{};
    HMONITOR Monitor{};
    PCWSTR ResolutionKey{};
    ZoneSetLayout Layout{};
    int ZoneCount{};
    int PaddingOuter{};
    int PaddingInner{};
    bool IsCustom{};
};

winrt::com_ptr<IZoneSet> MakeZoneSet(ZoneSetConfig const& config) noexcept;