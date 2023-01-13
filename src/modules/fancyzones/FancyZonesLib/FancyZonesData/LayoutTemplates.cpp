#include "../pch.h"
#include "LayoutTemplates.h"

#include <common/logger/logger.h>

#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>

namespace JsonUtils
{
    struct TemplateLayoutJSON
    {
        static std::optional<LayoutData> FromJson(const json::JsonObject& json)
        {
            try
            {
                LayoutData data;
                
                data.uuid = GUID_NULL;
                data.type = FancyZonesDataTypes::TypeFromString(std::wstring{ json.GetNamedString(NonLocalizable::LayoutTemplatesIds::TypeID) });
                data.showSpacing = json.GetNamedBoolean(NonLocalizable::LayoutTemplatesIds::ShowSpacingID, DefaultValues::ShowSpacing);
                data.spacing = static_cast<int>(json.GetNamedNumber(NonLocalizable::LayoutTemplatesIds::SpacingID, DefaultValues::Spacing));
                data.zoneCount = static_cast<int>(json.GetNamedNumber(NonLocalizable::LayoutTemplatesIds::ZoneCountID, DefaultValues::ZoneCount));
                data.sensitivityRadius = static_cast<int>(json.GetNamedNumber(NonLocalizable::LayoutTemplatesIds::SensitivityRadiusID, DefaultValues::SensitivityRadius));

                return data;
            }
            catch (const winrt::hresult_error&)
            {
                return std::nullopt;
            }
        }
    };

    std::vector<LayoutData> ParseJson(const json::JsonObject& json)
    {
        std::vector<LayoutData> vec{};
        auto layouts = json.GetNamedArray(NonLocalizable::LayoutTemplatesIds::LayoutTemplatesArrayID);

        for (uint32_t i = 0; i < layouts.Size(); ++i)
        {
            if (auto obj = TemplateLayoutJSON::FromJson(layouts.GetObjectAt(i)); obj.has_value())
            {
                vec.emplace_back(std::move(obj.value()));
            }
        }

        return vec;
    }
}

LayoutTemplates::LayoutTemplates()
{
    const std::wstring& fileName = LayoutTemplatesFileName();
    m_fileWatcher = std::make_unique<FileWatcher>(fileName, [&]() {
        PostMessageW(HWND_BROADCAST, WM_PRIV_LAYOUT_TEMPLATES_FILE_UPDATE, NULL, NULL);
    });
}

LayoutTemplates& LayoutTemplates::instance()
{
    static LayoutTemplates self;
    return self;
}

void LayoutTemplates::LoadData()
{
    auto data = json::from_file(LayoutTemplatesFileName());

    try
    {
        if (data)
        {
            m_layouts = JsonUtils::ParseJson(data.value());
        }
        else
        {
            m_layouts.clear();
            Logger::info(L"layout-templates.json file is missing or malformed");
        }
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error(L"Parsing layout-templates error: {}", e.message());
    }
}

std::optional<LayoutData> LayoutTemplates::GetLayout(FancyZonesDataTypes::ZoneSetLayoutType type) const noexcept
{
    for (const auto& layout : m_layouts)
    {
        if (layout.type == type)
        {
            return layout;
        }
    }

    return std::nullopt;
}

