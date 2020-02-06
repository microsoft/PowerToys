#pragma once

#include <common/settings_helpers.h>
#include <common/json.h>

#include <string>
#include <strsafe.h>
#include <unordered_map>
#include <variant>
#include <optional>
#include <vector>
#include <winnt.h>

namespace JSONHelpers
{
    constexpr int MAX_ZONE_COUNT = 50;

    enum class ZoneSetLayoutType : int
    {
        Focus = 0,
        Columns,
        Rows,
        Grid,
        PriorityGrid,
        Custom
    };

    enum class CustomLayoutType : int
    {
        Grid = 0,
        Canvas
    };

    std::wstring TypeToString(ZoneSetLayoutType type);
    ZoneSetLayoutType TypeFromString(const std::wstring& typeStr);

    ZoneSetLayoutType TypeFromLayoutId(int layoutID);

    struct CanvasLayoutInfo
    {
        int referenceWidth;
        int referenceHeight;
        struct Rect
        {
            int x;
            int y;
            int width;
            int height;
        };
        std::vector<CanvasLayoutInfo::Rect> zones;

        static json::JsonObject ToJson(const CanvasLayoutInfo& canvasInfo);
        static std::optional<CanvasLayoutInfo> FromJson(const json::JsonObject& infoJson);
    };

    class GridLayoutInfo
    {
    public:
        struct Minimal
        {
            int rows;
            int columns;
        };

        struct Full
        {
            int rows;
            int columns;
            const std::vector<int>& rowsPercents;
            const std::vector<int>& columnsPercents;
            const std::vector<std::vector<int>>& cellChildMap;
        };

        GridLayoutInfo(const Minimal& info);
        GridLayoutInfo(const Full& info);
        ~GridLayoutInfo() = default;

        static json::JsonObject ToJson(const GridLayoutInfo& gridInfo);
        static std::optional<GridLayoutInfo> FromJson(const json::JsonObject& infoJson);

        inline std::vector<int>& rowsPercents() { return m_rowsPercents; };
        inline std::vector<int>& columnsPercents() { return m_columnsPercents; };
        inline std::vector<std::vector<int>>& cellChildMap() { return m_cellChildMap; };

        inline int rows() const { return m_rows; }
        inline int columns() const { return m_columns; }

        inline const std::vector<int>& rowsPercents() const { return m_rowsPercents; };
        inline const std::vector<int>& columnsPercents() const { return m_columnsPercents; };
        inline const std::vector<std::vector<int>>& cellChildMap() const { return m_cellChildMap; };

    private:
        int m_rows;
        int m_columns;
        std::vector<int> m_rowsPercents;
        std::vector<int> m_columnsPercents;
        std::vector<std::vector<int>> m_cellChildMap;
    };

    struct CustomZoneSetData
    {
        std::wstring name;
        CustomLayoutType type;
        std::variant<CanvasLayoutInfo, GridLayoutInfo> info;
    };

    struct CustomZoneSetJSON
    {
        std::wstring uuid;
        CustomZoneSetData data;

        static json::JsonObject ToJson(const CustomZoneSetJSON& device);
        static std::optional<CustomZoneSetJSON> FromJson(const json::JsonObject& customZoneSet);
    };

    // TODO(stefan): This needs to be moved to ZoneSet.h (probably)
    struct ZoneSetData
    {
        std::wstring uuid;
        ZoneSetLayoutType type;

        static json::JsonObject ToJson(const ZoneSetData& zoneSet);
        static std::optional<ZoneSetData> FromJson(const json::JsonObject& zoneSet);
    };

    struct AppZoneHistoryData
    {
        std::wstring zoneSetUuid;
        std::wstring deviceId;
        int zoneIndex;
    };

    struct AppZoneHistoryJSON
    {
        std::wstring appPath;
        AppZoneHistoryData data;

        static json::JsonObject ToJson(const AppZoneHistoryJSON& appZoneHistory);
        static std::optional<AppZoneHistoryJSON> FromJson(const json::JsonObject& zoneSet);
    };

    struct DeviceInfoData
    {
        ZoneSetData activeZoneSet;
        bool showSpacing;
        int spacing;
        int zoneCount;
    };

    struct DeviceInfoJSON
    {
        std::wstring deviceId;
        DeviceInfoData data;

        static json::JsonObject ToJson(const DeviceInfoJSON& device);
        static std::optional<DeviceInfoJSON> FromJson(const json::JsonObject& device);
    };

    class FancyZonesData
    {
    public:
        FancyZonesData();

        const std::wstring& GetPersistFancyZonesJSONPath() const;
        json::JsonObject GetPersistFancyZonesJSON();

        inline const std::unordered_map<std::wstring, DeviceInfoData>& GetDeviceInfoMap() const
        {
            return deviceInfoMap;
        }

        inline const std::unordered_map<std::wstring, CustomZoneSetData>& GetCustomZoneSetsMap() const
        {
            return customZoneSetsMap;
        }

        inline const std::unordered_map<std::wstring, AppZoneHistoryData>& GetAppZoneHistoryMap() const
        {
            return appZoneHistoryMap;
        }

        inline const std::wstring GetActiveDeviceId() const
        {
            return activeDeviceId;
        }

        void SetActiveDeviceId(const std::wstring& deviceId)
        {
            activeDeviceId = deviceId;
        }

        inline bool DeleteTmpFile(std::wstring_view tmpFilePath) const
        {
            return DeleteFileW(tmpFilePath.data());
        }

        void AddDevice(const std::wstring& deviceId);

        int GetAppLastZoneIndex(HWND window, const std::wstring_view& deviceId, const std::wstring_view& zoneSetId) const;
        bool RemoveAppLastZone(HWND window, const std::wstring_view& deviceId, const std::wstring_view& zoneSetId);
        bool SetAppLastZone(HWND window, const std::wstring& deviceId, const std::wstring& zoneSetId, int zoneIndex);

        void SetActiveZoneSet(const std::wstring& deviceId, const std::wstring& uuid);

        void SerializeDeviceInfoToTmpFile(const DeviceInfoJSON& deviceInfo, std::wstring_view tmpFilePath) const;

        void ParseDeviceInfoFromTmpFile(std::wstring_view tmpFilePath);
        bool ParseCustomZoneSetFromTmpFile(std::wstring_view tmpFilePath, const std::wstring& deviceId);
        bool ParseDeletedCustomZoneSetsFromTmpFile(std::wstring_view tmpFilePath);

        bool ParseAppZoneHistory(const json::JsonObject& fancyZonesDataJSON);
        json::JsonArray SerializeAppZoneHistory() const;
        bool ParseDeviceInfos(const json::JsonObject& fancyZonesDataJSON);
        json::JsonArray SerializeDeviceInfos() const;
        bool ParseCustomZoneSets(const json::JsonObject& fancyZonesDataJSON);
        json::JsonArray SerializeCustomZoneSets() const;
        void CustomZoneSetsToJsonFile(std::wstring_view filePath) const;

        void LoadFancyZonesData();
        void SaveFancyZonesData() const;

        void MigrateDeviceInfoFromRegistry(const std::wstring& deviceId);

    private:
        void TmpMigrateAppliedZoneSetsFromRegistry();
        void MigrateCustomZoneSetsFromRegistry();

        std::unordered_map<std::wstring, ZoneSetData> appliedZoneSetsMap{};
        std::unordered_map<std::wstring, AppZoneHistoryData> appZoneHistoryMap{};
        std::unordered_map<std::wstring, DeviceInfoData> deviceInfoMap{};
        std::unordered_map<std::wstring, CustomZoneSetData> customZoneSetsMap{};

        std::wstring activeDeviceId;
        std::wstring jsonFilePath;
    };

    FancyZonesData& FancyZonesDataInstance();
}
