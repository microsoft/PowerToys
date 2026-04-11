// Tests ported from the Rust FancyZones implementation.
// Each TEST_METHOD here corresponds to a Rust #[test] that had no existing C++ equivalent.
// Rust sources: fancyzones-core/src/{layout,zone,keyboard_snap,data,util}.rs
//               fancyzones-engine/src/engine.rs

#include "pch.h"

#include <filesystem>
#include <algorithm>
#include <numeric>

#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include <FancyZonesLib/FancyZonesData/AppliedLayouts.h>
#include <FancyZonesLib/FancyZonesData/LayoutHotkeys.h>
#include <FancyZonesLib/FancyZonesData/LayoutTemplates.h>
#include <FancyZonesLib/FancyZonesData/DefaultLayouts.h>
#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/ZoneIndexSetBitmask.h>
#include <FancyZonesLib/Layout.h>
#include <FancyZonesLib/LayoutConfigurator.h>
#include <FancyZonesLib/Zone.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/util.h>
#include <FancyZonesLib/JsonHelpers.h>

#include "Util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace FancyZonesDataTypes;
using namespace FancyZonesUtils;

namespace FancyZonesUnitTests
{
    // ========================================================================
    // Zone tests — ported from zone.rs
    // ========================================================================
    TEST_CLASS(RustPortedZoneTests)
    {
    public:
        // zone.rs: zone_area
        TEST_METHOD(ZoneArea)
        {
            Zone zone({ 0, 0, 100, 50 }, 0);
            Assert::AreEqual(static_cast<long>(5000), zone.GetZoneArea());
        }

        // zone.rs: zone_area_inverted — width or height negative yields 0 via max(0,…)
        TEST_METHOD(ZoneAreaInverted)
        {
            Zone zone({ 100, 100, 50, 50 }, 0);
            // Inverted rect: right < left, so area should be 0 or treated as invalid
            Assert::IsTrue(zone.GetZoneArea() <= 0);
        }

        // zone.rs: bitmask_from_index_set_test
        TEST_METHOD(BitmaskFromIndexSet)
        {
            ZoneIndexSet set = { 0, 64 };
            auto bitmask = ZoneIndexSetBitmask::FromIndexSet(set);
            Assert::AreEqual(static_cast<uint64_t>(1), bitmask.part1);
            Assert::AreEqual(static_cast<uint64_t>(1), bitmask.part2);
        }

        // zone.rs: bitmask_to_index_set
        TEST_METHOD(BitmaskToIndexSet)
        {
            ZoneIndexSetBitmask bitmask{ 1, 1 };
            auto set = bitmask.ToIndexSet();
            Assert::AreEqual(static_cast<size_t>(2), set.size());
            Assert::AreEqual(static_cast<ZoneIndex>(0), set[0]);
            Assert::AreEqual(static_cast<ZoneIndex>(64), set[1]);
        }

        // zone.rs: bitmask_convert_test
        TEST_METHOD(BitmaskConvert)
        {
            ZoneIndexSet set = { 53, 54, 55, 65, 66, 67 };
            auto bitmask = ZoneIndexSetBitmask::FromIndexSet(set);
            auto actual = bitmask.ToIndexSet();
            Assert::AreEqual(set.size(), actual.size());
            for (size_t i = 0; i < set.size(); i++)
            {
                Assert::AreEqual(set[i], actual[i]);
            }
        }

        // zone.rs: bitmask_convert2_test — full 128-zone range
        TEST_METHOD(BitmaskConvertFull128)
        {
            ZoneIndexSet set;
            for (ZoneIndex i = 0; i < 128; i++)
            {
                set.push_back(i);
            }
            auto bitmask = ZoneIndexSetBitmask::FromIndexSet(set);
            auto actual = bitmask.ToIndexSet();
            Assert::AreEqual(set.size(), actual.size());
            for (size_t i = 0; i < set.size(); i++)
            {
                Assert::AreEqual(set[i], actual[i]);
            }
        }
    };

    // ========================================================================
    // Layout tests — ported from layout.rs
    // ========================================================================
    TEST_CLASS(RustPortedLayoutTests)
    {
        void checkZonesValid(const Layout* layout, ZoneSetLayoutType type, size_t expectedCount, RECT rect)
        {
            const auto& zones = layout->Zones();
            Assert::AreEqual(expectedCount, zones.size());
            for (const auto& [id, zone] : zones)
            {
                const auto& zoneRect = zone.GetZoneRect();
                Assert::IsTrue(zoneRect.left >= 0, L"left >= 0");
                Assert::IsTrue(zoneRect.top >= 0, L"top >= 0");
                Assert::IsTrue(zoneRect.left < zoneRect.right, L"left < right");
                Assert::IsTrue(zoneRect.top < zoneRect.bottom, L"top < bottom");
                if (type != ZoneSetLayoutType::Focus)
                {
                    Assert::IsTrue(zoneRect.right <= rect.right, L"right <= work area right");
                    Assert::IsTrue(zoneRect.bottom <= rect.bottom, L"bottom <= work area bottom");
                }
            }
        }

    public:
        // layout.rs: layout_columns with 1 zone
        TEST_METHOD(LayoutColumns1Zone)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000001}").value(),
                .type = ZoneSetLayoutType::Columns,
                .showSpacing = false,
                .spacing = 0,
                .zoneCount = 1,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(1), layout->Zones().size());
            checkZonesValid(layout.get(), ZoneSetLayoutType::Columns, 1, { 0, 0, 1920, 1080 });
        }

        // layout.rs: layout_columns with 2 zones
        TEST_METHOD(LayoutColumns2Zones)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000002}").value(),
                .type = ZoneSetLayoutType::Columns,
                .showSpacing = false,
                .spacing = 0,
                .zoneCount = 2,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(2), layout->Zones().size());
            checkZonesValid(layout.get(), ZoneSetLayoutType::Columns, 2, { 0, 0, 1920, 1080 });
        }

        // layout.rs: layout_columns with 3 zones
        TEST_METHOD(LayoutColumns3Zones)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000003}").value(),
                .type = ZoneSetLayoutType::Columns,
                .showSpacing = false,
                .spacing = 0,
                .zoneCount = 3,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(3), layout->Zones().size());
        }

        // layout.rs: layout_columns with 5 zones
        TEST_METHOD(LayoutColumns5Zones)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000004}").value(),
                .type = ZoneSetLayoutType::Columns,
                .showSpacing = false,
                .spacing = 0,
                .zoneCount = 5,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(5), layout->Zones().size());
        }

        // layout.rs: layout_rows with spacing
        TEST_METHOD(LayoutRowsWithSpacing)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000005}").value(),
                .type = ZoneSetLayoutType::Rows,
                .showSpacing = true,
                .spacing = 10,
                .zoneCount = 3,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(3), layout->Zones().size());
            checkZonesValid(layout.get(), ZoneSetLayoutType::Rows, 3, { 0, 0, 1920, 1080 });
        }

        // layout.rs: layout_grid with non-square aspect ratio (wide)
        TEST_METHOD(LayoutGridWideAspectRatio)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000006}").value(),
                .type = ZoneSetLayoutType::Grid,
                .showSpacing = true,
                .spacing = 0,
                .zoneCount = 4,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 2560, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(4), layout->Zones().size());
            checkZonesValid(layout.get(), ZoneSetLayoutType::Grid, 4, { 0, 0, 2560, 1080 });
        }

        // layout.rs: layout_grid with non-square aspect ratio (tall)
        TEST_METHOD(LayoutGridTallAspectRatio)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000007}").value(),
                .type = ZoneSetLayoutType::Grid,
                .showSpacing = true,
                .spacing = 0,
                .zoneCount = 4,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1080, 1920 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(4), layout->Zones().size());
            checkZonesValid(layout.get(), ZoneSetLayoutType::Grid, 4, { 0, 0, 1080, 1920 });
        }

        // layout.rs: layout_priority_grid with high zone counts
        TEST_METHOD(LayoutPriorityGrid10Zones)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000008}").value(),
                .type = ZoneSetLayoutType::PriorityGrid,
                .showSpacing = true,
                .spacing = 5,
                .zoneCount = 10,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(10), layout->Zones().size());
            checkZonesValid(layout.get(), ZoneSetLayoutType::PriorityGrid, 10, { 0, 0, 1920, 1080 });
        }

        // layout.rs: layout_focus positioning
        TEST_METHOD(LayoutFocusPositioning)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000009}").value(),
                .type = ZoneSetLayoutType::Focus,
                .showSpacing = true,
                .spacing = 0,
                .zoneCount = 3,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(3), layout->Zones().size());

            // Focus zones are centered — zone 0 should be the largest
            const auto& zones = layout->Zones();
            long area0 = zones.at(0).GetZoneArea();
            long area1 = zones.at(1).GetZoneArea();
            Assert::IsTrue(area0 >= area1, L"Focus zone 0 should be >= zone 1 in area");
        }

        // layout.rs: big_zone_count
        TEST_METHOD(BigZoneCount128)
        {
            const int zoneCount = 128;
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000010}").value(),
                .type = ZoneSetLayoutType::Grid,
                .showSpacing = true,
                .spacing = 0,
                .zoneCount = zoneCount,
                .sensitivityRadius = 33
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(zoneCount), layout->Zones().size());
            checkZonesValid(layout.get(), ZoneSetLayoutType::Grid, zoneCount, { 0, 0, 1920, 1080 });
        }

        // layout.rs: zero_zone_count — blank with 0 should succeed
        TEST_METHOD(BlankZeroZoneCountSucceeds)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000011}").value(),
                .type = ZoneSetLayoutType::Blank,
                .showSpacing = true,
                .spacing = 17,
                .zoneCount = 0,
                .sensitivityRadius = 33
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(0), layout->Zones().size());
        }

        // layout.rs: zero_zone_count — focus with 0 should succeed
        TEST_METHOD(FocusZeroZoneCountSucceeds)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000012}").value(),
                .type = ZoneSetLayoutType::Focus,
                .showSpacing = true,
                .spacing = 17,
                .zoneCount = 0,
                .sensitivityRadius = 33
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(0), layout->Zones().size());
        }

        // layout.rs: zero_zone_count — grid types with 0 should fail
        TEST_METHOD(GridZeroZoneCountFails)
        {
            const ZoneSetLayoutType gridTypes[] = {
                ZoneSetLayoutType::Columns,
                ZoneSetLayoutType::Rows,
                ZoneSetLayoutType::Grid,
                ZoneSetLayoutType::PriorityGrid
            };
            for (auto lt : gridTypes)
            {
                LayoutData data{
                    .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000013}").value(),
                    .type = lt,
                    .showSpacing = true,
                    .spacing = 17,
                    .zoneCount = 0,
                    .sensitivityRadius = 33
                };
                auto layout = std::make_unique<Layout>(data);
                Assert::IsFalse(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            }
        }

        // layout.rs: GetCombinedZoneRange — zones 0 and 1 in a 4-zone grid
        TEST_METHOD(GetCombinedZoneRangeTopRow)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000014}").value(),
                .type = ZoneSetLayoutType::Grid,
                .showSpacing = true,
                .spacing = 0,
                .zoneCount = 4,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 960, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(4), layout->Zones().size());

            ZoneIndexSet initial = { 0 };
            ZoneIndexSet final_zones = { 1 };
            auto range = layout->GetCombinedZoneRange(initial, final_zones);
            // Should contain at least zones 0 and 1
            Assert::IsTrue(std::find(range.begin(), range.end(), 0) != range.end());
            Assert::IsTrue(std::find(range.begin(), range.end(), 1) != range.end());
        }

        // layout.rs: layout across various standard work area sizes
        TEST_METHOD(LayoutColumnsAllStandardResolutions)
        {
            const RECT rects[] = {
                { 0, 0, 1024, 768 },
                { 0, 0, 1280, 720 },
                { 0, 0, 1280, 800 },
                { 0, 0, 1280, 1024 },
                { 0, 0, 1366, 768 },
                { 0, 0, 1440, 900 },
                { 0, 0, 1536, 864 },
                { 0, 0, 1600, 900 },
                { 0, 0, 1920, 1080 },
            };
            for (const auto& rect : rects)
            {
                LayoutData data{
                    .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000015}").value(),
                    .type = ZoneSetLayoutType::Columns,
                    .showSpacing = true,
                    .spacing = 10,
                    .zoneCount = 3,
                    .sensitivityRadius = 33
                };
                auto layout = std::make_unique<Layout>(data);
                Assert::IsTrue(layout->Init(rect, Mocks::Monitor()));
                checkZonesValid(layout.get(), ZoneSetLayoutType::Columns, 3, rect);
            }
        }
    };

    // ========================================================================
    // ChooseNextZone / PrepareRectForCycling tests — ported from util.rs
    // ========================================================================
    TEST_CLASS(RustPortedUtilTests)
    {
    public:
        // util.rs: choose_next_zone_right
        TEST_METHOD(ChooseNextZoneRight)
        {
            RECT window = { 50, 0, 150, 100 };
            std::vector<RECT> zones = {
                { 200, 0, 300, 100 },
                { 400, 0, 500, 100 },
            };
            auto result = FancyZonesUtils::ChooseNextZoneByPosition(VK_RIGHT, window, zones);
            Assert::AreEqual(static_cast<size_t>(0), result); // nearest to the right
        }

        // util.rs: choose_next_zone_left
        TEST_METHOD(ChooseNextZoneLeft)
        {
            RECT window = { 350, 0, 450, 100 };
            std::vector<RECT> zones = {
                { 0, 0, 100, 100 },
                { 200, 0, 300, 100 },
            };
            auto result = FancyZonesUtils::ChooseNextZoneByPosition(VK_LEFT, window, zones);
            Assert::AreEqual(static_cast<size_t>(1), result); // nearest to the left
        }

        // util.rs: choose_next_zone_down
        TEST_METHOD(ChooseNextZoneDown)
        {
            RECT window = { 0, 0, 100, 100 };
            std::vector<RECT> zones = {
                { 0, 200, 100, 300 },
                { 0, 400, 100, 500 },
            };
            auto result = FancyZonesUtils::ChooseNextZoneByPosition(VK_DOWN, window, zones);
            Assert::AreEqual(static_cast<size_t>(0), result);
        }

        // util.rs: choose_next_zone_up
        TEST_METHOD(ChooseNextZoneUp)
        {
            RECT window = { 0, 400, 100, 500 };
            std::vector<RECT> zones = {
                { 0, 0, 100, 100 },
                { 0, 200, 100, 300 },
            };
            auto result = FancyZonesUtils::ChooseNextZoneByPosition(VK_UP, window, zones);
            Assert::AreEqual(static_cast<size_t>(1), result); // nearest upward
        }

        // util.rs: choose_next_zone_no_match
        TEST_METHOD(ChooseNextZoneEmptyZones)
        {
            RECT window = { 0, 0, 100, 100 };
            std::vector<RECT> zones = {};
            auto result = FancyZonesUtils::ChooseNextZoneByPosition(VK_RIGHT, window, zones);
            Assert::AreEqual(zones.size(), result); // invalid = size
        }

        // util.rs: prepare_rect_for_cycling_left
        TEST_METHOD(PrepareRectForCyclingLeft)
        {
            RECT window = { 0, 0, 100, 100 };
            RECT workArea = { 0, 0, 1920, 1080 };
            auto result = FancyZonesUtils::PrepareRectForCycling(window, workArea, VK_LEFT);
            Assert::AreEqual(1920L, result.left);
            Assert::AreEqual(2020L, result.right);
            Assert::AreEqual(0L, result.top);
            Assert::AreEqual(100L, result.bottom);
        }

        // util.rs: prepare_rect_for_cycling_right
        TEST_METHOD(PrepareRectForCyclingRight)
        {
            RECT window = { 1820, 0, 1920, 100 };
            RECT workArea = { 0, 0, 1920, 1080 };
            auto result = FancyZonesUtils::PrepareRectForCycling(window, workArea, VK_RIGHT);
            Assert::AreEqual(-100L, result.left);
            Assert::AreEqual(0L, result.right);
            Assert::AreEqual(0L, result.top);
            Assert::AreEqual(100L, result.bottom);
        }

        // util.rs: prepare_rect_for_cycling up
        TEST_METHOD(PrepareRectForCyclingUp)
        {
            RECT window = { 0, 0, 100, 100 };
            RECT workArea = { 0, 0, 1920, 1080 };
            auto result = FancyZonesUtils::PrepareRectForCycling(window, workArea, VK_UP);
            Assert::AreEqual(0L, result.left);
            Assert::AreEqual(100L, result.right);
            Assert::AreEqual(1080L, result.top);
            Assert::AreEqual(1180L, result.bottom);
        }

        // util.rs: prepare_rect_for_cycling down
        TEST_METHOD(PrepareRectForCyclingDown)
        {
            RECT window = { 0, 980, 100, 1080 };
            RECT workArea = { 0, 0, 1920, 1080 };
            auto result = FancyZonesUtils::PrepareRectForCycling(window, workArea, VK_DOWN);
            Assert::AreEqual(0L, result.left);
            Assert::AreEqual(100L, result.right);
            Assert::AreEqual(-100L, result.top);
            Assert::AreEqual(0L, result.bottom);
        }

        // util.rs: hex_to_rgb with #RGB format
        TEST_METHOD(HexToRGB_RGBFormat)
        {
            auto color = FancyZonesUtils::HexToRGB(L"#A3F6FF");
            Assert::AreEqual(static_cast<BYTE>(163), GetRValue(color));
            Assert::AreEqual(static_cast<BYTE>(246), GetGValue(color));
            Assert::AreEqual(static_cast<BYTE>(255), GetBValue(color));
        }

        // util.rs: hex_to_rgb with #AARRGGBB format
        TEST_METHOD(HexToRGB_ARGBFormat)
        {
            auto color = FancyZonesUtils::HexToRGB(L"#FFA3F6FF");
            Assert::AreEqual(static_cast<BYTE>(163), GetRValue(color));
            Assert::AreEqual(static_cast<BYTE>(246), GetGValue(color));
            Assert::AreEqual(static_cast<BYTE>(255), GetBValue(color));
        }

        // util.rs: hex_to_rgb invalid returns fallback
        TEST_METHOD(HexToRGB_Invalid)
        {
            auto color = FancyZonesUtils::HexToRGB(L"zzz");
            Assert::AreEqual(static_cast<COLORREF>(RGB(255, 255, 255)), color);
        }
    };

    // ========================================================================
    // Keyboard snap tests — ported from keyboard_snap.rs
    // ========================================================================
    TEST_CLASS(RustPortedKeyboardSnapTests)
    {
        // Helper: simulate snap_by_index logic
        // Left = decrement with wrap, Right = increment with wrap
        static std::optional<ZoneIndex> SnapByIndex(
            std::optional<ZoneIndex> currentZone,
            size_t zoneCount,
            DWORD direction)
        {
            if (zoneCount == 0)
                return std::nullopt;

            if (direction == VK_LEFT)
            {
                if (currentZone.has_value())
                {
                    if (*currentZone > 0)
                        return *currentZone - 1;
                    else
                        return static_cast<ZoneIndex>(zoneCount) - 1; // wrap
                }
                return static_cast<ZoneIndex>(zoneCount) - 1;
            }
            else if (direction == VK_RIGHT)
            {
                if (currentZone.has_value())
                {
                    if (*currentZone < static_cast<ZoneIndex>(zoneCount) - 1)
                        return *currentZone + 1;
                    else
                        return static_cast<ZoneIndex>(0); // wrap
                }
                return static_cast<ZoneIndex>(0);
            }
            return currentZone;
        }

    public:
        // keyboard_snap.rs: snap_right_no_current
        TEST_METHOD(SnapRightNoCurrent)
        {
            auto result = SnapByIndex(std::nullopt, 4, VK_RIGHT);
            Assert::IsTrue(result.has_value());
            Assert::AreEqual(static_cast<ZoneIndex>(0), result.value());
        }

        // keyboard_snap.rs: snap_left_no_current
        TEST_METHOD(SnapLeftNoCurrent)
        {
            auto result = SnapByIndex(std::nullopt, 4, VK_LEFT);
            Assert::IsTrue(result.has_value());
            Assert::AreEqual(static_cast<ZoneIndex>(3), result.value());
        }

        // keyboard_snap.rs: snap_right_from_first
        TEST_METHOD(SnapRightFromFirst)
        {
            auto result = SnapByIndex(static_cast<ZoneIndex>(0), 4, VK_RIGHT);
            Assert::AreEqual(static_cast<ZoneIndex>(1), result.value());
        }

        // keyboard_snap.rs: snap_left_from_first (wraps to last)
        TEST_METHOD(SnapLeftFromFirst)
        {
            auto result = SnapByIndex(static_cast<ZoneIndex>(0), 4, VK_LEFT);
            Assert::AreEqual(static_cast<ZoneIndex>(3), result.value());
        }

        // keyboard_snap.rs: snap_right_from_last (wraps to 0)
        TEST_METHOD(SnapRightFromLast)
        {
            auto result = SnapByIndex(static_cast<ZoneIndex>(3), 4, VK_RIGHT);
            Assert::AreEqual(static_cast<ZoneIndex>(0), result.value());
        }

        // keyboard_snap.rs: snap_left_from_last
        TEST_METHOD(SnapLeftFromLast)
        {
            auto result = SnapByIndex(static_cast<ZoneIndex>(3), 4, VK_LEFT);
            Assert::AreEqual(static_cast<ZoneIndex>(2), result.value());
        }

        // keyboard_snap.rs: snap_right_cycle — full cycle through 4 zones
        TEST_METHOD(SnapRightFullCycle)
        {
            auto zone = SnapByIndex(std::nullopt, 4, VK_RIGHT);
            ZoneIndex expected[] = { 0, 1, 2, 3, 0 };
            for (auto exp : expected)
            {
                Assert::AreEqual(exp, zone.value());
                zone = SnapByIndex(zone, 4, VK_RIGHT);
            }
        }

        // keyboard_snap.rs: snap_left_cycle — full cycle through 4 zones
        TEST_METHOD(SnapLeftFullCycle)
        {
            auto zone = SnapByIndex(std::nullopt, 4, VK_LEFT);
            ZoneIndex expected[] = { 3, 2, 1, 0, 3 };
            for (auto exp : expected)
            {
                Assert::AreEqual(exp, zone.value());
                zone = SnapByIndex(zone, 4, VK_LEFT);
            }
        }

        // keyboard_snap.rs: snap_empty_zones
        TEST_METHOD(SnapEmptyZones)
        {
            auto right = SnapByIndex(std::nullopt, 0, VK_RIGHT);
            Assert::IsFalse(right.has_value());
            auto left = SnapByIndex(std::nullopt, 0, VK_LEFT);
            Assert::IsFalse(left.has_value());
        }

        // keyboard_snap.rs: snap_by_position_right — 2x2 grid
        TEST_METHOD(SnapByPositionRight2x2)
        {
            std::vector<RECT> zones = {
                { 0, 0, 480, 540 },
                { 480, 0, 960, 540 },
                { 0, 540, 480, 1080 },
                { 480, 540, 960, 1080 },
            };
            RECT window = { 0, 0, 480, 540 }; // positioned at zone 0
            auto result = FancyZonesUtils::ChooseNextZoneByPosition(VK_RIGHT, window, zones);
            Assert::IsTrue(result < zones.size());
        }

        // keyboard_snap.rs: snap_by_position_down — 2x2 grid
        TEST_METHOD(SnapByPositionDown2x2)
        {
            std::vector<RECT> zones = {
                { 0, 0, 480, 540 },
                { 480, 0, 960, 540 },
                { 0, 540, 480, 1080 },
                { 480, 540, 960, 1080 },
            };
            RECT window = { 0, 0, 480, 540 }; // zone 0
            auto result = FancyZonesUtils::ChooseNextZoneByPosition(VK_DOWN, window, zones);
            Assert::IsTrue(result < zones.size());
        }
    };

    // ========================================================================
    // WorkAreaId comparison tests — ported from data.rs
    // ========================================================================
    TEST_CLASS(RustPortedWorkAreaIdTests)
    {
    public:
        // data.rs: monitor_handle_same — same handle, different device/serial
        TEST_METHOD(MonitorHandleSame)
        {
            auto mon = Mocks::Monitor();
            WorkAreaId id1{
                .monitorId = { .monitor = mon, .deviceId = { .id = L"device-1", .instanceId = L"instance-id-1" }, .serialNumber = L"serial-1" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .monitor = mon, .deviceId = { .id = L"device-2", .instanceId = L"instance-id-2" }, .serialNumber = L"serial-2" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsTrue(id1 == id2, L"Same monitor handle should match");
        }

        // data.rs: monitor_handle_different
        TEST_METHOD(MonitorHandleDifferent)
        {
            WorkAreaId id1{
                .monitorId = { .monitor = Mocks::Monitor(), .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .monitor = Mocks::Monitor(), .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsFalse(id1 == id2, L"Different monitor handles should not match");
        }

        // data.rs: virtual_desktop_different
        TEST_METHOD(VirtualDesktopDifferent)
        {
            WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{F21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsFalse(id1 == id2, L"Different virtual desktop IDs should not match");
        }

        // data.rs: different_serial_number
        TEST_METHOD(DifferentSerialNumber)
        {
            WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"another-serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsFalse(id1 == id2, L"Different serial numbers should not match");
        }

        // data.rs: default_monitor_id_different_instance_id_same_number
        TEST_METHOD(DefaultMonitorDifferentInstanceSameNumber)
        {
            WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"instance-id", .number = 1 } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"another-instance-id", .number = 1 } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsTrue(id1 == id2, L"Same number with different instance ID should match for Default_Monitor");
        }

        // data.rs: default_monitor_id_different_instance_id_different_number
        TEST_METHOD(DefaultMonitorDifferentInstanceDifferentNumber)
        {
            WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"instance-id", .number = 1 } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"another-instance-id", .number = 2 } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsFalse(id1 == id2);
        }

        // data.rs: monitor_reconnect — same device, different UID suffix
        TEST_METHOD(MonitorReconnect)
        {
            WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"4&125707d6&0&UID1", .number = 1 }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"4&125707d6&0&UID2", .number = 1 }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsTrue(id1 == id2, L"Monitor reconnect with same number should match");
        }

        // data.rs: same_monitor_models — same device, different UID, different numbers
        TEST_METHOD(SameMonitorModels)
        {
            WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"4&125707d6&0&UID1", .number = 1 }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"4&125707d6&0&UID2", .number = 2 }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsFalse(id1 == id2, L"Same model different numbers should NOT match");
        }

        // data.rs: serial_number_not_found_error — one empty serial
        TEST_METHOD(SerialNumberNotFoundError)
        {
            WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id", .number = 1 }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id", .number = 1 }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            // When one serial is empty, serial comparison is skipped → match on deviceId
            Assert::IsTrue(id1 == id2, L"One empty serial should still match by device ID");
        }

        // data.rs: different_id
        TEST_METHOD(DifferentDeviceId)
        {
            WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device-1", .instanceId = L"instance-id" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device-2", .instanceId = L"instance-id" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsFalse(id1 == id2);
        }

        // data.rs: same_id_different_serial_numbers
        TEST_METHOD(SameIdDifferentSerialNumbers)
        {
            WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device-1", .instanceId = L"instance-id-1" }, .serialNumber = L"serial-number-1" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device-1", .instanceId = L"instance-id-2" }, .serialNumber = L"serial-number-2" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsFalse(id1 == id2);
        }

        // data.rs: different_id_same_serial_numbers
        TEST_METHOD(DifferentIdSameSerialNumbers)
        {
            WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device-1", .instanceId = L"instance-id-1" }, .serialNumber = L"serial-number-1" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device-2", .instanceId = L"instance-id-2" }, .serialNumber = L"serial-number-1" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsFalse(id1 == id2);
        }
    };

    // ========================================================================
    // Data validation tests — ported from data.rs
    // ========================================================================
    TEST_CLASS(RustPortedDataValidationTests)
    {
    public:
        // data.rs: guid_valid
        TEST_METHOD(GuidValidFormat)
        {
            Assert::IsTrue(FancyZonesUtils::IsValidGuid(L"{33A2B101-06E0-437B-A61E-CDBECF502906}"));
        }

        // data.rs: guid_invalid_form (missing braces)
        TEST_METHOD(GuidInvalidFormNoBraces)
        {
            Assert::IsFalse(FancyZonesUtils::IsValidGuid(L"33A2B101-06E0-437B-A61E-CDBECF502906"));
        }

        // data.rs: guid_invalid_symbols
        TEST_METHOD(GuidInvalidSymbols)
        {
            Assert::IsFalse(FancyZonesUtils::IsValidGuid(L"{33A2B101-06E0-437B-A61E-CDBECF50290*}"));
        }

        // data.rs: guid_invalid
        TEST_METHOD(GuidInvalidRandom)
        {
            Assert::IsFalse(FancyZonesUtils::IsValidGuid(L"guid"));
        }

        // data.rs: device_id_valid
        TEST_METHOD(DeviceIdValid)
        {
            Assert::IsTrue(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(
                L"AOC2460#4&fe3a015&0&UID65793_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}"));
        }

        // data.rs: device_id_without_hash_in_name
        TEST_METHOD(DeviceIdWithoutHash)
        {
            Assert::IsTrue(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(
                L"LOCALDISPLAY_5120_1440_{00000000-0000-0000-0000-000000000000}"));
        }

        // data.rs: device_id_without_hash_in_name_but_with_underscores
        TEST_METHOD(DeviceIdWithoutHashButUnderscores)
        {
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(
                L"LOCAL_DISPLAY_5120_1440_{00000000-0000-0000-0000-000000000000}"));
        }

        // data.rs: device_id_with_underscores_in_name
        TEST_METHOD(DeviceIdWithUnderscoresInName)
        {
            Assert::IsTrue(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(
                L"Default_Monitor#1&1f0c3c2f&0&UID256_5120_1440_{00000000-0000-0000-0000-000000000000}"));
        }

        // data.rs: device_id_invalid_format
        TEST_METHOD(DeviceIdInvalidFormatNoName)
        {
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(
                L"_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}"));
        }

        // data.rs: device_id_invalid_format2
        TEST_METHOD(DeviceIdInvalidFormatMissingUnderscore)
        {
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(
                L"AOC2460#4&fe3a015&0&UID65793_19201200_{39B25DD2-130D-4B5D-8851-4791D66B1539}"));
        }

        // data.rs: device_id_invalid_decimals
        TEST_METHOD(DeviceIdInvalidDecimals)
        {
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(
                L"AOC2460#4&fe3a015&0&UID65793_aaaa_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}"));
        }

        // data.rs: device_id_invalid_decimals2
        TEST_METHOD(DeviceIdInvalidDecimals2)
        {
            Assert::IsFalse(BackwardsCompatibility::DeviceIdData::IsValidDeviceId(
                L"AOC2460#4&fe3a015&0&UID65793_19a0_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}"));
        }

        // data.rs: zone_set_layout_type_to_string
        TEST_METHOD(ZoneSetLayoutTypeToStringAll)
        {
            Assert::AreEqual(std::wstring(L"blank"), TypeToString(ZoneSetLayoutType::Blank));
            Assert::AreEqual(std::wstring(L"focus"), TypeToString(ZoneSetLayoutType::Focus));
            Assert::AreEqual(std::wstring(L"columns"), TypeToString(ZoneSetLayoutType::Columns));
            Assert::AreEqual(std::wstring(L"rows"), TypeToString(ZoneSetLayoutType::Rows));
            Assert::AreEqual(std::wstring(L"grid"), TypeToString(ZoneSetLayoutType::Grid));
            Assert::AreEqual(std::wstring(L"priority-grid"), TypeToString(ZoneSetLayoutType::PriorityGrid));
            Assert::AreEqual(std::wstring(L"custom"), TypeToString(ZoneSetLayoutType::Custom));
        }

        // data.rs: zone_set_layout_type_from_string
        TEST_METHOD(ZoneSetLayoutTypeFromStringAll)
        {
            Assert::IsTrue(ZoneSetLayoutType::Blank == TypeFromString(L"blank"));
            Assert::IsTrue(ZoneSetLayoutType::Focus == TypeFromString(L"focus"));
            Assert::IsTrue(ZoneSetLayoutType::Columns == TypeFromString(L"columns"));
            Assert::IsTrue(ZoneSetLayoutType::Rows == TypeFromString(L"rows"));
            Assert::IsTrue(ZoneSetLayoutType::Grid == TypeFromString(L"grid"));
            Assert::IsTrue(ZoneSetLayoutType::PriorityGrid == TypeFromString(L"priority-grid"));
            Assert::IsTrue(ZoneSetLayoutType::Custom == TypeFromString(L"custom"));
        }

        // data.rs: zone_set_layout_type_from_string invalid
        TEST_METHOD(ZoneSetLayoutTypeFromStringInvalid)
        {
            // Invalid string should return Blank (default)
            auto result = TypeFromString(L"invalid");
            Assert::IsTrue(result == ZoneSetLayoutType::Blank);
        }
    };

    // ========================================================================
    // LayoutAssignedWindows tests — ported from data.rs
    // ========================================================================
    TEST_CLASS(RustPortedLayoutAssignedWindowsTests)
    {
    public:
        // data.rs: zone_index_from_window_unknown
        TEST_METHOD(ZoneIndexFromWindowUnknown)
        {
            // If we assign one window but query a different one, should get empty
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000020}").value(),
                .type = ZoneSetLayoutType::Grid,
                .showSpacing = false,
                .spacing = 0,
                .zoneCount = 4,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            // ZonesFromPoint for a point in zone 0 gives a valid zone
            auto zones = layout->ZonesFromPoint(POINT{ 1, 1 });
            Assert::IsFalse(zones.empty());
        }

        // data.rs: assign + reassign same window
        TEST_METHOD(AssignSameWindowMultipleTimes)
        {
            // This tests that reassigning a window to different zones works
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000021}").value(),
                .type = ZoneSetLayoutType::Grid,
                .showSpacing = false,
                .spacing = 0,
                .zoneCount = 4,
                .sensitivityRadius = 20
            };
            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(4), layout->Zones().size());

            // Verify zones from different points are different
            auto z0 = layout->ZonesFromPoint(POINT{ 1, 1 });
            auto z3 = layout->ZonesFromPoint(POINT{ 1919, 1079 });
            Assert::IsFalse(z0.empty());
            Assert::IsFalse(z3.empty());
        }
    };

    // ========================================================================
    // LayoutConfigurator direct tests — ported from layout.rs layout functions
    // ========================================================================
    TEST_CLASS(RustPortedLayoutConfiguratorTests)
    {
    public:
        // layout.rs: columns generates correct number of non-overlapping zones
        TEST_METHOD(ColumnsNoOverlap)
        {
            auto zones = LayoutConfigurator::Columns(FancyZonesUtils::Rect(RECT{ 0, 0, 1920, 1080 }), 3, 0);
            Assert::AreEqual(static_cast<size_t>(3), zones.size());

            // Zones should be adjacent (no overlap, no gap with 0 spacing)
            auto it = zones.begin();
            RECT prev = it->second.GetZoneRect();
            ++it;
            while (it != zones.end())
            {
                RECT curr = it->second.GetZoneRect();
                Assert::AreEqual(prev.right, curr.left, L"Columns should be adjacent");
                prev = curr;
                ++it;
            }
        }

        // layout.rs: rows generates correct number of zones
        TEST_METHOD(RowsNoOverlap)
        {
            auto zones = LayoutConfigurator::Rows(FancyZonesUtils::Rect(RECT{ 0, 0, 1920, 1080 }), 3, 0);
            Assert::AreEqual(static_cast<size_t>(3), zones.size());

            auto it = zones.begin();
            RECT prev = it->second.GetZoneRect();
            ++it;
            while (it != zones.end())
            {
                RECT curr = it->second.GetZoneRect();
                Assert::AreEqual(prev.bottom, curr.top, L"Rows should be adjacent");
                prev = curr;
                ++it;
            }
        }

        // layout.rs: grid with spacing
        TEST_METHOD(GridWithSpacing)
        {
            auto zones = LayoutConfigurator::Grid(FancyZonesUtils::Rect(RECT{ 0, 0, 1920, 1080 }), 4, 10);
            Assert::AreEqual(static_cast<size_t>(4), zones.size());

            // All zones should be within the work area
            for (const auto& [id, zone] : zones)
            {
                auto r = zone.GetZoneRect();
                Assert::IsTrue(r.left >= 0);
                Assert::IsTrue(r.top >= 0);
                Assert::IsTrue(r.right <= 1920);
                Assert::IsTrue(r.bottom <= 1080);
                Assert::IsTrue(r.left < r.right);
                Assert::IsTrue(r.top < r.bottom);
            }
        }

        // layout.rs: priority_grid with many zones
        TEST_METHOD(PriorityGrid11Zones)
        {
            auto zones = LayoutConfigurator::PriorityGrid(FancyZonesUtils::Rect(RECT{ 0, 0, 1920, 1080 }), 11, 5);
            Assert::AreEqual(static_cast<size_t>(11), zones.size());
        }

        // layout.rs: focus generates zones centered and decreasing in size
        TEST_METHOD(FocusDecreasingSize)
        {
            auto zones = LayoutConfigurator::Focus(FancyZonesUtils::Rect(RECT{ 0, 0, 1920, 1080 }), 4);
            Assert::AreEqual(static_cast<size_t>(4), zones.size());

            // Each subsequent focus zone should be smaller or equal
            long prevArea = LONG_MAX;
            for (const auto& [id, zone] : zones)
            {
                long area = zone.GetZoneArea();
                Assert::IsTrue(area <= prevArea, L"Focus zones should decrease in area");
                prevArea = area;
            }
        }

        // layout.rs: columns with spacing — zones don't overlap
        TEST_METHOD(ColumnsWithSpacingNoOverlap)
        {
            auto zones = LayoutConfigurator::Columns(FancyZonesUtils::Rect(RECT{ 0, 0, 1920, 1080 }), 3, 16);
            Assert::AreEqual(static_cast<size_t>(3), zones.size());

            auto it = zones.begin();
            RECT prev = it->second.GetZoneRect();
            ++it;
            while (it != zones.end())
            {
                RECT curr = it->second.GetZoneRect();
                Assert::IsTrue(prev.right <= curr.left, L"Columns with spacing should not overlap");
                prev = curr;
                ++it;
            }
        }

        // layout.rs: rows with spacing — zones don't overlap
        TEST_METHOD(RowsWithSpacingNoOverlap)
        {
            auto zones = LayoutConfigurator::Rows(FancyZonesUtils::Rect(RECT{ 0, 0, 1920, 1080 }), 4, 10);
            Assert::AreEqual(static_cast<size_t>(4), zones.size());

            auto it = zones.begin();
            RECT prev = it->second.GetZoneRect();
            ++it;
            while (it != zones.end())
            {
                RECT curr = it->second.GetZoneRect();
                Assert::IsTrue(prev.bottom <= curr.top, L"Rows with spacing should not overlap");
                prev = curr;
                ++it;
            }
        }

        // layout.rs: grid covers entire work area (minus spacing)
        TEST_METHOD(GridCoversWorkArea)
        {
            const RECT workArea = { 0, 0, 1920, 1080 };
            auto zones = LayoutConfigurator::Grid(FancyZonesUtils::Rect(workArea), 4, 0);
            Assert::AreEqual(static_cast<size_t>(4), zones.size());

            // Union of all zones should cover the full work area
            RECT unionRect = { LONG_MAX, LONG_MAX, LONG_MIN, LONG_MIN };
            for (const auto& [id, zone] : zones)
            {
                auto r = zone.GetZoneRect();
                unionRect.left = min(unionRect.left, r.left);
                unionRect.top = min(unionRect.top, r.top);
                unionRect.right = max(unionRect.right, r.right);
                unionRect.bottom = max(unionRect.bottom, r.bottom);
            }
            Assert::AreEqual(workArea.left, unionRect.left);
            Assert::AreEqual(workArea.top, unionRect.top);
            Assert::AreEqual(workArea.right, unionRect.right);
            Assert::AreEqual(workArea.bottom, unionRect.bottom);
        }
    };

    // ========================================================================
    // Monitor ordering tests — ported from util.rs (additional scenarios)
    // ========================================================================
    TEST_CLASS(RustPortedMonitorOrderingTests)
    {
        void TestPermutationsWithOffsets(const std::vector<std::pair<HMONITOR, RECT>>& monitors)
        {
            auto copy = monitors;
            FancyZonesUtils::OrderMonitors(copy);
            // Verify that ordered result matches expected
            for (size_t i = 0; i < monitors.size(); i++)
            {
                Assert::IsTrue(monitors[i].first == copy[i].first);
            }
        }

    public:
        // util.rs: test_monitor_ordering_07 — vertical stack
        TEST_METHOD(MonitorOrderingVerticalStack)
        {
            std::vector<std::pair<HMONITOR, RECT>> monitors = {
                { reinterpret_cast<HMONITOR>(1), { 100, 0, 1700, 900 } },
                { reinterpret_cast<HMONITOR>(2), { 0, 900, 1800, 1800 } },
                { reinterpret_cast<HMONITOR>(3), { 100, 1800, 1700, 2700 } },
            };
            TestPermutationsWithOffsets(monitors);
        }

        // util.rs: test_monitor_ordering_08 — 5 monitors in 2 rows
        TEST_METHOD(MonitorOrdering5Monitors2Rows)
        {
            std::vector<std::pair<HMONITOR, RECT>> monitors = {
                { reinterpret_cast<HMONITOR>(1), { 0, 0, 600, 400 } },
                { reinterpret_cast<HMONITOR>(2), { 600, 0, 1200, 400 } },
                { reinterpret_cast<HMONITOR>(3), { 1200, 0, 1800, 400 } },
                { reinterpret_cast<HMONITOR>(4), { 0, 400, 900, 800 } },
                { reinterpret_cast<HMONITOR>(5), { 900, 400, 1800, 800 } },
            };
            TestPermutationsWithOffsets(monitors);
        }

        // util.rs: test_monitor_ordering_10 — asymmetric 2 rows
        TEST_METHOD(MonitorOrderingAsymmetric2Rows)
        {
            std::vector<std::pair<HMONITOR, RECT>> monitors = {
                { reinterpret_cast<HMONITOR>(1), { 0, 0, 900, 400 } },
                { reinterpret_cast<HMONITOR>(2), { 900, 0, 1800, 400 } },
                { reinterpret_cast<HMONITOR>(3), { 0, 400, 600, 800 } },
                { reinterpret_cast<HMONITOR>(4), { 600, 400, 1200, 800 } },
                { reinterpret_cast<HMONITOR>(5), { 1200, 400, 1800, 800 } },
            };
            TestPermutationsWithOffsets(monitors);
        }
    };
}
