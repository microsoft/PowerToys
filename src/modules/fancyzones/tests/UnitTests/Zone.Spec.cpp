#include "pch.h"
#include "lib\Zone.h"
#include "lib\Settings.h"

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
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            Assert::IsNotNull(&zone);
            CustomAssert::AreEqual(m_zoneRect, zone->GetZoneRect());
        }

        TEST_METHOD(TestCreateZoneZeroRect)
        {
            RECT zoneRect{ 0, 0, 0, 0 };
            winrt::com_ptr<IZone> zone = MakeZone(zoneRect);
            Assert::IsNotNull(&zone);
            CustomAssert::AreEqual(zoneRect, zone->GetZoneRect());
        }

        TEST_METHOD(GetSetId)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            constexpr size_t id = 10;
            zone->SetId(id);
            Assert::AreEqual(zone->Id(), id);
        }
    };
}
