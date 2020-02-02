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

    using TZoneCount = int;
    using TZoneSetUUID = std::wstring;
    using TAppPath = std::wstring;
    using TDeviceID = std::wstring;

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
        TZoneSetUUID uuid;
        CustomZoneSetData data;

        static json::JsonObject ToJson(const CustomZoneSetJSON& device);
        static std::optional<CustomZoneSetJSON> FromJson(const json::JsonObject& customZoneSet);
    };

    // TODO(stefan): This needs to be moved to ZoneSet.h (probably)
    struct ZoneSetData
    {
        TZoneSetUUID uuid;
        ZoneSetLayoutType type;
        std::optional<int> zoneCount;

        static json::JsonObject ToJson(const ZoneSetData& zoneSet);
        static std::optional<ZoneSetData> FromJson(const json::JsonObject& zoneSet);
    };

    struct AppZoneHistoryData
    {
        TZoneSetUUID zoneSetUuid; //TODO(stefan): is this nessecary? It doesn't exist with registry impl.
        int zoneIndex;
        //TODO(stefan): Also, do we need DeviceID here? Do we want to support that - app history per monitor?
    };

    struct AppZoneHistoryJSON
    {
        TAppPath appPath;
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
        TDeviceID deviceId;
        DeviceInfoData data;

        static json::JsonObject ToJson(const DeviceInfoJSON& device);
        static std::optional<DeviceInfoJSON> FromJson(const json::JsonObject& device);
    };

    using TDeviceInfosMap = std::unordered_map<TZoneSetUUID, DeviceInfoData>;
    using TCustomZoneSetsMap = std::unordered_map<TZoneSetUUID, CustomZoneSetData>;
    using TAppliedZoneSetsMap = std::unordered_map<TZoneSetUUID, ZoneSetData>;
    using TAppZoneHistoryMap = std::unordered_map<TAppPath, AppZoneHistoryData>;

    static const std::wstring FANCY_ZONES_DATA_FILE = L"PersistFancyZones.json";

    class FancyZonesData
    {
    public:
        FancyZonesData();

        const std::wstring& GetPersistFancyZonesJSONPath() const;
        json::JsonObject GetPersistFancyZonesJSON();

        TDeviceInfosMap& GetDeviceInfoMap()
        {
            return deviceInfoMap;
        }

        inline const TCustomZoneSetsMap& GetCustomZoneSetsMap() const
        {
            return customZoneSetsMap;
        }

        inline const TAppZoneHistoryMap& GetAppZoneHistoryMap() const
        {
            return appZoneHistoryMap;
        }

        inline const TDeviceID GetActiveDeviceId() const
        {
            return activeDeviceId;
        }

        void SetActiveDeviceId(TDeviceID deviceId)
        {
            activeDeviceId = deviceId;
        }

        inline bool DeleteTmpFile(std::wstring_view tmpFilePath) const
        {
            return DeleteFileW(tmpFilePath.data());
        }

        int GetAppLastZone(HWND window, PCWSTR appPath) const;
        bool SetAppLastZone(HWND window, PCWSTR appPath, DWORD zoneIndex); //TODO(stefan): Missing zone uuid (pass as arg)

        void SetActiveZoneSet(const TDeviceID& deviceId, const TZoneSetUUID& uuid);

        void SerializeDeviceInfoToTmpFile(const DeviceInfoJSON& deviceInfo, std::wstring_view tmpFilePath) const;
        void ParseDeviceInfoFromTmpFile(std::wstring_view tmpFilePath);

        bool ParseCustomZoneSetFromTmpFile(std::wstring_view tmpFilePath, const TZoneSetUUID& uuid);
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

        void MigrateDeviceInfoFromRegistry(const TDeviceID& deviceId);

    private:
        void TmpMigrateAppliedZoneSetsFromRegistry();
        void MigrateAppZoneHistoryFromRegistry(); //TODO(stefan): If uuid is needed here, it needs to be resolved here some how
        void MigrateCustomZoneSetsFromRegistry();

        TAppliedZoneSetsMap appliedZoneSetsMap{};
        TAppZoneHistoryMap appZoneHistoryMap{};
        TDeviceInfosMap deviceInfoMap{};
        TCustomZoneSetsMap customZoneSetsMap{};

        TDeviceID activeDeviceId;
        std::wstring jsonFilePath;
    };

    FancyZonesData& FancyZonesDataInstance();
}
