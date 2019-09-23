#include "pch.h"

struct ZoneSet : winrt::implements<ZoneSet, IZoneSet>
{
public:
    ZoneSet(ZoneSetConfig const& config) : m_config(config)
    {
        if (config.ZoneCount > 0)
        {
            InitialPopulateZones();
        }
    }

    ZoneSet(ZoneSetConfig const& config, std::vector<winrt::com_ptr<IZone>> zones) :
        m_config(config),
        m_zones(zones)
    {
    }

    IFACEMETHODIMP_(GUID) Id() noexcept { return m_config.Id; }
    IFACEMETHODIMP_(WORD) LayoutId() noexcept { return m_config.LayoutId; }
    IFACEMETHODIMP AddZone(winrt::com_ptr<IZone> zone, bool front) noexcept;
    IFACEMETHODIMP RemoveZone(winrt::com_ptr<IZone> zone) noexcept;
    IFACEMETHODIMP_(winrt::com_ptr<IZone>) ZoneFromPoint(POINT pt) noexcept;
    IFACEMETHODIMP_(winrt::com_ptr<IZone>) ZoneFromWindow(HWND window) noexcept;
    IFACEMETHODIMP_(int) GetZoneIndexFromWindow(HWND window) noexcept;
    IFACEMETHODIMP_(std::vector<winrt::com_ptr<IZone>>) GetZones() noexcept { return m_zones; }
    IFACEMETHODIMP_(ZoneSetLayout) GetLayout() noexcept { return m_config.Layout; }
    IFACEMETHODIMP_(int) GetInnerPadding() noexcept { return m_config.PaddingInner; }
    IFACEMETHODIMP_(winrt::com_ptr<IZoneSet>) MakeCustomClone() noexcept;
    IFACEMETHODIMP_(void) Save() noexcept;
    IFACEMETHODIMP_(void) MoveZoneToFront(winrt::com_ptr<IZone> zone) noexcept;
    IFACEMETHODIMP_(void) MoveZoneToBack(winrt::com_ptr<IZone> zone) noexcept;
    IFACEMETHODIMP_(void) MoveWindowIntoZoneByIndex(HWND window, HWND zoneWindow, int index) noexcept;
    IFACEMETHODIMP_(void) MoveWindowIntoZoneByDirection(HWND window, HWND zoneWindow, DWORD vkCode) noexcept;
    IFACEMETHODIMP_(void) MoveSizeEnd(HWND window, HWND zoneWindow, POINT ptClient) noexcept;

private:
    void InitialPopulateZones() noexcept;
    void GenerateGridZones(MONITORINFO const& mi) noexcept;
    void DoGridLayout(SIZE const& zoneArea, int numCols, int numRows) noexcept;
    void GenerateFocusZones(MONITORINFO const& mi) noexcept;
    void StampZone(HWND window, _In_opt_ winrt::com_ptr<IZone> zone) noexcept;

    std::vector<winrt::com_ptr<IZone>> m_zones;
    ZoneSetConfig m_config;
};

IFACEMETHODIMP ZoneSet::AddZone(winrt::com_ptr<IZone> zone, bool front) noexcept
{
    // XXXX: need to reorder ids when inserting...
    if (front)
    {
        m_zones.insert(m_zones.begin(), zone);
    }
    else
    {
        m_zones.emplace_back(zone);
    }

    // Important not to set Id 0 since we store it in the HWND using SetProp.
    // SetProp(0) doesn't really work.
    zone->SetId(m_zones.size());
    return S_OK;
}

IFACEMETHODIMP ZoneSet::RemoveZone(winrt::com_ptr<IZone> zone) noexcept
{
    auto iter = std::find(m_zones.begin(), m_zones.end(), zone);
    if (iter != m_zones.end())
    {
        m_zones.erase(iter);
        return S_OK;
    }
    return E_INVALIDARG;
}

IFACEMETHODIMP_(winrt::com_ptr<IZone>) ZoneSet::ZoneFromPoint(POINT pt) noexcept
{
    winrt::com_ptr<IZone> smallestKnownZone = nullptr;
    for (auto iter = m_zones.begin(); iter != m_zones.end(); iter++)
    {
        if (winrt::com_ptr<IZone> zone = iter->try_as<IZone>())
        {
            RECT* newZoneRect = &zone->GetZoneRect();
            if (PtInRect(newZoneRect, pt))
            {
                if(smallestKnownZone == nullptr)
                {
                    smallestKnownZone = zone;
                }
                else
                {
                    RECT* r = &smallestKnownZone->GetZoneRect();
                    int knownZoneArea = (r->right-r->left)*(r->bottom-r->top);

                    int newZoneArea = (newZoneRect->right-newZoneRect->left)*(newZoneRect->bottom-newZoneRect->top);

                    if(newZoneArea<knownZoneArea)
                        smallestKnownZone = zone;
                }
            }
        }
    }

    return smallestKnownZone;
}

IFACEMETHODIMP_(winrt::com_ptr<IZone>) ZoneSet::ZoneFromWindow(HWND window) noexcept
{
    for (auto iter = m_zones.begin(); iter != m_zones.end(); iter++)
    {
        if (winrt::com_ptr<IZone> zone = iter->try_as<IZone>())
        {
            if (zone->ContainsWindow(window))
            {
                return zone;
            }
        }
    }
    return nullptr;
}

IFACEMETHODIMP_(winrt::com_ptr<IZoneSet>) ZoneSet::MakeCustomClone() noexcept
{
    if (SUCCEEDED_LOG(CoCreateGuid(&m_config.Id)))
    {
        m_config.IsCustom = true;
        return winrt::make_self<ZoneSet>(m_config, m_zones);
    }
    return nullptr;
}

IFACEMETHODIMP_(void) ZoneSet::Save() noexcept
{
    size_t const zoneCount = m_zones.size();
    if (zoneCount == 0)
    {
        RegistryHelpers::DeleteZoneSet(m_config.ResolutionKey, m_config.Id);
    }
    else
    {
        ZoneSetPersistedData data{};
        data.LayoutId = m_config.LayoutId;
        data.ZoneCount = static_cast<DWORD>(zoneCount);
        data.Layout = m_config.Layout;
        data.PaddingInner = m_config.PaddingInner;
        data.PaddingOuter = m_config.PaddingOuter;

        int i = 0;
        for (auto iter = m_zones.begin(); iter != m_zones.end(); iter++)
        {
            winrt::com_ptr<IZone> zone = iter->as<IZone>();
            CopyRect(&data.Zones[i++], &zone->GetZoneRect());
        }

        wil::unique_cotaskmem_string guid;
        if (SUCCEEDED_LOG(StringFromCLSID(m_config.Id, &guid)))
        {
            if (wil::unique_hkey hkey{ RegistryHelpers::CreateKey(m_config.ResolutionKey) })
            {
                RegSetValueExW(hkey.get(), guid.get(), 0, REG_BINARY, reinterpret_cast<BYTE*>(&data), sizeof(data));
            }
        }
    }
}

IFACEMETHODIMP_(void) ZoneSet::MoveZoneToFront(winrt::com_ptr<IZone> zone) noexcept
{
    auto iter = std::find(m_zones.begin(), m_zones.end(), zone);
    if (iter != m_zones.end())
    {
        std::rotate(m_zones.begin(), iter, iter + 1);
    }
}

IFACEMETHODIMP_(void) ZoneSet::MoveZoneToBack(winrt::com_ptr<IZone> zone) noexcept
{
    auto iter = std::find(m_zones.begin(), m_zones.end(), zone);
    if (iter != m_zones.end())
    {
        std::rotate(iter, iter + 1, m_zones.end());
    }
}

IFACEMETHODIMP_(int) ZoneSet::GetZoneIndexFromWindow(HWND window) noexcept
{
    int zoneIndex = 0;
    for (auto iter = m_zones.begin(); iter != m_zones.end(); iter++, zoneIndex++)
    {
        if (winrt::com_ptr<IZone> zone = iter->try_as<IZone>())
        {
            if (zone->ContainsWindow(window))
            {
                return zoneIndex;
            }
        }
    }
    return -1;
}

IFACEMETHODIMP_(void) ZoneSet::MoveWindowIntoZoneByIndex(HWND window, HWND windowZone, int index) noexcept
{
    if (index >= static_cast<int>(m_zones.size()))
    {
        index = 0;
    }

    if (index < m_zones.size())
    {
        if (auto zone = m_zones.at(index))
        {
            zone->AddWindowToZone(window, windowZone, false);
        }
    }
}

IFACEMETHODIMP_(void) ZoneSet::MoveWindowIntoZoneByDirection(HWND window, HWND windowZone, DWORD vkCode) noexcept
{
    winrt::com_ptr<IZone> oldZone;
    winrt::com_ptr<IZone> newZone;

    auto iter = std::find(m_zones.begin(), m_zones.end(), ZoneFromWindow(window));
    if (iter == m_zones.end())
    {
        iter = (vkCode == VK_RIGHT) ? m_zones.begin() : m_zones.end() - 1;
    }
    else if (oldZone = iter->as<IZone>())
    {
        if (vkCode == VK_LEFT)
        {
            if (iter == m_zones.begin())
            {
                iter = m_zones.end();
            }
            iter--;
        }
        else if (vkCode == VK_RIGHT)
        {
            iter++;
            if (iter == m_zones.end())
            {
                iter = m_zones.begin();
            }
        }
    }

    if (newZone = iter->as<IZone>())
    {
        if (oldZone)
        {
            oldZone->RemoveWindowFromZone(window, false);
        }
        newZone->AddWindowToZone(window, windowZone, true);
    }
}

IFACEMETHODIMP_(void) ZoneSet::MoveSizeEnd(HWND window, HWND zoneWindow, POINT ptClient) noexcept
{
    if (auto zoneDrop = ZoneFromWindow(window))
    {
        zoneDrop->RemoveWindowFromZone(window, !IsZoomed(window));
    }

    if (auto zone = ZoneFromPoint(ptClient))
    {
        zone->AddWindowToZone(window, zoneWindow, true);

        POINT pointAdjustedScreen = ptClient;
        MapWindowPoints(zoneWindow, nullptr, &pointAdjustedScreen, 1);
        SetCursorPos(pointAdjustedScreen.x, pointAdjustedScreen.y);
    }
}

void ZoneSet::InitialPopulateZones() noexcept
{
    // TODO: reconcile the pregenerated FZ layouts with the editor

    MONITORINFO mi{};
    mi.cbSize = sizeof(mi);
    if (GetMonitorInfoW(m_config.Monitor, &mi))
    {
        if ((m_config.Layout == ZoneSetLayout::Grid) || (m_config.Layout == ZoneSetLayout::Row))
        {
            GenerateGridZones(mi);
        }
        else if (m_config.Layout == ZoneSetLayout::Focus)
        {
            GenerateFocusZones(mi);
        }

        Save();
    }
}

void ZoneSet::GenerateGridZones(MONITORINFO const& mi) noexcept
{
    Rect workArea(mi.rcWork);

    int numCols, numRows;
    if (m_config.Layout == ZoneSetLayout::Grid)
    {
        switch (m_config.ZoneCount)
        {
            case 1: numCols = 1; numRows = 1; break;
            case 2: numCols = 2; numRows = 1; break;
            case 3: numCols = 2; numRows = 2; break;
            case 4: numCols = 2; numRows = 2; break;
            case 5: numCols = 3; numRows = 3; break;
            case 6: numCols = 3; numRows = 3; break;
            case 7: numCols = 3; numRows = 3; break;
            case 8: numCols = 3; numRows = 3; break;
            case 9: numCols = 3; numRows = 3; break;
        }

        if ((m_config.ZoneCount == 2) && (workArea.height() > workArea.width()))
        {
            numCols = 1;
            numRows = 2;
        }
    }
    else if (m_config.Layout == ZoneSetLayout::Row)
    {
        numCols = m_config.ZoneCount;
        numRows = 1;
    }

    SIZE const zoneArea = {
        workArea.width() - ((m_config.PaddingOuter * 2) + (m_config.PaddingInner * (numCols - 1))),
        workArea.height() - ((m_config.PaddingOuter * 2) + (m_config.PaddingInner * (numRows - 1)))
    };

    DoGridLayout(zoneArea, numCols, numRows);
}

void ZoneSet::DoGridLayout(SIZE const& zoneArea, int numCols, int numRows) noexcept
{
    auto x = m_config.PaddingOuter;
    auto y = m_config.PaddingOuter;
    auto const zoneWidth = (zoneArea.cx / numCols);
    auto const zoneHeight = (zoneArea.cy / numRows);
    for (auto i = 1; i <= m_config.ZoneCount; i++)
    {
        auto col = numCols - (i % numCols);
        RECT const zoneRect = { x, y, x + zoneWidth, y + zoneHeight };
        AddZone(MakeZone(zoneRect), false);

        x += zoneWidth + m_config.PaddingInner;
        if (col == numCols)
        {
            x = m_config.PaddingOuter;
            y += zoneHeight + m_config.PaddingInner;
        }
    }
}

void ZoneSet::GenerateFocusZones(MONITORINFO const& mi) noexcept
{
    Rect const workArea(mi.rcWork);

    SIZE const workHalf = { workArea.width() / 2, workArea.height() / 2 };
    RECT const safeZone = {
        m_config.PaddingOuter,
        m_config.PaddingOuter,
        workArea.width() - m_config.PaddingOuter,
        workArea.height() - m_config.PaddingOuter
    };

    int const width = min(1920, workArea.width() * 60 / 100);
    int const height = min(1200, workArea.height() * 75 / 100);
    int const halfWidth = width / 2;
    int const halfHeight = height / 2;
    int x = workHalf.cx - halfWidth;
    int y = workHalf.cy - halfHeight;

    RECT const focusRect = { x, y, x + width, y + height };
    AddZone(MakeZone(focusRect), false);

    for (auto i = 2; i <= m_config.ZoneCount; i++)
    {
        switch (i)
        {
            case 2: x = focusRect.right - halfWidth; y = focusRect.top + m_config.PaddingInner; break; // right
            case 3: x = focusRect.left - halfWidth; y = focusRect.top + (m_config.PaddingInner * 2); break; // left
            case 4: x = focusRect.left + m_config.PaddingInner; y = focusRect.top - halfHeight; break; // up
            case 5: x = focusRect.left - m_config.PaddingInner; y = focusRect.bottom - halfHeight; break; // down
        }

        // Bound into safe zone
        x = min(safeZone.right - width, max(safeZone.left, x));
        y = min(safeZone.bottom - height, max(safeZone.top, y));

        RECT const zoneRect = { x, y, x + width, y + height };
        AddZone(MakeZone(zoneRect), false);
    }
}

winrt::com_ptr<IZoneSet> MakeZoneSet(ZoneSetConfig const& config) noexcept
{
    return winrt::make_self<ZoneSet>(config);
}
