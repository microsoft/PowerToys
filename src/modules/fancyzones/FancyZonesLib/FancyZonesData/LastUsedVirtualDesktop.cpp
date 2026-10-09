#include "../pch.h"
#include "LastUsedVirtualDesktop.h"

#include <common/logger/logger.h>

#include <FancyZonesLib/util.h>

namespace JsonUtils
{
    GUID ParseJson(const json::JsonObject& json)
    {
        auto idStr = json.GetNamedString(NonLocalizable::LastUsedVirtualDesktop::LastUsedVirtualDesktopID);
        auto idOpt = FancyZonesUtils::GuidFromString(idStr.c_str());

        if (!idOpt.has_value())
        {
            return {};
        }

        return idOpt.value();
    }

    json::JsonObject SerializeJson(const GUID& id)
    {
        json::JsonObject result{};

        auto virtualDesktopStr = FancyZonesUtils::GuidToString(id);
        if (virtualDesktopStr)
        {
            result.SetNamedValue(NonLocalizable::LastUsedVirtualDesktop::LastUsedVirtualDesktopID, json::value(virtualDesktopStr.value()));
        }

        return result;
    }
}


LastUsedVirtualDesktop& LastUsedVirtualDesktop::instance()
{
    static LastUsedVirtualDesktop self;
    return self;
}

void LastUsedVirtualDesktop::LoadData()
{
    auto data = json::from_file(LastUsedVirtualDesktopFileName());

    try
    {
        if (data)
        {
            m_id = JsonUtils::ParseJson(data.value());
        }
        else
        {
            m_id = GUID_NULL;
        }
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error(L"Parsing last-used-virtual-desktop error: {}", e.message());
    }
}

void LastUsedVirtualDesktop::SaveData() const
{
    json::to_file(LastUsedVirtualDesktopFileName(), JsonUtils::SerializeJson(m_id));
}

GUID LastUsedVirtualDesktop::GetId() const
{
    return m_id;
}

void LastUsedVirtualDesktop::SetId(GUID id)
{
    m_id = id;
}