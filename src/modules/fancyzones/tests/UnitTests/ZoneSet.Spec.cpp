#include "pch.h"
#include "lib\JsonHelpers.h"
#include "lib\ZoneSet.h"

#include <filesystem>

#include "Util.h"
#include <common/settings_helpers.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using TZoneSetLayoutType = JSONHelpers::ZoneSetLayoutType;

namespace FancyZonesUnitTests
{
    TEST_CLASS (ZoneSetUnitTests)
    {
        GUID m_id;
        const TZoneSetLayoutType m_layoutType = TZoneSetLayoutType::Custom;
        const PCWSTR m_resolutionKey = L"WorkAreaIn";

        winrt::com_ptr<IZoneSet> m_set;

        TEST_METHOD_INITIALIZE(Init)
            {
                auto hres = CoCreateGuid(&m_id);
                Assert::AreEqual(S_OK, hres);

                ZoneSetConfig m_config = ZoneSetConfig(m_id, m_layoutType, Mocks::Monitor(), m_resolutionKey);
                m_set = MakeZoneSet(m_config);
            }

            void compareZones(const winrt::com_ptr<IZone>& expected, const winrt::com_ptr<IZone>& actual)
            {
                Assert::AreEqual(expected->Id(), actual->Id());
                Assert::AreEqual(expected->GetZoneRect().left, actual->GetZoneRect().left);
                Assert::AreEqual(expected->GetZoneRect().right, actual->GetZoneRect().right);
                Assert::AreEqual(expected->GetZoneRect().top, actual->GetZoneRect().top);
                Assert::AreEqual(expected->GetZoneRect().bottom, actual->GetZoneRect().bottom);
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
                ZoneSetConfig config(zoneSetId, m_layoutType, Mocks::Monitor(), m_resolutionKey);
                winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

                Assert::IsNotNull(&set);
                CustomAssert::AreEqual(set->Id(), zoneSetId);
                CustomAssert::AreEqual(set->LayoutType(), m_layoutType);
            }

            TEST_METHOD (TestCreateZoneSetMonitorEmpty)
            {
                ZoneSetConfig config(m_id, m_layoutType, nullptr, m_resolutionKey);
                winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);
                Assert::IsNotNull(&set);
                CustomAssert::AreEqual(set->Id(), m_id);
                CustomAssert::AreEqual(set->LayoutType(), m_layoutType);
            }

            TEST_METHOD (TestCreateZoneSetKeyEmpty)
            {
                ZoneSetConfig config(m_id, m_layoutType, Mocks::Monitor(), nullptr);
                winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);
                Assert::IsNotNull(&set);
                CustomAssert::AreEqual(set->Id(), m_id);
                CustomAssert::AreEqual(set->LayoutType(), m_layoutType);
            }

            TEST_METHOD (EmptyZones)
            {
                auto zones = m_set->GetZones();
                Assert::AreEqual((size_t)0, zones.size());
            }

            TEST_METHOD (AddOne)
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
                m_set->AddZone(zone);
                auto zones = m_set->GetZones();
                Assert::AreEqual((size_t)1, zones.size());
                compareZones(zone, zones[0]);
                Assert::AreEqual((size_t)1, zones[0]->Id());
            }

            TEST_METHOD (AddManySame)
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
                for (size_t i = 0; i < 1024; i++)
                {
                    m_set->AddZone(zone);
                    auto zones = m_set->GetZones();
                    Assert::AreEqual(i + 1, zones.size());
                    compareZones(zone, zones[i]);
                    Assert::AreEqual(i + 1, zones[i]->Id());
                }
            }

            TEST_METHOD (AddManyEqual)
            {
                for (size_t i = 0; i < 1024; i++)
                {
                    winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
                    m_set->AddZone(zone);
                    auto zones = m_set->GetZones();
                    Assert::AreEqual(i + 1, zones.size());
                    compareZones(zone, zones[i]);
                    Assert::AreEqual(i + 1, zones[i]->Id());
                }
            }

            TEST_METHOD (AddManyDifferent)
            {
                for (size_t i = 0; i < 1024; i++)
                {
                    winrt::com_ptr<IZone> zone = MakeZone({ rand() % 10, rand() % 10, rand() % 100, rand() % 100 });
                    m_set->AddZone(zone);
                    auto zones = m_set->GetZones();
                    Assert::AreEqual(i + 1, zones.size());
                    compareZones(zone, zones[i]);
                    Assert::AreEqual(i + 1, zones[i]->Id());
                }
            }

            TEST_METHOD (ZoneFromPointEmpty)
            {
                auto actual = m_set->ZonesFromPoint(POINT{ 0, 0 });
                Assert::IsTrue(actual.size() == 0);
            }

            TEST_METHOD (ZoneFromPointInner)
            {
                const int left = 0, top = 0, right = 100, bottom = 100;
                winrt::com_ptr<IZone> expected = MakeZone({ left, top, right, bottom });
                m_set->AddZone(expected);

                for (int i = left + 1; i < right; i++)
                {
                    for (int j = top + 1; j < bottom; j++)
                    {
                        auto actual = m_set->ZonesFromPoint(POINT{ i, j });
                        Assert::IsTrue(actual.size() == 1);
                        compareZones(expected, m_set->GetZones()[actual[0]]);
                    }
                }
            }

            TEST_METHOD (ZoneFromPointBorder)
            {
                const int left = 0, top = 0, right = 100, bottom = 100;
                winrt::com_ptr<IZone> expected = MakeZone({ left, top, right, bottom });
                m_set->AddZone(expected);

                for (int i = left; i < right; i++)
                {
                    auto actual = m_set->ZonesFromPoint(POINT{ i, top });
                    Assert::IsTrue(actual.size() == 1);
                    compareZones(expected, m_set->GetZones()[actual[0]]);
                }

                for (int i = top; i < bottom; i++)
                {
                    auto actual = m_set->ZonesFromPoint(POINT{ left, i });
                    Assert::IsTrue(actual.size() == 1);
                    compareZones(expected, m_set->GetZones()[actual[0]]);
                }

                //bottom and right borders considered to be outside
                for (int i = left; i < right; i++)
                {
                    auto actual = m_set->ZonesFromPoint(POINT{ i, bottom });
                    Assert::IsTrue(actual.size() == 0);
                }

                for (int i = top; i < bottom; i++)
                {
                    auto actual = m_set->ZonesFromPoint(POINT{ right, i });
                    Assert::IsTrue(actual.size() == 0);
                }
            }

            TEST_METHOD (ZoneFromPointOuter)
            {
                const int left = 0, top = 0, right = 100, bottom = 100;
                winrt::com_ptr<IZone> zone = MakeZone({ left, top, right, bottom });
                m_set->AddZone(zone);

                auto actual = m_set->ZonesFromPoint(POINT{ 200, 200 });
                Assert::IsTrue(actual.size() == 0);
            }

            TEST_METHOD (ZoneFromPointOverlapping)
            {
                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                m_set->AddZone(zone1);
                winrt::com_ptr<IZone> zone2 = MakeZone({ 10, 10, 90, 90 });
                m_set->AddZone(zone2);
                winrt::com_ptr<IZone> zone3 = MakeZone({ 10, 10, 150, 150 });
                m_set->AddZone(zone3);
                winrt::com_ptr<IZone> zone4 = MakeZone({ 10, 10, 50, 50 });
                m_set->AddZone(zone4);

                // zone4 is expected because it's the smallest one, and it's considered to be inside
                // since Multizones support

                auto actual = m_set->ZonesFromPoint(POINT{ 50, 50 });
                Assert::IsTrue(actual.size() == 1);
                compareZones(zone4, m_set->GetZones()[actual[0]]);
            }

            TEST_METHOD (ZoneFromPointMultizoneHorizontal)
            {
                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                m_set->AddZone(zone1);
                winrt::com_ptr<IZone> zone2 = MakeZone({ 100, 0, 200, 100 });
                m_set->AddZone(zone2);
                winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 100, 100, 200 });
                m_set->AddZone(zone3);
                winrt::com_ptr<IZone> zone4 = MakeZone({ 100, 100, 200, 200 });
                m_set->AddZone(zone4);

                auto actual = m_set->ZonesFromPoint(POINT{ 50, 100 });
                Assert::IsTrue(actual.size() == 2);
                compareZones(zone1, m_set->GetZones()[actual[0]]);
                compareZones(zone3, m_set->GetZones()[actual[1]]);
            }

            TEST_METHOD (ZoneFromPointMultizoneVertical)
            {
                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                m_set->AddZone(zone1);
                winrt::com_ptr<IZone> zone2 = MakeZone({ 100, 0, 200, 100 });
                m_set->AddZone(zone2);
                winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 100, 100, 200 });
                m_set->AddZone(zone3);
                winrt::com_ptr<IZone> zone4 = MakeZone({ 100, 100, 200, 200 });
                m_set->AddZone(zone4);

                auto actual = m_set->ZonesFromPoint(POINT{ 100, 50 });
                Assert::IsTrue(actual.size() == 2);
                compareZones(zone1, m_set->GetZones()[actual[0]]);
                compareZones(zone2, m_set->GetZones()[actual[1]]);
            }

            TEST_METHOD(ZoneFromPointMultizoneQuad)
            {
                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                m_set->AddZone(zone1);
                winrt::com_ptr<IZone> zone2 = MakeZone({ 100, 0, 200, 100 });
                m_set->AddZone(zone2);
                winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 100, 100, 200 });
                m_set->AddZone(zone3);
                winrt::com_ptr<IZone> zone4 = MakeZone({ 100, 100, 200, 200 });
                m_set->AddZone(zone4);

                auto actual = m_set->ZonesFromPoint(POINT{ 100, 100 });
                Assert::IsTrue(actual.size() == 4);
                compareZones(zone1, m_set->GetZones()[actual[0]]);
                compareZones(zone2, m_set->GetZones()[actual[1]]);
                compareZones(zone3, m_set->GetZones()[actual[2]]);
                compareZones(zone4, m_set->GetZones()[actual[3]]);
            }

            TEST_METHOD (ZoneFromPointWithNotNormalizedRect)
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 100, 100, 0, 0 });
                m_set->AddZone(zone);

                auto actual = m_set->ZonesFromPoint(POINT{ 50, 50 });
                Assert::IsTrue(actual.size() == 0);
            }

            TEST_METHOD (ZoneFromPointWithZeroRect)
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 0, 0 });
                m_set->AddZone(zone);

                auto actual = m_set->ZonesFromPoint(POINT{ 0, 0 });
                Assert::IsTrue(actual.size() == 0);
            }

            TEST_METHOD (ZoneIndexFromWindow)
            {
                HWND window = Mocks::Window();
                HWND zoneWindow = Mocks::Window();

                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone2 = MakeZone({ 20, 20, 200, 200 });
                winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone4 = MakeZone({ 10, 10, 100, 100 });
                winrt::com_ptr<IZone> zone5 = MakeZone({ 20, 20, 100, 100 });

                zone3->AddWindowToZone(window, zoneWindow, true);

                m_set->AddZone(zone1);
                m_set->AddZone(zone2);
                m_set->AddZone(zone3);
                m_set->AddZone(zone4);
                m_set->AddZone(zone5);

                const int expected = 2;
                auto actual = m_set->GetZoneIndexFromWindow(window);
                Assert::AreEqual(expected, actual);
            }

            TEST_METHOD (ZoneIndexFromWindowWithEqualWindows)
            {
                HWND window = Mocks::Window();
                HWND zoneWindow = Mocks::Window();

                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone2 = MakeZone({ 20, 20, 200, 200 });
                winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone4 = MakeZone({ 10, 10, 100, 100 });
                winrt::com_ptr<IZone> zone5 = MakeZone({ 20, 20, 100, 100 });

                zone3->AddWindowToZone(window, zoneWindow, true);
                zone4->AddWindowToZone(window, zoneWindow, true);

                m_set->AddZone(zone1);
                m_set->AddZone(zone2);
                m_set->AddZone(zone3);
                m_set->AddZone(zone4);
                m_set->AddZone(zone5);

                const int expected = 2;
                auto actual = m_set->GetZoneIndexFromWindow(window);
                Assert::AreEqual(expected, actual);
            }

            TEST_METHOD (ZoneIndexFromWindowUnknown)
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
                HWND window = Mocks::Window();
                HWND zoneWindow = Mocks::Window();
                zone->AddWindowToZone(window, zoneWindow, true);
                m_set->AddZone(zone);

                const int expected = -1;
                auto actual = m_set->GetZoneIndexFromWindow(Mocks::Window());
                Assert::AreEqual(expected, actual);
            }

            TEST_METHOD (ZoneIndexFromWindowNull)
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
                HWND window = Mocks::Window();
                HWND zoneWindow = Mocks::Window();
                zone->AddWindowToZone(window, zoneWindow, true);
                m_set->AddZone(zone);

                const int expected = -1;
                auto actual = m_set->GetZoneIndexFromWindow(nullptr);
                Assert::AreEqual(expected, actual);
            }

            TEST_METHOD (MoveWindowIntoZoneByIndex)
            {
                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone2 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
                m_set->AddZone(zone1);
                m_set->AddZone(zone2);
                m_set->AddZone(zone3);

                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 1);
                Assert::IsFalse(zone1->ContainsWindow(window));
                Assert::IsTrue(zone2->ContainsWindow(window));
                Assert::IsFalse(zone3->ContainsWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByIndexWithNoZones)
            {
                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
            }

            TEST_METHOD (MoveWindowIntoZoneByIndexWithInvalidIndex)
            {
                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone2 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
                m_set->AddZone(zone1);
                m_set->AddZone(zone2);
                m_set->AddZone(zone3);

                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 100);
                Assert::IsFalse(zone1->ContainsWindow(window));
                Assert::IsFalse(zone2->ContainsWindow(window));
                Assert::IsFalse(zone3->ContainsWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByIndexSeveralTimesSameWindow)
            {
                // Add a couple of zones.
                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone2 = MakeZone({ 1, 1, 101, 101 });
                winrt::com_ptr<IZone> zone3 = MakeZone({ 2, 2, 102, 102 });
                m_set->AddZone(zone1);
                m_set->AddZone(zone2);
                m_set->AddZone(zone3);

                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                Assert::IsTrue(zone1->ContainsWindow(window));
                Assert::IsFalse(zone2->ContainsWindow(window));
                Assert::IsFalse(zone3->ContainsWindow(window));

                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 1);
                Assert::IsFalse(zone1->ContainsWindow(window));
                Assert::IsTrue(zone2->ContainsWindow(window));
                Assert::IsFalse(zone3->ContainsWindow(window));

                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 2);
                Assert::IsFalse(zone1->ContainsWindow(window));
                Assert::IsFalse(zone2->ContainsWindow(window));
                Assert::IsTrue(zone3->ContainsWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByIndexSeveralTimesSameIndex)
            {
                // Add a couple of zones.
                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone2 = MakeZone({ 1, 1, 101, 101 });
                winrt::com_ptr<IZone> zone3 = MakeZone({ 2, 2, 102, 102 });
                m_set->AddZone(zone1);
                m_set->AddZone(zone2);
                m_set->AddZone(zone3);

                HWND window = Mocks::Window();
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                m_set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                Assert::IsTrue(zone1->ContainsWindow(window));
                Assert::IsFalse(zone2->ContainsWindow(window));
                Assert::IsFalse(zone3->ContainsWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByPointEmpty)
            {
                m_set->MoveWindowIntoZoneByPoint(Mocks::Window(), Mocks::Window(), POINT{ 0, 0 });
            }

            TEST_METHOD (MoveWindowIntoZoneByPointOuterPoint)
            {
                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                m_set->AddZone(zone1);

                auto window = Mocks::Window();
                m_set->MoveWindowIntoZoneByPoint(window, Mocks::Window(), POINT{ 200, 200 });

                Assert::IsFalse(zone1->ContainsWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByPointInnerPoint)
            {
                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                m_set->AddZone(zone1);

                auto window = Mocks::Window();
                m_set->MoveWindowIntoZoneByPoint(window, Mocks::Window(), POINT{ 50, 50 });

                Assert::IsTrue(zone1->ContainsWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByPointInnerPointOverlappingZones)
            {
                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone2 = MakeZone({ 10, 10, 90, 90 });
                m_set->AddZone(zone1);
                m_set->AddZone(zone2);

                auto window = Mocks::Window();
                m_set->MoveWindowIntoZoneByPoint(window, Mocks::Window(), POINT{ 50, 50 });

                Assert::IsFalse(zone1->ContainsWindow(window));
                Assert::IsTrue(zone2->ContainsWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByPointDropAddWindow)
            {
                const auto window = Mocks::Window();
                const auto zoneWindow = Mocks::Window();

                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone2 = MakeZone({ 10, 10, 90, 90 });

                zone1->AddWindowToZone(window, zoneWindow, false);

                m_set->AddZone(zone1);
                m_set->AddZone(zone2);

                m_set->MoveWindowIntoZoneByPoint(window, Mocks::Window(), POINT{ 50, 50 });

                Assert::IsFalse(zone1->ContainsWindow(window));
                Assert::IsTrue(zone2->ContainsWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByPointDropAddWindowToSameZone)
            {
                const auto window = Mocks::Window();
                const auto zoneWindow = Mocks::Window();

                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone2 = MakeZone({ 10, 10, 90, 90 });

                zone2->AddWindowToZone(window, zoneWindow, false);

                m_set->AddZone(zone1);
                m_set->AddZone(zone2);

                m_set->MoveWindowIntoZoneByPoint(window, Mocks::Window(), POINT{ 50, 50 });

                Assert::IsFalse(zone1->ContainsWindow(window));
                Assert::IsTrue(zone2->ContainsWindow(window));
            }

            TEST_METHOD (MoveWindowIntoZoneByPointSeveralZonesWithSameWindow)
            {
                const auto window = Mocks::Window();
                const auto zoneWindow = Mocks::Window();

                winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
                winrt::com_ptr<IZone> zone2 = MakeZone({ 10, 10, 90, 90 });
                winrt::com_ptr<IZone> zone3 = MakeZone({ 20, 20, 80, 80 });

                zone1->AddWindowToZone(window, zoneWindow, false);
                zone2->AddWindowToZone(window, zoneWindow, false);
                zone3->AddWindowToZone(window, zoneWindow, false);

                m_set->AddZone(zone1);
                m_set->AddZone(zone2);
                m_set->AddZone(zone3);

                m_set->MoveWindowIntoZoneByPoint(window, Mocks::Window(), POINT{ 50, 50 });

                Assert::IsFalse(zone1->ContainsWindow(window));
                Assert::IsFalse(zone2->ContainsWindow(window));
                Assert::IsTrue(zone3->ContainsWindow(window));
            }
    };

    // MoveWindowIntoZoneByDirection is complicated enough to warrant it's own test class
    TEST_CLASS (ZoneSetsMoveWindowIntoZoneByDirectionUnitTests)
    {
        winrt::com_ptr<IZoneSet> m_set;
        winrt::com_ptr<IZone> m_zone1;
        winrt::com_ptr<IZone> m_zone2;
        winrt::com_ptr<IZone> m_zone3;

        TEST_METHOD_INITIALIZE(Initialize)
        {
            ZoneSetConfig config({}, TZoneSetLayoutType::Custom, Mocks::Monitor(), L"WorkAreaIn");
            m_set = MakeZoneSet(config);

            // Add a couple of zones.
            m_zone1 = MakeZone({ 0, 0, 100, 100 });
            m_zone2 = MakeZone({ 0, 0, 100, 100 });
            m_zone3 = MakeZone({ 0, 0, 100, 100 });
            m_set->AddZone(m_zone1);
            m_set->AddZone(m_zone2);
            m_set->AddZone(m_zone3);
        }

        TEST_METHOD (EmptyZonesLeft)
        {
            ZoneSetConfig config({}, TZoneSetLayoutType::Custom, Mocks::Monitor(), L"WorkAreaIn");
            auto set = MakeZoneSet(config);

            set->MoveWindowIntoZoneByDirection(Mocks::Window(), Mocks::Window(), VK_LEFT, true);
        }

        TEST_METHOD (EmptyZonesRight)
        {
            ZoneSetConfig config({}, TZoneSetLayoutType::Custom, Mocks::Monitor(), L"WorkAreaIn");
            auto set = MakeZoneSet(config);

            set->MoveWindowIntoZoneByDirection(Mocks::Window(), Mocks::Window(), VK_RIGHT, true);
        }

        TEST_METHOD (MoveRightNoZones)
        {
            HWND window = Mocks::Window();
            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT, true);
            Assert::IsTrue(m_zone1->ContainsWindow(window));
            Assert::IsFalse(m_zone2->ContainsWindow(window));
            Assert::IsFalse(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveLeftNoZones)
        {
            HWND window = Mocks::Window();
            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsFalse(m_zone2->ContainsWindow(window));
            Assert::IsTrue(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveRightTwice)
        {
            HWND window = Mocks::Window();
            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT, true);
            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsTrue(m_zone2->ContainsWindow(window));
            Assert::IsFalse(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveLeftTwice)
        {
            HWND window = Mocks::Window();
            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT, true);
            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsTrue(m_zone2->ContainsWindow(window));
            Assert::IsFalse(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveRightMoreThanZonesCount)
        {
            HWND window = Mocks::Window();
            for (int i = 0; i <= m_set->GetZones().size(); i++)
            {
                m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT, true);
            }

            Assert::IsTrue(m_zone1->ContainsWindow(window));
            Assert::IsFalse(m_zone2->ContainsWindow(window));
            Assert::IsFalse(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveLeftMoreThanZonesCount)
        {
            HWND window = Mocks::Window();
            for (int i = 0; i <= m_set->GetZones().size(); i++)
            {
                m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT, true);
            }

            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsFalse(m_zone2->ContainsWindow(window));
            Assert::IsTrue(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveWindowIntoZoneByDirectionRight)
        {
            HWND window = Mocks::Window();
            m_zone1->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsTrue(m_zone2->ContainsWindow(window));
            Assert::IsFalse(m_zone3->ContainsWindow(window));

            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsFalse(m_zone2->ContainsWindow(window));
            Assert::IsTrue(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveRightWithSameWindowAdded)
        {
            HWND window = Mocks::Window();
            m_zone1->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            m_zone2->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);

            Assert::IsTrue(m_zone1->ContainsWindow(window));
            Assert::IsTrue(m_zone2->ContainsWindow(window));
            Assert::IsFalse(m_zone3->ContainsWindow(window));

            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsTrue(m_zone2->ContainsWindow(window));
            Assert::IsFalse(m_zone3->ContainsWindow(window));

            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsFalse(m_zone2->ContainsWindow(window));
            Assert::IsTrue(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveRightWithDifferentWindowsAdded)
        {
            HWND window1 = Mocks::Window();
            HWND window2 = Mocks::Window();
            m_zone1->AddWindowToZone(window1, Mocks::Window(), false /*stampZone*/);
            m_zone2->AddWindowToZone(window2, Mocks::Window(), false /*stampZone*/);

            Assert::IsTrue(m_zone1->ContainsWindow(window1));
            Assert::IsFalse(m_zone2->ContainsWindow(window1));
            Assert::IsFalse(m_zone3->ContainsWindow(window1));
            Assert::IsFalse(m_zone1->ContainsWindow(window2));
            Assert::IsTrue(m_zone2->ContainsWindow(window2));
            Assert::IsFalse(m_zone3->ContainsWindow(window2));

            m_set->MoveWindowIntoZoneByDirection(window1, Mocks::Window(), VK_RIGHT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window1));
            Assert::IsTrue(m_zone2->ContainsWindow(window1));
            Assert::IsFalse(m_zone3->ContainsWindow(window1));
            Assert::IsFalse(m_zone1->ContainsWindow(window2));
            Assert::IsTrue(m_zone2->ContainsWindow(window2));
            Assert::IsFalse(m_zone3->ContainsWindow(window2));

            m_set->MoveWindowIntoZoneByDirection(window1, Mocks::Window(), VK_RIGHT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window1));
            Assert::IsFalse(m_zone2->ContainsWindow(window1));
            Assert::IsTrue(m_zone3->ContainsWindow(window1));
            Assert::IsFalse(m_zone1->ContainsWindow(window2));
            Assert::IsTrue(m_zone2->ContainsWindow(window2));
            Assert::IsFalse(m_zone3->ContainsWindow(window2));
        }

        TEST_METHOD (MoveWindowIntoZoneByDirectionLeft)
        {
            HWND window = Mocks::Window();
            m_zone3->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsTrue(m_zone2->ContainsWindow(window));
            Assert::IsFalse(m_zone3->ContainsWindow(window));

            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT, true);
            Assert::IsTrue(m_zone1->ContainsWindow(window));
            Assert::IsFalse(m_zone2->ContainsWindow(window));
            Assert::IsFalse(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveLeftWithSameWindowAdded)
        {
            HWND window = Mocks::Window();
            m_zone2->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            m_zone3->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);

            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsTrue(m_zone2->ContainsWindow(window));
            Assert::IsTrue(m_zone3->ContainsWindow(window));

            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT, true);
            Assert::IsTrue(m_zone1->ContainsWindow(window));
            Assert::IsFalse(m_zone2->ContainsWindow(window));
            Assert::IsTrue(m_zone3->ContainsWindow(window));

            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsFalse(m_zone2->ContainsWindow(window));
            Assert::IsTrue(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveLeftWithDifferentWindowsAdded)
        {
            HWND window1 = Mocks::Window();
            HWND window2 = Mocks::Window();
            m_zone2->AddWindowToZone(window1, Mocks::Window(), false /*stampZone*/);
            m_zone3->AddWindowToZone(window2, Mocks::Window(), false /*stampZone*/);

            Assert::IsFalse(m_zone1->ContainsWindow(window1));
            Assert::IsTrue(m_zone2->ContainsWindow(window1));
            Assert::IsFalse(m_zone3->ContainsWindow(window1));
            Assert::IsFalse(m_zone1->ContainsWindow(window2));
            Assert::IsFalse(m_zone2->ContainsWindow(window2));
            Assert::IsTrue(m_zone3->ContainsWindow(window2));

            m_set->MoveWindowIntoZoneByDirection(window2, Mocks::Window(), VK_LEFT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window1));
            Assert::IsTrue(m_zone2->ContainsWindow(window1));
            Assert::IsFalse(m_zone3->ContainsWindow(window1));
            Assert::IsFalse(m_zone1->ContainsWindow(window2));
            Assert::IsTrue(m_zone2->ContainsWindow(window2));
            Assert::IsFalse(m_zone3->ContainsWindow(window2));

            m_set->MoveWindowIntoZoneByDirection(window2, Mocks::Window(), VK_LEFT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window1));
            Assert::IsTrue(m_zone2->ContainsWindow(window1));
            Assert::IsFalse(m_zone3->ContainsWindow(window1));
            Assert::IsTrue(m_zone1->ContainsWindow(window2));
            Assert::IsFalse(m_zone2->ContainsWindow(window2));
            Assert::IsFalse(m_zone3->ContainsWindow(window2));
        }

        TEST_METHOD (MoveWindowIntoZoneByDirectionWrapAroundRight)
        {
            HWND window = Mocks::Window();
            m_zone3->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT, true);
            Assert::IsTrue(m_zone1->ContainsWindow(window));
            Assert::IsFalse(m_zone2->ContainsWindow(window));
            Assert::IsFalse(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveWindowIntoZoneByDirectionWrapAroundLeft)
        {
            HWND window = Mocks::Window();
            m_zone1->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT, true);
            Assert::IsFalse(m_zone1->ContainsWindow(window));
            Assert::IsFalse(m_zone2->ContainsWindow(window));
            Assert::IsTrue(m_zone3->ContainsWindow(window));
        }

        TEST_METHOD (MoveSecondWindowIntoSameZone)
        {
            HWND window1 = Mocks::Window();
            m_zone1->AddWindowToZone(window1, Mocks::Window(), false /*stampZone*/);

            HWND window2 = Mocks::Window();
            m_set->MoveWindowIntoZoneByDirection(window2, Mocks::Window(), VK_RIGHT, true);

            Assert::IsTrue(m_zone1->ContainsWindow(window1));
            Assert::IsFalse(m_zone2->ContainsWindow(window1));
            Assert::IsFalse(m_zone3->ContainsWindow(window1));

            Assert::IsTrue(m_zone1->ContainsWindow(window2));
            Assert::IsFalse(m_zone2->ContainsWindow(window2));
            Assert::IsFalse(m_zone3->ContainsWindow(window2));
        }

        TEST_METHOD (MoveRightMoreThanZoneCountReturnsFalse)
        {
            HWND window = Mocks::Window();
            m_zone1->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            for (size_t i = 0; i < m_set->GetZones().size() - 1; ++i)
            {
                m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT, false);
            }
            bool moreZonesInLayout = m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT, false);
            Assert::IsFalse(moreZonesInLayout);
        }

        TEST_METHOD (MoveLeftMoreThanZoneCountReturnsFalse)
        {
            HWND window = Mocks::Window();
            m_zone3->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            for (size_t i = 0; i < m_set->GetZones().size() - 1; ++i)
            {
                m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT, false);
            }
            bool moreZonesInLayout = m_set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT, false);
            Assert::IsFalse(moreZonesInLayout);
        }
    };

    TEST_CLASS (ZoneSetCalculateZonesUnitTests)
    {
        GUID m_id;
        const TZoneSetLayoutType m_layoutType = TZoneSetLayoutType::Custom;
        const PCWSTR m_resolutionKey = L"WorkAreaIn";
        winrt::com_ptr<IZoneSet> m_set;

        HMONITOR m_monitor;
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

        MONITORINFO m_monitorInfo;

        const std::wstring m_path = PTSettingsHelper::get_module_save_folder_location(L"FancyZones") + L"\\" + std::wstring(L"testzones.json");

        TEST_METHOD_INITIALIZE(Init)
            {
                auto hres = CoCreateGuid(&m_id);
                Assert::AreEqual(S_OK, hres);

                m_monitor = MonitorFromPoint(POINT{ 0, 0 }, MONITOR_DEFAULTTOPRIMARY);

                ZoneSetConfig m_config = ZoneSetConfig(m_id, m_layoutType, m_monitor, m_resolutionKey);
                m_set = MakeZoneSet(m_config);
            }

            TEST_METHOD_CLEANUP(Cleanup)
                {
                    std::filesystem::remove(m_path);
                }

                void checkZones(const winrt::com_ptr<IZoneSet>& set, size_t expectedCount, MONITORINFO monitorInfo)
                {
                    auto zones = set->GetZones();
                    Assert::AreEqual(expectedCount, zones.size());

                    for (const auto& zone : zones)
                    {
                        Assert::IsTrue(zone->IsEmpty());

                        const auto& zoneRect = zone->GetZoneRect();
                        Assert::IsTrue(zoneRect.left >= 0, L"left border is less than zero");
                        Assert::IsTrue(zoneRect.top >= 0, L"top border is less than zero");

                        Assert::IsTrue(zoneRect.left < zoneRect.right, L"rect.left >= rect.right");
                        Assert::IsTrue(zoneRect.top < zoneRect.bottom, L"rect.top >= rect.bottom");

                        Assert::IsTrue(zoneRect.right <= monitorInfo.rcWork.right, L"right border is bigger than monitor work space");
                        Assert::IsTrue(zoneRect.bottom <= monitorInfo.rcWork.bottom, L"bottom border is bigger than monitor work space");
                    }
                }

            public:
                TEST_METHOD (ValidValues)
                {
                    const int spacing = 10;
                    const int zoneCount = 10;

                    for (int type = static_cast<int>(JSONHelpers::ZoneSetLayoutType::Focus); type < static_cast<int>(JSONHelpers::ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<TZoneSetLayoutType>(type), m_monitor, m_resolutionKey);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            auto set = MakeZoneSet(m_config);
                            auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                            Assert::IsTrue(result);
                            checkZones(set, zoneCount, monitorInfo);
                        }
                    }
                }
                TEST_METHOD (InvalidMonitorInfo)
                {
                    const int spacing = 10;
                    const int zoneCount = 10;

                    for (int type = static_cast<int>(JSONHelpers::ZoneSetLayoutType::Focus); type < static_cast<int>(JSONHelpers::ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<TZoneSetLayoutType>(type), m_monitor, m_resolutionKey);
                        auto set = MakeZoneSet(m_config);

                        MONITORINFO info{};
                        auto result = set->CalculateZones(info, zoneCount, spacing);
                        Assert::IsFalse(result);
                    }
                }

                TEST_METHOD (ZeroSpacing)
                {
                    const int spacing = 0;
                    const int zoneCount = 10;

                    for (int type = static_cast<int>(JSONHelpers::ZoneSetLayoutType::Focus); type < static_cast<int>(JSONHelpers::ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<TZoneSetLayoutType>(type), m_monitor, m_resolutionKey);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            auto set = MakeZoneSet(m_config);
                            auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                            Assert::IsTrue(result);
                            checkZones(set, zoneCount, monitorInfo);
                        }
                    }
                }

                TEST_METHOD (NegativeSpacing)
                {
                    const int spacing = -1;
                    const int zoneCount = 10;

                    for (int type = static_cast<int>(JSONHelpers::ZoneSetLayoutType::Focus); type < static_cast<int>(JSONHelpers::ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<TZoneSetLayoutType>(type), m_monitor, m_resolutionKey);
                        auto set = MakeZoneSet(m_config);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                            if (type == static_cast<int>(JSONHelpers::ZoneSetLayoutType::Focus))
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

                    for (int type = static_cast<int>(JSONHelpers::ZoneSetLayoutType::Focus); type < static_cast<int>(JSONHelpers::ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<TZoneSetLayoutType>(type), m_monitor, m_resolutionKey);
                        auto set = MakeZoneSet(m_config);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            const int spacing = monitorInfo.rcWork.right;
                            auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                            if (type == static_cast<int>(JSONHelpers::ZoneSetLayoutType::Focus))
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

                TEST_METHOD (VerticallyBigSpacing)
                {
                    const int zoneCount = 10;

                    for (int type = static_cast<int>(JSONHelpers::ZoneSetLayoutType::Focus); type < static_cast<int>(JSONHelpers::ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<TZoneSetLayoutType>(type), m_monitor, m_resolutionKey);
                        auto set = MakeZoneSet(m_config);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            const int spacing = monitorInfo.rcWork.bottom;
                            auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                            if (type == static_cast<int>(JSONHelpers::ZoneSetLayoutType::Focus))
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

                TEST_METHOD (ZeroZoneCount)
                {
                    const int spacing = 10;
                    const int zoneCount = 0;

                    for (int type = static_cast<int>(JSONHelpers::ZoneSetLayoutType::Focus); type < static_cast<int>(JSONHelpers::ZoneSetLayoutType::Custom); type++)
                    {
                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<TZoneSetLayoutType>(type), m_monitor, m_resolutionKey);
                        auto set = MakeZoneSet(m_config);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                            Assert::IsFalse(result);
                        }
                    }
                }

                TEST_METHOD (BigZoneCount)
                {
                    const int spacing = 1;

                    for (int type = static_cast<int>(JSONHelpers::ZoneSetLayoutType::Focus); type < static_cast<int>(JSONHelpers::ZoneSetLayoutType::Custom); type++)
                    {
                        const int spacing = 10;
                        const int zoneCount = 40; //editor limit

                        ZoneSetConfig m_config = ZoneSetConfig(m_id, static_cast<TZoneSetLayoutType>(type), m_monitor, m_resolutionKey);

                        for (const auto& monitorInfo : m_popularMonitors)
                        {
                            auto set = MakeZoneSet(m_config);
                            auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                            Assert::IsTrue(result);
                            checkZones(set, zoneCount, monitorInfo);
                        }
                    }
                }

                TEST_METHOD (CustomZonesFromUnexistedFile)
                {
                    const int spacing = 10;
                    const int zoneCount = 0;

                    //be sure that file does not exist
                    if (std::filesystem::exists(m_path))
                    {
                        std::filesystem::remove(m_path);
                    }

                    ZoneSetConfig m_config = ZoneSetConfig(m_id, TZoneSetLayoutType::Custom, m_monitor, m_resolutionKey);
                    auto set = MakeZoneSet(m_config);

                    for (const auto& monitorInfo : m_popularMonitors)
                    {
                        auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                        Assert::IsFalse(result);
                    }
                }

                TEST_METHOD (CustomZoneFromEmptyFile)
                {
                    const int spacing = 10;
                    const int zoneCount = 0;

                    Assert::IsTrue(std::filesystem::create_directories(m_path));
                    Assert::IsTrue(std::filesystem::exists(m_path));

                    ZoneSetConfig m_config = ZoneSetConfig(m_id, TZoneSetLayoutType::Custom, m_monitor, m_resolutionKey);
                    auto set = MakeZoneSet(m_config);

                    for (const auto& monitorInfo : m_popularMonitors)
                    {
                        auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                        Assert::IsFalse(result);
                    }
                }

                TEST_METHOD (CustomZoneFromInvalidCanvasLayoutInfo)
                {
                    using namespace JSONHelpers;

                    const std::wstring uuid = L"uuid";
                    const CanvasLayoutInfo info{ -1, 100, { CanvasLayoutInfo::Rect{ -10, -10, 100, 100 }, CanvasLayoutInfo::Rect{ 50, 50, 150, 150 } } };
                    CustomZoneSetJSON expected{ uuid, CustomZoneSetData{ L"name", CustomLayoutType::Canvas, info } };
                    json::to_file(m_path, CustomZoneSetJSON::ToJson(expected));
                    Assert::IsTrue(std::filesystem::exists(m_path));

                    const int spacing = 10;
                    const int zoneCount = static_cast<int>(info.zones.size());

                    ZoneSetConfig m_config = ZoneSetConfig(m_id, TZoneSetLayoutType::Custom, m_monitor, m_resolutionKey);
                    auto set = MakeZoneSet(m_config);

                    for (const auto& monitorInfo : m_popularMonitors)
                    {
                        auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                        Assert::IsFalse(result);
                    }
                }

                TEST_METHOD (CustomZoneFromInvalidGridLayoutInfo)
                {
                    using namespace JSONHelpers;

                    const std::wstring uuid = L"uuid";
                    const GridLayoutInfo grid(GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
                        .rows = 1,
                        .columns = 3,
                        .rowsPercents = { -100 }, //rows percents are negative
                        .columnsPercents = { 2500, 2500 }, //column percents count is invalid
                        .cellChildMap = { { 0, 1, 2 } } }));
                    CustomZoneSetJSON expected{ uuid, CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } };
                    json::to_file(m_path, CustomZoneSetJSON::ToJson(expected));
                    Assert::IsTrue(std::filesystem::exists(m_path));

                    const int spacing = 0;
                    const int zoneCount = grid.rows() * grid.columns();

                    ZoneSetConfig m_config = ZoneSetConfig(m_id, TZoneSetLayoutType::Custom, m_monitor, m_resolutionKey);
                    auto set = MakeZoneSet(m_config);

                    for (const auto& monitorInfo : m_popularMonitors)
                    {
                        auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                        Assert::IsFalse(result);
                    }
                }

                TEST_METHOD (CustomZoneFromValidCanvasLayoutInfo)
                {
                    using namespace JSONHelpers;

                    //prepare device data
                    {
                        const std::wstring zoneUuid = L"default_device_id";
                        DeviceInfoJSON deviceInfo{ zoneUuid, DeviceInfoData{ ZoneSetData{ L"uuid", ZoneSetLayoutType::Custom }, true, 16, 3 } };
                        const std::wstring deviceInfoPath = FancyZonesDataInstance().GetPersistFancyZonesJSONPath() + L".device_info_tmp";
                        FancyZonesDataInstance().SerializeDeviceInfoToTmpFile(deviceInfo, deviceInfoPath);

                        FancyZonesDataInstance().ParseDeviceInfoFromTmpFile(deviceInfoPath);
                        std::filesystem::remove(deviceInfoPath);
                    }

                    //prepare expected data
                    wil::unique_cotaskmem_string uuid;
                    Assert::AreEqual(S_OK, StringFromCLSID(m_id, &uuid));
                    const CanvasLayoutInfo info{ 123, 321, { CanvasLayoutInfo::Rect{ 0, 0, 100, 100 }, CanvasLayoutInfo::Rect{ 50, 50, 150, 150 } } };
                    CustomZoneSetJSON expected{ uuid.get(), CustomZoneSetData{ L"name", CustomLayoutType::Canvas, info } };
                    json::to_file(m_path, CustomZoneSetJSON::ToJson(expected));
                    Assert::IsTrue(std::filesystem::exists(m_path));
                    FancyZonesDataInstance().ParseCustomZoneSetFromTmpFile(m_path);

                    //test
                    const int spacing = 10;
                    const int zoneCount = static_cast<int>(info.zones.size());
                    ZoneSetConfig m_config = ZoneSetConfig(m_id, TZoneSetLayoutType::Custom, m_monitor, m_resolutionKey);
                    for (const auto& monitorInfo : m_popularMonitors)
                    {
                        auto set = MakeZoneSet(m_config);
                        auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                        Assert::IsTrue(result);
                        checkZones(set, zoneCount, monitorInfo);
                    }
                }

                TEST_METHOD (CustomZoneFromValidGridFullLayoutInfo)
                {
                    using namespace JSONHelpers;

                    //prepare device data
                    {
                        const std::wstring zoneUuid = L"default_device_id";
                        DeviceInfoJSON deviceInfo{ zoneUuid, DeviceInfoData{ ZoneSetData{ L"uuid", ZoneSetLayoutType::Custom }, true, 16, 3 } };
                        const std::wstring deviceInfoPath = FancyZonesDataInstance().GetPersistFancyZonesJSONPath() + L".device_info_tmp";
                        FancyZonesDataInstance().SerializeDeviceInfoToTmpFile(deviceInfo, deviceInfoPath);

                        FancyZonesDataInstance().ParseDeviceInfoFromTmpFile(deviceInfoPath);
                        std::filesystem::remove(deviceInfoPath);
                    }

                    //prepare expected data
                    wil::unique_cotaskmem_string uuid;
                    Assert::AreEqual(S_OK, StringFromCLSID(m_id, &uuid));
                    const GridLayoutInfo grid(GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
                        .rows = 1,
                        .columns = 3,
                        .rowsPercents = { 10000 },
                        .columnsPercents = { 2500, 5000, 2500 },
                        .cellChildMap = { { 0, 1, 2 } } }));
                    CustomZoneSetJSON expected{ uuid.get(), CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } };
                    json::to_file(m_path, CustomZoneSetJSON::ToJson(expected));
                    Assert::IsTrue(std::filesystem::exists(m_path));
                    FancyZonesDataInstance().ParseCustomZoneSetFromTmpFile(m_path);

                    const int spacing = 10;
                    const int zoneCount = grid.rows() * grid.columns();

                    ZoneSetConfig m_config = ZoneSetConfig(m_id, TZoneSetLayoutType::Custom, m_monitor, m_resolutionKey);

                    for (const auto& monitorInfo : m_popularMonitors)
                    {
                        auto set = MakeZoneSet(m_config);
                        auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                        Assert::IsTrue(result);
                        checkZones(set, zoneCount, monitorInfo);
                    }
                }

                TEST_METHOD (CustomZoneFromValidGridMinimalLayoutInfo)
                {
                    using namespace JSONHelpers;

                    const std::wstring uuid = L"uuid";
                    const GridLayoutInfo grid(GridLayoutInfo(JSONHelpers::GridLayoutInfo::Minimal{
                        .rows = 1,
                        .columns = 3 }));
                    CustomZoneSetJSON expected{ uuid, CustomZoneSetData{ L"name", CustomLayoutType::Grid, grid } };
                    json::to_file(m_path, CustomZoneSetJSON::ToJson(expected));
                    Assert::IsTrue(std::filesystem::exists(m_path));

                    const int spacing = 0;
                    const int zoneCount = grid.rows() * grid.columns();

                    ZoneSetConfig m_config = ZoneSetConfig(m_id, TZoneSetLayoutType::Custom, m_monitor, m_resolutionKey);
                    auto set = MakeZoneSet(m_config);

                    for (const auto& monitorInfo : m_popularMonitors)
                    {
                        auto result = set->CalculateZones(monitorInfo, zoneCount, spacing);
                        Assert::IsFalse(result);
                    }
                }
    };
}
