#include "pch.h"
#include "lib\ZoneSet.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS(ZoneSetUnitTests)
    {
    public:
        TEST_METHOD(TestCreateZoneSet)
        {
            GUID zoneSetId{};
            CoCreateGuid(&zoneSetId);
            constexpr size_t zoneCount = 0;
            constexpr WORD layoutId = 0xFFFF;
            constexpr int outerPadding = 3;
            constexpr int innerPadding = 4;

            ZoneSetConfig config(zoneSetId, layoutId, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, zoneCount, outerPadding, innerPadding);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);
            Assert::IsNotNull(&set);
            CustomAssert::AreEqual(set->Id(), zoneSetId);
            CustomAssert::AreEqual(set->LayoutId(), layoutId);
            Assert::IsTrue(set->GetLayout() == ZoneSetLayout::Grid);
            Assert::AreEqual(set->GetZones().size(), zoneCount);
            Assert::AreEqual(set->GetInnerPadding(), innerPadding);
        }

        TEST_METHOD(TestAddZone)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            // Add a zone
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
                set->AddZone(zone, false /*front*/);
                auto zones = set->GetZones();
                Assert::IsTrue(zones.size() == 1);
                Assert::IsTrue(zones[0] == zone);
                Assert::IsTrue(zone->Id() == 1);
            }

            // Add a second zone at the back.
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
                set->AddZone(zone, false /*front*/);
                auto zones = set->GetZones();
                Assert::IsTrue(zones.size() == 2);
                Assert::IsTrue(zones[1] == zone);
                Assert::IsTrue(zone->Id() == 2);
            }
        }

        TEST_METHOD(TestAddZoneFront)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            // Add a zone.
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
                set->AddZone(zone, false /*front*/);
                auto zones = set->GetZones();
                Assert::IsTrue(zones.size() == 1);
                Assert::IsTrue(zones[0] == zone);
                Assert::IsTrue(zone->Id() == 1);
            }

            // Add a second zone at the front.
            {
                winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
                set->AddZone(zone, true /*front*/);
                auto zones = set->GetZones();
                Assert::IsTrue(zones.size() == 2);
                Assert::IsTrue(zones[0] == zone);
                Assert::IsTrue(zone->Id() == 2);
            }
        }

        TEST_METHOD(TestRemoveZone)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            // Add a zone.
            winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
            set->AddZone(zone, false /*front*/);

            // And remove it.
            set->RemoveZone(zone);
            Assert::IsTrue(set->GetZones().size() == 0);
        }

        TEST_METHOD(TestRemoveInvalidZone)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
            Assert::AreEqual(set->RemoveZone(zone), E_INVALIDARG);
        }

        TEST_METHOD(TestMoveZoneToFront)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            // Add a couple of zones.
            winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone2 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
            set->AddZone(zone1, false /*front*/);
            set->AddZone(zone2, false /*front*/);
            set->AddZone(zone3, false /*front*/);

            // And move it to the back.
            set->MoveZoneToFront(zone3);
            auto zones = set->GetZones();
            Assert::IsTrue(zones.size() == 3);
            Assert::IsTrue(zones[0] == zone3);
            Assert::IsTrue(zones[1] == zone1);
            Assert::IsTrue(zones[2] == zone2);
        }

        TEST_METHOD(TestMoveZoneToFrontWithInvalidZone)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            // Add a couple of zones.
            winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone2 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
            set->AddZone(zone1, false /*front*/);
            set->AddZone(zone2, false /*front*/);
            set->AddZone(zone3, false /*front*/);

            // Create an invalid zone and try to move it.
            winrt::com_ptr<IZone> invalidZone = MakeZone({ 0, 0, 100, 100 });
            set->MoveZoneToFront(invalidZone);
            auto zones = set->GetZones();
            Assert::IsTrue(zones.size() == 3);
            Assert::IsTrue(zones[0] == zone1);
            Assert::IsTrue(zones[1] == zone2);
            Assert::IsTrue(zones[2] == zone3);
        }

        TEST_METHOD(TestMoveZoneToBack)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            // Add a couple of zones.
            winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone2 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
            set->AddZone(zone1, false /*front*/);
            set->AddZone(zone2, false /*front*/);
            set->AddZone(zone3, false /*front*/);

            // And move it to the back.
            set->MoveZoneToBack(zone1);
            auto zones = set->GetZones();
            Assert::IsTrue(zones.size() == 3);
            Assert::IsTrue(zones[0] == zone2);
            Assert::IsTrue(zones[1] == zone3);
            Assert::IsTrue(zones[2] == zone1);
        }

        TEST_METHOD(TestMoveZoneToBackWithInvalidZone)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            // Add a couple of zones.
            winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone2 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
            set->AddZone(zone1, false /*front*/);
            set->AddZone(zone2, false /*front*/);
            set->AddZone(zone3, false /*front*/);

            // Create an invalid zone and try to move it.
            winrt::com_ptr<IZone> invalidZone = MakeZone({ 0, 0, 100, 100 });
            set->MoveZoneToBack(invalidZone);
            auto zones = set->GetZones();
            Assert::IsTrue(zones.size() == 3);
            Assert::IsTrue(zones[0] == zone1);
            Assert::IsTrue(zones[1] == zone2);
            Assert::IsTrue(zones[2] == zone3);
        }

        TEST_METHOD(TestMoveWindowIntoZoneByIndex)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            // Add a couple of zones.
            winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone2 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
            set->AddZone(zone1, false /*front*/);
            set->AddZone(zone2, false /*front*/);
            set->AddZone(zone3, false /*front*/);

            HWND window = Mocks::Window();
            set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 1);
            Assert::IsFalse(zone1->ContainsWindow(window));
            Assert::IsTrue(zone2->ContainsWindow(window));
            Assert::IsFalse(zone3->ContainsWindow(window));
        }

        TEST_METHOD(TestMoveWindowIntoZoneByIndexWithNoZones)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            // Add a couple of zones.
            HWND window = Mocks::Window();
            set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
        }

        TEST_METHOD(TestMoveWindowIntoZoneByIndexWithInvalidIndex)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

            // Add a couple of zones.
            winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone2 = MakeZone({ 0, 0, 100, 100 });
            winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
            set->AddZone(zone1, false /*front*/);
            set->AddZone(zone2, false /*front*/);
            set->AddZone(zone3, false /*front*/);

            HWND window = Mocks::Window();
            set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 100);
            Assert::IsTrue(zone1->ContainsWindow(window));
            Assert::IsFalse(zone2->ContainsWindow(window));
            Assert::IsFalse(zone3->ContainsWindow(window));
        }
    };

    // MoveWindowIntoZoneByDirection is complicated enough to warrant it's own test class
    TEST_CLASS(MoveWindowIntoZoneByDirectionUnitTests)
    {
        winrt::com_ptr<IZoneSet> set;
        winrt::com_ptr<IZone> zone1;
        winrt::com_ptr<IZone> zone2;
        winrt::com_ptr<IZone> zone3;

        TEST_METHOD_INITIALIZE(Initialize)
        {
            ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn", ZoneSetLayout::Grid, 0, 3, 4);
            set = MakeZoneSet(config);

            // Add a couple of zones.
            zone1 = MakeZone({ 0, 0, 100, 100 });
            zone2 = MakeZone({ 0, 0, 100, 100 });
            zone3 = MakeZone({ 0, 0, 100, 100 });
            set->AddZone(zone1, false /*front*/);
            set->AddZone(zone2, false /*front*/);
            set->AddZone(zone3, false /*front*/);
        }

        TEST_METHOD(MoveWindowIntoZoneByDirectionRightNoZones)
        {
            HWND window = Mocks::Window();
            set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT);
            Assert::IsTrue(zone1->ContainsWindow(window));
            Assert::IsFalse(zone2->ContainsWindow(window));
            Assert::IsFalse(zone3->ContainsWindow(window));
        }

        TEST_METHOD(MoveWindowIntoZoneByDirectionLeftNoZones)
        {
            HWND window = Mocks::Window();
            set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT);
            Assert::IsFalse(zone1->ContainsWindow(window));
            Assert::IsFalse(zone2->ContainsWindow(window));
            Assert::IsTrue(zone3->ContainsWindow(window));
        }

        TEST_METHOD(MoveWindowIntoZoneByDirectionRight)
        {
            HWND window = Mocks::Window();
            zone1->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT);
            Assert::IsFalse(zone1->ContainsWindow(window));
            Assert::IsTrue(zone2->ContainsWindow(window));
            Assert::IsFalse(zone3->ContainsWindow(window));

            set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT);
            Assert::IsFalse(zone1->ContainsWindow(window));
            Assert::IsFalse(zone2->ContainsWindow(window));
            Assert::IsTrue(zone3->ContainsWindow(window));
        }

        TEST_METHOD(MoveWindowIntoZoneByDirectionLeft)
        {
            HWND window = Mocks::Window();
            zone3->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT);
            Assert::IsFalse(zone1->ContainsWindow(window));
            Assert::IsTrue(zone2->ContainsWindow(window));
            Assert::IsFalse(zone3->ContainsWindow(window));

            set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT);
            Assert::IsTrue(zone1->ContainsWindow(window));
            Assert::IsFalse(zone2->ContainsWindow(window));
            Assert::IsFalse(zone3->ContainsWindow(window));
        }

        TEST_METHOD(MoveWindowIntoZoneByDirectionWrapAroundRight)
        {
            HWND window = Mocks::Window();
            zone3->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_RIGHT);
            Assert::IsTrue(zone1->ContainsWindow(window));
            Assert::IsFalse(zone2->ContainsWindow(window));
            Assert::IsFalse(zone3->ContainsWindow(window));
        }

        TEST_METHOD(MoveWindowIntoZoneByDirectionWrapAroundLeft)
        {
            HWND window = Mocks::Window();
            zone1->AddWindowToZone(window, Mocks::Window(), false /*stampZone*/);
            set->MoveWindowIntoZoneByDirection(window, Mocks::Window(), VK_LEFT);
            Assert::IsFalse(zone1->ContainsWindow(window));
            Assert::IsFalse(zone2->ContainsWindow(window));
            Assert::IsTrue(zone3->ContainsWindow(window));
        }
    };
}
