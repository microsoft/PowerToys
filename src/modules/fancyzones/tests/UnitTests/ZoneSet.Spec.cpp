#include "pch.h"
#include "lib\ZoneSet.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS(ZoneSetUnitTests){
        public:
            TEST_METHOD(TestCreateZoneSet){
                GUID zoneSetId{};
    CoCreateGuid(&zoneSetId);
    constexpr WORD layoutId = 0xFFFF;

    ZoneSetConfig config(zoneSetId, layoutId, Mocks::Monitor(), L"WorkAreaIn");
    winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);
    Assert::IsNotNull(&set);
    CustomAssert::AreEqual(set->Id(), zoneSetId);
    CustomAssert::AreEqual(set->LayoutId(), layoutId);
}

TEST_METHOD(TestAddZone)
{
    ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn");
    winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

    // Add a zone
    {
        winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
        set->AddZone(zone);
        auto zones = set->GetZones();
        Assert::IsTrue(zones.size() == 1);
        Assert::IsTrue(zones[0] == zone);
        Assert::IsTrue(zone->Id() == 1);
    }

    // Add a second zone at the back.
    {
        winrt::com_ptr<IZone> zone = MakeZone({ 0, 0, 100, 100 });
        set->AddZone(zone);
        auto zones = set->GetZones();
        Assert::IsTrue(zones.size() == 2);
        Assert::IsTrue(zones[1] == zone);
        Assert::IsTrue(zone->Id() == 2);
    }
}

TEST_METHOD(TestMoveWindowIntoZoneByIndex)
{
    ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn");
    winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

    // Add a couple of zones.
    winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
    winrt::com_ptr<IZone> zone2 = MakeZone({ 0, 0, 100, 100 });
    winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
    set->AddZone(zone1);
    set->AddZone(zone2);
    set->AddZone(zone3);

    HWND window = Mocks::Window();
    set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 1);
    Assert::IsFalse(zone1->ContainsWindow(window));
    Assert::IsTrue(zone2->ContainsWindow(window));
    Assert::IsFalse(zone3->ContainsWindow(window));
}

TEST_METHOD(TestMoveWindowIntoZoneByIndexWithNoZones)
{
    ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn");
    winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

    // Add a couple of zones.
    HWND window = Mocks::Window();
    set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
}

TEST_METHOD(TestMoveWindowIntoZoneByIndexWithInvalidIndex)
{
    ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn");
    winrt::com_ptr<IZoneSet> set = MakeZoneSet(config);

    // Add a couple of zones.
    winrt::com_ptr<IZone> zone1 = MakeZone({ 0, 0, 100, 100 });
    winrt::com_ptr<IZone> zone2 = MakeZone({ 0, 0, 100, 100 });
    winrt::com_ptr<IZone> zone3 = MakeZone({ 0, 0, 100, 100 });
    set->AddZone(zone1);
    set->AddZone(zone2);
    set->AddZone(zone3);

    HWND window = Mocks::Window();
    set->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 100);
    Assert::IsTrue(zone1->ContainsWindow(window));
    Assert::IsFalse(zone2->ContainsWindow(window));
    Assert::IsFalse(zone3->ContainsWindow(window));
}
}
;

// MoveWindowIntoZoneByDirection is complicated enough to warrant it's own test class
TEST_CLASS(MoveWindowIntoZoneByDirectionUnitTests)
{
    winrt::com_ptr<IZoneSet> set;
    winrt::com_ptr<IZone> zone1;
    winrt::com_ptr<IZone> zone2;
    winrt::com_ptr<IZone> zone3;

    TEST_METHOD_INITIALIZE(Initialize)
    {
        ZoneSetConfig config({}, 0xFFFF, Mocks::Monitor(), L"WorkAreaIn");
        set = MakeZoneSet(config);

        // Add a couple of zones.
        zone1 = MakeZone({ 0, 0, 100, 100 });
        zone2 = MakeZone({ 0, 0, 100, 100 });
        zone3 = MakeZone({ 0, 0, 100, 100 });
        set->AddZone(zone1);
        set->AddZone(zone2);
        set->AddZone(zone3);
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
