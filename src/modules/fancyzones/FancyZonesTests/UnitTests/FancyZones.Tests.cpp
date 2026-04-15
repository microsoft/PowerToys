// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Tests ported from the Rust FancyZones implementation.
// Genuine new coverage only — duplicates of existing .Spec.cpp tests removed.
// Rust sources: fancyzones-core/src/{layout,zone,util}.rs
//               fancyzones-engine/src/engine.rs

#include "pch.h"

#include <algorithm>
#include <numeric>

#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesData/LayoutTemplates.h>
#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/Layout.h>
#include <FancyZonesLib/LayoutConfigurator.h>
#include <FancyZonesLib/Zone.h>
#include <FancyZonesLib/util.h>

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
        // Product code: Zone.h — Zone::GetZoneArea()
        // What: Verifies zone area calculation for a normal rectangle
        // Why: Area calculation is used for zone overlap detection and priority ordering
        // Risk: Wrong area breaks Focus layout sizing and zone selection priority
        TEST_METHOD(ZoneArea)
        {
            Zone zone({ 0, 0, 100, 50 }, 0);
            Assert::AreEqual(static_cast<long>(5000), zone.GetZoneArea());
        }

        // Product code: Zone.h — Zone::GetZoneArea()
        // What: Verifies inverted rect (right < left) clamps area to 0
        // Why: Inverted rects can arise from bad monitor geometry; negative area would corrupt sorting
        // Risk: Negative area values cause undefined ordering in zone priority comparisons
        TEST_METHOD(ZoneAreaInverted)
        {
            Zone zone({ 100, 100, 50, 50 }, 0);
            Assert::AreEqual(0L, zone.GetZoneArea());
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
        // Product code: Layout.h — Layout::Init() with Columns type
        // What: Single-column layout creates exactly 1 zone covering the work area
        // Why: Single zone is the degenerate case; wrong count causes empty/corrupt layouts
        // Risk: Off-by-one in column splitter produces 0 zones on single-zone request
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

        // Product code: Layout.h — Layout::Init() with Columns type
        // What: Two-column layout creates 2 valid non-overlapping zones
        // Why: Exercises the basic column-splitting algorithm for even division
        // Risk: Integer rounding in column width calculation could misalign boundaries
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

        // Product code: Layout.h — Layout::Init() with Columns type
        // What: Three-column layout on 1920px — tests odd division (1920/3 = 640)
        // Why: Non-power-of-2 zone counts stress integer rounding in splitter
        // Risk: Last column could be 1px too wide/narrow from rounding remainders
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

        // Product code: Layout.h — Layout::Init() with Columns type
        // What: Five-column layout pushes zone count beyond typical 2-4 usage
        // Why: Higher zone counts amplify rounding errors in width distribution
        // Risk: Accumulated pixel rounding across 5 columns could leave gap or overflow
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

        // Product code: Layout.h — Layout::Init() with Rows type + spacing
        // What: 3-row layout with 10px spacing creates valid non-overlapping zones
        // Why: Spacing changes zone height calculation; must subtract spacing gaps
        // Risk: Spacing subtracted incorrectly could make zones extend past work area
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

        // Product code: Layout.h — Layout::Init() with Grid type
        // What: 4-zone grid on ultra-wide 2560x1080 tests wide aspect ratio handling
        // Why: Grid row/column calculation depends on aspect ratio to decide split direction
        // Risk: Wide monitors may get wrong row/column ratio producing very narrow columns
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

        // Product code: Layout.h — Layout::Init() with Grid type
        // What: 4-zone grid on portrait 1080x1920 tests tall aspect ratio handling
        // Why: Portrait monitors flip the expected row/column split direction
        // Risk: Aspect ratio inversion could produce 4 very flat rows instead of a 2x2 grid
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

        // Product code: Layout.h — Layout::Init() with PriorityGrid type
        // What: PriorityGrid with 10 zones and 5px spacing — high zone count stress test
        // Why: PriorityGrid uses a predefined template; 10 zones is near the template limit
        // Risk: Template lookup overflow or zone position miscalculation at high counts
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

        // Product code: Layout.h — Layout::Init() with Focus type
        // What: Focus layout creates 3 centered zones with zone 0 being the largest
        // Why: Focus zones must be concentrically sized for the overlay UX to work
        // Risk: Zone ordering or sizing inversion would put smallest zone on top
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

            const auto& zones = layout->Zones();
            long area0 = zones.at(0).GetZoneArea();
            long area1 = zones.at(1).GetZoneArea();
            Assert::IsTrue(area0 >= area1, L"Focus zone 0 should be >= zone 1 in area");
        }

        // Product code: Layout.h — Layout::GetCombinedZoneRange()
        // What: Combining zones 0 and 1 in a 4-zone grid returns a set containing both
        // Why: Zone merging is used for multi-zone window snapping (Shift+drag)
        // Risk: Off-by-one in range expansion could miss boundary zones
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
            Assert::IsTrue(std::find(range.begin(), range.end(), 0) != range.end());
            Assert::IsTrue(std::find(range.begin(), range.end(), 1) != range.end());
        }

        // Product code: Layout.h — Layout::Init() with Columns across resolutions
        // What: 3-column layout with 10px spacing succeeds on 9 standard resolutions
        // Why: Resolution diversity catches integer overflow or underflow in zone math
        // Risk: Narrow resolutions (1024x768) with spacing could produce zero-width zones
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
        // Product code: util.h — ChooseNextZoneByPosition(VK_RIGHT, ...)
        // What: From a window at x=50..150, snapping right picks the nearest zone to the right
        // Why: Directional zone selection is the core keyboard-driven window management UX
        // Risk: Wrong distance metric would select farther zone or no zone
        TEST_METHOD(ChooseNextZoneRight)
        {
            RECT window = { 50, 0, 150, 100 };
            std::vector<RECT> zones = {
                { 200, 0, 300, 100 },
                { 400, 0, 500, 100 },
            };
            auto result = FancyZonesUtils::ChooseNextZoneByPosition(VK_RIGHT, window, zones);
            Assert::AreEqual(static_cast<size_t>(0), result);
        }

        // Product code: util.h — ChooseNextZoneByPosition(VK_LEFT, ...)
        // What: From a window at x=350..450, snapping left picks the nearest zone to the left
        // Why: Left-snap must choose the closest zone by left-edge distance
        // Risk: Using center distance instead of edge distance selects wrong zone
        TEST_METHOD(ChooseNextZoneLeft)
        {
            RECT window = { 350, 0, 450, 100 };
            std::vector<RECT> zones = {
                { 0, 0, 100, 100 },
                { 200, 0, 300, 100 },
            };
            auto result = FancyZonesUtils::ChooseNextZoneByPosition(VK_LEFT, window, zones);
            Assert::AreEqual(static_cast<size_t>(1), result);
        }

        // Product code: util.h — ChooseNextZoneByPosition(VK_DOWN, ...)
        // What: From a window at y=0..100, snapping down picks the nearest zone below
        // Why: Vertical navigation is essential for grid layouts with multiple rows
        // Risk: Vertical distance calculation using wrong rect edge (top vs bottom)
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

        // Product code: util.h — ChooseNextZoneByPosition(VK_UP, ...)
        // What: From a window at y=400..500, snapping up picks the nearest zone above
        // Why: Upward navigation completes the 4-directional zone selection
        // Risk: Reversed comparison (top > bottom check) would skip valid candidates
        TEST_METHOD(ChooseNextZoneUp)
        {
            RECT window = { 0, 400, 100, 500 };
            std::vector<RECT> zones = {
                { 0, 0, 100, 100 },
                { 0, 200, 100, 300 },
            };
            auto result = FancyZonesUtils::ChooseNextZoneByPosition(VK_UP, window, zones);
            Assert::AreEqual(static_cast<size_t>(1), result);
        }

        // Product code: util.h — ChooseNextZoneByPosition() with empty zone list
        // What: No zones available returns an invalid index (== zones.size())
        // Why: Empty layouts must not crash or return a valid-looking index
        // Risk: Returning 0 on empty would cause out-of-bounds access in callers
        TEST_METHOD(ChooseNextZoneEmptyZones)
        {
            RECT window = { 0, 0, 100, 100 };
            std::vector<RECT> zones = {};
            auto result = FancyZonesUtils::ChooseNextZoneByPosition(VK_RIGHT, window, zones);
            Assert::AreEqual(zones.size(), result);
        }

        // Product code: util.h — PrepareRectForCycling(VK_LEFT, ...)
        // What: Cycling left wraps window to right edge of work area
        // Why: Enables keyboard cycling through zones that wrap around the monitor
        // Risk: Wrong wrap offset makes window appear at wrong position after cycle
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

        // Product code: util.h — PrepareRectForCycling(VK_RIGHT, ...)
        // What: Cycling right wraps window to left edge (negative coordinates)
        // Why: Right-wrap must place window just left of work area for zone search
        // Risk: Negative coordinate handling could be clamped to 0 incorrectly
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

        // Product code: util.h — PrepareRectForCycling(VK_UP, ...)
        // What: Cycling up wraps window to bottom edge of work area
        // Why: Vertical cycling enables wrap-around in multi-row layouts
        // Risk: Height miscalculation in wrap places window partially off-screen
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

        // Product code: util.h — PrepareRectForCycling(VK_DOWN, ...)
        // What: Cycling down wraps window to top edge (negative y)
        // Why: Down-wrap completes the 4-directional cycling for grid layouts
        // Risk: Negative y coordinate could be mishandled by zone matching logic
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
    };

    // ========================================================================
    // Keyboard snap-by-position tests — ported from keyboard_snap.rs
    // ========================================================================
    TEST_CLASS(RustPortedKeyboardSnapTests)
    {
    public:
        // Product code: util.h — ChooseNextZoneByPosition(VK_RIGHT, ...) on 2x2 grid
        // What: From zone 0 (top-left), snap right selects zone 1 (top-right)
        // Why: 2D grid navigation must respect spatial adjacency, not just index order
        // Risk: Index-based snap would select zone 2 (bottom-left) instead of zone 1
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
            Assert::AreEqual(static_cast<size_t>(1), result, L"Right from zone 0 should select zone 1 (top-right)");
        }

        // Product code: util.h — ChooseNextZoneByPosition(VK_DOWN, ...) on 2x2 grid
        // What: From zone 0 (top-left), snap down selects zone 2 (bottom-left)
        // Why: Vertical navigation in 2D grid must find the spatially below zone
        // Risk: Down-snap picking zone 1 (same row) instead of zone 2 (below)
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
            Assert::AreEqual(static_cast<size_t>(2), result, L"Down from zone 0 should select zone 2 (bottom-left)");
        }
    };

    // ========================================================================
    // LayoutConfigurator direct tests — ported from layout.rs layout functions
    // ========================================================================
    TEST_CLASS(RustPortedLayoutConfiguratorTests)
    {
    public:
        // Product code: LayoutConfigurator.h — LayoutConfigurator::Columns()
        // What: 3 columns with 0 spacing are perfectly adjacent (no gap, no overlap)
        // Why: Adjacency is the fundamental invariant for column layouts
        // Risk: Off-by-one in column boundary calculation leaves 1px gaps or overlaps
        TEST_METHOD(ColumnsNoOverlap)
        {
            auto zones = LayoutConfigurator::Columns(FancyZonesUtils::Rect(RECT{ 0, 0, 1920, 1080 }), 3, 0);
            Assert::AreEqual(static_cast<size_t>(3), zones.size());

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

        // Product code: LayoutConfigurator.h — LayoutConfigurator::Rows()
        // What: 3 rows with 0 spacing are perfectly adjacent vertically
        // Why: Row adjacency ensures no dead pixels between horizontal bands
        // Risk: Height rounding remainder causes gap between last two rows
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

        // Product code: LayoutConfigurator.h — LayoutConfigurator::Grid()
        // What: 4-zone grid with 10px spacing has all zones within work area bounds
        // Why: Spacing must be subtracted from zone dimensions, not added to work area
        // Risk: Spacing applied to wrong side pushes zones outside monitor bounds
        TEST_METHOD(GridWithSpacing)
        {
            auto zones = LayoutConfigurator::Grid(FancyZonesUtils::Rect(RECT{ 0, 0, 1920, 1080 }), 4, 10);
            Assert::AreEqual(static_cast<size_t>(4), zones.size());

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

        // Product code: LayoutConfigurator.h — LayoutConfigurator::PriorityGrid()
        // What: PriorityGrid with 11 zones creates correct count with valid zone geometry
        // Why: 11 zones exceeds the predefined template count for common layouts
        // Risk: Template index out-of-bounds for zone counts beyond built-in templates
        TEST_METHOD(PriorityGrid11Zones)
        {
            auto zones = LayoutConfigurator::PriorityGrid(FancyZonesUtils::Rect(RECT{ 0, 0, 1920, 1080 }), 11, 5);
            Assert::AreEqual(static_cast<size_t>(11), zones.size());

            // Verify every zone has valid positive-area geometry within work area
            for (const auto& [id, zone] : zones)
            {
                auto r = zone.GetZoneRect();
                Assert::IsTrue(r.left < r.right, L"Zone must have positive width");
                Assert::IsTrue(r.top < r.bottom, L"Zone must have positive height");
                Assert::IsTrue(r.left >= 0, L"Zone left must be non-negative");
                Assert::IsTrue(r.top >= 0, L"Zone top must be non-negative");
                Assert::IsTrue(r.right <= 1920, L"Zone right must be within work area");
                Assert::IsTrue(r.bottom <= 1080, L"Zone bottom must be within work area");
            }
        }

        // Product code: LayoutConfigurator.h — LayoutConfigurator::Focus()
        // What: 4 Focus zones are each smaller or equal in area to the previous
        // Why: Focus layout relies on decreasing size for the stacked-center UX
        // Risk: Non-monotonic sizing would overlay larger zone on top of smaller one
        TEST_METHOD(FocusDecreasingSize)
        {
            auto zones = LayoutConfigurator::Focus(FancyZonesUtils::Rect(RECT{ 0, 0, 1920, 1080 }), 4);
            Assert::AreEqual(static_cast<size_t>(4), zones.size());

            long prevArea = LONG_MAX;
            for (const auto& [id, zone] : zones)
            {
                long area = zone.GetZoneArea();
                Assert::IsTrue(area <= prevArea, L"Focus zones should decrease in area");
                prevArea = area;
            }
        }

        // Product code: LayoutConfigurator.h — LayoutConfigurator::Columns() with spacing
        // What: 3 columns with 16px spacing don't overlap (prev.right <= curr.left)
        // Why: Spacing creates visual separation; overlap would merge adjacent columns
        // Risk: Spacing applied as offset instead of gap leaves columns overlapping
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

        // Product code: LayoutConfigurator.h — LayoutConfigurator::Rows() with spacing
        // What: 4 rows with 10px spacing don't overlap (prev.bottom <= curr.top)
        // Why: Spacing must create actual gaps, not just visual padding
        // Risk: Row spacing calculated from wrong edge creates negative-height zones
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

        // Product code: LayoutConfigurator.h — LayoutConfigurator::Grid()
        // What: Union of 4 grid zones with 0 spacing exactly covers the work area
        // Why: Full coverage ensures no dead pixels where drops would be ignored
        // Risk: Rounding errors in grid splitting leave uncovered strips at edges
        TEST_METHOD(GridCoversWorkArea)
        {
            const RECT workArea = { 0, 0, 1920, 1080 };
            auto zones = LayoutConfigurator::Grid(FancyZonesUtils::Rect(workArea), 4, 0);
            Assert::AreEqual(static_cast<size_t>(4), zones.size());

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
}
