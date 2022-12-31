#include "pch.h"

#include <filesystem>

#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include <FancyZonesLib/ZoneIndexSetBitmask.h>
#include <FancyZonesLib/Layout.h>
#include <FancyZonesLib/Settings.h>

#include "Util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace FancyZonesDataTypes;

namespace FancyZonesUnitTests
{
    TEST_CLASS (LayoutUnitTests)
    {
        const LayoutData m_data
        {
            .uuid = FancyZonesUtils::GuidFromString(L"{F762BAD6-DAA1-4997-9497-E11DFEB72F21}").value(),
            .type = ZoneSetLayoutType::Grid,
            .showSpacing = true,
            .spacing = 17,
            .zoneCount = 4,
            .sensitivityRadius = 33
        };
        std::unique_ptr<Layout> m_layout{};

        TEST_METHOD_INITIALIZE(Init)
        {
            m_layout = std::make_unique<Layout>(m_data);
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove_all(CustomLayouts::CustomLayoutsFileName());
        }

        void compareZones(const Zone& expected, const Zone& actual)
        {
            Assert::AreEqual(expected.Id(), actual.Id());
            Assert::AreEqual(expected.GetZoneRect().left, actual.GetZoneRect().left);
            Assert::AreEqual(expected.GetZoneRect().right, actual.GetZoneRect().right);
            Assert::AreEqual(expected.GetZoneRect().top, actual.GetZoneRect().top);
            Assert::AreEqual(expected.GetZoneRect().bottom, actual.GetZoneRect().bottom);
        }

        void saveCustomLayout(const std::vector<RECT>& zones)
        {
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            json::JsonObject canvasLayoutJson{};
            canvasLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::UuidID, json::value(FancyZonesUtils::GuidToString(m_data.uuid).value()));
            canvasLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::NameID, json::value(L"Custom canvas layout"));
            canvasLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::TypeID, json::value(NonLocalizable::CustomLayoutsIds::CanvasID));

            json::JsonObject info{};
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::RefWidthID, json::value(1920));
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::RefHeightID, json::value(1080));

            json::JsonArray zonesArray{};
            for (const auto& zoneRect : zones)
            {
                json::JsonObject zone{};
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::XID, json::value(zoneRect.left));
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::YID, json::value(zoneRect.top));
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::WidthID, json::value(zoneRect.right - zoneRect.left));
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::HeightID, json::value(zoneRect.bottom - zoneRect.top));
                zonesArray.Append(zone);
            }

            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::ZonesID, zonesArray);
            canvasLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::InfoID, info);
            layoutsArray.Append(canvasLayoutJson);
            root.SetNamedValue(NonLocalizable::CustomLayoutsIds::CustomLayoutsArrayID, layoutsArray);
            json::to_file(CustomLayouts::CustomLayoutsFileName(), root);

            CustomLayouts::instance().LoadData();
        }

    public:
        TEST_METHOD (TestCreateLayout)
        {
            CustomAssert::AreEqual(m_layout->Id(), m_data.uuid);
            CustomAssert::AreEqual(m_layout->Type(), m_data.type);
        }

        TEST_METHOD (EmptyZones)
        {
            auto zones = m_layout->Zones();
            Assert::AreEqual((size_t)0, zones.size());
        }

        TEST_METHOD (ZoneFromPointEmpty)
        {
            auto actual = m_layout->ZonesFromPoint(POINT{ 0, 0 });
            Assert::IsTrue(actual.size() == 0);
        }

        TEST_METHOD (ZoneFromPointInner)
        {
            LayoutData data = m_data;
            data.spacing = 0;
            auto layout = std::make_unique<Layout>(data);
            layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor());

            auto actual = layout->ZonesFromPoint(POINT{ 1, 1 });
            Assert::IsTrue(actual.size() == 1);
        }

        TEST_METHOD (ZoneFromPointBorder)
        {
            LayoutData data = m_data;
            data.spacing = 0;
            auto layout = std::make_unique<Layout>(data);
            layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor());

            Assert::IsTrue(layout->ZonesFromPoint(POINT{ 0, 0 }).size() == 1);
            Assert::IsTrue(layout->ZonesFromPoint(POINT{ 1920, 1080 }).size() == 0);
        }

        TEST_METHOD (ZoneFromPointOuter)
        {
            m_layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor());

            auto actual = m_layout->ZonesFromPoint(POINT{ 1921, 1080 });
            Assert::IsTrue(actual.size() == 0);
        }

        TEST_METHOD (ZoneFromPointOverlapping)
        {
            // prepare layout with overlapping zones
            saveCustomLayout({ RECT{ 0, 0, 100, 100 }, RECT{ 10, 10, 90, 90 }, RECT{ 10, 10, 150, 150 }, RECT{ 10, 10, 50, 50 } });

            LayoutData data = m_data;
            data.type = FancyZonesDataTypes::ZoneSetLayoutType::Custom;
            data.zoneCount = 4;
            
            // prepare settings
            PowerToysSettings::PowerToyValues values(NonLocalizable::ModuleKey, NonLocalizable::ModuleKey);
            values.add_property(L"fancyzones_overlappingZonesAlgorithm", json::value(static_cast<int>(OverlappingZonesAlgorithm::Smallest)));
            json::to_file(FancyZonesSettings::GetSettingsFileName(), values.get_raw_json());
            FancyZonesSettings::instance().LoadSettings();

            auto layout = std::make_unique<Layout>(data);
            layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor());
            
            // zone4 is expected because it's the smallest one, and it's considered to be inside
            // since Multizones support
            auto zones = layout->ZonesFromPoint(POINT{ 50, 50 });
            Assert::IsTrue(zones.size() == 1);

            Zone expected({ 10, 10, 50, 50 }, 3);
            const auto& actual = layout->Zones().at(zones.at(0));
            compareZones(expected, actual);
        }

        TEST_METHOD (ZoneFromPointMultizone)
        {
            // prepare layout with overlapping zones
            saveCustomLayout({ RECT{ 0, 0, 100, 100 }, RECT{ 100, 0, 200, 100 }, RECT{ 0, 100, 100, 200 }, RECT{ 100, 100, 200, 200 } });

            LayoutData data = m_data;
            data.type = FancyZonesDataTypes::ZoneSetLayoutType::Custom;
            data.zoneCount = 4;

            // prepare settings
            PowerToysSettings::PowerToyValues values(NonLocalizable::ModuleKey, NonLocalizable::ModuleKey);
            values.add_property(L"fancyzones_overlappingZonesAlgorithm", json::value(static_cast<int>(OverlappingZonesAlgorithm::Smallest)));
            json::to_file(FancyZonesSettings::GetSettingsFileName(), values.get_raw_json());
            FancyZonesSettings::instance().LoadSettings();

            auto layout = std::make_unique<Layout>(data);
            layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor());

            auto actual = layout->ZonesFromPoint(POINT{ 50, 100 });
            Assert::IsTrue(actual.size() == 2);

            Zone zone1({ 0, 0, 100, 100 }, 0);
            compareZones(zone1, layout->Zones().at(actual[0]));

            Zone zone3({ 0, 100, 100, 200 }, 2);
            compareZones(zone3, layout->Zones().at(actual[1]));
        }
    };

    TEST_CLASS (LayoutInitUnitTests)
    {
        const LayoutData m_data{
            .uuid = FancyZonesUtils::GuidFromString(L"{33A2B101-06E0-437B-A61E-CDBECF502906}").value(),
            .type = ZoneSetLayoutType::Grid,
            .showSpacing = true,
            .spacing = 17,
            .zoneCount = 4,
            .sensitivityRadius = 33
        };
        std::unique_ptr<Layout> m_layout{};

        HMONITOR m_monitor{};
        const std::array<RECT, 9> m_workAreaRects{
            RECT{ .left = 0, .top = 0, .right = 1024, .bottom = 768 },
            RECT{ .left = 0, .top = 0, .right = 1280, .bottom = 720 },
            RECT{ .left = 0, .top = 0, .right = 1280, .bottom = 800 },
            RECT{ .left = 0, .top = 0, .right = 1280, .bottom = 1024 },
            RECT{ .left = 0, .top = 0, .right = 1366, .bottom = 768 },
            RECT{ .left = 0, .top = 0, .right = 1440, .bottom = 900 },
            RECT{ .left = 0, .top = 0, .right = 1536, .bottom = 864 },
            RECT{ .left = 0, .top = 0, .right = 1600, .bottom = 900 },
            RECT{ .left = 0, .top = 0, .right = 1920, .bottom = 1080 }
        };

        void checkZones(const Layout* layout, ZoneSetLayoutType type, size_t expectedCount, RECT rect)
        {
            const auto& zones = layout->Zones();
            Assert::AreEqual(expectedCount, zones.size());

            int zoneId = 0;
            for (const auto& zone : zones)
            {
                const auto& zoneRect = zone.second.GetZoneRect();
                Assert::IsTrue(zoneRect.left >= 0, L"left border is less than zero");
                Assert::IsTrue(zoneRect.top >= 0, L"top border is less than zero");

                Assert::IsTrue(zoneRect.left < zoneRect.right, L"rect.left >= rect.right");
                Assert::IsTrue(zoneRect.top < zoneRect.bottom, L"rect.top >= rect.bottom");

                if (type != ZoneSetLayoutType::Focus)
                {
                    Assert::IsTrue(zoneRect.right <= rect.right, L"right border is bigger than monitor work space");
                    Assert::IsTrue(zoneRect.bottom <= rect.bottom, L"bottom border is bigger than monitor work space");
                }

                zoneId++;
            }
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            m_layout = std::make_unique<Layout>(m_data);
        }

    public:
        TEST_METHOD (ValidValues)
        {
            const int zoneCount = 10;

            for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
            {
                LayoutData data = m_data;
                data.type = static_cast<ZoneSetLayoutType>(type);
                data.spacing = 10;
                data.zoneCount = zoneCount;

                for (const auto& rect : m_workAreaRects)
                {
                    auto layout = std::make_unique<Layout>(data);
                    auto result = layout->Init(rect, Mocks::Monitor());
                    Assert::IsTrue(result);
                    checkZones(layout.get(), static_cast<ZoneSetLayoutType>(type), zoneCount, rect);
                }
            }
        }

        TEST_METHOD (InvalidMonitorInfo)
        {
            for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
            {
                LayoutData data = m_data;
                data.type = static_cast<ZoneSetLayoutType>(type);
                data.spacing = 10;
                data.zoneCount = 10;

                auto layout = std::make_unique<Layout>(data);

                auto result = layout->Init(RECT{0,0,0,0}, Mocks::Monitor());
                Assert::IsFalse(result);
            }
        }

        TEST_METHOD (ZeroSpacing)
        {
            for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
            {
                LayoutData data = m_data;
                data.type = static_cast<ZoneSetLayoutType>(type);
                data.spacing = 0;
                data.zoneCount = 10;

                for (const auto& rect : m_workAreaRects)
                {
                    auto layout = std::make_unique<Layout>(data);
                    auto result = layout->Init(rect, Mocks::Monitor());
                    Assert::IsTrue(result);
                    checkZones(layout.get(), static_cast<ZoneSetLayoutType>(type), data.zoneCount, rect);
                }
            }
        }

        TEST_METHOD (LargeNegativeSpacing)
        {
            for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
            {
                LayoutData data = m_data;
                data.type = static_cast<ZoneSetLayoutType>(type);
                data.zoneCount = 10;
                data.spacing = ZoneConstants::MAX_NEGATIVE_SPACING - 1;

                auto layout = std::make_unique<Layout>(data);

                for (const auto& rect : m_workAreaRects)
                {
                    auto result = layout->Init(rect, Mocks::Monitor());
                    if (type == static_cast<int>(ZoneSetLayoutType::Focus))
                    {
                        //Focus doesn't depends on spacing
                        Assert::IsTrue(result);
                    }
                    else
                    {
                        Assert::IsFalse(result);
                    }
                }
            }
        }

        TEST_METHOD (HorizontallyBigSpacing)
        {
            for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
            {
                LayoutData data = m_data;
                data.type = static_cast<ZoneSetLayoutType>(type);
                data.zoneCount = 10;

                for (const auto& rect : m_workAreaRects)
                {
                    data.spacing = rect.right;
                    auto layout = std::make_unique<Layout>(data);

                    auto result = layout->Init(rect, Mocks::Monitor());
                    if (type == static_cast<int>(ZoneSetLayoutType::Focus))
                    {
                        //Focus doesn't depend on spacing
                        Assert::IsTrue(result);
                    }
                    else
                    {
                        Assert::IsFalse(result);
                    }
                }
            }
        }

        TEST_METHOD (VerticallyBigSpacing)
        {
            for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
            {
                LayoutData data = m_data;
                data.type = static_cast<ZoneSetLayoutType>(type);
                data.zoneCount = 10;

                for (const auto& rect : m_workAreaRects)
                {
                    data.spacing = rect.bottom;
                    auto layout = std::make_unique<Layout>(data);

                    auto result = layout->Init(rect, Mocks::Monitor());
                    if (type == static_cast<int>(ZoneSetLayoutType::Focus))
                    {
                        //Focus doesn't depend on spacing
                        Assert::IsTrue(result);
                    }
                    else
                    {
                        Assert::IsFalse(result);
                    }
                }
            }
        }

        TEST_METHOD (ZeroZoneCount)
        {
            for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
            {
                LayoutData data = m_data;
                data.type = static_cast<ZoneSetLayoutType>(type);
                data.zoneCount = 0;
                auto layout = std::make_unique<Layout>(data);

                for (const auto& rect : m_workAreaRects)
                {
                    auto result = layout->Init(rect, Mocks::Monitor());
                    Assert::IsFalse(result);
                }
            }
        }

        TEST_METHOD (BigZoneCount)
        {
            const int zoneCount = 128; //editor limit
            
            for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
            {
                LayoutData data = m_data;
                data.type = static_cast<ZoneSetLayoutType>(type);
                data.zoneCount = zoneCount;
                data.spacing = 0;

                for (const auto& rect : m_workAreaRects)
                {
                    auto layout = std::make_unique<Layout>(data);
                    auto result = layout->Init(rect, Mocks::Monitor());
                    Assert::IsTrue(result);
                    checkZones(layout.get(), static_cast<ZoneSetLayoutType>(type), zoneCount, rect);
                }
            }
        }
    };

    TEST_CLASS (ZoneIndexSetUnitTests)
    {
        TEST_METHOD (BitmaskFromIndexSetTest)
        {
            // prepare
            ZoneIndexSet set{ 0, 64 };

            // test
            ZoneIndexSetBitmask bitmask = ZoneIndexSetBitmask::FromIndexSet(set);
            Assert::AreEqual(static_cast<uint64_t>(1), bitmask.part1);
            Assert::AreEqual(static_cast<uint64_t>(1), bitmask.part2);
        }

        TEST_METHOD (BitmaskToIndexSet)
        {
            // prepare
            ZoneIndexSetBitmask bitmask{
                .part1 = 1,
                .part2 = 1,
            };

            // test
            ZoneIndexSet set = bitmask.ToIndexSet();
            Assert::AreEqual(static_cast<size_t>(2), set.size());
            Assert::AreEqual(static_cast<ZoneIndex>(0), set[0]);
            Assert::AreEqual(static_cast<ZoneIndex>(64), set[1]);
        }

        TEST_METHOD (BitmaskConvertTest)
        {
            // prepare
            ZoneIndexSet set{ 53, 54, 55, 65, 66, 67 };

            ZoneIndexSetBitmask bitmask = ZoneIndexSetBitmask::FromIndexSet(set);

            // test
            ZoneIndexSet actual = bitmask.ToIndexSet();
            Assert::AreEqual(set.size(), actual.size());
            for (int i = 0; i < set.size(); i++)
            {
                Assert::AreEqual(set[i], actual[i]);
            }
        }

        TEST_METHOD (BitmaskConvert2Test)
        {
            // prepare
            ZoneIndexSet set;
            for (int i = 0; i < 128; i++)
            {
                set.push_back(i);
            }

            ZoneIndexSetBitmask bitmask = ZoneIndexSetBitmask::FromIndexSet(set);

            // test
            ZoneIndexSet actual = bitmask.ToIndexSet();

            Assert::AreEqual(set.size(), actual.size());
            for (int i = 0; i < set.size(); i++)
            {
                Assert::AreEqual(set[i], actual[i]);
            }
        }
    };
}
