#include "pch.h"
#include "FancyZonesLib\Zone.h"
#include "FancyZonesLib\Settings.h"

#include "Util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS(ZoneUnitTests)
    {
    private:
        RECT m_zoneRect{ 10, 10, 200, 200 };
        HINSTANCE m_hInst{};

        TEST_METHOD_INITIALIZE(Init)
        {
            m_hInst = (HINSTANCE)GetModuleHandleW(nullptr);
        }

    public:
        TEST_METHOD(TestCreateZone)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect, 1);
            Assert::IsNotNull(&zone);
            CustomAssert::AreEqual(m_zoneRect, zone->GetZoneRect());
        }

        TEST_METHOD(TestCreateZoneZeroRect)
        {
            RECT zoneRect{ 0, 0, 0, 0 };
            winrt::com_ptr<IZone> zone = MakeZone(zoneRect, 1);
            Assert::IsNotNull(&zone);
            CustomAssert::AreEqual(zoneRect, zone->GetZoneRect());
        }

        TEST_METHOD(GetSetId)
        {
            constexpr ZoneIndex zoneId = 123;
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect, zoneId);

            Assert::AreEqual(zone->Id(), zoneId);
        }
    };
}
