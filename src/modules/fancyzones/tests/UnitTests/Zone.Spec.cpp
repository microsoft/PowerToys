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

        HWND addWindow(const winrt::com_ptr<IZone>& zone, bool stamp)
        {
            HWND window = Mocks::WindowCreate(m_hInst);
            HWND zoneWindow = Mocks::WindowCreate(m_hInst);
            zone->AddWindowToZone(window, zoneWindow, stamp);

            return window;
        }

        void addMany(const winrt::com_ptr<IZone>& zone)
        {
            for (int i = 0; i < 10; i++)
            {
                addWindow(zone, i % 2 == 0);
            }
        }

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

        TEST_METHOD(IsEmpty)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            Assert::IsTrue(zone->IsEmpty());
        }

        TEST_METHOD(IsNonEmptyStampTrue)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            addWindow(zone, true);

            Assert::IsFalse(zone->IsEmpty());
        }

        TEST_METHOD(IsNonEmptyStampFalse)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            addWindow(zone, false);

            Assert::IsFalse(zone->IsEmpty());
        }

        TEST_METHOD(IsNonEmptyManyWindows)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND zoneWindow = Mocks::WindowCreate(m_hInst);
            for (int i = 0; i < 10; i++)
            {
                HWND window = Mocks::WindowCreate(m_hInst);
                zone->AddWindowToZone(window, zoneWindow, i % 2 == 0);
            }

            Assert::IsFalse(zone->IsEmpty());
        }

        TEST_METHOD(IsNonEmptyManyZoneWindows)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND window = Mocks::WindowCreate(m_hInst);
            for (int i = 0; i < 10; i++)
            {
                HWND zoneWindow = Mocks::WindowCreate(m_hInst);
                zone->AddWindowToZone(window, zoneWindow, i % 2 == 0);
            }

            Assert::IsFalse(zone->IsEmpty());
        }

        TEST_METHOD(IsNonEmptyMany)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            addMany(zone);

            Assert::IsFalse(zone->IsEmpty());
        }

        TEST_METHOD(ContainsWindowEmpty)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            HWND newWindow = Mocks::WindowCreate(m_hInst);
            Assert::IsFalse(zone->ContainsWindow(newWindow));
        }

        TEST_METHOD(ContainsWindowNot)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            addMany(zone);

            HWND newWindow = Mocks::WindowCreate(m_hInst);
            Assert::IsFalse(zone->ContainsWindow(newWindow));
        }

        TEST_METHOD(ContainsWindowStampTrue)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            HWND window = addWindow(zone, true);

            Assert::IsTrue(zone->ContainsWindow(window));
        }

        TEST_METHOD(ContainsWindowStampFalse)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            HWND window = addWindow(zone, false);

            Assert::IsTrue(zone->ContainsWindow(window));
        }

        TEST_METHOD(ContainsWindowManyWindows)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND zoneWindow = Mocks::WindowCreate(m_hInst);
            std::vector<HWND> windowVec{};
            for (int i = 0; i < 10; i++)
            {
                HWND window = Mocks::WindowCreate(m_hInst);
                windowVec.push_back(window);
                zone->AddWindowToZone(window, zoneWindow, i % 2 == 0);
            }

            for (auto wnd : windowVec)
            {
                Assert::IsTrue(zone->ContainsWindow(wnd));
            }
        }

        TEST_METHOD(ContainsWindowManyZoneWindows)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND window = Mocks::WindowCreate(m_hInst);
            std::vector<HWND> windowVec{};
            for (int i = 0; i < 10; i++)
            {
                HWND zoneWindow = Mocks::WindowCreate(m_hInst);
                windowVec.push_back(window);
                zone->AddWindowToZone(window, zoneWindow, i % 2 == 0);
            }

            for (auto wnd : windowVec)
            {
                Assert::IsTrue(zone->ContainsWindow(wnd));
            }
        }

        TEST_METHOD(ContainsWindowMany)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            std::vector<HWND> windowVec{};
            for (int i = 0; i < 10; i++)
            {
                HWND window = addWindow(zone, i % 2 == 0);
                windowVec.push_back(window);
            }

            for (auto wnd : windowVec)
            {
                Assert::IsTrue(zone->ContainsWindow(wnd));
            }
        }

        TEST_METHOD(AddWindowNullptr)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND window = nullptr;
            HWND zoneWindow = Mocks::WindowCreate(m_hInst);
            zone->AddWindowToZone(window, zoneWindow, true);

            Assert::IsFalse(zone->IsEmpty());
            Assert::IsTrue(zone->ContainsWindow(window));
        }

        TEST_METHOD(AddWindowZoneNullptr)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND window = Mocks::WindowCreate(m_hInst);
            HWND zoneWindow = nullptr;
            zone->AddWindowToZone(window, zoneWindow, true);

            Assert::IsFalse(zone->IsEmpty());
            Assert::IsTrue(zone->ContainsWindow(window));
        }

        TEST_METHOD(AddManySame)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND zoneWindow = Mocks::WindowCreate(m_hInst);
            HWND window = Mocks::WindowCreate(m_hInst);
            for (int i = 0; i < 10; i++)
            {
                zone->AddWindowToZone(window, zoneWindow, i % 2 == 0);
            }

            Assert::IsFalse(zone->IsEmpty());
            Assert::IsTrue(zone->ContainsWindow(window));
        }

        TEST_METHOD(AddManySameNullptr)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND zoneWindow = nullptr;
            HWND window = nullptr;
            for (int i = 0; i < 10; i++)
            {
                zone->AddWindowToZone(window, zoneWindow, i % 2 == 0);
            }

            Assert::IsTrue(zone->ContainsWindow(window));
        }

        TEST_METHOD(RemoveWindowRestoreSizeTrue)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            HWND newWindow = Mocks::WindowCreate(m_hInst);

            zone->AddWindowToZone(newWindow, Mocks::WindowCreate(m_hInst), true);
            Assert::IsFalse(zone->IsEmpty());
            Assert::IsTrue(zone->ContainsWindow(newWindow));

            zone->RemoveWindowFromZone(newWindow, true);
            Assert::IsTrue(zone->IsEmpty());
            Assert::IsFalse(zone->ContainsWindow(newWindow));
        }

        TEST_METHOD(RemoveWindowRestoreSizeFalse)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            HWND newWindow = Mocks::WindowCreate(m_hInst);

            zone->AddWindowToZone(newWindow, Mocks::WindowCreate(m_hInst), true);
            Assert::IsFalse(zone->IsEmpty());
            Assert::IsTrue(zone->ContainsWindow(newWindow));

            zone->RemoveWindowFromZone(newWindow, false);
            Assert::IsTrue(zone->IsEmpty());
            Assert::IsFalse(zone->ContainsWindow(newWindow));
        }

        TEST_METHOD(RemoveInvalidWindowRestoreSizeTrue)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            HWND newWindow = Mocks::WindowCreate(m_hInst);
            zone->RemoveWindowFromZone(newWindow, true);

            Assert::IsTrue(zone->IsEmpty());
            Assert::IsFalse(zone->ContainsWindow(newWindow));
        }

        TEST_METHOD(RemoveInvalidWindowRestoreSizeFalse)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            HWND newWindow = Mocks::WindowCreate(m_hInst);
            zone->RemoveWindowFromZone(newWindow, false);

            Assert::IsTrue(zone->IsEmpty());
            Assert::IsFalse(zone->ContainsWindow(newWindow));
        }

        TEST_METHOD(RemoveNullptrWindowRestoreSizeTrue)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            HWND newWindow = nullptr;

            zone->AddWindowToZone(newWindow, Mocks::WindowCreate(m_hInst), true);
            Assert::IsFalse(zone->IsEmpty());
            Assert::IsTrue(zone->ContainsWindow(newWindow));

            zone->RemoveWindowFromZone(newWindow, true);
            Assert::IsTrue(zone->IsEmpty());
            Assert::IsFalse(zone->ContainsWindow(newWindow));
        }

        TEST_METHOD(RemoveNullptrWindowRestoreSizeFalse)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            HWND newWindow = nullptr;

            zone->AddWindowToZone(newWindow, Mocks::WindowCreate(m_hInst), true);
            Assert::IsFalse(zone->IsEmpty());
            Assert::IsTrue(zone->ContainsWindow(newWindow));

            zone->RemoveWindowFromZone(newWindow, false);
            Assert::IsTrue(zone->IsEmpty());
            Assert::IsFalse(zone->ContainsWindow(newWindow));
        }

        TEST_METHOD(RemoveMany)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            std::vector<HWND> windowVec{};
            for (int i = 0; i < 10; i++)
            {
                HWND window = addWindow(zone, i % 2 == 0);
                windowVec.push_back(window);
            }

            for (auto wnd : windowVec)
            {
                zone->RemoveWindowFromZone(wnd, true);
            }

            Assert::IsTrue(zone->IsEmpty());
        }

        TEST_METHOD(RemoveManySame)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND zoneWindow = Mocks::WindowCreate(m_hInst);
            HWND window = Mocks::WindowCreate(m_hInst);
            for (int i = 0; i < 10; i++)
            {
                zone->AddWindowToZone(window, zoneWindow, i % 2 == 0);
            }

            zone->RemoveWindowFromZone(window, true);

            Assert::IsTrue(zone->IsEmpty());
            Assert::IsFalse(zone->ContainsWindow(window));
        }

        TEST_METHOD(RemoveDouble)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND zoneWindow = Mocks::WindowCreate(m_hInst);
            HWND window = Mocks::WindowCreate(m_hInst);
            for (int i = 0; i < 10; i++)
            {
                zone->AddWindowToZone(window, zoneWindow, i % 2 == 0);
            }

            zone->RemoveWindowFromZone(window, true);
            zone->RemoveWindowFromZone(window, true);

            Assert::IsTrue(zone->IsEmpty());
        }

        TEST_METHOD(StampTrue)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);
            size_t expected = 123456;
            zone->SetId(expected);

            HWND window = addWindow(zone, true);

            HANDLE actual = GetProp(window, ZONE_STAMP);
            Assert::IsNotNull(actual);

            size_t actualVal = HandleToLong(actual);
            Assert::AreEqual(expected, actualVal);
        }

        TEST_METHOD(StampTrueNoId)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND window = addWindow(zone, true);

            HANDLE actual = GetProp(window, ZONE_STAMP);
            Assert::IsNull(actual);
        }

        TEST_METHOD(StampFalse)
        {
            winrt::com_ptr<IZone> zone = MakeZone(m_zoneRect);

            HWND window = addWindow(zone, false);

            HANDLE actual = GetProp(window, ZONE_STAMP);
            Assert::IsNull(actual);
        }
    };
}
