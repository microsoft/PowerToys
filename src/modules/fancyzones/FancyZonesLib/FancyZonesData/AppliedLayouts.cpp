#include "../pch.h"
#include "AppliedLayouts.h"

#include <common/logger/call_tracer.h>
#include <common/logger/logger.h>

#include <FancyZonesLib/GuidUtils.h>
#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include <FancyZonesLib/FancyZonesData/DefaultLayouts.h>
#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesWinHookEventIDs.h>
#include <FancyZonesLib/JsonHelpers.h>
#include <FancyZonesLib/MonitorUtils.h>
#include <FancyZonesLib/VirtualDesktop.h>
#include <FancyZonesLib/util.h>

namespace 
{
    // didn't use default constants since if they'll be changed later, it'll break this function
    bool isLayoutDefault(const LayoutData& layout)
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
        static std::optional<LayoutData> FromJson(const json::JsonObject& json)
        {
            try
            {
                LayoutData data{};
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

        static json::JsonObject ToJson(const LayoutData& data)
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
        static std::pair<std::optional<FancyZonesDataTypes::WorkAreaId>, bool> WorkAreaIdFromJson(const json::JsonObject& json)
        {
            try
            {
                if (json.HasKey(NonLocalizable::AppliedLayoutsIds::DeviceID))
                {
                    json::JsonObject device = json.GetNamedObject(NonLocalizable::AppliedLayoutsIds::DeviceID);
                    std::wstring monitor = device.GetNamedString(NonLocalizable::AppliedLayoutsIds::MonitorID).c_str();
                    std::wstring monitorInstance = device.GetNamedString(NonLocalizable::AppliedLayoutsIds::MonitorInstanceID, L"").c_str();
                    std::wstring monitorSerialNumber = device.GetNamedString(NonLocalizable::AppliedLayoutsIds::MonitorSerialNumberID, L"").c_str();
                    int monitorNumber = static_cast<int>(device.GetNamedNumber(NonLocalizable::AppliedLayoutsIds::MonitorNumberID, 0));
                    std::wstring virtualDesktop = device.GetNamedString(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID).c_str();

                    auto virtualDesktopGuid = FancyZonesUtils::GuidFromString(virtualDesktop);
                    if (!virtualDesktopGuid)
                    {
                        return { std::nullopt, false };
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
                        deviceId.number = monitorNumber;
                    }

                    FancyZonesDataTypes::MonitorId monitorId{
                        .deviceId = deviceId,
                        .serialNumber = monitorSerialNumber
                    };

                    return { FancyZonesDataTypes::WorkAreaId{
                                 .monitorId = monitorId,
                                 .virtualDesktopId = virtualDesktopGuid.value(),
                             },
                             false };
                }
                else
                {
                    std::wstring deviceIdStr = json.GetNamedString(NonLocalizable::AppliedLayoutsIds::DeviceIdID).c_str();
                    auto bcDeviceId = BackwardsCompatibility::DeviceIdData::ParseDeviceId(deviceIdStr);
                    if (!bcDeviceId)
                    {
                        return { std::nullopt, false };
                    }

                    return { FancyZonesDataTypes::WorkAreaId{
                                 .monitorId = { .deviceId = MonitorUtils::Display::ConvertObsoleteDeviceId(bcDeviceId->deviceName) },
                                 .virtualDesktopId = bcDeviceId->virtualDesktopId,
                             },
                             true };
                }
            }
            catch (const winrt::hresult_error&)
            {
                return { std::nullopt, false };
            }
        }

    public:
        FancyZonesDataTypes::WorkAreaId workAreaId;
        LayoutData data{};
        bool hasResolutionInId = false;

        static std::optional<AppliedLayoutsJSON> FromJson(const json::JsonObject& json)
        {
            try
            {
                AppliedLayoutsJSON result;

                auto deviceIdOpt = WorkAreaIdFromJson(json);
                if (!deviceIdOpt.first.has_value())
                {
                    return std::nullopt;
                }

                auto layout = JsonUtils::LayoutJSON::FromJson(json.GetNamedObject(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID));
                if (!layout.has_value())
                {
                    return std::nullopt;
                }
                
                result.workAreaId = std::move(deviceIdOpt.first.value());
                result.data = std::move(layout.value());
                result.hasResolutionInId = deviceIdOpt.second;

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
            device.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorNumberID, json::value(value.workAreaId.monitorId.deviceId.number));

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
                if (obj->hasResolutionInId && isLayoutDefault(obj->data))
                {
                    continue;
                }

                if (!map.contains(obj->workAreaId))
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
    json::to_file(AppliedLayoutsFileName(), JsonUtils::SerializeJson(m_layouts));
}

void AppliedLayouts::AdjustWorkAreaIds(const std::vector<FancyZonesDataTypes::MonitorId>& ids)
{
    bool dirtyFlag = false;

    std::vector<std::pair<FancyZonesDataTypes::WorkAreaId, FancyZonesDataTypes::WorkAreaId>> replaceWithSerialNumber{};
    for (auto iter = m_layouts.begin(); iter != m_layouts.end(); ++iter)
    {
        const auto& [id, layout] = *iter;
        bool serialNumberNotSet = id.monitorId.serialNumber.empty() && !id.monitorId.deviceId.isDefault();
        bool monitorNumberNotSet = id.monitorId.deviceId.number == 0;
        if (serialNumberNotSet || monitorNumberNotSet)
        {
            for (const auto& monitorId : ids)
            {
                if (id.monitorId.deviceId.id == monitorId.deviceId.id && id.monitorId.deviceId.instanceId == monitorId.deviceId.instanceId)
                {
                    FancyZonesDataTypes::WorkAreaId updatedId = id;
                    updatedId.monitorId.serialNumber = monitorId.serialNumber;
                    updatedId.monitorId.deviceId.number = monitorId.deviceId.number;
                    replaceWithSerialNumber.push_back({ id, updatedId });
                    dirtyFlag = true;
                    break;
                }
            }
        }
    }

    for (const auto& id : replaceWithSerialNumber)
    {
        auto mapEntry = m_layouts.extract(id.first);
        mapEntry.key().monitorId = id.second.monitorId;
        m_layouts.insert(std::move(mapEntry));
    }

    if (dirtyFlag)
    {
        SaveData();
    }
}

void AppliedLayouts::SyncVirtualDesktops(const GUID& currentVirtualDesktop, const GUID& lastUsedVirtualDesktop, std::optional<std::vector<GUID>> desktops)
{
    TAppliedLayoutsMap layouts;

    auto findCurrentVirtualDesktopInSavedLayouts = [&](const std::pair<FancyZonesDataTypes::WorkAreaId, LayoutData>& val) -> bool { return val.first.virtualDesktopId == currentVirtualDesktop; };
    bool replaceLastUsedWithCurrent = !desktops.has_value() || currentVirtualDesktop == GUID_NULL ||
        std::find_if(m_layouts.begin(), m_layouts.end(), findCurrentVirtualDesktopInSavedLayouts) == m_layouts.end();
    bool copyToOtherVirtualDesktops = lastUsedVirtualDesktop == GUID_NULL && currentVirtualDesktop != GUID_NULL && desktops.has_value();

    for (const auto& [workAreaId, layout] : m_layouts)
    {
        if (replaceLastUsedWithCurrent && workAreaId.virtualDesktopId == lastUsedVirtualDesktop)
        {
            // replace "lastUsedVirtualDesktop" with "currentVirtualDesktop"
            auto updatedWorkAreaId = workAreaId;
            updatedWorkAreaId.virtualDesktopId = currentVirtualDesktop;
            layouts.insert({ updatedWorkAreaId, layout });

            if (copyToOtherVirtualDesktops)
            {
                // Copy to other virtual desktops on the 1st VD creation.
                // If we just replace the id, we'll lose the layout on the other desktops.
                // Usage scenario: 
                // apply the layout to the single virtual desktop with id = GUID_NULL, 
                // create the 2nd virtual desktop and switch to it, 
                // so virtual desktop id changes from GUID_NULL to a valid value of the 2nd VD.
                // Then change the layout on the 2nd VD and switch back to the 1st VD. 
                // Layout on the initial VD will be changed too without initializing it beforehand.
                for (const auto& id : desktops.value())
                {
                    if (id != currentVirtualDesktop)
                    {
                        auto copyWorkAreaId = workAreaId;
                        copyWorkAreaId.virtualDesktopId = id;
                        layouts.insert({ copyWorkAreaId, layout });
                    }
                }
            }
        }

        if (workAreaId.virtualDesktopId == currentVirtualDesktop || (desktops.has_value() && 
            std::find(desktops.value().begin(), desktops.value().end(), workAreaId.virtualDesktopId) != desktops.value().end()))
        {
            // keep only actual virtual desktop values
            layouts.insert({ workAreaId, layout });
        }
    }

    if (layouts != m_layouts)
    {
        m_layouts = layouts;
        SaveData();

        std::wstring currentStr = FancyZonesUtils::GuidToString(currentVirtualDesktop).value_or(L"incorrect guid");
        std::wstring lastUsedStr = FancyZonesUtils::GuidToString(lastUsedVirtualDesktop).value_or(L"incorrect guid");
        std::wstring registryStr{};
        if (desktops.has_value())
        {
            for (const auto& id : desktops.value())
            {
                registryStr += FancyZonesUtils::GuidToString(id).value_or(L"incorrect guid") + L" ";
            }
        }
        else
        {
            registryStr = L"empty";
        }

        Logger::info(L"Synced virtual desktops. Current: {}, last used: {}, registry: {}", currentStr, lastUsedStr, registryStr);
    }
}

std::optional<LayoutData> AppliedLayouts::GetDeviceLayout(const FancyZonesDataTypes::WorkAreaId& id) const noexcept
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

bool AppliedLayouts::ApplyLayout(const FancyZonesDataTypes::WorkAreaId& workAreaId, LayoutData layout)
{
    m_layouts[workAreaId] = layout;
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

    MonitorConfigurationType type = MonitorConfigurationType::Horizontal;
    MONITORINFOEX monitorInfo;
    monitorInfo.cbSize = sizeof(monitorInfo);
    if (GetMonitorInfo(deviceId.monitorId.monitor, &monitorInfo))
    {
        LONG width = monitorInfo.rcMonitor.right - monitorInfo.rcMonitor.left;
        LONG height = monitorInfo.rcMonitor.bottom - monitorInfo.rcMonitor.top;
        if (height > width)
        {
            type = MonitorConfigurationType::Vertical;
        }
    }

    m_layouts[deviceId] = DefaultLayouts::instance().GetDefaultLayout(type);
    
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
    return ApplyLayout(dstId, m_layouts[srcId]);
}
