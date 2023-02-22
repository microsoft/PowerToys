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
            m_hInst = static_cast<HINSTANCE>(GetModuleHandleW(nullptr));
        }

    public:
        TEST_METHOD(TestCreateZone)
        {
            Zone zone(m_zoneRect, 1);
            Assert::IsTrue(zone.IsValid());
            CustomAssert::AreEqual(m_zoneRect, zone.GetZoneRect());
        }

        TEST_METHOD(TestCreateZoneZeroRect)
        {
            RECT zoneRect{ 0, 0, 0, 0 };
            Zone zone(zoneRect, 1);
            Assert::IsTrue(zone.IsValid());
            CustomAssert::AreEqual(zoneRect, zone.GetZoneRect());
        }

        TEST_METHOD(GetSetId)
        {
            constexpr ZoneIndex zoneId = 123;
            Zone zone(m_zoneRect, zoneId);

            Assert::IsTrue(zone.IsValid());
            Assert::AreEqual(zone.Id(), zoneId);
        }

        TEST_METHOD(InvalidId)
        {
            Zone zone(m_zoneRect, -1);
            Assert::IsFalse(zone.IsValid());
        }

        TEST_METHOD (InvalidRect)
        {
            Zone zone({ 100, 100, 99, 101 }, 1);
            Assert::IsFalse(zone.IsValid());
        }
    };
}
