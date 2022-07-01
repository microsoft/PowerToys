#include "../pch.h"
#include "AppliedLayouts.h"

#include <common/logger/call_tracer.h>
#include <common/logger/logger.h>

#include <FancyZonesLib/GuidUtils.h>
#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>
#include <FancyZonesLib/JsonHelpers.h>
#include <FancyZonesLib/MonitorUtils.h>
#include <FancyZonesLib/VirtualDesktop.h>
#include <FancyZonesLib/util.h>

namespace 
{
    // didn't use default constants since if they'll be changed later, it'll break this function
    bool isLayoutDefault(const Layout& layout)
    {
        return layout.type == FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid &&
               layout.zoneCount == 3 &&
               layout.spacing == 16 &&
               layout.showSpacing == true &&
               layout.sensitivityRadius == 20;
    }
}

namespace JsonUtils
{
    struct LayoutJSON
    {
        static std::optional<Layout> FromJson(const json::JsonObject& json)
        {
            try
            {
                Layout data;
                auto idStr = json.GetNamedString(NonLocalizable::AppliedLayoutsIds::UuidID);
                auto id = FancyZonesUtils::GuidFromString(idStr.c_str());
                if (!id.has_value())
                {
                    return std::nullopt;
                }

                data.uuid = id.value();
                data.type = FancyZonesDataTypes::TypeFromString(std::wstring{ json.GetNamedString(NonLocalizable::AppliedLayoutsIds::TypeID) });
                data.showSpacing = json.GetNamedBoolean(NonLocalizable::AppliedLayoutsIds::ShowSpacingID);
                data.spacing = static_cast<int>(json.GetNamedNumber(NonLocalizable::AppliedLayoutsIds::SpacingID));
                data.zoneCount = static_cast<int>(json.GetNamedNumber(NonLocalizable::AppliedLayoutsIds::ZoneCountID));
                data.sensitivityRadius = static_cast<int>(json.GetNamedNumber(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, DefaultValues::SensitivityRadius));

                return data;
            }
            catch (const winrt::hresult_error&)
            {
                return std::nullopt;
            }
        }

        static json::JsonObject ToJson(const Layout& data)
        {
            json::JsonObject result{};
            result.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(FancyZonesUtils::GuidToString(data.uuid).value()));
            result.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(data.type)));
            result.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(data.showSpacing));
            result.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(data.spacing));
            result.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(data.zoneCount));
            result.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(data.sensitivityRadius));
            return result;
        }
    };

    struct AppliedLayoutsJSON
    {
    private:
        static std::optional<FancyZonesDataTypes::WorkAreaId> WorkAreaIdFromJson(const json::JsonObject& json)
        {
            try
            {
                if (json.HasKey(NonLocalizable::AppliedLayoutsIds::DeviceID))
                {
                    json::JsonObject device = json.GetNamedObject(NonLocalizable::AppliedLayoutsIds::DeviceID);
                    std::wstring monitor = device.GetNamedString(NonLocalizable::AppliedLayoutsIds::MonitorID).c_str();
                    std::wstring monitorInstance = device.GetNamedString(NonLocalizable::AppliedLayoutsIds::MonitorInstanceID, L"").c_str();
                    std::wstring monitorSerialNumber = device.GetNamedString(NonLocalizable::AppliedLayoutsIds::MonitorSerialNumberID, L"").c_str();
                    std::wstring virtualDesktop = device.GetNamedString(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID).c_str();

                    auto virtualDesktopGuid = FancyZonesUtils::GuidFromString(virtualDesktop);
                    if (!virtualDesktopGuid)
                    {
                        return std::nullopt;
                    }

                    FancyZonesDataTypes::DeviceId deviceId{};
                    if (monitorInstance.empty())
                    {
                        // old data
                        deviceId = MonitorUtils::Display::ConvertObsoleteDeviceId(monitor);
                    }
                    else
                    {
                        deviceId.id = monitor;
                        deviceId.instanceId = monitorInstance;
                    }
                    
                    FancyZonesDataTypes::MonitorId monitorId{
                        .deviceId = deviceId,
                        .serialNumber = monitorSerialNumber
                    };

                    return FancyZonesDataTypes::WorkAreaId{
                        .monitorId = monitorId,
                        .virtualDesktopId = virtualDesktopGuid.value(),
                    };
                }
                else
                {
                    std::wstring deviceIdStr = json.GetNamedString(NonLocalizable::AppliedLayoutsIds::DeviceIdID).c_str();
                    auto bcDeviceId = BackwardsCompatibility::DeviceIdData::ParseDeviceId(deviceIdStr);
                    if (!bcDeviceId)
                    {
                        return std::nullopt;
                    }

                    return FancyZonesDataTypes::WorkAreaId{
                        .monitorId = { .deviceId = MonitorUtils::Display::ConvertObsoleteDeviceId(bcDeviceId->deviceName) },
                        .virtualDesktopId = bcDeviceId->virtualDesktopId,
                    };
                }
            }
            catch (const winrt::hresult_error&)
            {
                return std::nullopt;
            }
        }

    public:
        FancyZonesDataTypes::WorkAreaId workAreaId;
        Layout data;

        static std::optional<AppliedLayoutsJSON> FromJson(const json::JsonObject& json)
        {
            try
            {
                AppliedLayoutsJSON result;

                auto deviceIdOpt = WorkAreaIdFromJson(json);
                if (!deviceIdOpt.has_value())
                {
                    return std::nullopt;
                }

                auto layout = JsonUtils::LayoutJSON::FromJson(json.GetNamedObject(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID));
                if (!layout.has_value())
                {
                    return std::nullopt;
                }
                
                result.workAreaId = std::move(deviceIdOpt.value());
                result.data = std::move(layout.value());
                return result;
            }
            catch (const winrt::hresult_error&)
            {
                return std::nullopt;
            }
        }

        static json::JsonObject ToJson(const AppliedLayoutsJSON& value)
        {
            json::JsonObject device{};
            device.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorID, json::value(value.workAreaId.monitorId.deviceId.id));
            device.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorInstanceID, json::value(value.workAreaId.monitorId.deviceId.instanceId));
            device.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorSerialNumberID, json::value(value.workAreaId.monitorId.serialNumber));

            auto virtualDesktopStr = FancyZonesUtils::GuidToString(value.workAreaId.virtualDesktopId);
            if (virtualDesktopStr)
            {
                device.SetNamedValue(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID, json::value(virtualDesktopStr.value()));
            }

            json::JsonObject result{};
            result.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceID, device);
            result.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, JsonUtils::LayoutJSON::ToJson(value.data));
            
            return result;
        }
    };

    AppliedLayouts::TAppliedLayoutsMap ParseJson(const json::JsonObject& json)
    {
        AppliedLayouts::TAppliedLayoutsMap map{};
        auto layouts = json.GetNamedArray(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID);

        for (uint32_t i = 0; i < layouts.Size(); ++i)
        {
            if (auto obj = AppliedLayoutsJSON::FromJson(layouts.GetObjectAt(i)); obj.has_value())
            {
                // skip default layouts in case if they were applied to different resolutions on the same monitor.
                // NOTE: keep the default layout check for users who update PT version from the v0.57 
                if (!map.contains(obj->workAreaId) && !isLayoutDefault(obj->data))
                {
                    map[obj->workAreaId] = std::move(obj->data);
                }
            }
        }

        return map;
    }

    json::JsonObject SerializeJson(const AppliedLayouts::TAppliedLayoutsMap& map)
    {
        json::JsonObject json{};
        json::JsonArray layoutArray{};

        for (const auto& [id, data] : map)
        {
            AppliedLayoutsJSON obj{};
            obj.workAreaId = id;
            obj.data = data;
            layoutArray.Append(AppliedLayoutsJSON::ToJson(obj));
        }

        json.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutArray);
        return json;
    }
}


AppliedLayouts::AppliedLayouts()
{
    const std::wstring& fileName = AppliedLayoutsFileName();
    m_fileWatcher = std::make_unique<FileWatcher>(fileName, [&]() {
        PostMessageW(HWND_BROADCAST, WM_PRIV_APPLIED_LAYOUTS_FILE_UPDATE, NULL, NULL);
    });
}

AppliedLayouts& AppliedLayouts::instance()
{
    static AppliedLayouts self;
    return self;
}

void AppliedLayouts::LoadData()
{
    auto data = json::from_file(AppliedLayoutsFileName());

    try
    {
        if (data)
        {
            m_layouts = JsonUtils::ParseJson(data.value());
        }
        else
        {
            m_layouts.clear();
            Logger::info(L"applied-layouts.json file is missing or malformed");
        }
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error(L"Parsing applied-layouts error: {}", e.message());
    }
}

void AppliedLayouts::SaveData()
{
    bool dirtyFlag = false;
    TAppliedLayoutsMap updatedMap;
    
    for (const auto& [id, data] : m_layouts)
    {
        auto updatedId = id;
        if (!VirtualDesktop::instance().IsVirtualDesktopIdSavedInRegistry(id.virtualDesktopId))
        {
            updatedId.virtualDesktopId = GUID_NULL;
            dirtyFlag = true;
        }

        updatedMap.insert({ updatedId, data });
    }

    if (dirtyFlag)
    {
        json::to_file(AppliedLayoutsFileName(), JsonUtils::SerializeJson(updatedMap));
    }
    else
    {
        json::to_file(AppliedLayoutsFileName(), JsonUtils::SerializeJson(m_layouts));
    }
}

void AppliedLayouts::AdjustWorkAreaIds(const std::vector<FancyZonesDataTypes::MonitorId>& ids)
{
    bool dirtyFlag = false;

    std::vector<std::pair<FancyZonesDataTypes::WorkAreaId, std::wstring>> replaceWithSerialNumber{};
    for (auto iter = m_layouts.begin(); iter != m_layouts.end(); ++iter)
    {
        const auto& [id, layout] = *iter;
        if (id.monitorId.serialNumber.empty() && !id.monitorId.deviceId.isDefault())
        {
            for (const auto& monitorId : ids)
            {
                if (id.monitorId.deviceId.id == monitorId.deviceId.id && id.monitorId.deviceId.instanceId == monitorId.deviceId.instanceId)
                {
                    replaceWithSerialNumber.push_back({ id, monitorId.serialNumber });
                    dirtyFlag = true;
                    break;
                }
            }
        }
    }

    for (const auto& id : replaceWithSerialNumber)
    {
        auto mapEntry = m_layouts.extract(id.first);
        mapEntry.key().monitorId.serialNumber = id.second;
        m_layouts.insert(std::move(mapEntry));
    }

    if (dirtyFlag)
    {
        SaveData();
    }
}

void AppliedLayouts::SyncVirtualDesktops()
{
    // Explorer persists current virtual desktop identifier to registry on a per session basis,
    // but only after first virtual desktop switch happens. If the user hasn't switched virtual
    // desktops in this session value in registry will be empty and we will use default GUID in
    // that case (00000000-0000-0000-0000-000000000000).

    auto savedInRegistryVirtualDesktopID = VirtualDesktop::instance().GetCurrentVirtualDesktopIdFromRegistry();
    if (!savedInRegistryVirtualDesktopID.has_value() || savedInRegistryVirtualDesktopID.value() == GUID_NULL)
    {
        return;
    }

    auto currentVirtualDesktopStr = FancyZonesUtils::GuidToString(savedInRegistryVirtualDesktopID.value());
    if (!currentVirtualDesktopStr.has_value())
    {
        Logger::error(L"Failed to convert virtual desktop GUID to string");
        return;
    }

    Logger::info(L"AppliedLayouts Sync virtual desktops: current {}", currentVirtualDesktopStr.value());

    bool dirtyFlag = false;

    std::vector<FancyZonesDataTypes::WorkAreaId> replaceWithCurrentId{};

    for (const auto& [id, data] : m_layouts)
    {
        if (id.virtualDesktopId == GUID_NULL)
        {
            replaceWithCurrentId.push_back(id);
            dirtyFlag = true;
        }
    }

    for (const auto& id : replaceWithCurrentId)
    {
        auto mapEntry = m_layouts.extract(id);
        mapEntry.key().virtualDesktopId = savedInRegistryVirtualDesktopID.value();
        m_layouts.insert(std::move(mapEntry));
    }

    if (dirtyFlag)
    {
        Logger::info(L"Update Virtual Desktop id to {}", currentVirtualDesktopStr.value());
        SaveData();
    }
}

void AppliedLayouts::RemoveDeletedVirtualDesktops(const std::vector<GUID>& activeDesktops)
{
    std::unordered_set<GUID> active(std::begin(activeDesktops), std::end(activeDesktops));
    bool dirtyFlag = false;

    for (auto it = std::begin(m_layouts); it != std::end(m_layouts);)
    {
        GUID desktopId = it->first.virtualDesktopId;

        if (desktopId != GUID_NULL)
        {
            auto foundId = active.find(desktopId);
            if (foundId == std::end(active))
            {
                wil::unique_cotaskmem_string virtualDesktopIdStr;
                if (SUCCEEDED(StringFromCLSID(desktopId, &virtualDesktopIdStr)))
                {
                    Logger::info(L"Remove Virtual Desktop id {}", virtualDesktopIdStr.get());
                }

                it = m_layouts.erase(it);
                dirtyFlag = true;
                continue;
            }
        }
        ++it;
    }

    if (dirtyFlag)
    {
        SaveData();
    }
}

std::optional<Layout> AppliedLayouts::GetDeviceLayout(const FancyZonesDataTypes::WorkAreaId& id) const noexcept
{
    auto iter = m_layouts.find(id);
    if (iter != m_layouts.end())
    {
        return iter->second;
    }

    return std::nullopt;
}

const AppliedLayouts::TAppliedLayoutsMap& AppliedLayouts::GetAppliedLayoutMap() const noexcept
{
    return m_layouts;
}

bool AppliedLayouts::IsLayoutApplied(const FancyZonesDataTypes::WorkAreaId& id) const noexcept
{
    auto iter = m_layouts.find(id);
    return iter != m_layouts.end();
}

bool AppliedLayouts::ApplyLayout(const FancyZonesDataTypes::WorkAreaId& deviceId, Layout layout)
{
    m_layouts[deviceId] = std::move(layout);
    return true;
}

bool AppliedLayouts::ApplyDefaultLayout(const FancyZonesDataTypes::WorkAreaId& deviceId)
{
    Logger::info(L"Set default layout on {}", deviceId.toString());

    GUID guid;
    auto result{ CoCreateGuid(&guid) };
    if (!SUCCEEDED(result))
    {
        Logger::error("Failed to create an ID for the new layout");
        return false;
    }

    Layout layout{
        .uuid = guid,
        .type = FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid,
        .showSpacing = DefaultValues::ShowSpacing,
        .spacing = DefaultValues::Spacing,
        .zoneCount = DefaultValues::ZoneCount,
        .sensitivityRadius = DefaultValues::SensitivityRadius
    };

    m_layouts[deviceId] = std::move(layout);
    
    // Saving default layout data doesn't make sense, since it's ignored on parsing.
    // Given that default layouts are ignored when parsing, 
    // saving default data can cause an infinite loop of reading, reapplying default layout and saving the same file.
    return true;
}

bool AppliedLayouts::CloneLayout(const FancyZonesDataTypes::WorkAreaId& srcId, const FancyZonesDataTypes::WorkAreaId& dstId)
{
    if (srcId == dstId || m_layouts.find(srcId) == m_layouts.end())
    {
        return false;
    }

    Logger::info(L"Clone layout from {} to {}", dstId.toString(), srcId.toString());
    m_layouts[dstId] = m_layouts[srcId];

    SaveData();

    return true;
}
