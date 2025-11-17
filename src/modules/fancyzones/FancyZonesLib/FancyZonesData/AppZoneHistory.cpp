#include "../pch.h"
#include "AppZoneHistory.h"

#include <common/logger/call_tracer.h>
#include <common/logger/logger.h>
#include <common/utils/process_path.h>

#include <FancyZonesLib/GuidUtils.h>
#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/JsonHelpers.h>
#include <FancyZonesLib/MonitorUtils.h>
#include <FancyZonesLib/VirtualDesktop.h>
#include <FancyZonesLib/util.h>

namespace JsonUtils
{
    struct AppZoneHistoryJSON
    {
    private:
        static std::optional<FancyZonesDataTypes::WorkAreaId> DeviceIdFromJson(const json::JsonObject& json)
        {
            try
            {
                if (json.HasKey(NonLocalizable::AppZoneHistoryIds::DeviceID))
                {
                    json::JsonObject device = json.GetNamedObject(NonLocalizable::AppZoneHistoryIds::DeviceID);
                    std::wstring monitor = device.GetNamedString(NonLocalizable::AppZoneHistoryIds::MonitorID).c_str();
                    std::wstring monitorInstance = device.GetNamedString(NonLocalizable::AppZoneHistoryIds::MonitorInstanceID, L"").c_str();
                    std::wstring monitorSerialNumber = device.GetNamedString(NonLocalizable::AppZoneHistoryIds::MonitorSerialNumberID, L"").c_str();
                    int monitorNumber = static_cast<int>(device.GetNamedNumber(NonLocalizable::AppZoneHistoryIds::MonitorNumberID, 0));
                    std::wstring virtualDesktop = device.GetNamedString(NonLocalizable::AppZoneHistoryIds::VirtualDesktopID).c_str();

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
                        deviceId.number = monitorNumber;
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
                    std::wstring deviceIdStr = json.GetNamedString(NonLocalizable::AppZoneHistoryIds::DeviceIdID).c_str();
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

        static std::optional<FancyZonesDataTypes::AppZoneHistoryData> ParseSingleAppZoneHistoryItem(const json::JsonObject& json)
        {
            FancyZonesDataTypes::AppZoneHistoryData data;
            if (json.HasKey(NonLocalizable::AppZoneHistoryIds::LayoutIndexesID))
            {
                data.zoneIndexSet = {};
                for (const auto& value : json.GetNamedArray(NonLocalizable::AppZoneHistoryIds::LayoutIndexesID))
                {
                    data.zoneIndexSet.push_back(static_cast<ZoneIndex>(value.GetNumber()));
                }
            }
            else if (json.HasKey(NonLocalizable::AppZoneHistoryIds::LayoutIndexesID))
            {
                data.zoneIndexSet = { static_cast<ZoneIndex>(json.GetNamedNumber(NonLocalizable::AppZoneHistoryIds::LayoutIndexesID)) };
            }

            auto deviceIdOpt = DeviceIdFromJson(json);
            if (!deviceIdOpt)
            {
                return std::nullopt;
            }

            data.workAreaId = deviceIdOpt.value();
            std::wstring layoutIdStr = json.GetNamedString(NonLocalizable::AppZoneHistoryIds::LayoutIdID).c_str();
            auto layoutIdOpt = FancyZonesUtils::GuidFromString(layoutIdStr);
            if (!layoutIdOpt.has_value())
            {
                return std::nullopt;
            }

            data.layoutId = layoutIdOpt.value();
            return data;
        }

    public:
        std::wstring appPath;
        std::vector<FancyZonesDataTypes::AppZoneHistoryData> data;
        
        static std::optional<AppZoneHistoryJSON> FromJson(const json::JsonObject& json)
        {
            try
            {
                AppZoneHistoryJSON result;

                result.appPath = json.GetNamedString(NonLocalizable::AppZoneHistoryIds::AppPathID);
                if (json.HasKey(NonLocalizable::AppZoneHistoryIds::HistoryID))
                {
                    auto appHistoryArray = json.GetNamedArray(NonLocalizable::AppZoneHistoryIds::HistoryID);
                    for (uint32_t i = 0; i < appHistoryArray.Size(); ++i)
                    {
                        json::JsonObject json_hist = appHistoryArray.GetObjectAt(i);
                        if (auto data = ParseSingleAppZoneHistoryItem(json_hist); data.has_value())
                        {
                            result.data.push_back(std::move(data.value()));
                        }
                    }
                }
                else
                {
                    // handle previous file format, with single desktop layout information per application
                    if (auto data = ParseSingleAppZoneHistoryItem(json); data.has_value())
                    {
                        result.data.push_back(std::move(data.value()));
                    }
                }
                if (result.data.empty())
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

        static json::JsonObject ToJson(const AppZoneHistoryJSON& appZoneHistory)
        {
            json::JsonObject result{};

            result.SetNamedValue(NonLocalizable::AppZoneHistoryIds::AppPathID, json::value(appZoneHistory.appPath));

            json::JsonArray appHistoryArray;
            for (const auto& data : appZoneHistory.data)
            {
                json::JsonObject desktopData;
                json::JsonArray jsonIndexSet;
                for (ZoneIndex index : data.zoneIndexSet)
                {
                    jsonIndexSet.Append(json::value(static_cast<int>(index)));
                }

                json::JsonObject device{};
                device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::MonitorID, json::value(data.workAreaId.monitorId.deviceId.id));
                device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::MonitorInstanceID, json::value(data.workAreaId.monitorId.deviceId.instanceId));
                device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::MonitorSerialNumberID, json::value(data.workAreaId.monitorId.serialNumber));
                device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::MonitorNumberID, json::value(data.workAreaId.monitorId.deviceId.number));

                auto virtualDesktopStr = FancyZonesUtils::GuidToString(data.workAreaId.virtualDesktopId);
                if (virtualDesktopStr)
                {
                    device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::VirtualDesktopID, json::value(virtualDesktopStr.value()));
                }

                desktopData.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIndexesID, jsonIndexSet);
                desktopData.SetNamedValue(NonLocalizable::AppZoneHistoryIds::DeviceID, device);
                auto layoutIdStr = FancyZonesUtils::GuidToString(data.layoutId);
                if (layoutIdStr)
                {
                    desktopData.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIdID, json::value(layoutIdStr.value()));
                }

                appHistoryArray.Append(desktopData);
            }

            result.SetNamedValue(NonLocalizable::AppZoneHistoryIds::HistoryID, appHistoryArray);

            return result;
        }
    };

    AppZoneHistory::TAppZoneHistoryMap ParseAppZoneHistory(const json::JsonObject& fancyZonesDataJSON)
    {
        try
        {
            AppZoneHistory::TAppZoneHistoryMap appZoneHistoryMap{};
            auto appLastZones = fancyZonesDataJSON.GetNamedArray(NonLocalizable::AppZoneHistoryIds::AppZoneHistoryID);

            for (uint32_t i = 0; i < appLastZones.Size(); ++i)
            {
                json::JsonObject appLastZone = appLastZones.GetObjectAt(i);
                if (auto appZoneHistory = AppZoneHistoryJSON::FromJson(appLastZone); appZoneHistory.has_value())
                {
                    appZoneHistoryMap[appZoneHistory->appPath] = std::move(appZoneHistory->data);
                }
            }

            return std::move(appZoneHistoryMap);
        }
        catch (const winrt::hresult_error&)
        {
            return {};
        }
    }

    json::JsonObject SerializeJson(const AppZoneHistory::TAppZoneHistoryMap& map)
    {
        json::JsonObject root{};
        json::JsonArray appHistoryArray{};

        for (const auto& [appPath, appZoneHistoryData] : map)
        {
            appHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, appZoneHistoryData }));
        }

        root.SetNamedValue(NonLocalizable::AppZoneHistoryIds::AppZoneHistoryID, appHistoryArray);
        return root;
    }
}


AppZoneHistory::AppZoneHistory()
{
}

AppZoneHistory& AppZoneHistory::instance()
{
    static AppZoneHistory self;
    return self;
}

void AppZoneHistory::LoadData()
{
    auto file = AppZoneHistoryFileName();
    auto data = json::from_file(file);

    try
    {
        if (data)
        {
            m_history = JsonUtils::ParseAppZoneHistory(data.value());
        }
        else
        {
            m_history.clear();
            Logger::error(L"app-zone-history.json file is missing or malformed");
        }
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error(L"Parsing app-zone-history error: {}", e.message());
    }
}

void AppZoneHistory::SaveData()
{
    json::to_file(AppZoneHistoryFileName(), JsonUtils::SerializeJson(m_history));
}

void AppZoneHistory::AdjustWorkAreaIds(const std::vector<FancyZonesDataTypes::MonitorId>& ids)
{
    bool dirtyFlag = false;

    for (auto& [app, data] : m_history)
    {
        for (auto& dataIter : data)
        {
            auto& dataMonitorId = dataIter.workAreaId.monitorId;
            bool serialNumberNotSet = dataMonitorId.serialNumber.empty() && !dataMonitorId.deviceId.isDefault();
            bool monitorNumberNotSet = dataMonitorId.deviceId.number == 0;
            if (serialNumberNotSet || monitorNumberNotSet)
            {
                for (const auto& monitorId : ids)
                {
                    if (dataMonitorId.deviceId.id == monitorId.deviceId.id && dataMonitorId.deviceId.instanceId == monitorId.deviceId.instanceId)
                    {
                        dataMonitorId.serialNumber = monitorId.serialNumber;
                        dataMonitorId.deviceId.number = monitorId.deviceId.number;
                        dirtyFlag = true;
                        break;
                    }
                }
            }
        }
    }

    if (dirtyFlag)
    {
        SaveData();
    }
}

bool AppZoneHistory::SetAppLastZones(HWND window, const FancyZonesDataTypes::WorkAreaId& workAreaId, const GUID& layoutId, const ZoneIndexSet& zoneIndexSet)
{
    if (IsAnotherWindowOfApplicationInstanceZoned(window, workAreaId))
    {
        return false;
    }

    auto processPath = get_process_path_waiting_uwp(window);
    if (processPath.empty())
    {
        return false;
    }

    auto layoutIdStr = FancyZonesUtils::GuidToString(layoutId);
    if (layoutIdStr)
    {
        Logger::info(L"Add app zone history, device: {}, layout: {}", workAreaId.toString(), layoutIdStr.value());
    }
    
    DWORD processId = 0;
    GetWindowThreadProcessId(window, &processId);

    auto history = m_history.find(processPath);
    if (history != std::end(m_history))
    {
        auto& perDesktopData = history->second;
        for (auto& data : perDesktopData)
        {
            if (data.workAreaId == workAreaId)
            {
                // application already has history on this work area, update it with new window position
                data.processIdToHandleMap[processId] = window;
                data.layoutId = layoutId;
                data.zoneIndexSet = zoneIndexSet;
                SaveData();
                return true;
            }
        }
    }

    std::unordered_map<DWORD, HWND> processIdToHandleMap{};
    processIdToHandleMap[processId] = window;
    FancyZonesDataTypes::AppZoneHistoryData data{ .processIdToHandleMap = processIdToHandleMap,
                                                  .layoutId = layoutId,
                                                  .workAreaId = workAreaId,
                                                  .zoneIndexSet = zoneIndexSet };

    if (m_history.contains(processPath))
    {
        // application already has history but on other desktop, add with new desktop info
        m_history[processPath].push_back(data);
    }
    else
    {
        // new application, create entry in app zone history map
        m_history[processPath] = std::vector<FancyZonesDataTypes::AppZoneHistoryData>{ data };
    }

    SaveData();
    return true;
}

bool AppZoneHistory::RemoveAppLastZone(HWND window, const FancyZonesDataTypes::WorkAreaId& workAreaId, const GUID& layoutId)
{
    auto processPath = get_process_path_waiting_uwp(window);
    if (processPath.empty())
    {
        return false;
    }

    auto history = m_history.find(processPath);
    if (history == std::end(m_history))
    {
        return false;
    }

    auto layoutIdStrOpt = FancyZonesUtils::GuidToString(layoutId);
    if (!layoutIdStrOpt)
    {
        Logger::error("Invalid layout id");
        return false;
    }

    Logger::info(L"Remove app zone history, device: {}, layout: {}", workAreaId.toString(), layoutIdStrOpt.value());

    auto& perDesktopData = history->second;
    for (auto data = std::begin(perDesktopData); data != std::end(perDesktopData);)
    {
        if (data->workAreaId == workAreaId && data->layoutId == layoutId)
        {
            if (!IsAnotherWindowOfApplicationInstanceZoned(window, workAreaId))
            {
                DWORD processId = 0;
                GetWindowThreadProcessId(window, &processId);

                data->processIdToHandleMap.erase(processId);
            }

            // if there is another instance of same application placed in the same zone don't erase history
            auto windowZoneStamps = FancyZonesWindowProperties::RetrieveZoneIndexProperty(window);
            for (auto placedWindow : data->processIdToHandleMap)
            {
                auto placedWindowZoneStamps = FancyZonesWindowProperties::RetrieveZoneIndexProperty(placedWindow.second);
                if (IsWindow(placedWindow.second) && (windowZoneStamps == placedWindowZoneStamps))
                {
                    return false;
                }
            }

            data = perDesktopData.erase(data);
            if (perDesktopData.empty())
            {
                m_history.erase(processPath);
            }
            SaveData();
            return true;
        }
        else
        {
            ++data;
        }
    }
    return false;
}

void AppZoneHistory::RemoveApp(const std::wstring& appPath)
{
    m_history.erase(appPath);
}

const AppZoneHistory::TAppZoneHistoryMap& AppZoneHistory::GetFullAppZoneHistory() const noexcept
{
    return m_history;
}

std::optional<FancyZonesDataTypes::AppZoneHistoryData> AppZoneHistory::GetZoneHistory(const std::wstring& appPath, const FancyZonesDataTypes::WorkAreaId& workAreaId) const noexcept
{
    auto app = appPath;
    auto pos = appPath.find_last_of('\\');
    if (pos != std::string::npos && pos + 1 < appPath.length())
    {
        app = appPath.substr(pos + 1);
    }

    auto srcVirtualDesktopIDStr = FancyZonesUtils::GuidToString(workAreaId.virtualDesktopId);
    if (srcVirtualDesktopIDStr)
    {
        Logger::debug(L"Get {} zone history on monitor: {}, virtual desktop: {}", app, workAreaId.toString(), srcVirtualDesktopIDStr.value());
    }

    auto iter = m_history.find(appPath);
    if (iter == m_history.end())
    {
        Logger::info("App history not found");
        return std::nullopt;
    }

    auto historyVector = iter->second;
    for (const auto& history : historyVector)
    {
        if (history.workAreaId == workAreaId)
        {
            auto vdStr = FancyZonesUtils::GuidToString(history.workAreaId.virtualDesktopId);
            if (vdStr)
            {
                Logger::debug(L"App zone history found on the device {} with virtual desktop {}", history.workAreaId.toString(), vdStr.value());
            }

            if (history.workAreaId.virtualDesktopId == workAreaId.virtualDesktopId || history.workAreaId.virtualDesktopId == GUID_NULL)
            {
                return history;
            }
        }
    }

    return std::nullopt;
}

bool AppZoneHistory::IsAnotherWindowOfApplicationInstanceZoned(HWND window, const FancyZonesDataTypes::WorkAreaId& workAreaId) const noexcept
{
    auto processPath = get_process_path_waiting_uwp(window);
    if (!processPath.empty())
    {
        auto history = m_history.find(processPath);
        if (history != std::end(m_history))
        {
            auto& perDesktopData = history->second;
            for (auto& data : perDesktopData)
            {
                if (data.workAreaId == workAreaId)
                {
                    DWORD processId = 0;
                    GetWindowThreadProcessId(window, &processId);

                    auto processIdIt = data.processIdToHandleMap.find(processId);

                    if (processIdIt == std::end(data.processIdToHandleMap))
                    {
                        return false;
                    }
                    else if (processIdIt->second != window && IsWindow(processIdIt->second))
                    {
                        return true;
                    }
                }
            }
        }
    }

    return false;
}

ZoneIndexSet AppZoneHistory::GetAppLastZoneIndexSet(HWND window, const FancyZonesDataTypes::WorkAreaId& workAreaId, const GUID& layoutId) const
{
    auto processPath = get_process_path_waiting_uwp(window);
    if (processPath.empty())
    {
        Logger::error("Process path is empty");
        return {};
    }

    auto app = processPath;
    auto pos = processPath.find_last_of('\\');
    if (pos != std::string::npos && pos + 1 < processPath.length())
    {
        app = processPath.substr(pos + 1);
    }

    Logger::info(L"Get {} zone history on work area: {}", app, workAreaId.toString());

    auto history = m_history.find(processPath);
    if (history == std::end(m_history))
    {
        return {};
    }

    const auto& perDesktopData = history->second;
    for (const auto& data : perDesktopData)
    {
        if (data.layoutId == layoutId && data.workAreaId == workAreaId)
        {
            if (data.workAreaId.virtualDesktopId == workAreaId.virtualDesktopId || data.workAreaId.virtualDesktopId == GUID_NULL)
            {
                Logger::info(L"App zone history found on the work area {}", data.workAreaId.toString());
                return data.zoneIndexSet;
            }
        }
    }
    
    return {};
}

void AppZoneHistory::SyncVirtualDesktops(const GUID& currentVirtualDesktop, const GUID& lastUsedVirtualDesktop, std::optional<std::vector<GUID>> desktops)
{
    TAppZoneHistoryMap history;

    std::unordered_set<GUID> activeDesktops{};
    if (desktops.has_value())
    {
        activeDesktops = std::unordered_set<GUID>(std::begin(desktops.value()), std::end(desktops.value()));
    }

    auto findCurrentVirtualDesktopInSavedHistory = [&](const std::pair<std::wstring, std::vector<FancyZonesDataTypes::AppZoneHistoryData>>& val) -> bool 
    { 
        for (auto& data : val.second)
        {
            if (data.workAreaId.virtualDesktopId == currentVirtualDesktop)
            {
                return true;
            }
        }
        return false;
    };
    bool replaceLastUsedWithCurrent = !desktops.has_value() || currentVirtualDesktop == GUID_NULL || lastUsedVirtualDesktop == GUID_NULL || std::find_if(m_history.begin(), m_history.end(), findCurrentVirtualDesktopInSavedHistory) == m_history.end();

    bool dirtyFlag = false;
    for (auto it = std::begin(m_history); it != std::end(m_history);)
    {
        auto& perDesktopData = it->second;
        for (auto desktopIt = std::begin(perDesktopData); desktopIt != std::end(perDesktopData);)
        {
            if (replaceLastUsedWithCurrent && desktopIt->workAreaId.virtualDesktopId == lastUsedVirtualDesktop)
            {
                desktopIt->workAreaId.virtualDesktopId = currentVirtualDesktop;
                dirtyFlag = true;
            }

            if (desktopIt->workAreaId.virtualDesktopId != currentVirtualDesktop && !activeDesktops.contains(desktopIt->workAreaId.virtualDesktopId))
            {
                desktopIt = perDesktopData.erase(desktopIt);
                dirtyFlag = true;
            }
            else
            {
                ++desktopIt;
            }
        }

        if (perDesktopData.empty())
        {
            it = m_history.erase(it);
            dirtyFlag = true;
        }
        else
        {
            ++it;
        }
    }

    if (dirtyFlag)
    {
        SaveData();
    }
}
