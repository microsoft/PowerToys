#include "../pch.h"
#include "AppZoneHistory.h"

#include <common/logger/call_tracer.h>
#include <common/logger/logger.h>
#include <common/utils/process_path.h>

#include <FancyZonesLib/GuidUtils.h>
#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/JsonHelpers.h>
#include <FancyZonesLib/util.h>

AppZoneHistory::AppZoneHistory()
{
}

AppZoneHistory& AppZoneHistory::instance()
{
    static AppZoneHistory self;
    return self;
}

void AppZoneHistory::SetVirtualDesktopCheckCallback(std::function<bool(GUID)> callback)
{
    m_virtualDesktopCheckCallback = callback;
}

void AppZoneHistory::LoadData()
{
    auto file = AppZoneHistoryFileName();
    auto data = json::from_file(file);

    try
    {
        if (data)
        {
            m_history = JSONHelpers::ParseAppZoneHistory(data.value());
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
    _TRACER_;

    bool dirtyFlag = false;
    std::unordered_map<std::wstring, std::vector<FancyZonesDataTypes::AppZoneHistoryData>> updatedHistory;
    if (m_virtualDesktopCheckCallback)
    {
        for (const auto& [path, dataVector] : m_history)
        {
            auto updatedVector = dataVector;
            for (auto& data : updatedVector)
            {
                if (!m_virtualDesktopCheckCallback(data.deviceId.virtualDesktopId))
                {
                    data.deviceId.virtualDesktopId = GUID_NULL;
                    dirtyFlag = true;
                }
            }

            updatedHistory.insert(std::make_pair(path, updatedVector));
        }
    }

    if (dirtyFlag)
    {
        JSONHelpers::SaveAppZoneHistory(AppZoneHistoryFileName(), updatedHistory);
    }
    else
    {
        JSONHelpers::SaveAppZoneHistory(AppZoneHistoryFileName(), m_history);
    }
}

bool AppZoneHistory::SetAppLastZones(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId, const std::wstring& zoneSetId, const ZoneIndexSet& zoneIndexSet)
{
    _TRACER_;

    if (IsAnotherWindowOfApplicationInstanceZoned(window, deviceId))
    {
        return false;
    }

    auto processPath = get_process_path(window);
    if (processPath.empty())
    {
        return false;
    }

    DWORD processId = 0;
    GetWindowThreadProcessId(window, &processId);

    auto history = m_history.find(processPath);
    if (history != std::end(m_history))
    {
        auto& perDesktopData = history->second;
        for (auto& data : perDesktopData)
        {
            if (data.deviceId == deviceId)
            {
                // application already has history on this work area, update it with new window position
                data.processIdToHandleMap[processId] = window;
                data.zoneSetUuid = zoneSetId;
                data.zoneIndexSet = zoneIndexSet;
                SaveData();
                return true;
            }
        }
    }

    std::unordered_map<DWORD, HWND> processIdToHandleMap{};
    processIdToHandleMap[processId] = window;
    FancyZonesDataTypes::AppZoneHistoryData data{ .processIdToHandleMap = processIdToHandleMap,
                                                  .zoneSetUuid = zoneSetId,
                                                  .deviceId = deviceId,
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

bool AppZoneHistory::RemoveAppLastZone(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId, const std::wstring_view& zoneSetId)
{
    _TRACER_;

    auto processPath = get_process_path(window);
    if (!processPath.empty())
    {
        auto history = m_history.find(processPath);
        if (history != std::end(m_history))
        {
            auto& perDesktopData = history->second;
            for (auto data = std::begin(perDesktopData); data != std::end(perDesktopData);)
            {
                if (data->deviceId == deviceId && data->zoneSetUuid == zoneSetId)
                {
                    if (!IsAnotherWindowOfApplicationInstanceZoned(window, deviceId))
                    {
                        DWORD processId = 0;
                        GetWindowThreadProcessId(window, &processId);

                        data->processIdToHandleMap.erase(processId);
                    }

                    // if there is another instance of same application placed in the same zone don't erase history
                    ZoneIndex windowZoneStamp = reinterpret_cast<ZoneIndex>(::GetProp(window, ZonedWindowProperties::PropertyMultipleZoneID));
                    for (auto placedWindow : data->processIdToHandleMap)
                    {
                        ZoneIndex placedWindowZoneStamp = reinterpret_cast<ZoneIndex>(::GetProp(placedWindow.second, ZonedWindowProperties::PropertyMultipleZoneID));
                        if (IsWindow(placedWindow.second) && (windowZoneStamp == placedWindowZoneStamp))
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

std::optional<FancyZonesDataTypes::AppZoneHistoryData> AppZoneHistory::GetZoneHistory(const std::wstring& appPath, const FancyZonesDataTypes::DeviceIdData& deviceId) const noexcept
{
    auto iter = m_history.find(appPath);
    if (iter != m_history.end())
    {
        auto historyVector = iter->second;
        for (const auto& history : historyVector)
        {
            if (history.deviceId == deviceId)
            {
                return history;
            }
        }
    }
    
    return std::nullopt;
}

bool AppZoneHistory::IsAnotherWindowOfApplicationInstanceZoned(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId) const noexcept
{
    auto processPath = get_process_path(window);
    if (!processPath.empty())
    {
        auto history = m_history.find(processPath);
        if (history != std::end(m_history))
        {
            auto& perDesktopData = history->second;
            for (auto& data : perDesktopData)
            {
                if (data.deviceId == deviceId)
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

void AppZoneHistory::UpdateProcessIdToHandleMap(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId)
{
    auto processPath = get_process_path(window);
    if (!processPath.empty())
    {
        auto history = m_history.find(processPath);
        if (history != std::end(m_history))
        {
            auto& perDesktopData = history->second;
            for (auto& data : perDesktopData)
            {
                if (data.deviceId == deviceId)
                {
                    DWORD processId = 0;
                    GetWindowThreadProcessId(window, &processId);
                    data.processIdToHandleMap[processId] = window;
                    break;
                }
            }
        }
    }
}

ZoneIndexSet AppZoneHistory::GetAppLastZoneIndexSet(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId, const std::wstring_view& zoneSetId) const
{
    auto processPath = get_process_path(window);
    if (!processPath.empty())
    {
        auto history = m_history.find(processPath);
        if (history != std::end(m_history))
        {
            const auto& perDesktopData = history->second;
            for (const auto& data : perDesktopData)
            {
                if (data.zoneSetUuid == zoneSetId && data.deviceId == deviceId)
                {
                    return data.zoneIndexSet;
                }
            }
        }
    }

    return {};
}

void AppZoneHistory::SyncVirtualDesktops(GUID currentVirtualDesktopId)
{
    _TRACER_;
    // Explorer persists current virtual desktop identifier to registry on a per session basis,
    // but only after first virtual desktop switch happens. If the user hasn't switched virtual
    // desktops in this session value in registry will be empty and we will use default GUID in
    // that case (00000000-0000-0000-0000-000000000000).
    
    bool dirtyFlag = false;

    for (auto& [path, perDesktopData] : m_history)
    {
        for (auto& data : perDesktopData)
        {
            if (data.deviceId.virtualDesktopId == GUID_NULL)
            {
                data.deviceId.virtualDesktopId = currentVirtualDesktopId;
                dirtyFlag = true;
            }
            else
            {
                if (m_virtualDesktopCheckCallback && !m_virtualDesktopCheckCallback(data.deviceId.virtualDesktopId))
                {
                    data.deviceId.virtualDesktopId = GUID_NULL;
                    dirtyFlag = true;
                }
            }
        }
    }

    if (dirtyFlag)
    {
        wil::unique_cotaskmem_string virtualDesktopIdStr;
        if (SUCCEEDED(StringFromCLSID(currentVirtualDesktopId, &virtualDesktopIdStr)))
        {
            Logger::info(L"Update Virtual Desktop id to {}", virtualDesktopIdStr.get());
        }

        SaveData();
    }
}

void AppZoneHistory::RemoveDeletedVirtualDesktops(const std::vector<GUID>& activeDesktops)
{
    std::unordered_set<GUID> active(std::begin(activeDesktops), std::end(activeDesktops));
    bool dirtyFlag = false;

    for (auto it = std::begin(m_history); it != std::end(m_history);)
    {
        auto& perDesktopData = it->second;
        for (auto desktopIt = std::begin(perDesktopData); desktopIt != std::end(perDesktopData);)
        {
            if (desktopIt->deviceId.virtualDesktopId != GUID_NULL && !active.contains(desktopIt->deviceId.virtualDesktopId))
            {
                auto virtualDesktopIdStr = FancyZonesUtils::GuidToString(desktopIt->deviceId.virtualDesktopId);
                if (virtualDesktopIdStr)
                {
                    Logger::info(L"Remove Virtual Desktop id {} from app-zone-history", virtualDesktopIdStr.value());
                }

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
