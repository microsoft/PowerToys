#include "pch.h"

#include <filesystem>

#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include "FancyZonesLib\ZoneIndexSetBitmask.h"
#include "FancyZonesLib\ZoneSet.h"
#include <FancyZonesLib/util.h>

#include "Util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace FancyZonesDataTypes;

namespace FancyZonesUnitTests
{
    TEST_CLASS (ZoneSetUnitTests)
    {
        GUID m_id;
        const ZoneSetLayoutType m_layoutType = ZoneSetLayoutType::Grid;

        winrt::com_ptr<IZoneSet> m_set;

        TEST_METHOD_INITIALIZE(Init)
        {
            auto hres = CoCreateGuid(&m_id);
            Assert::AreEqual(S_OK, hres);

            ZoneSetConfig m_config = ZoneSetConfig(m_id, m_layoutType, Mocks::Monitor(), DefaultValues::SensitivityRadius, OverlappingZonesAlgorithm::Smallest);
            m_set = MakeZoneSet(m_config);
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove_all(CustomLayouts::CustomLayoutsFileName());
        }

        void compareZones(const winrt::com_ptr<IZone>& expected, const winrt::com_ptr<IZone>& actual)
        {
            Assert::AreEqual(expected->Id(), actual->Id());
            Assert::AreEqual(expected->GetZoneRect().left, actual->GetZoneRect().left);
            Assert::AreEqual(expected->GetZoneRect().right, actual->GetZoneRect().right);
            Assert::AreEqual(expected->GetZoneRect().top, actual->GetZoneRect().top);
            Assert::AreEqual(expected->GetZoneRect().bottom, actual->GetZoneRect().bottom);
        }

            void saveCustomLayout(const std::vector<RECT>& zones)
            {
                json::JsonObject root{};
                json::JsonArray layoutsArray{};

                json::JsonObject canvasLayoutJson{};
                canvasLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::UuidID, json::value(FancyZonesUtils::GuidToString(m_id).value()));
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
            TEST_METHOD (TestCreateZoneSet)
            {
                Assert::IsNotNull(&m_set);
                CustomAssert::AreEqual(m_set->Id(), m_id);
                CustomAssert::AreEqual(m_set->LayoutType(), m_layoutType);
            }

        TEST_METHOD (TestCreateZoneSetGuidEmpty)
        {
            GUID zoneSetId{};
            ZoneSetConfig config(zoneSetId, m_layoutType, Mocks::Monitor(), DefaultValues::SensitivityRadius);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            Assert::IsNotNull(&set);
            CustomAssert::AreEqual(set->Id(), zoneSetId);
            CustomAssert::AreEqual(set->LayoutType(), m_layoutType);
        }

            TEST_METHOD (EmptyZones)
            {
                auto zones = m_set->GetZones();
                Assert::AreEqual((size_t)0, zones.size());
            }

            TEST_METHOD (MakeZoneFromZeroRect)
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 0, 0 }, 1);
                Assert::IsNotNull(zone.get());
            }

            TEST_METHOD (MakeZoneFromInvalidRectWidth)
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 100, 100, 99, 101 }, 1);
                Assert::IsNull(zone.get());
            }

            TEST_METHOD (MakeZoneFromInvalidRectHeight)
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 100, 100, 101, 99 }, 1);
                Assert::IsNull(zone.get());
            }

            TEST_METHOD (MakeZoneFromInvalidRectCoords)
            {
                const int invalid = ZoneConstants::MAX_NEGATIVE_SPACING - 1;
                winrt::com_ptr<IZone> zone = MakeZone({ invalid, invalid, invalid, invalid }, 1);
                Assert::IsNull(zone.get());
            }

            TEST_METHOD (ZoneFromPointEmpty)
            {
                auto actual = m_set->ZonesFromPoint(POINT{ 0, 0 });
                Assert::IsTrue(actual.size() == 0);
            }

            TEST_METHOD (ZoneFromPointInner)
            {
                m_set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 1, 0);
                                
                auto actual = m_set->ZonesFromPoint(POINT{ 1, 1 });
                Assert::IsTrue(actual.size() == 1);
            }

            TEST_METHOD (ZoneFromPointBorder)
            {
                m_set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 1, 0);

                Assert::IsTrue(m_set->ZonesFromPoint(POINT{ 0, 0 }).size() == 1);
                Assert::IsTrue(m_set->ZonesFromPoint(POINT{ 1920, 1080 }).size() == 0);
            }

            TEST_METHOD (ZoneFromPointOuter)
            {
                m_set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 1, 0);

                auto actual = m_set->ZonesFromPoint(POINT{ 1921, 1080 });
                Assert::IsTrue(actual.size() == 0);
            }

            TEST_METHOD (ZoneFromPointOverlapping)
            {
                // prepare layout with overlapping zones
                saveCustomLayout({ RECT{ 0, 0, 100, 100 }, RECT{ 10, 10, 90, 90 }, RECT{ 10, 10, 150, 150 }, RECT{ 10, 10, 50, 50 } });

                ZoneSetConfig config = ZoneSetConfig(m_id, FancyZonesDataTypes::ZoneSetLayoutType::Custom, Mocks::Monitor(), DefaultValues::SensitivityRadius, OverlappingZonesAlgorithm::Smallest);
                auto set = MakeZoneSet(config);
                set->CalculateZones(RECT{0,0,1920,1080}, 4, 0);

                // zone4 is expected because it's the smallest one, and it's considered to be inside
                // since Multizones support
                auto zones = set->ZonesFromPoint(POINT{ 50, 50 });
                Assert::IsTrue(zones.size() == 1);

                auto expected = MakeZone({ 10, 10, 50, 50 }, 3);
                auto actual = set->GetZones()[zones[0]];
                compareZones(expected, actual);
            }

            TEST_METHOD (ZoneFromPointMultizone)
            {
                // prepare layout with overlapping zones
                saveCustomLayout({ RECT{ 0, 0, 100, 100 }, RECT{ 100, 0, 200, 100 }, RECT{ 0, 100, 100, 200 }, RECT{ 100, 100, 200, 200 } });

                ZoneSetConfig config = ZoneSetConfig(m_id, FancyZonesDataTypes::ZoneSetLayoutType::Custom, Mocks::Monitor(), DefaultValues::SensitivityRadius, OverlappingZonesAlgorithm::Smallest);
                auto set = MakeZoneSet(config);
                set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 4, 0);

                auto actual = set->ZonesFromPoint(POINT{ 50, 100 });
                Assert::IsTrue(actual.size() == 2);

                auto zone1 = MakeZone({ 0, 0, 100, 100 }, 0);
                compareZones(zone1, set->GetZones()[actual[0]]);

                auto zone3 = MakeZone({ 0, 100, 100, 200 }, 2);
                compareZones(zone3, set->GetZones()[actual[1]]);
            }

            TEST_METHOD (ZoneIndexFromWindowUnknown)
            {
                HWND window = Mocks::Window();
                HWND workArea = Mocks::Window();
                m_set->CalculateZones(RECT{0,0,1920, 1080}, 1, 0);
                m_set->MoveWindowIntoZoneByIndexSet(window, workArea, { 0 });

                auto actual = m_set->GetZoneIndexSetFromWindow(Mocks::Window());
                Assert::IsTrue(std::vector<ZoneIndex>{} == actual);
            }

            TEST_METHOD (ZoneIndexFromWindowNull)
            {
                HWND window = Mocks::Window();
                HWND workArea = Mocks::Window();
                m_set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 1, 0);
                m_set->MoveWindowIntoZoneByIndexSet(window, workArea, { 0 });

                auto actual = m_set->GetZoneIndexSetFromWindow(nullptr);
                Assert::IsTrue(std::vector<ZoneIndex>{} == actual);
            }

            TEST_METHOD (MoveWindowIntoZoneByIndex)
            {
                // prepare layout with overlapping zones
                saveCustomLayout({ RECT{ 0, 0, 100, 100 }, RECT{ 0, 0, 100, 100 }, RECT{ 0, 0, 100, 100 } });

                ZoneSetConfig config = ZoneSetConfig(m_id, FancyZonesDataTypes::ZoneSetLayoutType::Custom, Mocks::Monitor(), DefaultValues::SensitivityRadius, OverlappingZonesAlgorithm::Smallest);
                auto set = MakeZoneSet(config);
                set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 3, 0);

                HWND window = Mocks::Window();
                set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 1);
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByIndexWithNoZones)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
            }

            TEST_METHOD (MoveWindowIntoZoneByIndexWithInvalidIndex)
            {
                m_set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 1, 0);

                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 100);
                Assert::IsTrue(std::vector<ZoneIndex>{} == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByIndexSeveralTimesSameWindow)
            {
                m_set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 3, 0);

                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window));

                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 1);
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window));

                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 2);
                Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByIndexSeveralTimesSameIndex)
            {
                m_set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 3, 0);

                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window));
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window));
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByPointEmpty)
            {
                m_set->MoveWindowIntoZoneByPoint(Mocks::Window(), Mocks::Window(), POINT{ 0, 0 });
            }

            TEST_METHOD (MoveWindowIntoZoneByPointOuterPoint)
            {
                m_set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 1, 0);

                auto window = Mocks::Window();
                m_set->MoveWindowIntoZoneByPoint(window, Mocks::Window(), POINT{ 1921, 1081 });

                Assert::IsTrue(std::vector<ZoneIndex>{} == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByPointInnerPoint)
            {
                m_set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 1, 0);

                auto window = Mocks::Window();
                m_set->MoveWindowIntoZoneByPoint(window, Mocks::Window(), POINT{ 50, 50 });

                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByPointInnerPointOverlappingZones)
            {
                saveCustomLayout({ RECT{ 0, 0, 100, 100 }, RECT{ 10, 10, 90, 90 } });

                ZoneSetConfig config = ZoneSetConfig(m_id, FancyZonesDataTypes::ZoneSetLayoutType::Custom, Mocks::Monitor(), DefaultValues::SensitivityRadius, OverlappingZonesAlgorithm::Smallest);
                auto set = MakeZoneSet(config);
                set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 3, 0);

                auto window = Mocks::Window();
                set->MoveWindowIntoZoneByPoint(window, Mocks::Window(), POINT{ 50, 50 });

                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByPointDropAddWindow)
            {
                saveCustomLayout({ RECT{ 0, 0, 100, 100 }, RECT{ 10, 10, 90, 90 } });

                ZoneSetConfig config = ZoneSetConfig(m_id, FancyZonesDataTypes::ZoneSetLayoutType::Custom, Mocks::Monitor(), DefaultValues::SensitivityRadius, OverlappingZonesAlgorithm::Smallest);
                auto set = MakeZoneSet(config);
                set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 3, 0);

                const auto window = Mocks::Window();
                const auto workArea = Mocks::Window();

                set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                set->MoveWindowIntoZoneByPoint(window, Mocks::Window(), POINT{ 50, 50 });

                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == set->GetZoneIndexSetFromWindow(window));
            }
    };

    // MoveWindowIntoZoneByDirectionAndIndex is complicated enough to warrant it's own test class
    TEST_CLASS (ZoneSetsMoveWindowIntoZoneByDirectionUnitTests)
    {
        winrt::com_ptr<IZoneSet> m_set;
        winrt::com_ptr<IZone> m_zone1;
        winrt::com_ptr<IZone> m_zone2;
        winrt::com_ptr<IZone> m_zone3;

        TEST_METHOD_INITIALIZE(Initialize)
            {
                ZoneSetConfig config({}, ZoneSetLayoutType::Grid, Mocks::Monitor(), DefaultValues::SensitivityRadius);
                m_set = MakeZoneSet(config);
                m_set->CalculateZones(RECT{ 0, 0, 1920, 1080 }, 3, 10);
            }

            TEST_METHOD (EmptyZonesLeft)
            {
                ZoneSetConfig config({}, ZoneSetLayoutType::Custom, Mocks::Monitor(), DefaultValues::SensitivityRadius);
                auto set = MakeZoneSet(config);

                set->MoveWindowIntoZoneByDirectionAndIndex(Mocks::Window(), Mocks::Window(), VK_LEFT, true);
            }

            TEST_METHOD (EmptyZonesRight)
            {
                ZoneSetConfig config({}, ZoneSetLayoutType::Custom, Mocks::Monitor(), DefaultValues::SensitivityRadius);
                auto set = MakeZoneSet(config);

                set->MoveWindowIntoZoneByDirectionAndIndex(Mocks::Window(), Mocks::Window(), VK_RIGHT, true);
            }

            TEST_METHOD (MoveRightNoZones)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_RIGHT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveLeftNoZones)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_LEFT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveRightTwice)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_RIGHT, true);
                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_RIGHT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveLeftTwice)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_LEFT, true);
                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_LEFT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveRightMoreThanZonesCount)
            {
                HWND window = Mocks::Window();
                for (int i = 0; i <= m_set->GetZones().size(); i++)
                {
                    m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_RIGHT, true);
                }

                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveLeftMoreThanZonesCount)
            {
                HWND window = Mocks::Window();
                for (int i = 0; i <= m_set->GetZones().size(); i++)
                {
                    m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_LEFT, true);
                }

                Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByDirectionRight)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_RIGHT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window));

                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_RIGHT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveRightWithSameWindowAdded)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndexSet(window, Mocks::Window(), { 0, 1 });

                Assert::IsTrue(std::vector<ZoneIndex>{ 0, 1 } == m_set->GetZoneIndexSetFromWindow(window));

                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_RIGHT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window));

                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_RIGHT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveRightWithDifferentWindowsAdded)
            {
                HWND window1 = Mocks::Window();
                HWND window2 = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window1, Mocks::Window(), { 0 });
                m_set->MoveWindowIntoZoneByIndex(window2, Mocks::Window(), { 1 });

                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window1));
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window2));

                m_set->MoveWindowIntoZoneByDirectionAndIndex(window1, Mocks::Window(), VK_RIGHT, true);

                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window1));
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window2));

                m_set->MoveWindowIntoZoneByDirectionAndIndex(window1, Mocks::Window(), VK_RIGHT, true);

                Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == m_set->GetZoneIndexSetFromWindow(window1));
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window2));
            }

            TEST_METHOD (MoveWindowIntoZoneByDirectionLeft)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 2);
                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_LEFT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window));

                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_LEFT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveLeftWithSameWindowAdded)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndexSet(window, Mocks::Window(), { 1, 2 });

                Assert::IsTrue(std::vector<ZoneIndex>{ 1, 2 } == m_set->GetZoneIndexSetFromWindow(window));

                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_LEFT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window));

                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_LEFT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveLeftWithDifferentWindowsAdded)
            {
                HWND window1 = Mocks::Window();
                HWND window2 = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window1, Mocks::Window(), 1);
                m_set->MoveWindowIntoZoneByIndex(window2, Mocks::Window(), 2);

                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window1));
                Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == m_set->GetZoneIndexSetFromWindow(window2));

                m_set->MoveWindowIntoZoneByDirectionAndIndex(window2, Mocks::Window(), VK_LEFT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window1));
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window2));

                m_set->MoveWindowIntoZoneByDirectionAndIndex(window2, Mocks::Window(), VK_LEFT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == m_set->GetZoneIndexSetFromWindow(window1));
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window2));
            }

            TEST_METHOD (MoveWindowIntoZoneByDirectionWrapAroundRight)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 2);
                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_RIGHT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByDirectionWrapAroundLeft)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_LEFT, true);
                Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == m_set->GetZoneIndexSetFromWindow(window));
            }

            TEST_METHOD (MoveSecondWindowIntoSameZone)
            {
                HWND window1 = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window1, Mocks::Window(), 0);

                HWND window2 = Mocks::Window();
                m_set->MoveWindowIntoZoneByDirectionAndIndex(window2, Mocks::Window(), VK_RIGHT, true);

                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window1));
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == m_set->GetZoneIndexSetFromWindow(window2));
            }

            TEST_METHOD (MoveRightMoreThanZoneCountReturnsFalse)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                for (size_t i = 0; i < m_set->GetZones().size() - 1; ++i)
                {
                    m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_RIGHT, false);
                }
                bool moreZonesInLayout = m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_RIGHT, false);
                Assert::IsFalse(moreZonesInLayout);
            }

            TEST_METHOD (MoveLeftMoreThanZoneCountReturnsFalse)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 2);
                for (size_t i = 0; i < m_set->GetZones().size() - 1; ++i)
                {
                    m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_LEFT, false);
                }
                bool moreZonesInLayout = m_set->MoveWindowIntoZoneByDirectionAndIndex(window, Mocks::Window(), VK_LEFT, false);
                Assert::IsFalse(moreZonesInLayout);
            }
    };
    
    TEST_CLASS (ZoneSetCalculateZonesUnitTests)
    {
        GUID m_id;
        const ZoneSetLayoutType m_layoutType = ZoneSetLayoutType::Custom;
        winrt::com_ptr<IZoneSet> m_set;

        HMONITOR m_monitor{};
        const std::array<MONITORINFO, 9> m_popularMonitors{
            MONITORINFO{ .cbSize = sizeof(MONITORINFO), .rcWork{ .left = 0, .top = 0, .right = 1024, .bottom = 768 } },
            MONITORINFO{ .cbSize = sizeof(MONITORINFO), .rcWork{ .left = 0, .top = 0, .right = 1280, .bottom = 720 } },
            MONITORINFO{ .cbSize = sizeof(MONITORINFO), .rcWork{ .left = 0, .top = 0, .right = 1280, .bottom = 800 } },
            MONITORINFO{ .cbSize = sizeof(MONITORINFO), .rcWork{ .left = 0, .top = 0, .right = 1280, .bottom = 1024 } },
            MONITORINFO{ .cbSize = sizeof(MONITORINFO), .rcWork{ .left = 0, .top = 0, .right = 1366, .bottom = 768 } },
            MONITORINFO{ .cbSize = sizeof(MONITORINFO), .rcWork{ .left = 0, .top = 0, .right = 1440, .bottom = 900 } },
            MONITORINFO{ .cbSize = sizeof(MONITORINFO), .rcWork{ .left = 0, .top = 0, .right = 1536, .bottom = 864 } },
            MONITORINFO{ .cbSize = sizeof(MONITORINFO), .rcWork{ .left = 0, .top = 0, .right = 1600, .bottom = 900 } },
            MONITORINFO{ .cbSize = sizeof(MONITORINFO), .rcWork{ .left = 0, .top = 0, .right = 1920, .bottom = 1080 } }
        };

        void checkZones(const winrt::com_ptr<IZoneSet>& set, ZoneSetLayoutType type, size_t expectedCount, MONITORINFO monitorInfo)
        {
            auto zones = set->GetZones();
            Assert::AreEqual(expectedCount, zones.size());

            int zoneId = 0;
            for (const auto& zone : zones)
            {
                Assert::IsTrue(set->IsZoneEmpty(zoneId));

                const auto& zoneRect = zone.second->GetZoneRect();
                Assert::IsTrue(zoneRect.left >= 0, L"left border is less than zero");
                Assert::IsTrue(zoneRect.top >= 0, L"top border is less than zero");

                Assert::IsTrue(zoneRect.left < zoneRect.right, L"rect.left >= rect.right");
                Assert::IsTrue(zoneRect.top < zoneRect.bottom, L"rect.top >= rect.bottom");

                if (type != ZoneSetLayoutType::Focus)
                {
                    Assert::IsTrue(zoneRect.right <= monitorInfo.rcWork.right, L"right border is bigger than monitor work space");
                    Assert::IsTrue(zoneRect.bottom <= monitorInfo.rcWork.bottom, L"bottom border is bigger than monitor work space");
                }

                zoneId++;
            }
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            m_id = FancyZonesUtils::GuidFromString(L"{33A2B101-06E0-437B-A61E-CDBECF502906}").value();
            
            ZoneSetConfig m_config = ZoneSetConfig(m_id, m_layoutType, m_monitor, DefaultValues::SensitivityRadius);
            m_set = MakeZoneSet(m_config);
        }

        public:
                TEST_METHOD (ValidValues)
                {
                    const int spacing = 10;
                    const int zoneCount = 10;

                    for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<ZoneSetLayoutType>(type), m_monitor, DefaultValues::SensitivityRadius);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            auto set = MakeZoneSet(m_config);
                            auto result = set->CalculateZones(monitorInfo.rcWork, zoneCount, spacing);
                            Assert::IsTrue(result);
                            checkZones(set, static_cast<ZoneSetLayoutType>(type), zoneCount, monitorInfo);
                        }
                    }
                }
                TEST_METHOD (InvalidMonitorInfo)
                {
                    const int spacing = 10;
                    const int zoneCount = 10;

                    for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<ZoneSetLayoutType>(type), m_monitor, DefaultValues::SensitivityRadius);
                        auto set = MakeZoneSet(m_config);

                        MONITORINFO info{};
                        auto result = set->CalculateZones(info.rcWork, zoneCount, spacing);
                        Assert::IsFalse(result);
                    }
                }

                TEST_METHOD (ZeroSpacing)
                {
                    const int spacing = 0;
                    const int zoneCount = 10;

                    for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<ZoneSetLayoutType>(type), m_monitor, DefaultValues::SensitivityRadius);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            auto set = MakeZoneSet(m_config);
                            auto result = set->CalculateZones(monitorInfo.rcWork, zoneCount, spacing);
                            Assert::IsTrue(result);
                            checkZones(set, static_cast<ZoneSetLayoutType>(type), zoneCount, monitorInfo);
                        }
                    }
                }

                TEST_METHOD (LargeNegativeSpacing)
                {
                    const int spacing = ZoneConstants::MAX_NEGATIVE_SPACING - 1;
                    const int zoneCount = 10;

                    for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<ZoneSetLayoutType>(type), m_monitor, DefaultValues::SensitivityRadius);
                        auto set = MakeZoneSet(m_config);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            auto result = set->CalculateZones(monitorInfo.rcWork, zoneCount, spacing);
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
                    const int zoneCount = 10;

                    for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<ZoneSetLayoutType>(type), m_monitor, DefaultValues::SensitivityRadius);
                        auto set = MakeZoneSet(m_config);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            const int spacing = monitorInfo.rcWork.right;
                            auto result = set->CalculateZones(monitorInfo.rcWork, zoneCount, spacing);
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
                    const int zoneCount = 10;

                    for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<ZoneSetLayoutType>(type), m_monitor, DefaultValues::SensitivityRadius);
                        auto set = MakeZoneSet(m_config);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            const int spacing = monitorInfo.rcWork.bottom;
                            auto result = set->CalculateZones(monitorInfo.rcWork, zoneCount, spacing);
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
                    const int spacing = 10;
                    const int zoneCount = 0;

                    for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<ZoneSetLayoutType>(type), m_monitor, DefaultValues::SensitivityRadius);
                        auto set = MakeZoneSet(m_config);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            auto result = set->CalculateZones(monitorInfo.rcWork, zoneCount, spacing);
                            Assert::IsFalse(result);
                        }
                    }
                }

                TEST_METHOD (BigZoneCount)
                {
                    const int zoneCount = 128; //editor limit
                    const int spacing = 0;

                    for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<ZoneSetLayoutType>(type), m_monitor, DefaultValues::SensitivityRadius);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            auto set = MakeZoneSet(m_config);
                            auto result = set->CalculateZones(monitorInfo.rcWork, zoneCount, spacing);
                            Assert::IsTrue(result);
                            checkZones(set, static_cast<ZoneSetLayoutType>(type), zoneCount, monitorInfo);
                        }
                    }
                }
    };
    
    TEST_CLASS(ZoneIndexSetUnitTests)
    {
        TEST_METHOD (BitmaskFromIndexSetTest)
        {
            // prepare
            ZoneIndexSet set {0, 64};
            
            // test
            ZoneIndexSetBitmask bitmask = ZoneIndexSetBitmask::FromIndexSet(set);
            Assert::AreEqual(static_cast<uint64_t>(1), bitmask.part1);
            Assert::AreEqual(static_cast<uint64_t>(1), bitmask.part2);
        }

        TEST_METHOD(BitmaskToIndexSet)
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
