#include "pch.h"
#include <filesystem>

#include <lib/JsonHelpers.h>
#include "util.h"

#include <CppUnitTestLogger.h>

using namespace JSONHelpers;
using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    void compareJsonObjects(const json::JsonObject& expected, const json::JsonObject& actual, bool recursive = true)
    {
        auto iter = expected.First();
        while (iter.HasCurrent())
        {
            const auto key = iter.Current().Key();
            Assert::IsTrue(actual.HasKey(key), key.c_str());

            const std::wstring expectedStringified = iter.Current().Value().Stringify().c_str();
            const std::wstring actualStringified = actual.GetNamedValue(key).Stringify().c_str();

            if (recursive)
            {
                json::JsonObject expectedJson;
                if (json::JsonObject::TryParse(expectedStringified, expectedJson))
                {
                    json::JsonObject actualJson;
                    if (json::JsonObject::TryParse(actualStringified, actualJson))
                    {
                        compareJsonObjects(expectedJson, actualJson, true);
                    }
                    else
                    {
                        Assert::IsTrue(false, key.c_str());
                    }
                }
                else
                {
                    Assert::AreEqual(expectedStringified, actualStringified, key.c_str());
                }
            }
            else
            {
                Assert::AreEqual(expectedStringified, actualStringified, key.c_str());
            }

            iter.MoveNext();
        }
    }

    TEST_CLASS(ZoneSetLayoutTypeUnitTest)
    {
        TEST_METHOD(ZoneSetLayoutTypeToString){
            std::map<int, std::wstring> expectedMap = {
                std::make_pair(-1, L"TypeToString_ERROR"),
                std::make_pair(0, L"focus"),
                std::make_pair(1, L"columns"),
                std::make_pair(2, L"rows"),
                std::make_pair(3, L"grid"),
                std::make_pair(4, L"priority-grid"),
                std::make_pair(5, L"custom"),
                std::make_pair(6, L"TypeToString_ERROR"),
            };

            for (const auto& expected : expectedMap)
            {
                auto actual = JSONHelpers::TypeToString(static_cast<ZoneSetLayoutType>(expected.first));
                Assert::AreEqual(expected.second, actual);
            }
        }

        TEST_METHOD(ZoneSetLayoutTypeFromString)
        {
            std::map<ZoneSetLayoutType, std::wstring> expectedMap = {
                std::make_pair(ZoneSetLayoutType::Focus, L"focus"),
                std::make_pair(ZoneSetLayoutType::Columns, L"columns"),
                std::make_pair(ZoneSetLayoutType::Rows, L"rows"),
                std::make_pair(ZoneSetLayoutType::Grid, L"grid"),
                std::make_pair(ZoneSetLayoutType::PriorityGrid, L"priority-grid"),
                std::make_pair(ZoneSetLayoutType::Custom, L"custom"),
            };

            for (const auto& expected : expectedMap)
            {
                auto actual = JSONHelpers::TypeFromString(expected.second);
                Assert::AreEqual(static_cast<int>(expected.first), static_cast<int>(actual));
            }
        }

        TEST_METHOD(ZoneSetLayoutTypeFromLayoutId)
        {
            std::map<ZoneSetLayoutType, int> expectedMap = {
                std::make_pair(ZoneSetLayoutType::Focus, 0xFFFF),
                std::make_pair(ZoneSetLayoutType::Columns, 0xFFFD),
                std::make_pair(ZoneSetLayoutType::Rows, 0xFFFE),
                std::make_pair(ZoneSetLayoutType::Grid, 0xFFFC),
                std::make_pair(ZoneSetLayoutType::PriorityGrid, 0xFFFB),
                std::make_pair(ZoneSetLayoutType::Custom, 0xFFFA),
                std::make_pair(ZoneSetLayoutType::Custom, 0),
                std::make_pair(ZoneSetLayoutType::Custom, -1),
            };

            for (const auto& expected : expectedMap)
            {
                auto actual = JSONHelpers::TypeFromLayoutId(expected.second);
                Assert::AreEqual(static_cast<int>(expected.first), static_cast<int>(actual));
            }
        }
    };

    TEST_CLASS(CanvasLayoutInfoUnitTests)
    {
        json::JsonObject m_json = json::JsonObject::Parse(L"{\"ref-width\": 123, \"ref-height\": 321, \"zones\": [{\"X\": 11, \"Y\": 22, \"width\": 33, \"height\": 44}, {\"X\": 55, \"Y\": 66, \"width\": 77, \"height\": 88}]}");

        TEST_METHOD(ToJson)
        {
            CanvasLayoutInfo info;
            info.referenceWidth = 123;
            info.referenceHeight = 321;
            info.zones = { CanvasLayoutInfo::Rect{ 11, 22, 33, 44 }, CanvasLayoutInfo::Rect{ 55, 66, 77, 88 } };

            auto actual = CanvasLayoutInfo::ToJson(info);
            compareJsonObjects(m_json, actual);
        }

        TEST_METHOD(FromJson)
        {
            CanvasLayoutInfo expected;
            expected.referenceWidth = 123;
            expected.referenceHeight = 321;
            expected.zones = { CanvasLayoutInfo::Rect{ 11, 22, 33, 44 }, CanvasLayoutInfo::Rect{ 55, 66, 77, 88 } };

            auto actual = CanvasLayoutInfo::FromJson(m_json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.referenceHeight, actual->referenceHeight);
            Assert::AreEqual(expected.referenceWidth, actual->referenceWidth);
            Assert::AreEqual(expected.zones.size(), actual->zones.size());
            for (int i = 0; i < expected.zones.size(); i++)
            {
                Assert::AreEqual(expected.zones[i].x, actual->zones[i].x);
                Assert::AreEqual(expected.zones[i].y, actual->zones[i].y);
                Assert::AreEqual(expected.zones[i].width, actual->zones[i].width);
                Assert::AreEqual(expected.zones[i].height, actual->zones[i].height);
            }
        }

        TEST_METHOD(FromJsonMissingKeys)
        {
            CanvasLayoutInfo info{ 123, 321, { CanvasLayoutInfo::Rect{ 11, 22, 33, 44 }, CanvasLayoutInfo::Rect{ 55, 66, 77, 88 } } };
            const auto json = CanvasLayoutInfo::ToJson(info);

            auto iter = json.First();
            while (iter.HasCurrent())
            {
                json::JsonObject modifiedJson = json::JsonObject::Parse(json.Stringify());
                modifiedJson.Remove(iter.Current().Key());

                auto actual = CanvasLayoutInfo::FromJson(modifiedJson);
                Assert::IsFalse(actual.has_value());

                iter.MoveNext();
            }
        }
    };

    TEST_CLASS(GridLayoutInfoUnitTests)
    {
    private:
        GridLayoutInfo m_info = GridLayoutInfo(GridLayoutInfo::Minimal{ .rows = 3, .columns = 4 });
        json::JsonObject m_gridJson = json::JsonObject();
        json::JsonArray m_rowsArray, m_columnsArray, m_cells;

        void compareSizes(int expectedRows, int expectedColumns, const GridLayoutInfo& actual)
        {
            Assert::AreEqual(expectedRows, actual.rows());
            Assert::AreEqual(expectedColumns, actual.columns());
            Assert::AreEqual((size_t)expectedRows, actual.rowsPercents().size());
            Assert::AreEqual((size_t)expectedColumns, actual.columnsPercents().size());
            Assert::AreEqual((size_t)expectedRows, actual.cellChildMap().size());

            for (int i = 0; i < expectedRows; i++)
            {
                Assert::AreEqual((size_t)expectedColumns, actual.cellChildMap()[i].size());
            }
        }

        void compareVectors(const std::vector<int>& expected, const std::vector<int>& actual)
        {
            Assert::AreEqual(expected.size(), actual.size());
            for (int i = 0; i < expected.size(); i++)
            {
                Assert::AreEqual(expected[i], actual[i]);
            }
        }

        void compareGridInfos(const GridLayoutInfo& expected, const GridLayoutInfo& actual)
        {
            compareSizes(expected.rows(), expected.columns(), actual);

            compareVectors(expected.rowsPercents(), actual.rowsPercents());
            compareVectors(expected.columnsPercents(), actual.columnsPercents());
            for (int i = 0; i < expected.cellChildMap().size(); i++)
            {
                compareVectors(expected.cellChildMap()[i], actual.cellChildMap()[i]);
            }
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            m_info = GridLayoutInfo(GridLayoutInfo::Minimal{ .rows = 3, .columns = 4 });
            for (int i = 0; i < m_info.rows(); i++)
            {
                int row = rand() % 100;
                m_rowsArray.Append(json::JsonValue::CreateNumberValue(row));
                m_info.rowsPercents()[i] = row;
            }

            for (int i = 0; i < m_info.columns(); i++)
            {
                int column = rand() % 100;
                m_columnsArray.Append(json::JsonValue::CreateNumberValue(column));
                m_info.columnsPercents()[i] = column;
            }

            for (int i = 0; i < m_info.rows(); i++)
            {
                json::JsonArray cellsArray;
                for (int j = 0; j < m_info.columns(); j++)
                {
                    int cell = rand() % 100;
                    m_info.cellChildMap()[i][j] = cell;
                    cellsArray.Append(json::JsonValue::CreateNumberValue(cell));
                }
                m_cells.Append(cellsArray);
            }

            m_gridJson = json::JsonObject::Parse(L"{\"rows\": 3, \"columns\": 4}");
            m_gridJson.SetNamedValue(L"rows-percentage", m_rowsArray);
            m_gridJson.SetNamedValue(L"columns-percentage", m_columnsArray);
            m_gridJson.SetNamedValue(L"cell-child-map", m_cells);
        }

        TEST_METHOD_CLEANUP(Cleanup)
        {
            m_rowsArray.Clear();
            m_cells.Clear();
            m_columnsArray.Clear();
            m_gridJson.Clear();
            m_info = GridLayoutInfo(GridLayoutInfo::Minimal{ .rows = 3, .columns = 4 });
        }

    public:
        TEST_METHOD(CreationZero)
        {
            const int expectedRows = 0, expectedColumns = 0;
            GridLayoutInfo info(GridLayoutInfo::Minimal{ .rows = expectedRows, .columns = expectedColumns });
            compareSizes(expectedRows, expectedColumns, info);
        }

        TEST_METHOD(Creation)
        {
            const int expectedRows = 3, expectedColumns = 4;
            const std::vector<int> expectedRowsPercents = { 0, 0, 0 };
            const std::vector<int> expectedColumnsPercents = { 0, 0, 0, 0 };

            GridLayoutInfo info(GridLayoutInfo::Minimal{ .rows = expectedRows, .columns = expectedColumns });
            compareSizes(expectedRows, expectedColumns, info);

            compareVectors(expectedRowsPercents, info.rowsPercents());
            compareVectors(expectedColumnsPercents, info.columnsPercents());
            for (int i = 0; i < info.cellChildMap().size(); i++)
            {
                compareVectors({ 0, 0, 0, 0 }, info.cellChildMap()[i]);
            }
        }

        TEST_METHOD(CreationFull)
        {
            const int expectedRows = 3, expectedColumns = 4;
            const std::vector<int> expectedRowsPercents = { 1, 2, 3 };
            const std::vector<int> expectedColumnsPercents = { 4, 3, 2, 1 };
            const std::vector<std::vector<int>> expectedCells = { expectedColumnsPercents, expectedColumnsPercents, expectedColumnsPercents };

            GridLayoutInfo info(GridLayoutInfo::Full{
                .rows = expectedRows,
                .columns = expectedColumns ,
                .rowsPercents = expectedRowsPercents,
                .columnsPercents = expectedColumnsPercents,
                .cellChildMap = expectedCells });
            compareSizes(expectedRows, expectedColumns, info);

            compareVectors(expectedRowsPercents, info.rowsPercents());
            compareVectors(expectedColumnsPercents, info.columnsPercents());
            for (int i = 0; i < info.cellChildMap().size(); i++)
            {
                compareVectors(expectedCells[i], info.cellChildMap()[i]);
            }
        }

        TEST_METHOD(CreationFullVectorsSmaller)
        {
            const int expectedRows = 3, expectedColumns = 4;
            const std::vector<int> expectedRowsPercents = { 1, 2, 0 };
            const std::vector<int> expectedColumnsPercents = { 4, 3, 0, 0 };
            const std::vector<std::vector<int>> expectedCells = { { 0, 0, 0, 0 }, { 1, 0, 0, 0 }, { 1, 2, 0, 0 } };

            GridLayoutInfo info(GridLayoutInfo::Full{
                .rows = expectedRows,
                .columns = expectedColumns,
                .rowsPercents = { 1, 2 },
                .columnsPercents = { 4, 3 },
                .cellChildMap = { {}, { 1 }, { 1, 2 } } });
            compareSizes(expectedRows, expectedColumns, info);

            compareVectors(expectedRowsPercents, info.rowsPercents());
            compareVectors(expectedColumnsPercents, info.columnsPercents());
            for (int i = 0; i < info.cellChildMap().size(); i++)
            {
                compareVectors(expectedCells[i], info.cellChildMap()[i]);
            }
        }

        TEST_METHOD(CreationFullVectorsBigger)
        {
            const int expectedRows = 3, expectedColumns = 4;
            const std::vector<int> expectedRowsPercents = { 1, 2, 3 };
            const std::vector<int> expectedColumnsPercents = { 4, 3, 2, 1 };
            const std::vector<std::vector<int>> expectedCells = { expectedColumnsPercents, expectedColumnsPercents, expectedColumnsPercents };

            GridLayoutInfo info(GridLayoutInfo::Full{
                .rows = expectedRows,
                .columns = expectedColumns,
                .rowsPercents = { 1, 2, 3, 4, 5 },
                .columnsPercents = { 4, 3, 2, 1, 0, -1 },
                .cellChildMap = { { 4, 3, 2, 1, 0, -1 }, { 4, 3, 2, 1, 0, -1 }, { 4, 3, 2, 1, 0, -1 } } });
            compareSizes(expectedRows, expectedColumns, info);

            compareVectors(expectedRowsPercents, info.rowsPercents());
            compareVectors(expectedColumnsPercents, info.columnsPercents());
            for (int i = 0; i < info.cellChildMap().size(); i++)
            {
                compareVectors(expectedCells[i], info.cellChildMap()[i]);
            }
        }

        TEST_METHOD(ToJson)
        {
            json::JsonObject expected = json::JsonObject(m_gridJson);
            GridLayoutInfo info = m_info;

            auto actual = GridLayoutInfo::ToJson(info);
            compareJsonObjects(expected, actual);
        }

        TEST_METHOD(FromJson)
        {
            json::JsonObject json = json::JsonObject(m_gridJson);
            GridLayoutInfo expected = m_info;

            auto actual = GridLayoutInfo::FromJson(json);
            Assert::IsTrue(actual.has_value());
            compareGridInfos(expected, *actual);
        }

        TEST_METHOD(FromJsonEmptyArray)
        {
            json::JsonObject json = json::JsonObject::Parse(L"{\"rows\": 0, \"columns\": 0}");
            GridLayoutInfo expected(GridLayoutInfo::Minimal{ 0, 0 });

            json.SetNamedValue(L"rows-percentage", json::JsonArray());
            json.SetNamedValue(L"columns-percentage", json::JsonArray());
            json.SetNamedValue(L"cell-child-map", json::JsonArray());

            auto actual = GridLayoutInfo::FromJson(json);
            Assert::IsTrue(actual.has_value());
            compareGridInfos(expected, *actual);
        }

        TEST_METHOD(FromJsonSmallerArray)
        {
            GridLayoutInfo expected = m_info;
            expected.rowsPercents().pop_back();
            expected.columnsPercents().pop_back();
            expected.cellChildMap().pop_back();
            expected.cellChildMap()[0].pop_back();
            json::JsonObject json = GridLayoutInfo::ToJson(expected);

            auto actual = GridLayoutInfo::FromJson(json);
            Assert::IsFalse(actual.has_value());
        }

        TEST_METHOD(FromJsonBiggerArray)
        {
            GridLayoutInfo expected = m_info;

            //extra
            for (int i = 0; i < 5; i++)
            {
                expected.rowsPercents().push_back(rand() % 100);
                expected.columnsPercents().push_back(rand() % 100);
                expected.cellChildMap().push_back({});

                for (int j = 0; j < 5; j++)
                {
                    expected.cellChildMap()[i].push_back(rand() % 100);
                }
            }

            auto json = GridLayoutInfo::ToJson(expected);

            auto actual = GridLayoutInfo::FromJson(json);
            Assert::IsFalse(actual.has_value());
        }

        TEST_METHOD(FromJsonMissingKeys)
        {
            GridLayoutInfo info = m_info;
            const auto json = json::JsonObject(m_gridJson);

            auto iter = json.First();
            while (iter.HasCurrent())
            {
                json::JsonObject modifiedJson = json::JsonObject::Parse(json.Stringify());
                modifiedJson.Remove(iter.Current().Key());

                auto actual = GridLayoutInfo::FromJson(modifiedJson);
                Assert::IsFalse(actual.has_value());

                iter.MoveNext();
            }
        }
    };

    TEST_CLASS(CustomZoneSetUnitTests)
    {
        TEST_METHOD(ToJsonGrid)
        {
            CustomZoneSetJSON zoneSet{ L"uuid", CustomZoneSetData{ L"name", CustomLayoutType::Grid, GridLayoutInfo(GridLayoutInfo::Minimal{}) } };

            json::JsonObject expected = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"name\": \"name\", \"type\": \"grid\"}");
            expected.SetNamedValue(L"info", GridLayoutInfo::ToJson(std::get<GridLayoutInfo>(zoneSet.data.info)));

            auto actual = CustomZoneSetJSON::ToJson(zoneSet);
            compareJsonObjects(expected, actual);
        }

        TEST_METHOD(ToJsonCanvas)
        {
            CustomZoneSetJSON zoneSet{ L"uuid", CustomZoneSetData{ L"name", CustomLayoutType::Canvas, CanvasLayoutInfo{} } };

            json::JsonObject expected = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"name\": \"name\", \"type\": \"canvas\"}");
            expected.SetNamedValue(L"info", CanvasLayoutInfo::ToJson(std::get<CanvasLayoutInfo>(zoneSet.data.info)));

            auto actual = CustomZoneSetJSON::ToJson(zoneSet);
            compareJsonObjects(expected, actual);
        }

        TEST_METHOD(FromJsonGrid)
        {
            const auto grid = GridLayoutInfo(GridLayoutInfo::Full{ 1, 3, { 10000 }, { 2500, 5000, 2500 }, { { 0, 1, 2 } } });
            CustomZoneSetJSON expected{ L"uuid", CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"name\": \"name\", \"type\": \"grid\"}");
            json.SetNamedValue(L"info", GridLayoutInfo::ToJson(std::get<GridLayoutInfo>(expected.data.info)));

            auto actual = CustomZoneSetJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.uuid.c_str(), actual->uuid.c_str());
            Assert::AreEqual(expected.data.name.c_str(), actual->data.name.c_str());
            Assert::AreEqual((int)expected.data.type, (int)actual->data.type);

            auto expectedGrid = std::get<GridLayoutInfo>(expected.data.info);
            auto actualGrid = std::get<GridLayoutInfo>(actual->data.info);
            Assert::AreEqual(expectedGrid.rows(), actualGrid.rows());
            Assert::AreEqual(expectedGrid.columns(), actualGrid.columns());
        }

        TEST_METHOD(FromJsonCanvas)
        {
            CustomZoneSetJSON expected{ L"uuid", CustomZoneSetData{ L"name", CustomLayoutType::Canvas, CanvasLayoutInfo{ 2, 1 } } };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"name\": \"name\", \"type\": \"canvas\"}");
            json.SetNamedValue(L"info", CanvasLayoutInfo::ToJson(std::get<CanvasLayoutInfo>(expected.data.info)));

            auto actual = CustomZoneSetJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.uuid.c_str(), actual->uuid.c_str());
            Assert::AreEqual(expected.data.name.c_str(), actual->data.name.c_str());
            Assert::AreEqual((int)expected.data.type, (int)actual->data.type);

            auto expectedGrid = std::get<CanvasLayoutInfo>(expected.data.info);
            auto actualGrid = std::get<CanvasLayoutInfo>(actual->data.info);
            Assert::AreEqual(expectedGrid.referenceWidth, actualGrid.referenceWidth);
            Assert::AreEqual(expectedGrid.referenceHeight, actualGrid.referenceHeight);
        }

        TEST_METHOD(FromJsonMissingKeys)
        {
            CustomZoneSetJSON zoneSet{ L"uuid", CustomZoneSetData{ L"name", CustomLayoutType::Canvas, CanvasLayoutInfo{ 2, 1 } } };
            const auto json = CustomZoneSetJSON::ToJson(zoneSet);

            auto iter = json.First();
            while (iter.HasCurrent())
            {
                json::JsonObject modifiedJson = json::JsonObject::Parse(json.Stringify());
                modifiedJson.Remove(iter.Current().Key());

                auto actual = CustomZoneSetJSON::FromJson(modifiedJson);
                Assert::IsFalse(actual.has_value());

                iter.MoveNext();
            }
        }
    };

    TEST_CLASS(ZoneSetDataUnitTest){
        TEST_METHOD(ToJsonCustom)
        {
            json::JsonObject expected = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"type\": \"custom\"}");
            ZoneSetData data{ L"uuid", ZoneSetLayoutType::Custom };
            const auto actual = ZoneSetData::ToJson(data);
            compareJsonObjects(expected, actual);
        }

        TEST_METHOD(ToJsonGeneral)
        {
            json::JsonObject expected = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"type\": \"rows\"}");
            ZoneSetData data{ L"uuid", ZoneSetLayoutType::Rows };
            const auto actual = ZoneSetData::ToJson(data);
            compareJsonObjects(expected, actual);
        }

        TEST_METHOD(FromJsonCustom)
        {
            ZoneSetData expected{ L"uuid", ZoneSetLayoutType::Custom };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"type\": \"custom\"}");
            auto actual = ZoneSetData::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.uuid.c_str(), actual->uuid.c_str());
            Assert::AreEqual((int)expected.type, (int)actual->type);
        }

        TEST_METHOD(FromJsonCustomZoneAdded)
        {
            ZoneSetData expected{ L"uuid", ZoneSetLayoutType::Custom };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"type\": \"custom\"}");
            auto actual = ZoneSetData::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.uuid.c_str(), actual->uuid.c_str());
            Assert::AreEqual((int)expected.type, (int)actual->type);
        }

        TEST_METHOD(FromJsonGeneral)
        {
            ZoneSetData expected{ L"uuid", ZoneSetLayoutType::Columns };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"type\": \"columns\"}");
            auto actual = ZoneSetData::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.uuid.c_str(), actual->uuid.c_str());
            Assert::AreEqual((int)expected.type, (int)actual->type);
        }

        TEST_METHOD(FromJsonTypeInvalid)
        {
            ZoneSetData expected{ L"uuid", ZoneSetLayoutType::Custom };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"type\": \"invalid_type\"}");
            auto actual = ZoneSetData::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.uuid.c_str(), actual->uuid.c_str());
            Assert::AreEqual((int)expected.type, (int)actual->type);
        }

        TEST_METHOD(FromJsonMissingKeys)
        {
            ZoneSetData data{ L"uuid", ZoneSetLayoutType::Columns };
            const auto json = ZoneSetData::ToJson(data);

            auto iter = json.First();
            while (iter.HasCurrent())
            {
                json::JsonObject modifiedJson = json::JsonObject::Parse(json.Stringify());
                modifiedJson.Remove(iter.Current().Key());

                auto actual = ZoneSetData::FromJson(modifiedJson);
                Assert::IsFalse(actual.has_value());

                iter.MoveNext();
            }
        }
    };

    TEST_CLASS(AppZoneHistoryUnitTests)
    {
        TEST_METHOD(ToJson)
        {
            AppZoneHistoryJSON appZoneHistory{ L"appPath", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndex = 54321 } };
            json::JsonObject expected = json::JsonObject::Parse(L"{\"app-path\": \"appPath\", \"device-id\": \"device-id\", \"zoneset-uuid\": \"zoneset-uuid\", \"zone-index\": 54321}");

            auto actual = AppZoneHistoryJSON::ToJson(appZoneHistory);
            compareJsonObjects(expected, actual);
        }

        TEST_METHOD(FromJson)
        {
            AppZoneHistoryJSON expected{ L"appPath", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndex = 54321 } };
            json::JsonObject json = json::JsonObject::Parse(L"{\"app-path\": \"appPath\", \"device-id\": \"device-id\", \"zoneset-uuid\": \"zoneset-uuid\", \"zone-index\": 54321}");

            auto actual = AppZoneHistoryJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.appPath.c_str(), actual->appPath.c_str());
            Assert::AreEqual(expected.data.zoneIndex, actual->data.zoneIndex);
            Assert::AreEqual(expected.data.deviceId.c_str(), actual->data.deviceId.c_str());
            Assert::AreEqual(expected.data.zoneSetUuid.c_str(), actual->data.zoneSetUuid.c_str());
        }

        TEST_METHOD(FromJsonMissingKeys)
        {
            AppZoneHistoryJSON appZoneHistory{ L"appPath", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndex = 54321 } };
            const auto json = AppZoneHistoryJSON::ToJson(appZoneHistory);

            auto iter = json.First();
            while (iter.HasCurrent())
            {
                json::JsonObject modifiedJson = json::JsonObject::Parse(json.Stringify());
                modifiedJson.Remove(iter.Current().Key());

                auto actual = AppZoneHistoryJSON::FromJson(modifiedJson);
                Assert::IsFalse(actual.has_value());

                iter.MoveNext();
            }
        }
    };

    TEST_CLASS(DeviceInfoUnitTests)
    {
    private:
        DeviceInfoJSON m_defaultDeviceInfo = DeviceInfoJSON{ L"default_device_id", DeviceInfoData{ ZoneSetData{ L"uuid", ZoneSetLayoutType::Custom }, true, 16, 3 } };
        json::JsonObject m_defaultJson = json::JsonObject::Parse(L"{\"device-id\": \"default_device_id\", \"active-zoneset\": {\"type\": \"custom\", \"uuid\": \"uuid\"}, \"editor-show-spacing\": true, \"editor-spacing\": 16, \"editor-zone-count\": 3}");

    public:
        TEST_METHOD(ToJson)
        {
            DeviceInfoJSON deviceInfo = m_defaultDeviceInfo;
            json::JsonObject expected = m_defaultJson;

            auto actual = DeviceInfoJSON::ToJson(deviceInfo);
            compareJsonObjects(expected, actual);
        }

        TEST_METHOD(FromJson)
        {
            DeviceInfoJSON expected = m_defaultDeviceInfo;
            expected.data.spacing = true;

            json::JsonObject json = DeviceInfoJSON::ToJson(expected);
            auto actual = DeviceInfoJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.deviceId.c_str(), actual->deviceId.c_str(), L"device id");
            Assert::AreEqual(expected.data.zoneCount, actual->data.zoneCount, L"zone count");
            Assert::AreEqual((int)expected.data.activeZoneSet.type, (int)actual->data.activeZoneSet.type, L"zone set type");
            Assert::AreEqual(expected.data.activeZoneSet.uuid.c_str(), actual->data.activeZoneSet.uuid.c_str(), L"zone set uuid");
        }

        TEST_METHOD(FromJsonSpacingTrue)
        {
            DeviceInfoJSON expected = m_defaultDeviceInfo;
            expected.data.spacing = true;

            json::JsonObject json = DeviceInfoJSON::ToJson(expected);
            auto actual = DeviceInfoJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.data.spacing, actual->data.spacing);
        }

        TEST_METHOD(FromJsonSpacingFalse)
        {
            DeviceInfoJSON expected = m_defaultDeviceInfo;
            expected.data.activeZoneSet.type = ZoneSetLayoutType::Custom;

            json::JsonObject json = DeviceInfoJSON::ToJson(expected);
            auto actual = DeviceInfoJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.data.spacing, actual->data.spacing);
        }

        TEST_METHOD(FromJsonZoneCustom)
        {
            DeviceInfoJSON expected = m_defaultDeviceInfo;
            expected.data.activeZoneSet.type = ZoneSetLayoutType::Custom;

            json::JsonObject json = DeviceInfoJSON::ToJson(expected);
            auto actual = DeviceInfoJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual((int)expected.data.activeZoneSet.type, (int)actual->data.activeZoneSet.type, L"zone set type");
        }

        TEST_METHOD(FromJsonZoneGeneral)
        {
            DeviceInfoJSON expected = m_defaultDeviceInfo;
            expected.data.activeZoneSet.type = ZoneSetLayoutType::PriorityGrid;

            json::JsonObject json = DeviceInfoJSON::ToJson(expected);
            auto actual = DeviceInfoJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual((int)expected.data.activeZoneSet.type, (int)actual->data.activeZoneSet.type, L"zone set type");
        }

        TEST_METHOD(FromJsonMissingKeys)
        {
            DeviceInfoJSON deviceInfo{ L"default_device_id", DeviceInfoData{ ZoneSetData{ L"uuid", ZoneSetLayoutType::Custom }, true, 16, 3 } };
            const auto json = DeviceInfoJSON::ToJson(deviceInfo);

            auto iter = json.First();
            while (iter.HasCurrent())
            {
                json::JsonObject modifiedJson = json::JsonObject::Parse(json.Stringify());
                modifiedJson.Remove(iter.Current().Key());

                auto actual = DeviceInfoJSON::FromJson(modifiedJson);
                Assert::IsFalse(actual.has_value());

                iter.MoveNext();
            }
        }
    };

    TEST_CLASS(FancyZonesDataUnitTests)
    {
    private:
        const std::wstring m_defaultCustomDeviceStr = L"{\"device-id\": \"default_device_id\", \"active-zoneset\": {\"type\": \"custom\", \"uuid\": \"uuid\"}, \"editor-show-spacing\": true, \"editor-spacing\": 16, \"editor-zone-count\": 3}";
        const json::JsonValue m_defaultCustomDeviceValue = json::JsonValue::Parse(m_defaultCustomDeviceStr);
        const json::JsonObject m_defaultCustomDeviceObj = json::JsonObject::Parse(m_defaultCustomDeviceStr);

        HINSTANCE m_hInst{};
        FancyZonesData& m_fzData = FancyZonesDataInstance();

        void compareJsonArrays(const json::JsonArray& expected, const json::JsonArray& actual)
        {
            Assert::AreEqual(expected.Size(), actual.Size());
            for (uint32_t i = 0; i < expected.Size(); i++)
            {
                compareJsonObjects(expected.GetObjectAt(i), actual.GetObjectAt(i));
            }
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            m_hInst = (HINSTANCE)GetModuleHandleW(nullptr);
            m_fzData = FancyZonesData();
        }

    public:
        TEST_METHOD(FancyZonesDataPath)
        {
            FancyZonesData data;
            Assert::IsFalse(data.GetPersistFancyZonesJSONPath().empty());
        }

        TEST_METHOD(FancyZonesDataJsonEmpty)
        {
            FancyZonesData data;
            const auto jsonPath = data.GetPersistFancyZonesJSONPath();
            auto savedJson = json::from_file(jsonPath);

            if (std::filesystem::exists(jsonPath))
            {
                std::filesystem::remove(jsonPath);
            }

            json::JsonObject expected;
            auto actual = data.GetPersistFancyZonesJSON();

            Assert::AreEqual(expected.Stringify().c_str(), actual.Stringify().c_str());

            if (savedJson)
            {
                json::to_file(jsonPath, *savedJson);
            }
        }

        TEST_METHOD(FancyZonesDataJson)
        {
            FancyZonesData data;
            const auto jsonPath = data.GetPersistFancyZonesJSONPath();
            auto savedJson = json::from_file(jsonPath);

            if (std::filesystem::exists(jsonPath))
            {
                std::filesystem::remove(jsonPath);
            }

            json::JsonObject expected = json::JsonObject::Parse(L"{\"fancy-zones\":{\"custom-zonesets \":[{\"uuid\":\"uuid1\",\"name\":\"Custom1\",\"type\":\"custom\" }] } }");
            json::to_file(jsonPath, expected);

            auto actual = data.GetPersistFancyZonesJSON();
            Assert::AreEqual(expected.Stringify().c_str(), actual.Stringify().c_str());

            if (savedJson)
            {
                json::to_file(jsonPath, *savedJson);
            }
            else
            {
                std::filesystem::remove(jsonPath);
            }
        }

        TEST_METHOD(FancyZonesDataDeviceInfoMap)
        {
            FancyZonesData data;
            const auto actual = data.GetDeviceInfoMap();
            Assert::IsTrue(actual.empty());
        }

        TEST_METHOD(FancyZonesDataDeviceInfoMapParseEmpty)
        {
            FancyZonesData data;

            json::JsonObject json;
            data.ParseDeviceInfos(json);

            const auto actual = data.GetDeviceInfoMap();
            Assert::IsTrue(actual.empty());
        }

        TEST_METHOD(FancyZonesDataDeviceInfoMapParseValidEmpty)
        {
            FancyZonesData data;

            json::JsonObject expected;
            json::JsonArray zoneSets;
            expected.SetNamedValue(L"devices", zoneSets);

            data.ParseDeviceInfos(expected);

            const auto actual = data.GetDeviceInfoMap();
            Assert::IsTrue(actual.empty());
        }

        TEST_METHOD(FancyZonesDataDeviceInfoMapParseInvalid)
        {
            json::JsonArray devices;
            devices.Append(json::JsonObject::Parse(m_defaultCustomDeviceStr));
            devices.Append(json::JsonObject::Parse(L"{\"device-id\": \"device_id\"}"));

            json::JsonObject expected;
            expected.SetNamedValue(L"devices", devices);

            FancyZonesData data;
            auto actual = data.ParseDeviceInfos(expected);

            Assert::IsFalse(actual);
        }

        TEST_METHOD(FancyZonesDataDeviceInfoMapParseSingle)
        {
            json::JsonArray devices;
            devices.Append(m_defaultCustomDeviceValue);
            json::JsonObject expected;
            expected.SetNamedValue(L"devices", devices);

            FancyZonesData data;
            data.ParseDeviceInfos(expected);

            const auto actualMap = data.GetDeviceInfoMap();
            Assert::AreEqual((size_t)1, actualMap.size());
        }

        TEST_METHOD(FancyZonesDataDeviceInfoMapParseMany)
        {
            json::JsonArray devices;
            for (int i = 0; i < 10; i++)
            {
                json::JsonObject obj = json::JsonObject::Parse(m_defaultCustomDeviceStr);
                obj.SetNamedValue(L"device-id", json::JsonValue::CreateStringValue(std::to_wstring(i)));

                Logger::WriteMessage(obj.Stringify().c_str());
                Logger::WriteMessage("\n");

                devices.Append(obj);
            }

            json::JsonObject expected;
            expected.SetNamedValue(L"devices", devices);
            Logger::WriteMessage(expected.Stringify().c_str());
            Logger::WriteMessage("\n");

            FancyZonesData data;
            data.ParseDeviceInfos(expected);

            const auto actualMap = data.GetDeviceInfoMap();
            Assert::AreEqual((size_t)10, actualMap.size());
        }

        TEST_METHOD(FancyZonesDataSerialize)
        {
            json::JsonArray expectedDevices;
            expectedDevices.Append(m_defaultCustomDeviceObj);
            json::JsonObject expected;
            expected.SetNamedValue(L"devices", expectedDevices);

            FancyZonesData data;
            data.ParseDeviceInfos(expected);

            auto actual = data.SerializeDeviceInfos();
            compareJsonArrays(expectedDevices, actual);
        }

        TEST_METHOD(DeviceInfoSaveTemp)
        {
            FancyZonesData data;
            DeviceInfoJSON deviceInfo{ L"default_device_id", DeviceInfoData{ ZoneSetData{ L"uuid", ZoneSetLayoutType::Custom }, true, 16, 3 } };

            const std::wstring path = data.GetPersistFancyZonesJSONPath() + L".test_tmp";
            data.SerializeDeviceInfoToTmpFile(deviceInfo, path);

            bool actualFileExists = std::filesystem::exists(path);
            Assert::IsTrue(actualFileExists);

            auto expectedData = DeviceInfoJSON::ToJson(deviceInfo);
            auto actualSavedData = json::from_file(path);
            std::filesystem::remove(path); //clean up before compare asserts

            Assert::IsTrue(actualSavedData.has_value());
            compareJsonObjects(expectedData, *actualSavedData);
        }

        TEST_METHOD(DeviceInfoReadTemp)
        {
            FancyZonesData data;
            const std::wstring zoneUuid = L"default_device_id";
            DeviceInfoJSON expected{ zoneUuid, DeviceInfoData{ ZoneSetData{ L"uuid", ZoneSetLayoutType::Custom }, true, 16, 3 } };
            const std::wstring path = data.GetPersistFancyZonesJSONPath() + L".test_tmp";
            data.SerializeDeviceInfoToTmpFile(expected, path);

            data.ParseDeviceInfoFromTmpFile(path);

            bool actualFileExists = std::filesystem::exists(path);
            if (actualFileExists)
            {
                std::filesystem::remove(path); //clean up before compare asserts
            }
            Assert::IsFalse(actualFileExists);

            auto devices = data.GetDeviceInfoMap();
            Assert::AreEqual((size_t)1, devices.size());

            auto actual = devices.find(zoneUuid)->second;
            Assert::AreEqual(expected.data.showSpacing, actual.showSpacing);
            Assert::AreEqual(expected.data.spacing, actual.spacing);
            Assert::AreEqual(expected.data.zoneCount, actual.zoneCount);
            Assert::AreEqual((int)expected.data.activeZoneSet.type, (int)actual.activeZoneSet.type);
            Assert::AreEqual(expected.data.activeZoneSet.uuid.c_str(), actual.activeZoneSet.uuid.c_str());
        }

        TEST_METHOD(DeviceInfoReadTempUnexsisted)
        {
            FancyZonesData data;
            const std::wstring path = data.GetPersistFancyZonesJSONPath() + L".test_tmp";
            data.ParseDeviceInfoFromTmpFile(path);

            auto devices = data.GetDeviceInfoMap();
            Assert::AreEqual((size_t)0, devices.size());
        }

        TEST_METHOD(AppZoneHistoryParseSingle)
        {
            const std::wstring expectedAppPath = L"appPath";
            const std::wstring expectedDeviceId = L"device-id";
            const std::wstring expectedZoneSetId = L"zone-set-id";
            const int expectedIndex = 54321;
            
            json::JsonObject json;
            AppZoneHistoryJSON expected{ expectedAppPath, AppZoneHistoryData{ .zoneSetUuid = expectedZoneSetId, .deviceId = expectedDeviceId, .zoneIndex = expectedIndex } };
            json::JsonArray zoneHistoryArray;
            zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(expected));
            json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(zoneHistoryArray.Stringify()));

            FancyZonesData data;
            data.ParseAppZoneHistory(json);

            const auto actualProcessHistoryMap = data.GetAppZoneHistoryMap();
            Assert::AreEqual((size_t)zoneHistoryArray.Size(), actualProcessHistoryMap.size());

            const auto actualProcessHistory = actualProcessHistoryMap.begin();
            Assert::AreEqual(expectedAppPath.c_str(), actualProcessHistory->first.c_str());

            const auto actualAppZoneHistory = actualProcessHistory->second;
            Assert::AreEqual(expectedZoneSetId.c_str(), actualAppZoneHistory.zoneSetUuid.c_str());
            Assert::AreEqual(expectedDeviceId.c_str(), actualAppZoneHistory.deviceId.c_str());
            Assert::AreEqual(expectedIndex, actualAppZoneHistory.zoneIndex);           
        }

        TEST_METHOD(AppZoneHistoryParseManyApps)
        {
            json::JsonObject json;
            json::JsonArray zoneHistoryArray;
            zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-1", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid-1", .deviceId = L"device-id-1", .zoneIndex = 1 } }));
            zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-2", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid-2", .deviceId = L"device-id-2", .zoneIndex = 2 } }));
            zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-3", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid-3", .deviceId = L"device-id-3", .zoneIndex = 3 } }));
            zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-4", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid-4", .deviceId = L"device-id-4", .zoneIndex = 4 } }));

            json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(zoneHistoryArray.Stringify()));

            FancyZonesData data;
            data.ParseAppZoneHistory(json);

            auto actualMap = data.GetAppZoneHistoryMap();
            Assert::AreEqual((size_t)zoneHistoryArray.Size(), actualMap.size());

            const auto actualProcessHistoryMap = data.GetAppZoneHistoryMap();
            Assert::AreEqual((size_t)zoneHistoryArray.Size(), actualProcessHistoryMap.size());

            auto iter = zoneHistoryArray.First();
            while (iter.HasCurrent())
            {
                auto expected = AppZoneHistoryJSON::FromJson(json::JsonObject::Parse(iter.Current().Stringify()));         

                const auto actual = actualProcessHistoryMap.at(expected->appPath);
                Assert::AreEqual(expected->data.deviceId.c_str(), actual.deviceId.c_str());
                Assert::AreEqual(expected->data.zoneSetUuid.c_str(), actual.zoneSetUuid.c_str());
                Assert::AreEqual(expected->data.zoneIndex, actual.zoneIndex);

                iter.MoveNext();
            }
        }

        TEST_METHOD(AppZoneHistoryParseManyZonesForSingleApp)
        {
            json::JsonObject json;
            json::JsonArray zoneHistoryArray;

            const auto appPath = L"app-path";
            zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid-1", .deviceId = L"device-id-1", .zoneIndex = 1 } }));
            zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid-2", .deviceId = L"device-id-2", .zoneIndex = 2 } }));
            zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid-3", .deviceId = L"device-id-3", .zoneIndex = 3 } }));
            const auto expected = AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid-4", .deviceId = L"device-id-4", .zoneIndex = 4 };
            zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, expected }));
            json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(zoneHistoryArray.Stringify()));

            FancyZonesData data;
            data.ParseAppZoneHistory(json);

            const auto actualProcessHistoryMap = data.GetAppZoneHistoryMap();
            Assert::AreEqual((size_t)1, actualProcessHistoryMap.size());
            
            const auto actual = actualProcessHistoryMap.at(appPath);
            Assert::AreEqual(expected.deviceId.c_str(), actual.deviceId.c_str());
            Assert::AreEqual(expected.zoneSetUuid.c_str(), actual.zoneSetUuid.c_str());
            Assert::AreEqual(expected.zoneIndex, actual.zoneIndex);
        }

        TEST_METHOD(AppZoneHistoryParseEmpty)
        {
            FancyZonesData data;
            data.ParseAppZoneHistory(json::JsonObject());

            auto actual = data.GetAppZoneHistoryMap();
            Assert::IsTrue(actual.empty());
        }

        TEST_METHOD(AppZoneHistoryParseInvalid)
        {
            const std::wstring appPath = L"appPath";
            json::JsonObject json;
            AppZoneHistoryJSON expected{ appPath, AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndex = 54321 } };
            json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(AppZoneHistoryJSON::ToJson(expected).Stringify()));

            FancyZonesData data;
            bool actual = data.ParseAppZoneHistory(json);

            Assert::IsFalse(actual);
        }

        TEST_METHOD(AppZoneHistorySerializeSingle)
        {
            const std::wstring appPath = L"appPath";
            json::JsonArray expected;
            expected.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndex = 54321 } }));
            json::JsonObject json;
            json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(expected.Stringify()));

            FancyZonesData data;
            data.ParseAppZoneHistory(json);

            auto actual = data.SerializeAppZoneHistory();
            compareJsonArrays(expected, actual);
        }

        TEST_METHOD(AppZoneHistorySerializeMany)
        {
            json::JsonObject json;
            json::JsonArray expected;
            expected.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-1", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndex = 54321 } }));
            expected.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-2", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndex = 54321 } }));
            expected.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-3", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndex = 54321 } }));
            expected.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-4", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndex = 54321 } }));
            json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(expected.Stringify()));

            FancyZonesData data;
            data.ParseAppZoneHistory(json);

            auto actual = data.SerializeAppZoneHistory();
            compareJsonArrays(expected, actual);
        }

        TEST_METHOD(AppZoneHistorySerializeEmpty)
        {
            json::JsonArray expected;
            json::JsonObject json;
            json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(expected.Stringify()));

            FancyZonesData data;
            data.ParseAppZoneHistory(json);

            auto actual = data.SerializeAppZoneHistory();
            compareJsonArrays(expected, actual);
        }

        TEST_METHOD(CustomZoneSetsParseSingle)
        {
            const std::wstring zoneUuid = L"uuid";
            GridLayoutInfo grid(GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
                .rows = 1,
                .columns = 3,
                .rowsPercents = { 10000 },
                .columnsPercents = { 2500, 5000, 2500 },
                .cellChildMap = { { 0, 1, 2 } } }));

            json::JsonObject json;
            CustomZoneSetJSON expected{ zoneUuid, CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } };
            json::JsonArray array;
            array.Append(CustomZoneSetJSON::ToJson(expected));
            json.SetNamedValue(L"custom-zone-sets", json::JsonValue::Parse(array.Stringify()));

            FancyZonesData data;
            data.ParseCustomZoneSets(json);

            auto actualMap = data.GetCustomZoneSetsMap();
            Assert::AreEqual((size_t)array.Size(), actualMap.size());

            auto actual = actualMap.find(zoneUuid)->second;
            Assert::AreEqual(expected.data.name.c_str(), actual.name.c_str());
            Assert::AreEqual((int)expected.data.type, (int)actual.type);

            auto expectedGrid = std::get<GridLayoutInfo>(expected.data.info);
            auto actualGrid = std::get<GridLayoutInfo>(actual.info);
            Assert::AreEqual(expectedGrid.rows(), actualGrid.rows());
            Assert::AreEqual(expectedGrid.columns(), actualGrid.columns());
        }

        TEST_METHOD(CustomZoneSetsParseMany)
        {
            json::JsonObject json;
            json::JsonArray array;
            const GridLayoutInfo grid(GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
                .rows = 1,
                .columns = 3,
                .rowsPercents = { 10000 },
                .columnsPercents = { 2500, 5000, 2500 },
                .cellChildMap = { { 0, 1, 2 } } }));
            array.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ L"zone-uuid-1", CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } }));
            array.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ L"zone-uuid-2", CustomZoneSetData{ L"name", CustomLayoutType::Canvas, CanvasLayoutInfo{ 1, 2 } } }));
            array.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ L"zone-uuid-3", CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } }));
            array.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ L"zone-uuid-4", CustomZoneSetData{ L"name", CustomLayoutType::Canvas, CanvasLayoutInfo{ 1, 2 } } }));
            json.SetNamedValue(L"custom-zone-sets", json::JsonValue::Parse(array.Stringify()));

            FancyZonesData data;
            data.ParseCustomZoneSets(json);

            auto actualMap = data.GetCustomZoneSetsMap();
            Assert::AreEqual((size_t)array.Size(), actualMap.size());

            auto iter = array.First();
            while (iter.HasCurrent())
            {
                auto expected = CustomZoneSetJSON::FromJson(json::JsonObject::Parse(iter.Current().Stringify()));
                auto actual = actualMap.find(expected->uuid)->second;
                Assert::AreEqual(expected->data.name.c_str(), actual.name.c_str(), L"name");
                Assert::AreEqual((int)expected->data.type, (int)actual.type, L"type");

                if (expected->data.type == CustomLayoutType::Grid)
                {
                    auto expectedInfo = std::get<GridLayoutInfo>(expected->data.info);
                    auto actualInfo = std::get<GridLayoutInfo>(actual.info);
                    Assert::AreEqual(expectedInfo.rows(), actualInfo.rows(), L"grid rows");
                    Assert::AreEqual(expectedInfo.columns(), actualInfo.columns(), L"grid columns");
                }
                else
                {
                    auto expectedInfo = std::get<CanvasLayoutInfo>(expected->data.info);
                    auto actualInfo = std::get<CanvasLayoutInfo>(actual.info);
                    Assert::AreEqual(expectedInfo.referenceWidth, actualInfo.referenceWidth, L"canvas width");
                    Assert::AreEqual(expectedInfo.referenceHeight, actualInfo.referenceHeight, L"canvas height");
                }

                iter.MoveNext();
            }
        }

        TEST_METHOD(CustomZoneSetsParseEmpty)
        {
            FancyZonesData data;
            data.ParseCustomZoneSets(json::JsonObject());

            auto actual = data.GetCustomZoneSetsMap();
            Assert::IsTrue(actual.empty());
        }

        TEST_METHOD(CustomZoneSetsParseInvalid)
        {
            json::JsonObject json;
            CustomZoneSetJSON expected{ L"uuid", CustomZoneSetData{ L"name", CustomLayoutType::Grid, GridLayoutInfo(GridLayoutInfo::Minimal{ 1, 2 }) } };
            json.SetNamedValue(L"custom-zone-sets", json::JsonValue::Parse(CustomZoneSetJSON::ToJson(expected).Stringify()));

            FancyZonesData data;
            auto actual = data.ParseCustomZoneSets(json);

            Assert::IsFalse(actual);
        }

        TEST_METHOD(CustomZoneSetsSerializeSingle)
        {
            json::JsonArray expected;
            const GridLayoutInfo grid(GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
                .rows = 1,
                .columns = 3,
                .rowsPercents = { 10000 },
                .columnsPercents = { 2500, 5000, 2500 },
                .cellChildMap = { { 0, 1, 2 } } }));
            expected.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ L"uuid", CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } }));
            json::JsonObject json;
            json.SetNamedValue(L"custom-zone-sets", json::JsonValue::Parse(expected.Stringify()));

            FancyZonesData data;
            data.ParseCustomZoneSets(json);

            auto actual = data.SerializeCustomZoneSets();
            compareJsonArrays(expected, actual);
        }

        TEST_METHOD(CustomZoneSetsSerializeMany)
        {
            json::JsonObject json;
            json::JsonArray expected;
            const GridLayoutInfo grid(GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
                .rows = 1,
                .columns = 3,
                .rowsPercents = { 10000 },
                .columnsPercents = { 2500, 5000, 2500 },
                .cellChildMap = { { 0, 1, 2 } } }));

            expected.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ L"zone-uuid-1", CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } }));
            expected.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ L"zone-uuid-2", CustomZoneSetData{ L"name", CustomLayoutType::Canvas, CanvasLayoutInfo{ 1, 2 } } }));
            expected.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ L"zone-uuid-3", CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } }));
            expected.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ L"zone-uuid-4", CustomZoneSetData{ L"name", CustomLayoutType::Canvas, CanvasLayoutInfo{ 1, 2 } } }));
            json.SetNamedValue(L"custom-zone-sets", json::JsonValue::Parse(expected.Stringify()));

            FancyZonesData data;
            data.ParseCustomZoneSets(json);

            auto actual = data.SerializeCustomZoneSets();
            compareJsonArrays(expected, actual);
        }

        TEST_METHOD(CustomZoneSetsSerializeEmpty)
        {
            json::JsonArray expected;
            json::JsonObject json;
            json.SetNamedValue(L"custom-zone-sets", json::JsonValue::Parse(expected.Stringify()));

            FancyZonesData data;
            data.ParseCustomZoneSets(json);

            auto actual = data.SerializeCustomZoneSets();
            compareJsonArrays(expected, actual);
        }

        TEST_METHOD(CustomZoneSetsReadTemp)
        {
            //prepare device data
            const std::wstring deviceId = L"default_device_id";

            {    
                DeviceInfoJSON deviceInfo{ deviceId, DeviceInfoData{ ZoneSetData{ L"uuid", ZoneSetLayoutType::Custom }, true, 16, 3 } };
                const std::wstring deviceInfoPath = m_fzData.GetPersistFancyZonesJSONPath() + L".device_info_tmp";
                m_fzData.SerializeDeviceInfoToTmpFile(deviceInfo, deviceInfoPath);

                m_fzData.ParseDeviceInfoFromTmpFile(deviceInfoPath);
                std::filesystem::remove(deviceInfoPath);
            }

            const std::wstring uuid = L"uuid";
            const GridLayoutInfo grid(GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
                .rows = 1,
                .columns = 3,
                .rowsPercents = { 10000 },
                .columnsPercents = { 2500, 5000, 2500 },
                .cellChildMap = { { 0, 1, 2 } } }));
            CustomZoneSetJSON expected{ uuid, CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } };

            FancyZonesData data;
            const std::wstring path = data.GetPersistFancyZonesJSONPath() + L".test_tmp";
            json::to_file(path, CustomZoneSetJSON::ToJson(expected));            
            m_fzData.ParseCustomZoneSetFromTmpFile(path, deviceId);

            bool actualFileExists = std::filesystem::exists(path);
            if (actualFileExists)
            {
                std::filesystem::remove(path); //clean up before compare asserts
            }
            Assert::IsFalse(actualFileExists);

            auto devices = m_fzData.GetCustomZoneSetsMap();
            Assert::AreEqual((size_t)1, devices.size());

            auto actual = devices.find(uuid)->second;
            Assert::AreEqual((int)expected.data.type, (int)actual.type);
            Assert::AreEqual(expected.data.name.c_str(), actual.name.c_str());
            auto expectedGrid = std::get<GridLayoutInfo>(expected.data.info);
            auto actualGrid = std::get<GridLayoutInfo>(actual.info);
            Assert::AreEqual(expectedGrid.rows(), actualGrid.rows());
            Assert::AreEqual(expectedGrid.columns(), actualGrid.columns());
        }

        TEST_METHOD(CustomZoneSetsReadTempUnexsisted)
        {
            const std::wstring path = m_fzData.GetPersistFancyZonesJSONPath() + L".test_tmp";
            const std::wstring deviceId = L"default_device_id";

            m_fzData.ParseCustomZoneSetFromTmpFile(path, deviceId);
            auto devices = m_fzData.GetDeviceInfoMap();
            Assert::AreEqual((size_t)0, devices.size());
        }

        TEST_METHOD(SetActiveZoneSet)
        {
            FancyZonesData data;
            const std::wstring uuid = L"uuid";
            const std::wstring uniqueId = L"default_device_id";

            json::JsonArray devices;
            devices.Append(m_defaultCustomDeviceValue);
            json::JsonObject json;
            json.SetNamedValue(L"devices", devices);
            data.ParseDeviceInfos(json);

            data.SetActiveZoneSet(uniqueId, uuid);

            auto actual = data.GetDeviceInfoMap().find(uniqueId)->second;
            Assert::AreEqual(uuid, actual.activeZoneSet.uuid);
        }

        TEST_METHOD(SetActiveZoneSetUuidEmpty)
        {
            FancyZonesData data;
            const std::wstring uuid = L"";
            const std::wstring expected = L"uuid";
            const std::wstring uniqueId = L"default_device_id";

            json::JsonArray devices;
            devices.Append(m_defaultCustomDeviceValue);
            json::JsonObject json;
            json.SetNamedValue(L"devices", devices);
            data.ParseDeviceInfos(json);

            data.SetActiveZoneSet(uniqueId, uuid);

            auto actual = data.GetDeviceInfoMap().find(uniqueId)->second;
            Assert::AreEqual(expected, actual.activeZoneSet.uuid);
        }

        TEST_METHOD(SetActiveZoneSetUniqueIdInvalid)
        {
            FancyZonesData data;
            const std::wstring uuid = L"new_uuid";
            const std::wstring expected = L"uuid";
            const std::wstring uniqueId = L"id_not_contained_by_device_info_map";

            json::JsonArray devices;
            devices.Append(m_defaultCustomDeviceValue);
            json::JsonObject json;
            json.SetNamedValue(L"devices", devices);
            bool parseRes = data.ParseDeviceInfos(json);
            Assert::IsTrue(parseRes);

            data.SetActiveZoneSet(uniqueId, uuid);

            const auto& deviceInfoMap = data.GetDeviceInfoMap();
            auto actual = deviceInfoMap.find(L"default_device_id")->second;
            Assert::AreEqual(expected, actual.activeZoneSet.uuid);

            Assert::IsTrue(deviceInfoMap.end() == deviceInfoMap.find(uniqueId), L"new device info should not be added");
        }

        TEST_METHOD(LoadFancyZonesDataFromJson)
        {
            FancyZonesData data;
            const auto jsonPath = data.GetPersistFancyZonesJSONPath();
            auto savedJson = json::from_file(jsonPath);

            if (std::filesystem::exists(jsonPath))
            {
                std::filesystem::remove(jsonPath);
            }

            const GridLayoutInfo grid(GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
                .rows = 1,
                .columns = 3,
                .rowsPercents = { 10000 },
                .columnsPercents = { 2500, 5000, 2500 },
                .cellChildMap = { { 0, 1, 2 } } }));
            CustomZoneSetJSON zoneSets{ L"zone-set-uuid", CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } };
            AppZoneHistoryJSON appZoneHistory{ L"app-path", AppZoneHistoryData{ .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndex = 54321 } };
            DeviceInfoJSON deviceInfo{ L"uuid", DeviceInfoData{ ZoneSetData{ L"uuid", ZoneSetLayoutType::Custom }, true, 16, 3 } };
            json::JsonArray zoneSetsArray, appZonesArray, deviceInfoArray;
            zoneSetsArray.Append(CustomZoneSetJSON::ToJson(zoneSets));
            appZonesArray.Append(AppZoneHistoryJSON::ToJson(appZoneHistory));
            deviceInfoArray.Append(DeviceInfoJSON::ToJson(deviceInfo));
            json::JsonObject fancyZones;
            fancyZones.SetNamedValue(L"custom-zone-sets", zoneSetsArray);
            fancyZones.SetNamedValue(L"app-zone-history", appZonesArray);
            fancyZones.SetNamedValue(L"devices", deviceInfoArray);

            json::to_file(jsonPath, fancyZones);

            data.LoadFancyZonesData();
            if (savedJson)
            {
                json::to_file(jsonPath, *savedJson);
            }
            else
            {
                std::filesystem::remove(jsonPath);
            }

            Assert::IsFalse(data.GetCustomZoneSetsMap().empty());
            Assert::IsFalse(data.GetCustomZoneSetsMap().empty());
            Assert::IsFalse(data.GetCustomZoneSetsMap().empty());
        }

        TEST_METHOD(LoadFancyZonesDataFromRegistry)
        {
            FancyZonesData data;
            const auto jsonPath = data.GetPersistFancyZonesJSONPath();
            auto savedJson = json::from_file(jsonPath);

            if (std::filesystem::exists(jsonPath))
            {
                std::filesystem::remove(jsonPath);
            }

            data.LoadFancyZonesData();
            bool actual = std::filesystem::exists(jsonPath);
            if (savedJson)
            {
                json::to_file(jsonPath, *savedJson);
            }
            else
            {
                std::filesystem::remove(jsonPath);
            }

            Assert::IsTrue(actual);
        }

        TEST_METHOD(SaveFancyZonesData)
        {
            FancyZonesData data;
            const auto jsonPath = data.GetPersistFancyZonesJSONPath();
            auto savedJson = json::from_file(jsonPath);

            if (std::filesystem::exists(jsonPath))
            {
                std::filesystem::remove(jsonPath);
            }

            data.SaveFancyZonesData();
            bool actual = std::filesystem::exists(jsonPath);

            if (savedJson)
            {
                json::to_file(jsonPath, *savedJson);
            }
            else
            {
                std::filesystem::remove(jsonPath);
            }

            Assert::IsTrue(actual);
        }

        TEST_METHOD(AppLastZoneIndex)
        {
            const std::wstring deviceId = L"device-id";
            const std::wstring zoneSetId = L"zoneset-uuid";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            Assert::AreEqual(-1, data.GetAppLastZoneIndex(window, deviceId, zoneSetId));

            const int expectedZoneIndex = 10;
            Assert::IsTrue(data.SetAppLastZone(window, deviceId, zoneSetId, expectedZoneIndex));
            Assert::AreEqual(expectedZoneIndex, data.GetAppLastZoneIndex(window, deviceId, zoneSetId));
        }

        TEST_METHOD(AppLastZoneIndexZero)
        {
            const std::wstring zoneSetId = L"zoneset-uuid";
            const std::wstring deviceId = L"device-id";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            const int expectedZoneIndex = 0;
            Assert::IsTrue(data.SetAppLastZone(window, deviceId, zoneSetId, expectedZoneIndex));
            Assert::AreEqual(expectedZoneIndex, data.GetAppLastZoneIndex(window, deviceId, zoneSetId));
        }

        TEST_METHOD(AppLastZoneIndexNegative)
        {
            const std::wstring zoneSetId = L"zoneset-uuid";
            const std::wstring deviceId = L"device-id";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            const int expectedZoneIndex = -1;
            Assert::IsTrue(data.SetAppLastZone(window, deviceId, zoneSetId, expectedZoneIndex));
            Assert::AreEqual(expectedZoneIndex, data.GetAppLastZoneIndex(window, deviceId, zoneSetId));
        }

        TEST_METHOD(AppLastZoneIndexOverflow)
        {
            const std::wstring zoneSetId = L"zoneset-uuid";
            const std::wstring deviceId = L"device-id";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            const long expectedZoneIndex = LONG_MAX;
            Assert::IsTrue(data.SetAppLastZone(window, deviceId, zoneSetId, expectedZoneIndex));
            Assert::AreEqual(static_cast<int>(expectedZoneIndex), data.GetAppLastZoneIndex(window, deviceId, zoneSetId));
        }

        TEST_METHOD(AppLastZoneIndexOverride)
        {
            const std::wstring zoneSetId = L"zoneset-uuid";
            const std::wstring deviceId = L"device-id";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            const int expectedZoneIndex = 3;
            Assert::IsTrue(data.SetAppLastZone(window, deviceId, zoneSetId, 1));
            Assert::IsTrue(data.SetAppLastZone(window, deviceId, zoneSetId, 2));
            Assert::IsTrue(data.SetAppLastZone(window, deviceId, zoneSetId, expectedZoneIndex));
            Assert::AreEqual(expectedZoneIndex, data.GetAppLastZoneIndex(window, deviceId, zoneSetId));
        }

        TEST_METHOD(AppLastZoneInvalidWindow)
        {
            const std::wstring zoneSetId = L"zoneset-uuid";
            const std::wstring deviceId = L"device-id";
            const auto window = Mocks::Window();
            FancyZonesData data;

            Assert::AreEqual(-1, data.GetAppLastZoneIndex(window, deviceId, zoneSetId));

            const int expectedZoneIndex = 1;
            Assert::IsFalse(data.SetAppLastZone(window, deviceId, zoneSetId, expectedZoneIndex));
        }

        TEST_METHOD(AppLastZoneNullWindow)
        {
            const std::wstring zoneSetId = L"zoneset-uuid";
            const auto window = nullptr;
            FancyZonesData data;

            const int expectedZoneIndex = 1;
            Assert::IsFalse(data.SetAppLastZone(window, L"device-id", zoneSetId, expectedZoneIndex));
        }

        TEST_METHOD(AppLastdeviceIdTest)
        {
            const std::wstring zoneSetId = L"zoneset-uuid";
            const std::wstring deviceId1 = L"device-id-1";
            const std::wstring deviceId2 = L"device-id-2";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            const int expectedZoneIndex = 10;
            Assert::IsTrue(data.SetAppLastZone(window, deviceId1, zoneSetId, expectedZoneIndex));
            Assert::AreEqual(expectedZoneIndex, data.GetAppLastZoneIndex(window, deviceId1, zoneSetId));
            Assert::AreEqual(-1, data.GetAppLastZoneIndex(window, deviceId2, zoneSetId));
        }

        TEST_METHOD(AppLastZoneSetIdTest)
        {
            const std::wstring zoneSetId1 = L"zoneset-uuid-1";
            const std::wstring zoneSetId2 = L"zoneset-uuid-2";
            const std::wstring deviceId = L"device-id";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            const int expectedZoneIndex = 10;
            Assert::IsTrue(data.SetAppLastZone(window, deviceId, zoneSetId1, expectedZoneIndex));
            Assert::AreEqual(expectedZoneIndex, data.GetAppLastZoneIndex(window, deviceId, zoneSetId1));
            Assert::AreEqual(-1, data.GetAppLastZoneIndex(window, deviceId, zoneSetId2));
        }

        TEST_METHOD(AppLastZoneRemoveWindow)
        {
            const std::wstring zoneSetId = L"zoneset-uuid";
            const std::wstring deviceId = L"device-id";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            Assert::IsTrue(data.SetAppLastZone(window, deviceId, zoneSetId, 1));
            Assert::IsTrue(data.RemoveAppLastZone(window, deviceId, zoneSetId));
            Assert::AreEqual(-1, data.GetAppLastZoneIndex(window, deviceId, zoneSetId));
        }

        TEST_METHOD(AppLastZoneRemoveUnknownWindow)
        {
            const std::wstring zoneSetId = L"zoneset-uuid";
            const std::wstring deviceId = L"device-id";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            Assert::IsFalse(data.RemoveAppLastZone(window, deviceId, zoneSetId));
            Assert::AreEqual(-1, data.GetAppLastZoneIndex(window, deviceId, zoneSetId));
        }

        TEST_METHOD(AppLastZoneRemoveUnknownZoneSetId)
        {
            const std::wstring zoneSetIdToInsert = L"zoneset-uuid-to-insert";
            const std::wstring zoneSetIdToRemove = L"zoneset-uuid-to-remove";
            const std::wstring deviceId = L"device-id";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            Assert::IsTrue(data.SetAppLastZone(window, deviceId, zoneSetIdToInsert, 1));
            Assert::IsFalse(data.RemoveAppLastZone(window, deviceId, zoneSetIdToRemove));
            Assert::AreEqual(1, data.GetAppLastZoneIndex(window, deviceId, zoneSetIdToInsert));
        }

        TEST_METHOD(AppLastZoneRemoveUnknownWindowId)
        {
            const std::wstring zoneSetId = L"zoneset-uuid";
            const std::wstring deviceIdToInsert = L"device-id-insert";
            const std::wstring deviceIdToRemove = L"device-id-remove";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            Assert::IsTrue(data.SetAppLastZone(window, deviceIdToInsert, zoneSetId, 1));
            Assert::IsFalse(data.RemoveAppLastZone(window, deviceIdToRemove, zoneSetId));
            Assert::AreEqual(1, data.GetAppLastZoneIndex(window, deviceIdToInsert, zoneSetId));
        }

        TEST_METHOD(AppLastZoneRemoveNullWindow)
        {
            const std::wstring zoneSetId = L"zoneset-uuid";
            const std::wstring deviceId = L"device-id";
            const auto window = Mocks::WindowCreate(m_hInst);
            FancyZonesData data;

            Assert::IsFalse(data.RemoveAppLastZone(nullptr, deviceId, zoneSetId));
        }
    };
}