#include "../pch.h"
#include "DefaultLayouts.h"

#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>
#include <FancyZonesLib/util.h>

#include <common/logger/logger.h>

namespace DefaultLayoutsJsonUtils
{
    MonitorConfigurationType TypeFromString(const std::wstring& data)
    {
        if (data == L"vertical")
        {
            return MonitorConfigurationType::Vertical;
        }

        return MonitorConfigurationType::Horizontal;
    }

    std::wstring TypeToString(MonitorConfigurationType type)
    {
        switch (type)
        {
        case MonitorConfigurationType::Horizontal:
            return L"horizontal";
        case MonitorConfigurationType::Vertical:
            return L"vertical";
        default:
            return L"horizontal";
        }
    }

    struct LayoutJSON
    {
        static std::optional<LayoutData> FromJson(const json::JsonObject& json)
        {
            try
            {
                LayoutData data{};
                auto idStr = json.GetNamedString(NonLocalizable::DefaultLayoutsIds::UuidID, L"");
                if (!idStr.empty())
                {
                    auto id = FancyZonesUtils::GuidFromString(idStr.c_str());
                    if (!id.has_value())
                    {
                        return std::nullopt;
                    }

                    data.uuid = id.value();
                }
                
                data.type = FancyZonesDataTypes::TypeFromString(std::wstring{ json.GetNamedString(NonLocalizable::DefaultLayoutsIds::TypeID) });
                data.showSpacing = json.GetNamedBoolean(NonLocalizable::DefaultLayoutsIds::ShowSpacingID, DefaultValues::ShowSpacing);
                data.spacing = static_cast<int>(json.GetNamedNumber(NonLocalizable::DefaultLayoutsIds::SpacingID, DefaultValues::Spacing));
                data.zoneCount = static_cast<int>(json.GetNamedNumber(NonLocalizable::DefaultLayoutsIds::ZoneCountID, DefaultValues::ZoneCount));
                data.sensitivityRadius = static_cast<int>(json.GetNamedNumber(NonLocalizable::DefaultLayoutsIds::SensitivityRadiusID, DefaultValues::SensitivityRadius));

                return data;
            }
            catch (const winrt::hresult_error&)
            {
                return std::nullopt;
            }
        }

        static json::JsonObject ToJson(const LayoutData& data)
        {
            json::JsonObject result{};
            result.SetNamedValue(NonLocalizable::DefaultLayoutsIds::UuidID, json::value(FancyZonesUtils::GuidToString(data.uuid).value()));
            result.SetNamedValue(NonLocalizable::DefaultLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(data.type)));
            result.SetNamedValue(NonLocalizable::DefaultLayoutsIds::ShowSpacingID, json::value(data.showSpacing));
            result.SetNamedValue(NonLocalizable::DefaultLayoutsIds::SpacingID, json::value(data.spacing));
            result.SetNamedValue(NonLocalizable::DefaultLayoutsIds::ZoneCountID, json::value(data.zoneCount));
            result.SetNamedValue(NonLocalizable::DefaultLayoutsIds::SensitivityRadiusID, json::value(data.sensitivityRadius));
            return result;
        }
    };

    struct DefaultLayoutJSON
    {
        MonitorConfigurationType monitorConfigurationType{ MonitorConfigurationType::Horizontal };
        LayoutData layout{};

        static std::optional<DefaultLayoutJSON> FromJson(const json::JsonObject& json)
        {
            try
            {
                DefaultLayoutJSON result;

                auto type = TypeFromString(std::wstring{ json.GetNamedString(NonLocalizable::DefaultLayoutsIds::MonitorConfigurationTypeID) });
                auto layout = DefaultLayoutsJsonUtils::LayoutJSON::FromJson(json.GetNamedObject(NonLocalizable::DefaultLayoutsIds::LayoutID));
                if (!layout.has_value())
                {
                    return std::nullopt;
                }

                result.monitorConfigurationType = std::move(type);
                result.layout = std::move(layout.value());
                
                return result;
            }
            catch (const winrt::hresult_error&)
            {
                return std::nullopt;
            }
        }
    };

    DefaultLayouts::TDefaultLayoutsContainer ParseJson(const json::JsonObject& json)
    {
        DefaultLayouts::TDefaultLayoutsContainer map{};
        auto layouts = json.GetNamedArray(NonLocalizable::DefaultLayoutsIds::DefaultLayoutsArrayID);

        for (uint32_t i = 0; i < layouts.Size(); ++i)
        {
            if (auto obj = DefaultLayoutJSON::FromJson(layouts.GetObjectAt(i)); obj.has_value())
            {
                map[static_cast<MonitorConfigurationType>(obj->monitorConfigurationType)] = std::move(obj->layout);
            }
        }

        return std::move(map);
    }
}


DefaultLayouts::DefaultLayouts()
{
    const std::wstring& dataFileName = DefaultLayoutsFileName();
    m_fileWatcher = std::make_unique<FileWatcher>(dataFileName, [&]() {
        PostMessageW(HWND_BROADCAST, WM_PRIV_DEFAULT_LAYOUTS_FILE_UPDATE, NULL, NULL);
    });
}


DefaultLayouts& DefaultLayouts::instance()
{
    static DefaultLayouts self;
    return self;
}

void DefaultLayouts::LoadData()
{
    auto data = json::from_file(DefaultLayoutsFileName());

    try
    {
        if (data)
        {
            m_layouts = DefaultLayoutsJsonUtils::ParseJson(data.value());
        }
        else
        {
            m_layouts.clear();
            Logger::info(L"default-layouts.json file is missing or malformed");
        }
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error(L"Parsing default-layouts error: {}", e.message());
    }
}

LayoutData DefaultLayouts::GetDefaultLayout(MonitorConfigurationType type) const noexcept
{
    auto iter = m_layouts.find(type);
    if (iter != m_layouts.end())
    {
        return iter->second;
    }

    return LayoutData{};
}
