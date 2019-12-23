#include "pch.h"

#include <lib/util.h>
#include <lib/ZoneSet.h>
#include <lib/ZoneWindow.h>
#include "Util.h"
using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    struct MockZoneWindowHost : public winrt::implements<MockZoneWindowHost, IZoneWindowHost>
    {
        IFACEMETHODIMP_(void)
        MoveWindowsOnActiveZoneSetChange() noexcept {};
        IFACEMETHODIMP_(COLORREF)
        GetZoneHighlightColor() noexcept
        {
            return RGB(0xFF, 0xFF, 0xFF);
        }
        IFACEMETHODIMP_(GUID)
        GetCurrentMonitorZoneSetId(HMONITOR monitor) noexcept
        {
            return m_guid;
        }
        IFACEMETHODIMP_(int)
        GetZoneHighlightOpacity() noexcept
        {
            return 225;
        }

        GUID m_guid;
    };

    TEST_CLASS(ZoneWindowUnitTests){
        public:

            TEST_METHOD(TestCreateZoneWindow){
                winrt::com_ptr<IZoneWindow> zoneWindow = MakeZoneWindow(nullptr, Mocks::Instance(), Mocks::Monitor(), L"DeviceId", L"MyVirtualDesktopId", false);
    Assert::IsNotNull(zoneWindow.get());
}

TEST_METHOD(TestDeviceId)
{
    // Window initialization requires a valid HMONITOR - just use the primary for now.
    HMONITOR pimaryMonitor = MonitorFromWindow(HWND(), MONITOR_DEFAULTTOPRIMARY);
    MockZoneWindowHost host;
    std::wstring expectedDeviceId = L"SomeRandomValue";
    winrt::com_ptr<IZoneWindow> zoneWindow = MakeZoneWindow(dynamic_cast<IZoneWindowHost*>(&host), Mocks::Instance(), pimaryMonitor, expectedDeviceId.c_str(), L"MyVirtualDesktopId", false);

    Assert::AreEqual(expectedDeviceId, zoneWindow->DeviceId());
}

TEST_METHOD(TestUniqueId)
{
    // Unique id of the format "ParsedMonitorDeviceId_MonitorWidth_MonitorHeight_VirtualDesktopId
    // Example: "DELA026#5&10a58c63&0&UID16777488_1024_768_MyVirtualDesktopId"
    std::wstring deviceId(L"\\\\?\\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}");
    // Window initialization requires a valid HMONITOR - just use the primary for now.
    HMONITOR pimaryMonitor = MonitorFromWindow(HWND(), MONITOR_DEFAULTTOPRIMARY);
    MONITORINFO info;
    info.cbSize = sizeof(info);
    Assert::IsTrue(GetMonitorInfo(pimaryMonitor, &info));

    Rect monitorRect = Rect(info.rcMonitor);
    std::wstringstream ss;
    ss << L"DELA026#5&10a58c63&0&UID16777488_" << monitorRect.width() << "_" << monitorRect.height() << "_MyVirtualDesktopId";

    MockZoneWindowHost host;
    winrt::com_ptr<IZoneWindow> zoneWindow = MakeZoneWindow(dynamic_cast<IZoneWindowHost*>(&host), Mocks::Instance(), pimaryMonitor, deviceId.c_str(), L"MyVirtualDesktopId", false);
    Assert::AreEqual(zoneWindow->UniqueId().compare(ss.str()), 0);
}
}
;
}
