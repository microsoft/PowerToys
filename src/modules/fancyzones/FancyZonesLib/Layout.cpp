#include "pch.h"
#include "Layout.h"

#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/LayoutConfigurator.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/WindowUtils.h>

#include <common/logger/logger.h>

namespace ZoneSelectionAlgorithms
{
    constexpr int OVERLAPPING_CENTERS_SENSITIVITY = 75;

    template<class CompareF>
    ZoneIndexSet ZoneSelectPriority(const ZonesMap& zones, const ZoneIndexSet& capturedZones, CompareF compare)
    {
        size_t chosen = 0;

        for (size_t i = 1; i < capturedZones.size(); ++i)
        {
            if (compare(zones.at(capturedZones[i]), zones.at(capturedZones[chosen])))
            {
                chosen = i;
            }
        }

        return { capturedZones[chosen] };
    }

    ZoneIndexSet ZoneSelectSubregion(const ZonesMap& zones, const ZoneIndexSet& capturedZones, POINT pt, int sensitivityRadius)
    {
        auto expand = [&](RECT& rect) {
            rect.top -= sensitivityRadius / 2;
            rect.bottom += sensitivityRadius / 2;
            rect.left -= sensitivityRadius / 2;
            rect.right += sensitivityRadius / 2;
        };

        // Compute the overlapped rectangle.
        RECT overlap = zones.at(capturedZones[0]).GetZoneRect();
        expand(overlap);

        for (size_t i = 1; i < capturedZones.size(); ++i)
        {
            RECT current = zones.at(capturedZones[i]).GetZoneRect();
            expand(current);

            overlap.top = max(overlap.top, current.top);
            overlap.left = max(overlap.left, current.left);
            overlap.bottom = min(overlap.bottom, current.bottom);
            overlap.right = min(overlap.right, current.right);
        }

        // Avoid division by zero
        int width = max(overlap.right - overlap.left, 1);
        int height = max(overlap.bottom - overlap.top, 1);

        bool verticalSplit = height > width;
        ZoneIndex zoneIndex;

        if (verticalSplit)
        {
            zoneIndex = (static_cast<ZoneIndex>(pt.y) - overlap.top) * capturedZones.size() / height;
        }
        else
        {
            zoneIndex = (static_cast<ZoneIndex>(pt.x) - overlap.left) * capturedZones.size() / width;
        }

        zoneIndex = std::clamp(zoneIndex, static_cast<ZoneIndex>(0), static_cast<ZoneIndex>(capturedZones.size()) - 1);

        return { capturedZones[zoneIndex] };
    }

    ZoneIndexSet ZoneSelectClosestCenter(const ZonesMap& zones, const ZoneIndexSet& capturedZones, POINT pt)
    {
        auto getCenter = [](auto zone) {
            RECT rect = zone.GetZoneRect();
            return POINT{ (rect.right + rect.left) / 2, (rect.top + rect.bottom) / 2 };
        };
        auto pointDifference = [](POINT pt1, POINT pt2) {
            return (pt1.x - pt2.x) * (pt1.x - pt2.x) + (pt1.y - pt2.y) * (pt1.y - pt2.y);
        };
        auto distanceFromCenter = [&](auto zone) {
            POINT center = getCenter(zone);
            return pointDifference(center, pt);
        };
        auto closerToCenter = [&](auto zone1, auto zone2) {
            if (pointDifference(getCenter(zone1), getCenter(zone2)) > OVERLAPPING_CENTERS_SENSITIVITY)
            {
                return distanceFromCenter(zone1) < distanceFromCenter(zone2);
            }
            else
            {
                return zone1.GetZoneArea() < zone2.GetZoneArea();
            };
        };
        return ZoneSelectPriority(zones, capturedZones, closerToCenter);
    }
}

Layout::Layout(const LayoutData& data) :
    m_data(data)
{
}

bool Layout::Init(const FancyZonesUtils::Rect& workArea, HMONITOR monitor) noexcept
{
    //invalid work area
    if (workArea.width() == 0 || workArea.height() == 0)
    {
        Logger::error(L"Layout initialization: invalid work area");
        return false;
    }

    //invalid zoneCount, may cause division by zero
    if (m_data.zoneCount <= 0 && m_data.type != FancyZonesDataTypes::ZoneSetLayoutType::Custom)
    {
        Logger::error(L"Layout initialization: invalid zone count");
        return false;
    }

    auto spacing = m_data.showSpacing ? m_data.spacing : 0; 

    switch (m_data.type)
    {
    case FancyZonesDataTypes::ZoneSetLayoutType::Focus:
        m_zones = LayoutConfigurator::Focus(workArea, m_data.zoneCount);
        break;
    case FancyZonesDataTypes::ZoneSetLayoutType::Columns:
        m_zones = LayoutConfigurator::Columns(workArea, m_data.zoneCount, spacing);
        break;
    case FancyZonesDataTypes::ZoneSetLayoutType::Rows:
        m_zones = LayoutConfigurator::Rows(workArea, m_data.zoneCount, spacing);
        break;
    case FancyZonesDataTypes::ZoneSetLayoutType::Grid:
        m_zones = LayoutConfigurator::Grid(workArea, m_data.zoneCount, spacing);
        break;
    case FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid:
        m_zones = LayoutConfigurator::PriorityGrid(workArea, m_data.zoneCount, spacing);
        break;
    case FancyZonesDataTypes::ZoneSetLayoutType::Custom:
    {
        const auto customLayoutData = CustomLayouts::instance().GetCustomLayoutData(m_data.uuid);
        if (customLayoutData.has_value())
        {
            m_zones = LayoutConfigurator::Custom(workArea, monitor, customLayoutData.value(), spacing);
        }
        else
        {
            Logger::error(L"Custom layout not found");
            return false;
        }
    }
    break;
    }

    return m_zones.size() == m_data.zoneCount;
}

GUID Layout::Id() const noexcept
{
    return m_data.uuid;
}

FancyZonesDataTypes::ZoneSetLayoutType Layout::Type() const noexcept
{
    return m_data.type;
}

const ZonesMap& Layout::Zones() const noexcept
{
    return m_zones;
}

ZoneIndexSet Layout::ZonesFromPoint(POINT pt) const noexcept
{
    ZoneIndexSet capturedZones;
    ZoneIndexSet strictlyCapturedZones;
    for (const auto& [zoneId, zone] : m_zones)
    {
        const RECT& zoneRect = zone.GetZoneRect();
        if (zoneRect.left - m_data.sensitivityRadius <= pt.x && pt.x <= zoneRect.right + m_data.sensitivityRadius &&
            zoneRect.top - m_data.sensitivityRadius <= pt.y && pt.y <= zoneRect.bottom + m_data.sensitivityRadius)
        {
            capturedZones.emplace_back(zoneId);
        }

        if (zoneRect.left <= pt.x && pt.x < zoneRect.right &&
            zoneRect.top <= pt.y && pt.y < zoneRect.bottom)
        {
            strictlyCapturedZones.emplace_back(zoneId);
        }
    }

    // If only one zone is captured, but it's not strictly captured
    // don't consider it as captured
    if (capturedZones.size() == 1 && strictlyCapturedZones.size() == 0)
    {
        return {};
    }

    // If captured zones do not overlap, return all of them
    // Otherwise, return one of them based on the chosen selection algorithm.
    bool overlap = false;
    for (size_t i = 0; i < capturedZones.size(); ++i)
    {
        for (size_t j = i + 1; j < capturedZones.size(); ++j)
        {
            RECT rectI;
            RECT rectJ;
            try
            {
                rectI = m_zones.at(capturedZones[i]).GetZoneRect();
                rectJ = m_zones.at(capturedZones[j]).GetZoneRect();
            }
            catch (std::out_of_range)
            {
                return {};
            }

            if (max(rectI.top, rectJ.top) + m_data.sensitivityRadius < min(rectI.bottom, rectJ.bottom) &&
                max(rectI.left, rectJ.left) + m_data.sensitivityRadius < min(rectI.right, rectJ.right))
            {
                overlap = true;
                break;
            }
        }
        if (overlap)
        {
            break;
        }
    }

    if (overlap)
    {
        try
        {
            using Algorithm = OverlappingZonesAlgorithm;

            switch (FancyZonesSettings::settings().overlappingZonesAlgorithm)
            {
            case Algorithm::Smallest:
                return ZoneSelectionAlgorithms::ZoneSelectPriority(m_zones, capturedZones, [&](auto zone1, auto zone2) { return zone1.GetZoneArea() < zone2.GetZoneArea(); });
            case Algorithm::Largest:
                return ZoneSelectionAlgorithms::ZoneSelectPriority(m_zones, capturedZones, [&](auto zone1, auto zone2) { return zone1.GetZoneArea() > zone2.GetZoneArea(); });
            case Algorithm::Positional:
                return ZoneSelectionAlgorithms::ZoneSelectSubregion(m_zones, capturedZones, pt, m_data.sensitivityRadius);
            case Algorithm::ClosestCenter:
                return ZoneSelectionAlgorithms::ZoneSelectClosestCenter(m_zones, capturedZones, pt);
            }
        }
        catch (std::out_of_range)
        {
            Logger::error("Exception out_of_range was thrown in ZoneSet::ZonesFromPoint");
            return { capturedZones[0] };
        }
    }

    return capturedZones;
}

ZoneIndexSet Layout::GetCombinedZoneRange(const ZoneIndexSet& initialZones, const ZoneIndexSet& finalZones) const noexcept
{
    ZoneIndexSet combinedZones, result;
    std::set_union(begin(initialZones), end(initialZones), begin(finalZones), end(finalZones), std::back_inserter(combinedZones));

    RECT boundingRect{};
    bool boundingRectEmpty = true;

    for (ZoneIndex zoneId : combinedZones)
    {
        if (m_zones.contains(zoneId))
        {
            const RECT rect = m_zones.at(zoneId).GetZoneRect();
            if (boundingRectEmpty)
            {
                boundingRect = rect;
                boundingRectEmpty = false;
            }
            else
            {
                boundingRect.left = min(boundingRect.left, rect.left);
                boundingRect.top = min(boundingRect.top, rect.top);
                boundingRect.right = max(boundingRect.right, rect.right);
                boundingRect.bottom = max(boundingRect.bottom, rect.bottom);
            }
        }
    }

    if (!boundingRectEmpty)
    {
        for (const auto& [zoneId, zone] : m_zones)
        {
            const RECT rect = zone.GetZoneRect();
            if (boundingRect.left <= rect.left && rect.right <= boundingRect.right &&
                boundingRect.top <= rect.top && rect.bottom <= boundingRect.bottom)
            {
                result.push_back(zoneId);
            }
        }
    }

    return result;
}

RECT Layout::GetCombinedZonesRect(const ZoneIndexSet& zones)
{
    RECT size{};
    bool sizeEmpty = true;

    for (ZoneIndex id : zones)
    {
        if (m_zones.contains(id))
        {
            const auto& zone = m_zones.at(id);
            const RECT newSize = zone.GetZoneRect();
            if (!sizeEmpty)
            {
                size.left = min(size.left, newSize.left);
                size.top = min(size.top, newSize.top);
                size.right = max(size.right, newSize.right);
                size.bottom = max(size.bottom, newSize.bottom);
            }
            else
            {
                size = newSize;
                sizeEmpty = false;
            }
        }
    }

    return size;
}
