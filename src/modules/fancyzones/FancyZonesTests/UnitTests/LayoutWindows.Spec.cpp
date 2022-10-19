#include "pch.h"

#include <filesystem>

#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include <FancyZonesLib/ZoneIndexSetBitmask.h>
#include <FancyZonesLib/LayoutWindows.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/util.h>

#include "Util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace FancyZonesDataTypes;

namespace FancyZonesUnitTests
{
    TEST_CLASS (LayoutWindowsUnitTests)
    {
        TEST_METHOD (ZoneIndexFromWindowUnknown)
        {
            LayoutWindows layoutWindows{};

            layoutWindows.Assign(Mocks::Window(), { 0 });

            auto actual = layoutWindows.GetZoneIndexSetFromWindow(Mocks::Window());
            Assert::IsTrue(std::vector<ZoneIndex>{} == actual);
        }

        TEST_METHOD (ZoneIndexFromWindowNull)
        {
            LayoutWindows layoutWindows{};

            layoutWindows.Assign(Mocks::Window(), { 0 });

            auto actual = layoutWindows.GetZoneIndexSetFromWindow(nullptr);
            Assert::IsTrue(std::vector<ZoneIndex>{} == actual);
        }

        TEST_METHOD (Assign)
        {
            HWND window = Mocks::Window();

            LayoutWindows layoutWindows{};
            layoutWindows.Assign(window, { 1, 2, 3 });

            Assert::IsTrue(std::vector<ZoneIndex>{ 1, 2, 3 } == layoutWindows.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (AssignEmpty)
        {
            HWND window = Mocks::Window();

            LayoutWindows layoutWindows{};
            layoutWindows.Assign(window, {});

            Assert::IsTrue(std::vector<ZoneIndex>{} == layoutWindows.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (AssignSeveralTimesSameWindow)
        {
            LayoutWindows layoutWindows{};
            HWND window = Mocks::Window();

            layoutWindows.Assign(window, { 0 });
            Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));

            layoutWindows.Assign(window, { 1 });
            Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == layoutWindows.GetZoneIndexSetFromWindow(window));

            layoutWindows.Assign(window, { 2 });
            Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == layoutWindows.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (DismissWindow)
        {
            LayoutWindows layoutWindows{};
            HWND window = Mocks::Window();

            layoutWindows.Assign(window, { 0 });

            layoutWindows.Dismiss(window);
            Assert::IsTrue(std::vector<ZoneIndex>{} == layoutWindows.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Empty)
        {
            LayoutWindows layoutWindows{};
            HWND window = Mocks::Window();

            layoutWindows.Assign(window, { 0 });
            Assert::IsFalse(layoutWindows.IsZoneEmpty(0));
            Assert::IsTrue(layoutWindows.IsZoneEmpty(1));
        }
    };
}