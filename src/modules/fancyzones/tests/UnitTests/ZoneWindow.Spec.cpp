#include "pch.h"
#include "lib\ZoneSet.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS(ZoneWindowUnitTests){
        public:
            TEST_METHOD(TestCreateZoneWindow){
                winrt::com_ptr<IZoneWindow> zoneWindow = MakeZoneWindow(nullptr, Mocks::Instance(), Mocks::Monitor(), L"DeviceId", L"MyVirtualDesktopId", false);
    Assert::IsNotNull(zoneWindow.get());
}

TEST_METHOD(TestDeviceId)
{
    // Window initialization requires a valid HMONITOR - just use the primary for now.
    HMONITOR pimaryMonitor = MonitorFromWindow(nullptr, MONITOR_DEFAULTTOPRIMARY);
    winrt::com_ptr<IZoneWindow> zoneWindow = MakeZoneWindow(nullptr, Mocks::Instance(), pimaryMonitor, L"SomeRandomValue", L"MyVirtualDesktopId", false);
    // We have no way to test the correctness, just do our best and check its not an empty string.
    Assert::IsTrue(zoneWindow->DeviceId().size() > 0);
}

TEST_METHOD(TestUniqueId)
{
    // Unique id of the format "ParsedMonitorDeviceId_MonitorWidth_MonitorHeight_VirtualDesktopId
    // Example: "DELA026#5&10a58c63&0&UID16777488_1024_768_MyVirtualDesktopId"
    std::wstring deviceId(L"\\\\?\\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}");
    // Window initialization requires a valid HMONITOR - just use the primary for now.
    HMONITOR pimaryMonitor = MonitorFromWindow(nullptr, MONITOR_DEFAULTTOPRIMARY);
    MONITORINFO info;
    info.cbSize = sizeof(info);
    Assert::IsTrue(GetMonitorInfo(pimaryMonitor, &info));

    Rect monitorRect = Rect(info.rcMonitor);
    std::wstringstream ss;
    ss << L"DELA026#5&10a58c63&0&UID16777488_" << monitorRect.width() << "_" << monitorRect.height() << "_MyVirtualDesktopId";

    winrt::com_ptr<IZoneWindow> zoneWindow = MakeZoneWindow(nullptr, Mocks::Instance(), pimaryMonitor, deviceId.c_str(), L"MyVirtualDesktopId", false);
    Assert::AreEqual(zoneWindow->UniqueId().compare(ss.str()), 0);
}
}
;
}
