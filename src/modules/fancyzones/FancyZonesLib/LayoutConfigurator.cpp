#include "pch.h"
#include "LayoutConfigurator.h"

#include <common/display/dpi_aware.h>
#include <common/logger/logger.h>

#include <FancyZonesLib/FancyZonesDataTypes.h>

namespace
{
    constexpr int C_MULTIPLIER = 10000;

    // PriorityGrid layout is unique for zoneCount <= 11. For zoneCount > 11 PriorityGrid is same as Grid
    FancyZonesDataTypes::GridLayoutInfo predefinedPriorityGridLayouts[11] = {
        /* 1 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 1,
            .columns = 1,
            .rowsPercents = { 10000 },
            .columnsPercents = { 10000 },
            .cellChildMap = { { 0 } } }),
        /* 2 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 1,
            .columns = 2,
            .rowsPercents = { 10000 },
            .columnsPercents = { 6667, 3333 },
            .cellChildMap = { { 0, 1 } } }),
        /* 3 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 1,
            .columns = 3,
            .rowsPercents = { 10000 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 } } }),
        /* 4 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 2,
            .columns = 3,
            .rowsPercents = { 5000, 5000 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 0, 1, 3 } } }),
        /* 5 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 2,
            .columns = 3,
            .rowsPercents = { 5000, 5000 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 3, 1, 4 } } }),
        /* 6 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 3,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 0, 1, 3 }, { 4, 1, 5 } } }),
        /* 7 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 3,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 3, 1, 4 }, { 5, 1, 6 } } }),
        /* 8 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 4,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 2500, 2500, 2500 },
            .cellChildMap = { { 0, 1, 2, 3 }, { 4, 1, 2, 5 }, { 6, 1, 2, 7 } } }),
        /* 9 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 4,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 2500, 2500, 2500 },
            .cellChildMap = { { 0, 1, 2, 3 }, { 4, 1, 2, 5 }, { 6, 1, 7, 8 } } }),
        /* 10 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 4,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 2500, 2500, 2500 },
            .cellChildMap = { { 0, 1, 2, 3 }, { 4, 1, 5, 6 }, { 7, 1, 8, 9 } } }),
        /* 11 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 4,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 2500, 2500, 2500 },
            .cellChildMap = { { 0, 1, 2, 3 }, { 4, 1, 5, 6 }, { 7, 8, 9, 10 } } }),
    };
}

bool AddZone(Zone zone, ZonesMap& zones) noexcept
{
    auto zoneId = zone.Id();
    if (zones.contains(zoneId))
    {
        return false;
    }

    zones.insert({ zoneId, std::move(zone) });
    return true;
}

ZonesMap CalculateGridZones(FancyZonesUtils::Rect workArea, FancyZonesDataTypes::GridLayoutInfo gridLayoutInfo, int spacing)
{
    ZonesMap zones;

    long totalWidth = workArea.width();
    long totalHeight = workArea.height();
    struct Info
    {
        long Extent;
        long Start;
        long End;
    };
    std::vector<Info> rowInfo(gridLayoutInfo.rows());
    std::vector<Info> columnInfo(gridLayoutInfo.columns());

    // Note: The expressions below are carefully written to
    // make the sum of all zones' sizes exactly total{Width|Height}
    int totalPercents = 0;
    for (int row = 0; row < gridLayoutInfo.rows(); row++)
    {
        rowInfo[row].Start = totalPercents * totalHeight / C_MULTIPLIER;
        totalPercents += gridLayoutInfo.rowsPercents()[row];
        rowInfo[row].End = totalPercents * totalHeight / C_MULTIPLIER;
        rowInfo[row].Extent = rowInfo[row].End - rowInfo[row].Start;
    }

    totalPercents = 0;
    for (int col = 0; col < gridLayoutInfo.columns(); col++)
    {
        columnInfo[col].Start = totalPercents * totalWidth / C_MULTIPLIER;
        totalPercents += gridLayoutInfo.columnsPercents()[col];
        columnInfo[col].End = totalPercents * totalWidth / C_MULTIPLIER;
        columnInfo[col].Extent = columnInfo[col].End - columnInfo[col].Start;
    }

    for (int64_t row = 0; row < gridLayoutInfo.rows(); row++)
    {
        for (int64_t col = 0; col < gridLayoutInfo.columns(); col++)
        {
            int i = gridLayoutInfo.cellChildMap()[row][col];
            if (((row == 0) || (gridLayoutInfo.cellChildMap()[row - 1][col] != i)) &&
                ((col == 0) || (gridLayoutInfo.cellChildMap()[row][col - 1] != i)))
            {
                long left = columnInfo[col].Start;
                long top = rowInfo[row].Start;

                int64_t maxRow = row;
                while (((maxRow + 1) < gridLayoutInfo.rows()) && (gridLayoutInfo.cellChildMap()[maxRow + 1][col] == i))
                {
                    maxRow++;
                }
                int64_t maxCol = col;
                while (((maxCol + 1) < gridLayoutInfo.columns()) && (gridLayoutInfo.cellChildMap()[row][maxCol + 1] == i))
                {
                    maxCol++;
                }

                long right = columnInfo[maxCol].End;
                long bottom = rowInfo[maxRow].End;

                top += row == 0 ? spacing : spacing / 2;
                bottom -= maxRow == static_cast<int64_t>(gridLayoutInfo.rows()) - 1 ? spacing : spacing / 2;
                left += col == 0 ? spacing : spacing / 2;
                right -= maxCol == static_cast<int64_t>(gridLayoutInfo.columns()) - 1 ? spacing : spacing / 2;

                Zone zone(RECT{ left, top, right, bottom }, i);
                if (zone.IsValid())
                {
                    if (!AddZone(zone, zones))
                    {
                        Logger::error(L"Failed to create grid layout. Invalid zone id");
                        return {};
                    }
                }
                else
                {
                    // All zones within zone set should be valid in order to use its functionality.
                    Logger::error(L"Failed to create grid layout. Invalid zone");
                    return {};
                }
            }
        }
    }

    return zones;
}

ZonesMap LayoutConfigurator::Focus(FancyZonesUtils::Rect workArea, int zoneCount) noexcept
{
    ZonesMap zones;

    long left{ 100 };
    long top{ 100 };
    long right{ left + static_cast<long>(workArea.width() * 0.4) };
    long bottom{ top + static_cast<long>(workArea.height() * 0.4) };

    RECT focusZoneRect{ left, top, right, bottom };

    long focusRectXIncrement = (zoneCount <= 1) ? 0 : 50;
    long focusRectYIncrement = (zoneCount <= 1) ? 0 : 50;

    for (int i = 0; i < zoneCount; i++)
    {
        Zone zone(focusZoneRect, zones.size());
        if (zone.IsValid())
        {
            if (!AddZone(zone, zones))
            {
                Logger::error(L"Failed to create Focus layout. Invalid zone id");
                return {};
            }
        }
        else
        {
            // All zones within zone set should be valid in order to use its functionality.
            Logger::error(L"Failed to create Focus layout. Invalid zone");
            return {};
        }

        focusZoneRect.left += focusRectXIncrement;
        focusZoneRect.right += focusRectXIncrement;
        focusZoneRect.bottom += focusRectYIncrement;
        focusZoneRect.top += focusRectYIncrement;
    }

    return zones;
}

ZonesMap LayoutConfigurator::Rows(FancyZonesUtils::Rect workArea, int zoneCount, int spacing) noexcept
{
    ZonesMap zones;
    
    long totalWidth = workArea.width() - (spacing * 2);
    long totalHeight = workArea.height() - (spacing * (zoneCount + 1));

    long top = spacing;
    long left = spacing;
    long bottom;
    long right;

    // Note: The expressions below are NOT equal to total{Width|Height} / zoneCount and are done
    // like this to make the sum of all zones' sizes exactly total{Width|Height}.
    for (int zoneIndex = 0; zoneIndex < zoneCount; ++zoneIndex)
    {
        right = totalWidth + spacing;
        bottom = top + (zoneIndex + 1) * totalHeight / zoneCount - zoneIndex * totalHeight / zoneCount;

        Zone zone(RECT{ left, top, right, bottom }, zones.size());
        if (zone.IsValid())
        {
            if (!AddZone(zone, zones))
            {
                Logger::error(L"Failed to create Rows layout. Invalid zone id");
                return {};
            }
        }
        else
        {
            // All zones within zone set should be valid in order to use its functionality.
            Logger::error(L"Failed to create Rows layout. Invalid zone");
            return {};
        }

        top = bottom + spacing;
    }

    return zones;
}

ZonesMap LayoutConfigurator::Columns(FancyZonesUtils::Rect workArea, int zoneCount, int spacing) noexcept
{
    ZonesMap zones;

    long totalWidth = workArea.width() - (spacing * (zoneCount + 1));
    long totalHeight = workArea.height() - (spacing * 2);

    long top = spacing;
    long left = spacing;
    long bottom;
    long right;

    // Note: The expressions below are NOT equal to total{Width|Height} / zoneCount and are done
    // like this to make the sum of all zones' sizes exactly total{Width|Height}.
    for (int zoneIndex = 0; zoneIndex < zoneCount; ++zoneIndex)
    {
        right = left + (zoneIndex + 1) * totalWidth / zoneCount - zoneIndex * totalWidth / zoneCount;
        bottom = totalHeight + spacing;

        Zone zone(RECT{ left, top, right, bottom }, zones.size());
        if (zone.IsValid())
        {
            if (!AddZone(zone, zones))
            {
                Logger::error(L"Failed to create Columns layout. Invalid zone id");
                return {};
            }
        }
        else
        {
            // All zones within zone set should be valid in order to use its functionality.
            Logger::error(L"Failed to create Columns layout. Invalid zone");
            return {};
        }

        left = right + spacing;
    }

    return zones;
}

ZonesMap LayoutConfigurator::Grid(FancyZonesUtils::Rect workArea, int zoneCount, int spacing) noexcept
{
    int rows = 1, columns = 1;
    while (zoneCount / rows >= rows)
    {
        rows++;
    }
    rows--;
    columns = zoneCount / rows;
    if (zoneCount % rows == 0)
    {
        // even grid
    }
    else
    {
        columns++;
    }

    FancyZonesDataTypes::GridLayoutInfo gridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Minimal{ .rows = rows, .columns = columns });

    // Note: The expressions below are NOT equal to C_MULTIPLIER / {rows|columns} and are done
    // like this to make the sum of all percents exactly C_MULTIPLIER
    for (int row = 0; row < rows; row++)
    {
        gridLayoutInfo.rowsPercents()[row] = C_MULTIPLIER * (row + 1) / rows - C_MULTIPLIER * row / rows;
    }
    for (int col = 0; col < columns; col++)
    {
        gridLayoutInfo.columnsPercents()[col] = C_MULTIPLIER * (col + 1) / columns - C_MULTIPLIER * col / columns;
    }

    for (int i = 0; i < rows; ++i)
    {
        gridLayoutInfo.cellChildMap()[i] = std::vector<int>(columns);
    }

    int index = 0;
    for (int row = 0; row < rows; row++)
    {
        for (int col = 0; col < columns; col++)
        {
            gridLayoutInfo.cellChildMap()[row][col] = index++;
            if (index == zoneCount)
            {
                index--;
            }
        }
    }

    return CalculateGridZones(workArea, gridLayoutInfo, spacing);
}

ZonesMap LayoutConfigurator::PriorityGrid(FancyZonesUtils::Rect workArea, int zoneCount, int spacing) noexcept
{
    if (zoneCount <= 0)
    {
        return {};
    }

    constexpr int predefinedLayoutsCount = sizeof(predefinedPriorityGridLayouts) / sizeof(FancyZonesDataTypes::GridLayoutInfo);
    if (zoneCount < predefinedLayoutsCount)
    {
        return CalculateGridZones(workArea, predefinedPriorityGridLayouts[zoneCount - 1], spacing); 
    }

    return Grid(workArea, zoneCount, spacing);
}

ZonesMap LayoutConfigurator::Custom(FancyZonesUtils::Rect workArea, HMONITOR monitor, const FancyZonesDataTypes::CustomLayoutData& zoneSet, int spacing) noexcept
{
    if (zoneSet.type == FancyZonesDataTypes::CustomLayoutType::Canvas && std::holds_alternative<FancyZonesDataTypes::CanvasLayoutInfo>(zoneSet.info))
    {
        ZonesMap zones;
        const auto& zoneSetInfo = std::get<FancyZonesDataTypes::CanvasLayoutInfo>(zoneSet.info);

        float width = static_cast<float>(workArea.width());
        float height = static_cast<float>(workArea.height());

        DPIAware::InverseConvert(monitor, width, height);

        for (const auto& zone : zoneSetInfo.zones)
        {
            float x = static_cast<float>(zone.x) * width / zoneSetInfo.lastWorkAreaWidth;
            float y = static_cast<float>(zone.y) * height / zoneSetInfo.lastWorkAreaHeight;
            float zoneWidth = static_cast<float>(zone.width) * width / zoneSetInfo.lastWorkAreaWidth;
            float zoneHeight = static_cast<float>(zone.height) * height / zoneSetInfo.lastWorkAreaHeight;

            DPIAware::Convert(monitor, x, y);
            DPIAware::Convert(monitor, zoneWidth, zoneHeight);
            
            Zone zone_to_add(RECT{ static_cast<long>(x), static_cast<long>(y), static_cast<long>(x + zoneWidth), static_cast<long>(y + zoneHeight) }, zones.size());
            if (zone_to_add.IsValid())
            {
                if (!AddZone(zone_to_add, zones))
                {
                    Logger::error(L"Failed to create Custom layout. Invalid zone id");
                    return {};
                }
            }
            else
            {
                // All zones within zone set should be valid in order to use its functionality.
                Logger::error(L"Failed to create Custom layout. Invalid zone");
                return {};
            }
        }

        return zones;
    }
    else if (zoneSet.type == FancyZonesDataTypes::CustomLayoutType::Grid && std::holds_alternative<FancyZonesDataTypes::GridLayoutInfo>(zoneSet.info))
    {
        const auto& info = std::get<FancyZonesDataTypes::GridLayoutInfo>(zoneSet.info);
        return CalculateGridZones(workArea, info, spacing);
    }

    return {};
}
