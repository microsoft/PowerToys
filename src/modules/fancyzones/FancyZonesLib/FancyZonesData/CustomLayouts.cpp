#include "../pch.h"
#include "CustomLayouts.h"

#include <common/logger/logger.h>

#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>
#include <FancyZonesLib/JsonHelpers.h>
#include <FancyZonesLib/util.h>

namespace JsonUtils
{
    std::vector<int> JsonArrayToNumVec(const json::JsonArray& arr)
    {
        std::vector<int> vec;
        for (const auto& val : arr)
        {
            vec.emplace_back(static_cast<int>(val.GetNumber()));
        }

        return vec;
    }

    namespace CanvasLayoutInfoJSON
    {
        std::optional<FancyZonesDataTypes::CanvasLayoutInfo> FromJson(const json::JsonObject& infoJson)
        {
            try
            {
                FancyZonesDataTypes::CanvasLayoutInfo info;
                info.lastWorkAreaWidth = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::CustomLayoutsIds::RefWidthID));
                info.lastWorkAreaHeight = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::CustomLayoutsIds::RefHeightID));

                json::JsonArray zonesJson = infoJson.GetNamedArray(NonLocalizable::CustomLayoutsIds::ZonesID);
                uint32_t size = zonesJson.Size();
                info.zones.reserve(size);
                for (uint32_t i = 0; i < size; ++i)
                {
                    json::JsonObject zoneJson = zonesJson.GetObjectAt(i);
                    const int x = static_cast<int>(zoneJson.GetNamedNumber(NonLocalizable::CustomLayoutsIds::XAxisID));
                    const int y = static_cast<int>(zoneJson.GetNamedNumber(NonLocalizable::CustomLayoutsIds::YAxisID));
                    const int width = static_cast<int>(zoneJson.GetNamedNumber(NonLocalizable::CustomLayoutsIds::WidthID));
                    const int height = static_cast<int>(zoneJson.GetNamedNumber(NonLocalizable::CustomLayoutsIds::HeightID));
                    FancyZonesDataTypes::CanvasLayoutInfo::Rect zone{ x, y, width, height };
                    info.zones.push_back(zone);
                }

                info.sensitivityRadius = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::CustomLayoutsIds::SensitivityRadiusID, DefaultValues::SensitivityRadius));
                return info;
            }
            catch (const winrt::hresult_error&)
            {
                return std::nullopt;
            }
        }
    }

    namespace GridLayoutInfoJSON
    {
        std::optional<FancyZonesDataTypes::GridLayoutInfo> FromJson(const json::JsonObject& infoJson)
        {
            try
            {
                FancyZonesDataTypes::GridLayoutInfo info(FancyZonesDataTypes::GridLayoutInfo::Minimal{});

                info.m_rows = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::CustomLayoutsIds::RowsID));
                info.m_columns = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::CustomLayoutsIds::ColumnsID));

                json::JsonArray rowsPercentage = infoJson.GetNamedArray(NonLocalizable::CustomLayoutsIds::RowsPercentageID);
                json::JsonArray columnsPercentage = infoJson.GetNamedArray(NonLocalizable::CustomLayoutsIds::ColumnsPercentageID);
                json::JsonArray cellChildMap = infoJson.GetNamedArray(NonLocalizable::CustomLayoutsIds::CellChildMapID);

                if (static_cast<int>(rowsPercentage.Size()) != info.m_rows || static_cast<int>(columnsPercentage.Size()) != info.m_columns || static_cast<int>(cellChildMap.Size()) != info.m_rows)
                {
                    return std::nullopt;
                }

                info.m_rowsPercents = JsonArrayToNumVec(rowsPercentage);
                info.m_columnsPercents = JsonArrayToNumVec(columnsPercentage);
                for (const auto& cellsRow : cellChildMap)
                {
                    const auto cellsArray = cellsRow.GetArray();
                    if (static_cast<int>(cellsArray.Size()) != info.m_columns)
                    {
                        return std::nullopt;
                    }
                    info.cellChildMap().push_back(JsonArrayToNumVec(cellsArray));
                }

                info.m_showSpacing = infoJson.GetNamedBoolean(NonLocalizable::CustomLayoutsIds::ShowSpacingID, DefaultValues::ShowSpacing);
                info.m_spacing = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::CustomLayoutsIds::SpacingID, DefaultValues::Spacing));
                info.m_sensitivityRadius = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::CustomLayoutsIds::SensitivityRadiusID, DefaultValues::SensitivityRadius));

                return info;
            }
            catch (const winrt::hresult_error&)
            {
                return std::nullopt;
            }
        }
    }
    
    struct CustomLayoutJSON
    {
        GUID layoutId{};
        FancyZonesDataTypes::CustomLayoutData data;

        static std::optional<CustomLayoutJSON> FromJson(const json::JsonObject& json)
        {
            try
            {
                CustomLayoutJSON result;

                auto idStr = json.GetNamedString(NonLocalizable::CustomLayoutsIds::UuidID);
                auto id = FancyZonesUtils::GuidFromString(idStr.c_str());
                if (!id)
                {
                    return std::nullopt;
                }

                result.layoutId = id.value();
                result.data.name = json.GetNamedString(NonLocalizable::CustomLayoutsIds::NameID);

                json::JsonObject infoJson = json.GetNamedObject(NonLocalizable::CustomLayoutsIds::InfoID);
                std::wstring zoneSetType = std::wstring{ json.GetNamedString(NonLocalizable::CustomLayoutsIds::TypeID) };
                if (zoneSetType.compare(NonLocalizable::CustomLayoutsIds::CanvasID) == 0)
                {
                    if (auto info = CanvasLayoutInfoJSON::FromJson(infoJson); info.has_value())
                    {
                        result.data.type = FancyZonesDataTypes::CustomLayoutType::Canvas;
                        result.data.info = std::move(info.value());
                    }
                    else
                    {
                        return std::nullopt;
                    }
                }
                else if (zoneSetType.compare(NonLocalizable::CustomLayoutsIds::GridID) == 0)
                {
                    if (auto info = GridLayoutInfoJSON::FromJson(infoJson); info.has_value())
                    {
                        result.data.type = FancyZonesDataTypes::CustomLayoutType::Grid;
                        result.data.info = std::move(info.value());
                    }
                    else
                    {
                        return std::nullopt;
                    }
                }
                else
                {
                    return std::nullopt;
                }

                return result;
            }
            catch (const winrt::hresult_error&)
            {
                return std::nullopt;
            }
        }
    };

    CustomLayouts::TCustomLayoutMap ParseJson(const json::JsonObject& json)
    {
        CustomLayouts::TCustomLayoutMap map{};
        auto layouts = json.GetNamedArray(NonLocalizable::CustomLayoutsIds::CustomLayoutsArrayID);

        for (uint32_t i = 0; i < layouts.Size(); ++i)
        {
            if (auto obj = CustomLayoutJSON::FromJson(layouts.GetObjectAt(i)); obj.has_value())
            {
                map[obj->layoutId] = std::move(obj->data);
            }
        }

        return std::move(map);
    }
}


CustomLayouts::CustomLayouts()
{
    const std::wstring& dataFileName = CustomLayoutsFileName();
    m_fileWatcher = std::make_unique<FileWatcher>(dataFileName, [&]() {
        PostMessageW(HWND_BROADCAST, WM_PRIV_CUSTOM_LAYOUTS_FILE_UPDATE, NULL, NULL);
    });
}


CustomLayouts& CustomLayouts::instance()
{
    static CustomLayouts self;
    return self;
}

void CustomLayouts::LoadData()
{
    auto data = json::from_file(CustomLayoutsFileName());

    try
    {
        if (data)
        {
            m_layouts = JsonUtils::ParseJson(data.value());
        }
        else
        {
            m_layouts.clear();
            Logger::info(L"custom-layouts.json file is missing or malformed");
        }
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error(L"Parsing custom-layouts error: {}", e.message());
    }
}

std::optional<LayoutData> CustomLayouts::GetLayout(const GUID& id) const noexcept
{
    auto iter = m_layouts.find(id);
    if (iter == m_layouts.end())
    {
        return std::nullopt;
    }
    
    FancyZonesDataTypes::CustomLayoutData customLayout = iter->second;
    LayoutData layout{
        .uuid = id,
        .type = FancyZonesDataTypes::ZoneSetLayoutType::Custom
    };

    if (customLayout.type == FancyZonesDataTypes::CustomLayoutType::Grid)
    {
        auto layoutInfo = std::get<FancyZonesDataTypes::GridLayoutInfo>(customLayout.info);
        layout.sensitivityRadius = layoutInfo.sensitivityRadius();
        layout.showSpacing = layoutInfo.showSpacing();
        layout.spacing = layoutInfo.spacing();
        layout.zoneCount = layoutInfo.zoneCount();
    }
    else if (customLayout.type == FancyZonesDataTypes::CustomLayoutType::Canvas)
    {
        auto layoutInfo = std::get<FancyZonesDataTypes::CanvasLayoutInfo>(customLayout.info);
        layout.sensitivityRadius = layoutInfo.sensitivityRadius;
        layout.zoneCount = static_cast<int>(layoutInfo.zones.size());
    }

    return layout;
}

std::optional<FancyZonesDataTypes::CustomLayoutData> CustomLayouts::GetCustomLayoutData(const GUID& id) const noexcept
{
    auto iter = m_layouts.find(id);
    if (iter != m_layouts.end())
    {
        return iter->second;
    }

    return std::nullopt;
}

const CustomLayouts::TCustomLayoutMap& CustomLayouts::GetAllLayouts() const noexcept
{
    return m_layouts;
}
