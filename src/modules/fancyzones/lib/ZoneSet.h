#pragma once

#include "Zone.h"
#include "JsonHelpers.h"


interface __declspec(uuid("{E4839EB7-669D-49CF-84A9-71A2DFD851A3}")) IZoneSet : public IUnknown
{
    IFACEMETHOD_(GUID, Id)() = 0;
    IFACEMETHOD_(JSONHelpers::ZoneSetLayoutType, LayoutType)() = 0;
    IFACEMETHOD(AddZone)(winrt::com_ptr<IZone> zone) = 0;
    IFACEMETHOD_(winrt::com_ptr<IZone>, ZoneFromPoint)(POINT pt) = 0;
    IFACEMETHOD_(int, GetZoneIndexFromWindow)(HWND window) = 0;
    IFACEMETHOD_(std::vector<winrt::com_ptr<IZone>>, GetZones)() = 0;
    IFACEMETHOD_(void, MoveWindowIntoZoneByIndex)(HWND window, HWND zoneWindow, int index) = 0;
    IFACEMETHOD_(void, MoveWindowIntoZoneByDirection)(HWND window, HWND zoneWindow, DWORD vkCode) = 0;
    IFACEMETHOD_(void, MoveWindowIntoZoneByPoint)(HWND window, HWND zoneWindow, POINT ptClient) = 0;
    IFACEMETHOD_(bool, CalculateZones)
    (MONITORINFO monitorInfo, int zoneCount, int spacing, const std::wstring& customZoneSetFilePath) = 0;
};

#define VERSION_PERSISTEDDATA 0x0000F00D
struct ZoneSetPersistedData
{
    static constexpr inline size_t MAX_ZONES = 40;

    DWORD Version{VERSION_PERSISTEDDATA};
    WORD LayoutId{};
    DWORD ZoneCount{};
    JSONHelpers::ZoneSetLayoutType Layout{};
    RECT Zones[MAX_ZONES]{};
};

struct ZoneSetPersistedDataOLD
{
    static constexpr inline size_t MAX_ZONES = 40;
    DWORD Version{ VERSION_PERSISTEDDATA };
    WORD LayoutId{};
    DWORD ZoneCount{};
    JSONHelpers::ZoneSetLayoutType Layout{};
    DWORD PaddingInner{};
    DWORD PaddingOuter{};
    RECT Zones[MAX_ZONES]{};
};


struct ZoneSetConfig
{
    ZoneSetConfig(
        GUID id,
        JSONHelpers::ZoneSetLayoutType layoutType,
        HMONITOR monitor,
        PCWSTR resolutionKey) noexcept :
            Id(id),
            LayoutType(layoutType),
            Monitor(monitor),
            ResolutionKey(resolutionKey)
    {
    }

    GUID Id{};
    JSONHelpers::ZoneSetLayoutType LayoutType{};
    HMONITOR Monitor{};
    PCWSTR ResolutionKey{};
};

winrt::com_ptr<IZoneSet> MakeZoneSet(ZoneSetConfig const& config) noexcept;