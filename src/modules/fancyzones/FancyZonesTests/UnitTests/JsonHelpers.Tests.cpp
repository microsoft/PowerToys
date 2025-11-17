#include "pch.h"
#include <filesystem>
#include <fstream>
#include <utility>

#include <FancyZonesLib/FancyZonesData/AppZoneHistory.h>
#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/JsonHelpers.h>
#include <FancyZonesLib/util.h>

#include "util.h"

#include <CppUnitTestLogger.h>

using namespace JSONHelpers;
using namespace FancyZonesDataTypes;
using namespace FancyZonesUtils;
using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS (IdValidationUnitTest)
    {
        TEST_METHOD (GuidValid)
        {
            const auto guidStr = Helpers::CreateGuidString();
            Assert::IsTrue(IsValidGuid(guidStr));
        }

        TEST_METHOD (GuidInvalidForm)
        {
            const auto guidStr = L"33A2B101-06E0-437B-A61E-CDBECF502906";
            Assert::IsFalse(IsValidGuid(guidStr));
        }

        TEST_METHOD (GuidInvalidSymbols)
        {
            const auto guidStr = L"{33A2B101-06E0-437B-A61E-CDBECF50290*}";
            Assert::IsFalse(IsValidGuid(guidStr));
        }

        TEST_METHOD (GuidInvalid)
        {
            const auto guidStr = L"guid";
            Assert::IsFalse(IsValidGuid(guidStr));
        }

        TEST_METHOD (DeviceId)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            Assert::IsTrue(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdWithoutHashInName)
        {
            const auto deviceId = L"LOCALDISPLAY_5120_1440_{00000000-0000-0000-0000-000000000000}";
            Assert::IsTrue(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdWithoutHashInNameButWithUnderscores)
        {
            const auto deviceId = L"LOCAL_DISPLAY_5120_1440_{00000000-0000-0000-0000-000000000000}";
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdWithUnderscoresInName)
        {
            const auto deviceId = L"Default_Monitor#1&1f0c3c2f&0&UID256_5120_1440_{00000000-0000-0000-0000-000000000000}";
            Assert::IsTrue(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidFormat)
        {
            const auto deviceId = L"_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidFormat2)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_19201200_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidDecimals)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_aaaa_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidDecimals2)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_19a0_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidDecimals3)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_1900_120000000000000_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidGuid)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(deviceId));
        }
    };
    
    TEST_CLASS (ZoneSetLayoutTypeUnitTest)
    {
        TEST_METHOD (ZoneSetLayoutTypeToString)
        {
            std::map<int, std::wstring> expectedMap = {
                std::make_pair(-1, L"TypeToString_ERROR"),
                std::make_pair(0, L"blank"),
                std::make_pair(1, L"focus"),
                std::make_pair(2, L"columns"),
                std::make_pair(3, L"rows"),
                std::make_pair(4, L"grid"),
                std::make_pair(5, L"priority-grid"),
                std::make_pair(6, L"custom"),
                std::make_pair(7, L"TypeToString_ERROR"),
            };

            for (const auto& expected : expectedMap)
            {
                auto actual = FancyZonesDataTypes::TypeToString(static_cast<ZoneSetLayoutType>(expected.first));
                Assert::AreEqual(expected.second, actual);
            }
        }

        TEST_METHOD (ZoneSetLayoutTypeFromString)
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
                auto actual = FancyZonesDataTypes::TypeFromString(expected.second);
                Assert::AreEqual(static_cast<int>(expected.first), static_cast<int>(actual));
            }
        }
    };

    TEST_CLASS (CanvasLayoutInfoUnitTests)
    {
        json::JsonObject m_json = json::JsonObject::Parse(L"{\"ref-width\": 123, \"ref-height\": 321, \"zones\": [{\"X\": 11, \"Y\": 22, \"width\": 33, \"height\": 44}, {\"X\": 55, \"Y\": 66, \"width\": 77, \"height\": 88}], \"sensitivity-radius\": 50}");
        json::JsonObject m_jsonWithoutOptionalValues = json::JsonObject::Parse(L"{\"ref-width\": 123, \"ref-height\": 321, \"zones\": [{\"X\": 11, \"Y\": 22, \"width\": 33, \"height\": 44}, {\"X\": 55, \"Y\": 66, \"width\": 77, \"height\": 88}]}");

        TEST_METHOD (ToJson)
        {
            CanvasLayoutInfo info;
            info.lastWorkAreaWidth = 123;
            info.lastWorkAreaHeight = 321;
            info.zones = { CanvasLayoutInfo::Rect{ 11, 22, 33, 44 }, CanvasLayoutInfo::Rect{ 55, 66, 77, 88 } };
            info.sensitivityRadius = 50;

            auto actual = CanvasLayoutInfoJSON::ToJson(info);
            auto res = CustomAssert::CompareJsonObjects(m_json, actual);
            Assert::IsTrue(res.first, res.second.c_str());
        }

        TEST_METHOD (FromJson)
        {
            CanvasLayoutInfo expected;
            expected.lastWorkAreaWidth = 123;
            expected.lastWorkAreaHeight = 321;
            expected.zones = { CanvasLayoutInfo::Rect{ 11, 22, 33, 44 }, CanvasLayoutInfo::Rect{ 55, 66, 77, 88 } };
            expected.sensitivityRadius = 50;

            auto actual = CanvasLayoutInfoJSON::FromJson(m_json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.lastWorkAreaHeight, actual->lastWorkAreaHeight);
            Assert::AreEqual(expected.lastWorkAreaWidth, actual->lastWorkAreaWidth);
            Assert::AreEqual(expected.zones.size(), actual->zones.size());
            Assert::AreEqual(expected.sensitivityRadius, actual->sensitivityRadius);
            for (int i = 0; i < expected.zones.size(); i++)
            {
                Assert::AreEqual(expected.zones[i].x, actual->zones[i].x);
                Assert::AreEqual(expected.zones[i].y, actual->zones[i].y);
                Assert::AreEqual(expected.zones[i].width, actual->zones[i].width);
                Assert::AreEqual(expected.zones[i].height, actual->zones[i].height);
            }
        }

        TEST_METHOD (FromJsonWithoutOptionalValues)
        {
            CanvasLayoutInfo expected;
            expected.lastWorkAreaWidth = 123;
            expected.lastWorkAreaHeight = 321;
            expected.zones = { CanvasLayoutInfo::Rect{ 11, 22, 33, 44 }, CanvasLayoutInfo::Rect{ 55, 66, 77, 88 } };
            
            auto actual = CanvasLayoutInfoJSON::FromJson(m_jsonWithoutOptionalValues);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.lastWorkAreaHeight, actual->lastWorkAreaHeight);
            Assert::AreEqual(expected.lastWorkAreaWidth, actual->lastWorkAreaWidth);
            Assert::AreEqual(expected.zones.size(), actual->zones.size());
            Assert::AreEqual(DefaultValues::SensitivityRadius, actual->sensitivityRadius);
            for (int i = 0; i < expected.zones.size(); i++)
            {
                Assert::AreEqual(expected.zones[i].x, actual->zones[i].x);
                Assert::AreEqual(expected.zones[i].y, actual->zones[i].y);
                Assert::AreEqual(expected.zones[i].width, actual->zones[i].width);
                Assert::AreEqual(expected.zones[i].height, actual->zones[i].height);
            }
        }

        TEST_METHOD (FromJsonMissingKeys)
        {
            CanvasLayoutInfo info{ 123, 321, { CanvasLayoutInfo::Rect{ 11, 22, 33, 44 }, CanvasLayoutInfo::Rect{ 55, 66, 77, 88 } }, 50 };
            const auto json = CanvasLayoutInfoJSON::ToJson(info);

            auto iter = json.First();
            while (iter.HasCurrent())
            {
                json::JsonObject modifiedJson = json::JsonObject::Parse(json.Stringify());
                modifiedJson.Remove(iter.Current().Key());
                if (iter.Current().Key() == L"sensitivity-radius")
                {
                    iter.MoveNext();
                    continue;
                }

                auto actual = CanvasLayoutInfoJSON::FromJson(modifiedJson);
                Assert::IsFalse(actual.has_value());

                iter.MoveNext();
            }
        }

        TEST_METHOD (FromJsonInvalidTypes)
        {
            json::JsonObject local_json = json::JsonObject::Parse(L"{\"ref-width\": true, \"ref-height\": \"string\", \"zones\": [{\"X\": \"11\", \"Y\": \"22\", \"width\": \".\", \"height\": \"*\"}, {\"X\": null, \"Y\": {}, \"width\": [], \"height\": \"абвгд\"}]}");
            Assert::IsFalse(CanvasLayoutInfoJSON::FromJson(local_json).has_value());
        }
    };

    TEST_CLASS (GridLayoutInfoUnitTests)
    {
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

                TEST_METHOD (CreationZero)
                {
                    const int expectedRows = 0, expectedColumns = 0;
                    GridLayoutInfo info(GridLayoutInfo::Minimal{ .rows = expectedRows, .columns = expectedColumns });
                    compareSizes(expectedRows, expectedColumns, info);
                }

                TEST_METHOD (Creation)
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

                TEST_METHOD (CreationFull)
                {
                    const int expectedRows = 3, expectedColumns = 4;
                    const std::vector<int> expectedRowsPercents = { 1, 2, 3 };
                    const std::vector<int> expectedColumnsPercents = { 4, 3, 2, 1 };
                    const std::vector<std::vector<int>> expectedCells = { expectedColumnsPercents, expectedColumnsPercents, expectedColumnsPercents };

                    GridLayoutInfo info(GridLayoutInfo::Full{
                        .rows = expectedRows,
                        .columns = expectedColumns,
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

                TEST_METHOD (CreationFullVectorsSmaller)
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

                TEST_METHOD (CreationFullVectorsBigger)
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

                TEST_METHOD (ToJson)
                {
                    json::JsonObject expected = json::JsonObject(m_gridJson);
                    GridLayoutInfo info = m_info;

                    auto actual = GridLayoutInfoJSON::ToJson(info);
                    auto res = CustomAssert::CompareJsonObjects(expected, actual);
                    Assert::IsTrue(res.first, res.second.c_str());
                }

                
                TEST_METHOD (ToJsonWithOptionals)
                {
                    json::JsonObject expected = json::JsonObject();
                    expected = json::JsonObject::Parse(L"{\"rows\": 3, \"columns\": 4}");
                    expected.SetNamedValue(L"rows-percentage", m_rowsArray);
                    expected.SetNamedValue(L"columns-percentage", m_columnsArray);
                    expected.SetNamedValue(L"cell-child-map", m_cells);
                    expected.SetNamedValue(L"show-spacing", json::value(true));
                    expected.SetNamedValue(L"spacing", json::value(99));
                    expected.SetNamedValue(L"sensitivity-radius", json::value(55));

                    GridLayoutInfo info = m_info;
                    info.m_sensitivityRadius = 55;
                    info.m_showSpacing = true;
                    info.m_spacing = 99;

                    auto actual = GridLayoutInfoJSON::ToJson(info);
                    auto res = CustomAssert::CompareJsonObjects(expected, actual);
                    Assert::IsTrue(res.first, res.second.c_str());
                }

                TEST_METHOD (FromJson)
                {
                    json::JsonObject json = json::JsonObject(m_gridJson);
                    GridLayoutInfo expected = m_info;

                    auto actual = GridLayoutInfoJSON::FromJson(json);
                    Assert::IsTrue(actual.has_value());
                    compareGridInfos(expected, *actual);
                }

                TEST_METHOD (FromJsonEmptyArray)
                {
                    json::JsonObject json = json::JsonObject::Parse(L"{\"rows\": 0, \"columns\": 0}");
                    GridLayoutInfo expected(GridLayoutInfo::Minimal{ 0, 0 });

                    json.SetNamedValue(L"rows-percentage", json::JsonArray());
                    json.SetNamedValue(L"columns-percentage", json::JsonArray());
                    json.SetNamedValue(L"cell-child-map", json::JsonArray());

                    auto actual = GridLayoutInfoJSON::FromJson(json);
                    Assert::IsTrue(actual.has_value());
                    compareGridInfos(expected, *actual);
                }

                TEST_METHOD (FromJsonSmallerArray)
                {
                    GridLayoutInfo expected = m_info;
                    expected.rowsPercents().pop_back();
                    expected.columnsPercents().pop_back();
                    expected.cellChildMap().pop_back();
                    expected.cellChildMap()[0].pop_back();
                    json::JsonObject json = GridLayoutInfoJSON::ToJson(expected);

                    auto actual = GridLayoutInfoJSON::FromJson(json);
                    Assert::IsFalse(actual.has_value());
                }

                TEST_METHOD (FromJsonBiggerArray)
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

                    auto json = GridLayoutInfoJSON::ToJson(expected);

                    auto actual = GridLayoutInfoJSON::FromJson(json);
                    Assert::IsFalse(actual.has_value());
                }

                TEST_METHOD (FromJsonMissingKeys)
                {
                    GridLayoutInfo info = m_info;
                    const auto json = json::JsonObject(m_gridJson);

                    auto iter = json.First();
                    while (iter.HasCurrent())
                    {
                        json::JsonObject modifiedJson = json::JsonObject::Parse(json.Stringify());
                        modifiedJson.Remove(iter.Current().Key());

                        auto actual = GridLayoutInfoJSON::FromJson(modifiedJson);
                        Assert::IsFalse(actual.has_value());

                        iter.MoveNext();
                    }
                }

                TEST_METHOD(FromJsonWithOptionals)
                {
                    json::JsonObject json = json::JsonObject();
                    json = json::JsonObject::Parse(L"{\"rows\": 3, \"columns\": 4}");
                    json.SetNamedValue(L"rows-percentage", m_rowsArray);
                    json.SetNamedValue(L"columns-percentage", m_columnsArray);
                    json.SetNamedValue(L"cell-child-map", m_cells);
                    json.SetNamedValue(L"show-spacing", json::value(true));
                    json.SetNamedValue(L"spacing", json::value(99));
                    json.SetNamedValue(L"sensitivity-radius", json::value(55));

                    GridLayoutInfo expected = m_info;
                    expected.m_sensitivityRadius = 55;
                    expected.m_showSpacing = true;
                    expected.m_spacing = 99;

                    auto actual = GridLayoutInfoJSON::FromJson(json);
                    Assert::IsTrue(actual.has_value());
                    compareGridInfos(expected, *actual);
                }

                TEST_METHOD (FromJsonInvalidTypes)
                {
                    json::JsonObject gridJson = json::JsonObject::Parse(L"{\"rows\": \"три\", \"columns\": \"четыре\"}");
                    Assert::IsFalse(GridLayoutInfoJSON::FromJson(gridJson).has_value());
                }
    };

    TEST_CLASS (CustomZoneSetUnitTests)
    {
        TEST_METHOD (ToJsonGrid)
        {
            CustomZoneSetJSON zoneSet{ L"uuid", CustomLayoutData{ L"name", CustomLayoutType::Grid, GridLayoutInfo(GridLayoutInfo::Minimal{}) } };

            json::JsonObject expected = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"name\": \"name\", \"type\": \"grid\"}");
            expected.SetNamedValue(L"info", GridLayoutInfoJSON::ToJson(std::get<GridLayoutInfo>(zoneSet.data.info)));

            auto actual = CustomZoneSetJSON::ToJson(zoneSet);
            auto res = CustomAssert::CompareJsonObjects(expected, actual);
            Assert::IsTrue(res.first, res.second.c_str());
        }

        TEST_METHOD (ToJsonCanvas)
        {
            CustomZoneSetJSON zoneSet{ L"uuid", CustomLayoutData{ L"name", CustomLayoutType::Canvas, CanvasLayoutInfo{} } };

            json::JsonObject expected = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"name\": \"name\", \"type\": \"canvas\"}");
            expected.SetNamedValue(L"info", CanvasLayoutInfoJSON::ToJson(std::get<CanvasLayoutInfo>(zoneSet.data.info)));

            auto actual = CustomZoneSetJSON::ToJson(zoneSet);
            auto res = CustomAssert::CompareJsonObjects(expected, actual);
            Assert::IsTrue(res.first, res.second.c_str());
        }

        TEST_METHOD (FromJsonGrid)
        {
            const auto grid = GridLayoutInfo(GridLayoutInfo::Full{ 1, 3, { 10000 }, { 2500, 5000, 2500 }, { { 0, 1, 2 } } });
            CustomZoneSetJSON expected{ L"{33A2B101-06E0-437B-A61E-CDBECF502906}", CustomLayoutData{ L"name", CustomLayoutType::Grid, grid } };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"name\": \"name\", \"type\": \"grid\"}");
            json.SetNamedValue(L"info", GridLayoutInfoJSON::ToJson(std::get<GridLayoutInfo>(expected.data.info)));

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

        TEST_METHOD (FromJsonCanvas)
        {
            CustomZoneSetJSON expected{ L"{33A2B101-06E0-437B-A61E-CDBECF502906}", CustomLayoutData{ L"name", CustomLayoutType::Canvas, CanvasLayoutInfo{ 2, 1 } } };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"name\": \"name\", \"type\": \"canvas\"}");
            json.SetNamedValue(L"info", CanvasLayoutInfoJSON::ToJson(std::get<CanvasLayoutInfo>(expected.data.info)));

            auto actual = CustomZoneSetJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.uuid.c_str(), actual->uuid.c_str());
            Assert::AreEqual(expected.data.name.c_str(), actual->data.name.c_str());
            Assert::AreEqual((int)expected.data.type, (int)actual->data.type);

            auto expectedGrid = std::get<CanvasLayoutInfo>(expected.data.info);
            auto actualGrid = std::get<CanvasLayoutInfo>(actual->data.info);
            Assert::AreEqual(expectedGrid.lastWorkAreaWidth, actualGrid.lastWorkAreaWidth);
            Assert::AreEqual(expectedGrid.lastWorkAreaHeight, actualGrid.lastWorkAreaHeight);
        }

        TEST_METHOD (FromJsonGridInvalidUuid)
        {
            const auto grid = GridLayoutInfo(GridLayoutInfo::Full{ 1, 3, { 10000 }, { 2500, 5000, 2500 }, { { 0, 1, 2 } } });
            CustomZoneSetJSON expected{ L"uuid", CustomLayoutData{ L"name", CustomLayoutType::Grid, grid } };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"name\": \"name\", \"type\": \"grid\"}");
            json.SetNamedValue(L"info", GridLayoutInfoJSON::ToJson(std::get<GridLayoutInfo>(expected.data.info)));

            auto actual = CustomZoneSetJSON::FromJson(json);
            Assert::IsFalse(actual.has_value());
        }

        TEST_METHOD (FromJsonCanvasInvalidUuid)
        {
            CustomZoneSetJSON expected{ L"uuid", CustomLayoutData{ L"name", CustomLayoutType::Canvas, CanvasLayoutInfo{ 2, 1 } } };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"name\": \"name\", \"type\": \"canvas\"}");
            json.SetNamedValue(L"info", CanvasLayoutInfoJSON::ToJson(std::get<CanvasLayoutInfo>(expected.data.info)));

            auto actual = CustomZoneSetJSON::FromJson(json);
            Assert::IsFalse(actual.has_value());
        }

        TEST_METHOD (FromJsonMissingKeys)
        {
            CustomZoneSetJSON zoneSet{ L"uuid", CustomLayoutData{ L"name", CustomLayoutType::Canvas, CanvasLayoutInfo{ 2, 1 } } };
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

        TEST_METHOD (FromJsonInvalidTypes)
        {
            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": null, \"name\": \"имя\", \"type\": true}");
            Assert::IsFalse(CustomZoneSetJSON::FromJson(json).has_value());

            json = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"name\": \"name\", \"type\": \"unknown type\"}");
            Assert::IsFalse(CustomZoneSetJSON::FromJson(json).has_value());
        }
    };

    TEST_CLASS (ZoneSetDataUnitTest)
    {
        TEST_METHOD (FromJsonGeneral)
        {
            ZoneSetData expected{ L"{33A2B101-06E0-437B-A61E-CDBECF502906}", ZoneSetLayoutType::Columns };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"type\": \"columns\"}");
            auto actual = ZoneSetDataJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.uuid.c_str(), actual->uuid.c_str());
            Assert::AreEqual((int)expected.type, (int)actual->type);
        }

        TEST_METHOD (FromJsonTypeInvalid)
        {
            ZoneSetData expected{ L"{33A2B101-06E0-437B-A61E-CDBECF502906}", ZoneSetLayoutType::Blank };

            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"type\": \"invalid_type\"}");
            auto actual = ZoneSetDataJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.uuid.c_str(), actual->uuid.c_str());
            Assert::AreEqual((int)expected.type, (int)actual->type);
        }

        TEST_METHOD (FromJsonUuidInvalid)
        {
            json::JsonObject json = json::JsonObject::Parse(L"{\"uuid\": \"uuid\", \"type\": \"invalid_type\"}");
            auto actual = ZoneSetDataJSON::FromJson(json);
            Assert::IsFalse(actual.has_value());
        }

        TEST_METHOD (FromJsonMissingKeys)
        {
            const auto json = json::JsonObject::Parse(L"{\"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"type\": \"columns\"}");
            
            auto iter = json.First();
            while (iter.HasCurrent())
            {
                json::JsonObject modifiedJson = json::JsonObject::Parse(json.Stringify());
                modifiedJson.Remove(iter.Current().Key());

                auto actual = ZoneSetDataJSON::FromJson(modifiedJson);
                Assert::IsFalse(actual.has_value());

                iter.MoveNext();
            }
        }
    };

    TEST_CLASS (DeviceInfoUnitTests)
    {
    private:
        DeviceInfoJSON m_defaultDeviceInfo = DeviceInfoJSON{ BackwardsCompatibility::DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1080_{33A2B101-06E0-437B-A61E-CDBECF502907}").value(), DeviceInfoData{ ZoneSetData{ L"{33A2B101-06E0-437B-A61E-CDBECF502906}", ZoneSetLayoutType::Custom }, true, 16, 3 } };
        json::JsonObject m_defaultJson = json::JsonObject::Parse(L"{\"device-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"active-zoneset\": {\"type\": \"custom\", \"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\"}, \"editor-show-spacing\": true, \"editor-spacing\": 16, \"editor-zone-count\": 3}");

    public:
        TEST_METHOD (FromJson)
        {
            DeviceInfoJSON expected = m_defaultDeviceInfo;
            expected.data.spacing = true;

            json::JsonObject json = json::JsonObject::Parse(L"{\"device-id\":\"AOC2460#4&fe3a015&0&UID65793_1920_1080_{33A2B101-06E0-437B-A61E-CDBECF502907}\",\"active-zoneset\":{\"uuid\":\"{33A2B101-06E0-437B-A61E-CDBECF502906}\",\"type\":\"custom\"},\"editor-show-spacing\":true,\"editor-spacing\":16,\"editor-zone-count\":3, \"sensitivity-radius\":20}");
            
            auto actual = DeviceInfoJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::IsTrue(expected.deviceId == actual->deviceId);
            Assert::AreEqual(expected.data.zoneCount, actual->data.zoneCount, L"zone count");
            Assert::AreEqual((int)expected.data.activeZoneSet.type, (int)actual->data.activeZoneSet.type, L"zone set type");
            Assert::AreEqual(expected.data.activeZoneSet.uuid.c_str(), actual->data.activeZoneSet.uuid.c_str(), L"zone set uuid");
        }

        TEST_METHOD (FromJsonMissingSensitivityRadiusUsesDefault)
        {
            //json without "editor-sensitivity-radius"
            json::JsonObject json = json::JsonObject::Parse(L"{\"device-id\":\"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"active-zoneset\":{\"uuid\":\"{33A2B101-06E0-437B-A61E-CDBECF502906}\",\"type\":\"custom\"},\"editor-show-spacing\":true,\"editor-spacing\":16,\"editor-zone-count\":3}");
            auto actual = DeviceInfoJSON::FromJson(json);

            Assert::IsTrue(actual.has_value());
            Assert::AreEqual(DefaultValues::SensitivityRadius, actual->data.sensitivityRadius);
        }

        TEST_METHOD (FromJsonInvalid)
        {
            json::JsonObject json = json::JsonObject::Parse(L"{\"device-id\": true, \"active-zoneset\": {\"type\": \"custom\", \"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\"}, \"editor-show-spacing\": true, \"editor-spacing\": 16, \"editor-zone-count\": 3}");
            Assert::IsFalse(DeviceInfoJSON::FromJson(json).has_value());
        }
    };

    TEST_CLASS (FancyZonesDataUnitTests)
    {
    private:
        const std::wstring_view m_moduleName = L"FancyZonesUnitTests";
        const std::wstring m_defaultCustomDeviceStr = L"{\"device-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"active-zoneset\": {\"type\": \"custom\", \"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\"}, \"editor-show-spacing\": true, \"editor-spacing\": 16, \"editor-zone-count\": 3}";
        const std::wstring m_defaultCustomLayoutStr = L"{\"device-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"applied-layout\": {\"type\": \"custom\", \"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"show-spacing\": true, \"spacing\": 16, \"zone-count\": 3, \"sensitivity-radius\": 30}}";
        const json::JsonValue m_defaultCustomDeviceValue = json::JsonValue::Parse(m_defaultCustomDeviceStr);
                
        TEST_METHOD_INITIALIZE(Init)
        {
            std::filesystem::remove_all(PTSettingsHelper::get_module_save_folder_location(m_moduleName));
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {    
            std::filesystem::remove_all(PTSettingsHelper::get_module_save_folder_location(m_moduleName));
            std::filesystem::remove_all(AppZoneHistory::AppZoneHistoryFileName());
        }

        public:
            TEST_METHOD (FancyZonesDataDeviceInfoMapParseEmpty)
            {
                json::JsonObject deviceInfoJson;
                const auto& deviceInfoMap = ParseDeviceInfos(deviceInfoJson);
                Assert::IsFalse(deviceInfoMap.has_value());
            }

            TEST_METHOD (FancyZonesDataDeviceInfoMapParseValidEmpty)
            {
                json::JsonObject deviceInfoJson;
                json::JsonArray zoneSets;
                deviceInfoJson.SetNamedValue(L"devices", zoneSets);

                const auto& deviceInfoMap = ParseDeviceInfos(deviceInfoJson);

                Assert::IsTrue(deviceInfoMap.has_value());
                Assert::IsTrue(deviceInfoMap->empty());
            }

            TEST_METHOD (FancyZonesDataDeviceInfoMapParseValidAndInvalid)
            {
                json::JsonArray devices;
                devices.Append(json::JsonObject::Parse(m_defaultCustomDeviceStr));
                devices.Append(json::JsonObject::Parse(L"{\"device-id\": \"device_id\"}"));

                json::JsonObject deviceInfoJson;
                deviceInfoJson.SetNamedValue(L"devices", devices);

                const auto& deviceInfoMap = ParseDeviceInfos(deviceInfoJson);

                Assert::AreEqual((size_t)1, deviceInfoMap->size());
            }

            TEST_METHOD (FancyZonesDataDeviceInfoMapParseInvalid)
            {
                json::JsonArray devices;
                devices.Append(json::JsonObject::Parse(L"{\"device-id\": \"device_id\"}"));

                json::JsonObject deviceInfoJson;
                deviceInfoJson.SetNamedValue(L"devices", devices);

                const auto& deviceInfoMap = ParseDeviceInfos(deviceInfoJson);

                Assert::IsTrue(deviceInfoMap->empty());
            }

            TEST_METHOD (FancyZonesDataDeviceInfoMapParseSingle)
            {
                json::JsonArray devices;
                devices.Append(m_defaultCustomDeviceValue);
                json::JsonObject deviceInfoJson;
                deviceInfoJson.SetNamedValue(L"devices", devices);

                const auto& deviceInfoMap = ParseDeviceInfos(deviceInfoJson);

                Assert::AreEqual((size_t)1, deviceInfoMap->size());
            }

            TEST_METHOD (FancyZonesDataDeviceInfoMapParseMany)
            {
                json::JsonArray devices;
                for (int i = 0; i < 10; i++)
                {
                    json::JsonObject obj = json::JsonObject::Parse(m_defaultCustomDeviceStr);
                    obj.SetNamedValue(L"device-id", json::JsonValue::CreateStringValue(std::to_wstring(i) + L"_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}"));
                    devices.Append(obj);
                }

                json::JsonObject expected;
                expected.SetNamedValue(L"devices", devices);
                Logger::WriteMessage(expected.Stringify().c_str());
                Logger::WriteMessage("\n");

                const auto& deviceInfoMap = ParseDeviceInfos(expected);

                Assert::AreEqual((size_t)10, deviceInfoMap->size());
            }
    };
}