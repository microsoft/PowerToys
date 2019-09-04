#include "pch.h"
#include "lib\Zone.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS(ZoneUnitTests)
    {
    public:
        TEST_METHOD(TestCreateZone)
        {
            RECT zoneRect{ 10, 10, 200, 200 };
            winrt::com_ptr<IZone> zone = MakeZone(zoneRect);
            Assert::IsNotNull(&zone);
            CustomAssert::AreEqual(zoneRect, zone->GetZoneRect());

            constexpr size_t id = 10;
            zone->SetId(id);
            Assert::AreEqual(zone->Id(), id);
        }

        TEST_METHOD(ContainsWindow)
        {
            RECT zoneRect{ 10, 10, 200, 200 };
            winrt::com_ptr<IZone> zone = MakeZone(zoneRect);
            HWND newWindow = Mocks::Window();
            Assert::IsFalse(zone->ContainsWindow(newWindow));
        }

        TEST_METHOD(TestAddRemoveWindow)
        {
            RECT zoneRect{ 10, 10, 200, 200 };
            winrt::com_ptr<IZone> zone = MakeZone(zoneRect);
            HWND newWindow = Mocks::Window();

            Assert::IsFalse(zone->ContainsWindow(newWindow));
            zone->AddWindowToZone(newWindow, Mocks::Window(), true);
            Assert::IsTrue(zone->ContainsWindow(newWindow));

            zone->RemoveWindowFromZone(newWindow, false);
            Assert::IsFalse(zone->ContainsWindow(newWindow));
        }

        TEST_METHOD(TestRemoveInvalidWindow)
        {
            RECT zoneRect{ 10, 10, 200, 200 };
            winrt::com_ptr<IZone> zone = MakeZone(zoneRect);
            HWND newWindow = Mocks::Window();
            zone->RemoveWindowFromZone(newWindow, false);
        }

    };
}
