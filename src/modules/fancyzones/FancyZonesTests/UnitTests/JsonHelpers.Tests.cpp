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
            Assert::IsTrue(FancyZonesDataTypes::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdWithoutHashInName)
        {
            const auto deviceId = L"LOCALDISPLAY_5120_1440_{00000000-0000-0000-0000-000000000000}";
            Assert::IsTrue(FancyZonesDataTypes::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdWithoutHashInNameButWithUnderscores)
        {
            const auto deviceId = L"LOCAL_DISPLAY_5120_1440_{00000000-0000-0000-0000-000000000000}";
            Assert::IsFalse(FancyZonesDataTypes::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdWithUnderscoresInName)
        {
            const auto deviceId = L"Default_Monitor#1&1f0c3c2f&0&UID256_5120_1440_{00000000-0000-0000-0000-000000000000}";
            Assert::IsTrue(FancyZonesDataTypes::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidFormat)
        {
            const auto deviceId = L"_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(FancyZonesDataTypes::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidFormat2)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_19201200_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(FancyZonesDataTypes::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidDecimals)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_aaaa_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(FancyZonesDataTypes::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidDecimals2)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_19a0_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(FancyZonesDataTypes::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidDecimals3)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_1900_120000000000000_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(FancyZonesDataTypes::DeviceIdData::IsValidDeviceId(deviceId));
        }

        TEST_METHOD (DeviceIdInvalidGuid)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-4B5D-8851-4791D66B1539}";
            Assert::IsFalse(FancyZonesDataTypes::DeviceIdData::IsValidDeviceId(deviceId));
        }
    };
    TEST_CLASS (ZoneSetLayoutTypeUnitTest)
    {
        TEST_METHOD (ZoneSetLayoutTypeToString)
        {
            std::map<int, std::wstring> expectedMap = {
                std::make_pair(-2, L"TypeToString_ERROR"),
                std::make_pair(-1, L"blank"),
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
            json::JsonObject m_json = json::JsonObject::Parse(L"{\"ref-width\": true, \"ref-height\": \"string\", \"zones\": [{\"X\": \"11\", \"Y\": \"22\", \"width\": \".\", \"height\": \"*\"}, {\"X\": null, \"Y\": {}, \"width\": [], \"height\": \"абвгд\"}]}");
            Assert::IsFalse(CanvasLayoutInfoJSON::FromJson(m_json).has_value());
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
        TEST_METHOD (ToJsonGeneral)
        {
            json::JsonObject expected = json::JsonObject::Parse(L"{\"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"type\": \"rows\"}");
            ZoneSetData data{ L"{33A2B101-06E0-437B-A61E-CDBECF502906}", ZoneSetLayoutType::Rows };
            const auto actual = ZoneSetDataJSON::ToJson(data);
            auto res = CustomAssert::CompareJsonObjects(expected, actual);
            Assert::IsTrue(res.first, res.second.c_str());
        }

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
            ZoneSetData data{ L"{33A2B101-06E0-437B-A61E-CDBECF502906}", ZoneSetLayoutType::Columns };
            const auto json = ZoneSetDataJSON::ToJson(data);

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

    TEST_CLASS (AppZoneHistoryUnitTests)
    {
        TEST_METHOD (ToJson)
        {
            AppZoneHistoryData data{
                .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndexSet = { 54321 }
            };
            AppZoneHistoryJSON appZoneHistory{ L"appPath", std::vector<AppZoneHistoryData>{ data } };
            json::JsonObject expected = json::JsonObject::Parse(L"{\"app-path\": \"appPath\", \"history\":[{\"zone-index-set\": [54321], \"device-id\": \"device-id_0_0_{00000000-0000-0000-0000-000000000000}\", \"zoneset-uuid\": \"zoneset-uuid\"}]}");

            auto actual = AppZoneHistoryJSON::ToJson(appZoneHistory);
            auto res = CustomAssert::CompareJsonObjects(expected, actual);
            Assert::IsTrue(res.first, res.second.c_str());
        }

        TEST_METHOD (FromJson)
        {
            AppZoneHistoryData data{
                .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502906}", .deviceId = DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}").value(), .zoneIndexSet = {
                54321 }
            };
            AppZoneHistoryJSON expected{ L"appPath", std::vector<AppZoneHistoryData>{ data } };
            json::JsonObject json = json::JsonObject::Parse(L"{\"app-path\": \"appPath\", \"history\": [{\"device-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"zoneset-uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"zone-index\": 54321}]}");

            auto actual = AppZoneHistoryJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.appPath.c_str(), actual->appPath.c_str());
            Assert::AreEqual(expected.data.size(), actual->data.size());
            Assert::IsTrue(expected.data[0].zoneIndexSet == actual->data[0].zoneIndexSet);
            Assert::IsTrue(expected.data[0].deviceId == actual->data[0].deviceId);
            Assert::AreEqual(expected.data[0].zoneSetUuid.c_str(), actual->data[0].zoneSetUuid.c_str());
        }

        TEST_METHOD (FromJsonInvalidUuid)
        {
            json::JsonObject json = json::JsonObject::Parse(L"{\"app-path\": \"appPath\", \"history\": [{\"device-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"zoneset-uuid\": \"zoneset-uuid\", \"zone-index\": 54321}]}");
            auto actual = AppZoneHistoryJSON::FromJson(json);
            Assert::IsFalse(actual.has_value());
        }

        TEST_METHOD (FromJsonInvalidDeviceId)
        {
            json::JsonObject json = json::JsonObject::Parse(L"{\"app-path\": \"appPath\", \"history\": [{\"device-id\": \"device-id\", \"zoneset-uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"zone-index\": 54321}]}");
            auto actual = AppZoneHistoryJSON::FromJson(json);
            Assert::IsFalse(actual.has_value());
        }

        TEST_METHOD (FromJsonMissingKeys)
        {
            AppZoneHistoryData data{
                .zoneSetUuid = L"zoneset-uuid", .deviceId = L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}", .zoneIndexSet = { 54321 }
            };
            AppZoneHistoryJSON appZoneHistory{ L"appPath", std::vector<AppZoneHistoryData>{ data } };
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

        TEST_METHOD (FromJsonInvalidTypes)
        {
            json::JsonObject json = json::JsonObject::Parse(L"{\"app-path\": false, \"history\": [{\"device-id\": [], \"zoneset-uuid\": {}, \"zone-index\": \"54321\"}]}");
            Assert::IsFalse(AppZoneHistoryJSON::FromJson(json).has_value());
        }

        TEST_METHOD (ToJsonMultipleDesktopAppHistory)
        {
            AppZoneHistoryData data1{
                .zoneSetUuid = L"zoneset-uuid1", .deviceId = L"device-id1", .zoneIndexSet = { 54321 }
            };
            AppZoneHistoryData data2{
                .zoneSetUuid = L"zoneset-uuid2", .deviceId = L"device-id2", .zoneIndexSet = { 12345 }
            };
            AppZoneHistoryJSON appZoneHistory{
                L"appPath", std::vector<AppZoneHistoryData>{ data1, data2 }
            };
            json::JsonObject expected = json::JsonObject::Parse(L"{\"app-path\": \"appPath\", \"history\": [{\"zone-index-set\": [54321], \"device-id\": \"device-id1_0_0_{00000000-0000-0000-0000-000000000000}\", \"zoneset-uuid\": \"zoneset-uuid1\"}, {\"zone-index-set\": [12345], \"device-id\": \"device-id2_0_0_{00000000-0000-0000-0000-000000000000}\", \"zoneset-uuid\": \"zoneset-uuid2\"}]}");

            auto actual = AppZoneHistoryJSON::ToJson(appZoneHistory);
            std::wstring s = actual.Stringify().c_str();
            auto res = CustomAssert::CompareJsonObjects(expected, actual);
            Assert::IsTrue(res.first, res.second.c_str());
        }

        TEST_METHOD (FromJsonMultipleDesktopAppHistory)
        {
            AppZoneHistoryData data1{
                .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502906}", .deviceId = DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}").value(), .zoneIndexSet = { 54321 }
            };
            AppZoneHistoryData data2{
                .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502906}", .deviceId = DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{8a0b9205-6128-45a2-934a-b97f5b271235}").value(), .zoneIndexSet = {
                12345 }
            };
            AppZoneHistoryJSON expected{
                L"appPath", std::vector<AppZoneHistoryData>{ data1, data2 }
            };
            json::JsonObject json = json::JsonObject::Parse(L"{\"app-path\": \"appPath\", \"history\": [{\"device-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"zoneset-uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"zone-index-set\": [54321]}, {\"device-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{8a0b9205-6128-45a2-934a-b97f5b271235}\", \"zoneset-uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"zone-index-set\": [12345]}]}");

            auto actual = AppZoneHistoryJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.appPath.c_str(), actual->appPath.c_str());
            Assert::AreEqual(expected.data.size(), actual->data.size());

            for (size_t i = 0; i < expected.data.size(); ++i)
            {
                Assert::IsTrue(expected.data[i].zoneIndexSet == actual->data[i].zoneIndexSet);
                Assert::IsTrue(expected.data[i].deviceId == actual->data[i].deviceId);
                Assert::AreEqual(expected.data[i].zoneSetUuid.c_str(), actual->data[i].zoneSetUuid.c_str());
            }
        }
    };

    TEST_CLASS (DeviceInfoUnitTests)
    {
    private:
        FancyZonesDataTypes::DeviceIdData m_defaultDeviceId{ L"AOC2460#4&fe3a015&0&UID65793", 1920, 1200,  };
        DeviceInfoJSON m_defaultDeviceInfo = DeviceInfoJSON{ m_defaultDeviceId, DeviceInfoData{ ZoneSetData{ L"{33A2B101-06E0-437B-A61E-CDBECF502906}", ZoneSetLayoutType::Custom }, true, 16, 3 } };
        json::JsonObject m_defaultJson = json::JsonObject::Parse(L"{\"device-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"active-zoneset\": {\"type\": \"custom\", \"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\"}, \"editor-show-spacing\": true, \"editor-spacing\": 16, \"editor-zone-count\": 3}");

        TEST_METHOD_INITIALIZE(Init)
        {
            CLSIDFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}", &m_defaultDeviceId.virtualDesktopId);
            m_defaultDeviceInfo.deviceId = m_defaultDeviceId;
        }

    public:
        TEST_METHOD (ToJson)
        {
            DeviceInfoJSON deviceInfo = m_defaultDeviceInfo;
            json::JsonObject expected = m_defaultJson;

            auto actual = DeviceInfoJSON::ToJson(deviceInfo);
            auto res = CustomAssert::CompareJsonObjects(expected, actual);
            Assert::IsTrue(res.first, res.second.c_str());
        }

        TEST_METHOD (FromJson)
        {
            DeviceInfoJSON expected = m_defaultDeviceInfo;
            expected.data.spacing = true;

            json::JsonObject json = DeviceInfoJSON::ToJson(expected);
            auto actual = DeviceInfoJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::IsTrue(expected.deviceId == actual->deviceId);
            Assert::AreEqual(expected.data.zoneCount, actual->data.zoneCount, L"zone count");
            Assert::AreEqual((int)expected.data.activeZoneSet.type, (int)actual->data.activeZoneSet.type, L"zone set type");
            Assert::AreEqual(expected.data.activeZoneSet.uuid.c_str(), actual->data.activeZoneSet.uuid.c_str(), L"zone set uuid");
        }

        TEST_METHOD (FromJsonSpacingTrue)
        {
            DeviceInfoJSON expected = m_defaultDeviceInfo;
            expected.data.spacing = true;

            json::JsonObject json = DeviceInfoJSON::ToJson(expected);
            auto actual = DeviceInfoJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.data.spacing, actual->data.spacing);
        }

        TEST_METHOD (FromJsonSpacingFalse)
        {
            DeviceInfoJSON expected = m_defaultDeviceInfo;
            expected.data.activeZoneSet.type = ZoneSetLayoutType::Custom;

            json::JsonObject json = DeviceInfoJSON::ToJson(expected);
            auto actual = DeviceInfoJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual(expected.data.spacing, actual->data.spacing);
        }

        TEST_METHOD (FromJsonZoneGeneral)
        {
            DeviceInfoJSON expected = m_defaultDeviceInfo;
            expected.data.activeZoneSet.type = ZoneSetLayoutType::PriorityGrid;

            json::JsonObject json = DeviceInfoJSON::ToJson(expected);
            auto actual = DeviceInfoJSON::FromJson(json);
            Assert::IsTrue(actual.has_value());

            Assert::AreEqual((int)expected.data.activeZoneSet.type, (int)actual->data.activeZoneSet.type, L"zone set type");
        }

        TEST_METHOD (FromJsonMissingKeys)
        {
            DeviceInfoJSON deviceInfo{ m_defaultDeviceId, DeviceInfoData{ ZoneSetData{ L"{33A2B101-06E0-437B-A61E-CDBECF502906}", ZoneSetLayoutType::Custom }, true, 16, 3, DefaultValues::SensitivityRadius } };
            const auto json = DeviceInfoJSON::ToJson(deviceInfo);

            auto iter = json.First();
            while (iter.HasCurrent())
            {
                //this setting has been added later and gets a default value, so missing key still result is valid Json
                if (iter.Current().Key() == L"editor-sensitivity-radius")
                {
                    iter.MoveNext();
                    continue;
                }

                json::JsonObject modifiedJson = json::JsonObject::Parse(json.Stringify());
                modifiedJson.Remove(iter.Current().Key());

                auto actual = DeviceInfoJSON::FromJson(modifiedJson);
                Assert::IsFalse(actual.has_value());

                iter.MoveNext();
            }
        }

        TEST_METHOD (FromJsonMissingSensitivityRadiusUsesDefault)
        {
            //json without "editor-sensitivity-radius"
            json::JsonObject json = json::JsonObject::Parse(L"{\"device-id\":\"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"active-zoneset\":{\"uuid\":\"{33A2B101-06E0-437B-A61E-CDBECF502906}\",\"type\":\"custom\"},\"editor-show-spacing\":true,\"editor-spacing\":16,\"editor-zone-count\":3}");
            auto actual = DeviceInfoJSON::FromJson(json);

            Assert::IsTrue(actual.has_value());
            Assert::AreEqual(DefaultValues::SensitivityRadius, actual->data.sensitivityRadius);
        }

        TEST_METHOD (FromJsonInvalidTypes)
        {
            json::JsonObject json = json::JsonObject::Parse(L"{\"device-id\": true, \"active-zoneset\": {\"type\": null, \"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\"}, \"editor-show-spacing\": true, \"editor-spacing\": 16, \"editor-zone-count\": 3}");
            Assert::IsFalse(DeviceInfoJSON::FromJson(json).has_value());
        }

        TEST_METHOD (FromJsonInvalidUuid)
        {
            json::JsonObject json = json::JsonObject::Parse(L"{\"device-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"active-zoneset\": {\"type\": \"custom\", \"uuid\": \"uuid\"}, \"editor-show-spacing\": true, \"editor-spacing\": 16, \"editor-zone-count\": 3}");
            Assert::IsFalse(DeviceInfoJSON::FromJson(json).has_value());
        }

        TEST_METHOD (FromJsonInvalidDeviceId)
        {
            json::JsonObject json = json::JsonObject::Parse(L"{\"device-id\": true, \"active-zoneset\": {\"type\": \"custom\", \"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\"}, \"editor-show-spacing\": true, \"editor-spacing\": 16, \"editor-zone-count\": 3}");
            Assert::IsFalse(DeviceInfoJSON::FromJson(json).has_value());
        }
    };

    TEST_CLASS (FancyZonesDataUnitTests)
    {
    private:
        const std::wstring_view m_moduleName = L"FancyZonesUnitTests";
        const std::wstring m_defaultDeviceIdStr = L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
        const std::wstring m_defaultCustomDeviceStr = L"{\"device-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"active-zoneset\": {\"type\": \"custom\", \"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\"}, \"editor-show-spacing\": true, \"editor-spacing\": 16, \"editor-zone-count\": 3}";
        const std::wstring m_defaultCustomLayoutStr = L"{\"device-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"applied-layout\": {\"type\": \"custom\", \"uuid\": \"{33A2B101-06E0-437B-A61E-CDBECF502906}\", \"show-spacing\": true, \"spacing\": 16, \"zone-count\": 3, \"sensitivity-radius\": 30}}";
        const json::JsonValue m_defaultCustomDeviceValue = json::JsonValue::Parse(m_defaultCustomDeviceStr);
        const json::JsonObject m_defaultCustomDeviceObj = json::JsonObject::Parse(m_defaultCustomDeviceStr);
        const json::JsonObject m_defaultCustomLayoutObj = json::JsonObject::Parse(m_defaultCustomLayoutStr);

        const FancyZonesDataTypes::DeviceIdData m_defaultDeviceId = FancyZonesDataTypes::DeviceIdData::ParseDeviceId(m_defaultDeviceIdStr).value();

        GUID m_defaultVDId;
        
        HINSTANCE m_hInst{};

        TEST_METHOD_INITIALIZE(Init)
        {
            m_hInst = (HINSTANCE)GetModuleHandleW(nullptr);
            std::filesystem::remove_all(PTSettingsHelper::get_module_save_folder_location(m_moduleName));

            auto guid = Helpers::StringToGuid(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}");
            Assert::IsTrue(guid.has_value());
            m_defaultVDId = *guid;
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

            TEST_METHOD (AppZoneHistoryParseSingle)
            {
                const std::wstring expectedAppPath = L"appPath";
                const auto expectedDeviceId = m_defaultDeviceId;
                const std::wstring expectedZoneSetId = L"{33A2B101-06E0-437B-A61E-CDBECF502906}";
                const size_t expectedIndex = 54321;

                AppZoneHistoryData data{
                    .zoneSetUuid = expectedZoneSetId, .deviceId = expectedDeviceId, .zoneIndexSet = { expectedIndex }
                };
                AppZoneHistoryJSON expected{ expectedAppPath, std::vector<AppZoneHistoryData>{ data } };
                json::JsonArray zoneHistoryArray;
                zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(expected));
                json::JsonObject json;
                json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(zoneHistoryArray.Stringify()));

                const auto& appZoneHistoryMap = ParseAppZoneHistory(json);

                Assert::AreEqual((size_t)zoneHistoryArray.Size(), appZoneHistoryMap.size());

                const auto& entry = appZoneHistoryMap.begin();
                Assert::AreEqual(expectedAppPath.c_str(), entry->first.c_str());

                const auto entryData = entry->second;
                Assert::AreEqual(expected.data.size(), entryData.size());
                Assert::AreEqual(expectedZoneSetId.c_str(), entryData[0].zoneSetUuid.c_str());
                Assert::IsTrue(expectedDeviceId == entryData[0].deviceId);
                Assert::IsTrue(std::vector<ZoneIndex>{ expectedIndex } == entryData[0].zoneIndexSet);
            }

            TEST_METHOD (AppZoneHistoryParseManyApps)
            {
                json::JsonObject json;
                json::JsonArray zoneHistoryArray;
                AppZoneHistoryData data1{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502900}", .deviceId = DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1530}").value(), .zoneIndexSet = {
                        1 }
                };
                AppZoneHistoryData data2{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502901}", .deviceId = DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1531}").value(), .zoneIndexSet = {
                        2 }
                };
                AppZoneHistoryData data3{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502902}", .deviceId = DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1532}").value(), .zoneIndexSet = {
                        3 }
                };
                AppZoneHistoryData data4{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502903}", .deviceId = DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1533}").value(), .zoneIndexSet = {
                        4 }
                };
                zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-1", std::vector<AppZoneHistoryData>{ data1 } }));
                zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-2", std::vector<AppZoneHistoryData>{ data2 } }));
                zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-3", std::vector<AppZoneHistoryData>{ data3 } }));
                zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ L"app-path-4", std::vector<AppZoneHistoryData>{ data4 } }));

                json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(zoneHistoryArray.Stringify()));

                const auto& appZoneHistoryMap = ParseAppZoneHistory(json);

                Assert::AreEqual((size_t)zoneHistoryArray.Size(), appZoneHistoryMap.size());

                auto iter = zoneHistoryArray.First();
                while (iter.HasCurrent())
                {
                    auto expected = AppZoneHistoryJSON::FromJson(json::JsonObject::Parse(iter.Current().Stringify()));

                    const auto& actual = appZoneHistoryMap.at(expected->appPath);
                    Assert::AreEqual(expected->data.size(), actual.size());
                    Assert::IsTrue(expected->data[0].deviceId == actual[0].deviceId);
                    Assert::AreEqual(expected->data[0].zoneSetUuid.c_str(), actual[0].zoneSetUuid.c_str());
                    Assert::IsTrue(expected->data[0].zoneIndexSet == actual[0].zoneIndexSet);

                    iter.MoveNext();
                }
            }

            TEST_METHOD (AppZoneHistoryParseManyZonesForSingleApp)
            {
                json::JsonObject json;
                json::JsonArray zoneHistoryArray;

                const auto appPath = L"app-path";
                AppZoneHistoryData data1{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502900}", .deviceId = DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1530}").value(), .zoneIndexSet = {
                        1 }
                };
                zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, std::vector<AppZoneHistoryData>{ data1 } }));
                AppZoneHistoryData data2{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502901}", .deviceId = DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1531}").value(), .zoneIndexSet = {
                        2 }
                };
                zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, std::vector<AppZoneHistoryData>{ data2 } }));
                AppZoneHistoryData data3{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502902}", .deviceId = DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1532}").value(), .zoneIndexSet = {
                        3 }
                };
                zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, std::vector<AppZoneHistoryData>{ data3 } }));
                AppZoneHistoryData expected{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502903}", .deviceId = DeviceIdData::ParseDeviceId(L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1533}").value(), .zoneIndexSet = {
                        4 }
                };
                zoneHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, std::vector<AppZoneHistoryData>{ expected } }));
                json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(zoneHistoryArray.Stringify()));

                const auto& appZoneHistoryMap = ParseAppZoneHistory(json);

                Assert::AreEqual((size_t)1, appZoneHistoryMap.size());

                const auto& actual = appZoneHistoryMap.at(appPath);
                Assert::AreEqual((size_t)1, actual.size());
                Assert::IsTrue(expected.deviceId == actual[0].deviceId);
                Assert::AreEqual(expected.zoneSetUuid.c_str(), actual[0].zoneSetUuid.c_str());
                Assert::IsTrue(expected.zoneIndexSet == actual[0].zoneIndexSet);
            }

            TEST_METHOD (AppZoneHistoryParseEmpty)
            {
                const auto& appZoneHistoryMap = ParseAppZoneHistory(json::JsonObject());

                Assert::IsTrue(appZoneHistoryMap.empty());
            }

            TEST_METHOD (AppZoneHistoryParseInvalid)
            {
                const std::wstring appPath = L"appPath";
                json::JsonObject json;
                AppZoneHistoryData data{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502906}", .deviceId = L"device-id", .zoneIndexSet = { 54321 }
                };
                AppZoneHistoryJSON expected{ appPath, std::vector<AppZoneHistoryData>{ data } };
                json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(AppZoneHistoryJSON::ToJson(expected).Stringify()));

                const auto& appZoneHistoryMap = ParseAppZoneHistory(json);

                Assert::IsTrue(appZoneHistoryMap.empty());
            }

            TEST_METHOD (AppZoneHistoryParseInvalidUuid)
            {
                const std::wstring appPath = L"appPath";
                json::JsonObject json;
                AppZoneHistoryData data{
                    .zoneSetUuid = L"zoneset-uuid", .deviceId = L"device-id", .zoneIndexSet = { 54321 }
                };
                AppZoneHistoryJSON expected{ appPath, std::vector<AppZoneHistoryData>{ data } };
                json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(AppZoneHistoryJSON::ToJson(expected).Stringify()));

                const auto& appZoneHistoryMap = ParseAppZoneHistory(json);

                Assert::IsTrue(appZoneHistoryMap.empty());
            }

            TEST_METHOD (AppZoneHistorySerializeSingle)
            {
                const std::wstring appPath = L"appPath";
                json::JsonArray expected;
                AppZoneHistoryData data{
                    .zoneSetUuid = L"{39B25DD2-130D-4B5D-8851-4791D66B1539}", .deviceId = m_defaultDeviceId, .zoneIndexSet = { 54321 }
                };
                AppZoneHistoryJSON appZoneHistory{
                    appPath, std::vector<AppZoneHistoryData>{ data }
                };
                expected.Append(AppZoneHistoryJSON::ToJson(appZoneHistory));
                json::JsonObject json;
                json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(expected.Stringify()));

                auto appZoneHistoryMap = ParseAppZoneHistory(json);

                const auto& actual = SerializeAppZoneHistory(appZoneHistoryMap);
                auto res = CustomAssert::CompareJsonArrays(expected, actual);
                Assert::IsTrue(res.first, res.second.c_str());
            }

            TEST_METHOD (AppZoneHistorySerializeMany)
            {
                json::JsonObject json;
                json::JsonArray expected;
                AppZoneHistoryData data1{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502906}", .deviceId = m_defaultDeviceId, .zoneIndexSet = { 54321 }
                };
                AppZoneHistoryJSON appZoneHistory1{
                    L"app-path-1", std::vector<AppZoneHistoryData>{ data1 }
                };
                AppZoneHistoryData data2{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502906}", .deviceId = m_defaultDeviceId, .zoneIndexSet = { 54321 }
                };
                AppZoneHistoryJSON appZoneHistory2{
                    L"app-path-2", std::vector<AppZoneHistoryData>{ data2 }
                };
                AppZoneHistoryData data3{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502906}", .deviceId = m_defaultDeviceId, .zoneIndexSet = { 54321 }
                };
                AppZoneHistoryJSON appZoneHistory3{
                    L"app-path-3", std::vector<AppZoneHistoryData>{ data3 }
                };
                AppZoneHistoryData data4{
                    .zoneSetUuid = L"{33A2B101-06E0-437B-A61E-CDBECF502906}", .deviceId = m_defaultDeviceId, .zoneIndexSet = { 54321 }
                };
                AppZoneHistoryJSON appZoneHistory4{
                    L"app-path-4", std::vector<AppZoneHistoryData>{ data4 }
                };
                expected.Append(AppZoneHistoryJSON::ToJson(appZoneHistory1));
                expected.Append(AppZoneHistoryJSON::ToJson(appZoneHistory2));
                expected.Append(AppZoneHistoryJSON::ToJson(appZoneHistory3));
                expected.Append(AppZoneHistoryJSON::ToJson(appZoneHistory4));
                json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(expected.Stringify()));

                const auto& appZoneHistoryMap = ParseAppZoneHistory(json);

                const auto& actual = SerializeAppZoneHistory(appZoneHistoryMap);
                auto res = CustomAssert::CompareJsonArrays(expected, actual);
                Assert::IsTrue(res.first, res.second.c_str());
            }

            TEST_METHOD (AppZoneHistorySerializeEmpty)
            {
                json::JsonArray expected;
                json::JsonObject json;
                json.SetNamedValue(L"app-zone-history", json::JsonValue::Parse(expected.Stringify()));

                const auto& appZoneHistoryMap = ParseAppZoneHistory(json);

                const auto& actual = SerializeAppZoneHistory(appZoneHistoryMap);
                auto res = CustomAssert::CompareJsonArrays(expected, actual);
                Assert::IsTrue(res.first, res.second.c_str());
            }
    };

    TEST_CLASS(EditorArgsUnitTests)
    {
        TEST_METHOD(MonitorToJson)
        {
            const auto deviceId = L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}";
            MonitorInfo monitor{ 144, deviceId, -10, 0, 1920, 1080, true };

            const auto expectedStr = L"{\"dpi\": 144, \"monitor-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"top-coordinate\": -10, \"left-coordinate\": 0, \"width\": 1920, \"height\": 1080, \"is-selected\": true}";
            const auto expected = json::JsonObject::Parse(expectedStr);

            const auto actual = MonitorInfo::ToJson(monitor);

            auto res = CustomAssert::CompareJsonObjects(expected, actual);
            Assert::IsTrue(res.first, res.second.c_str());
        }

        TEST_METHOD(EditorArgsToJson)
        {
            MonitorInfo monitor1{ 144, L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}", -10, 0, 1920, 1080, true };
            MonitorInfo monitor2{ 96, L"AOC2460#4&fe3a015&0&UID65793_1920_1080_{39B25DD2-130D-4B5D-8851-4791D66B1538}", 0, 1920, 1920, 1080, false };
            EditorArgs args{
                1, true, std::vector<MonitorInfo>{ monitor1, monitor2 }
            };

            const std::wstring expectedMonitor1 = L"{\"dpi\": 144, \"monitor-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\", \"top-coordinate\": -10, \"left-coordinate\": 0, \"width\": 1920, \"height\": 1080, \"is-selected\": true}";
            const std::wstring expectedMonitor2 = L"{\"dpi\": 96, \"monitor-id\": \"AOC2460#4&fe3a015&0&UID65793_1920_1080_{39B25DD2-130D-4B5D-8851-4791D66B1538}\", \"top-coordinate\": 0, \"left-coordinate\": 1920, \"width\": 1920, \"height\": 1080, \"is-selected\": false}";
            const std::wstring expectedStr = L"{\"process-id\": 1, \"span-zones-across-monitors\": true, \"monitors\": [" + expectedMonitor1 + L", " + expectedMonitor2 + L"]}";
            
            const auto expected = json::JsonObject::Parse(expectedStr);
            const auto actual = EditorArgs::ToJson(args);

            auto res = CustomAssert::CompareJsonObjects(expected, actual);
            Assert::IsTrue(res.first, res.second.c_str());
        }
    };
}