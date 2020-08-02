#include "pch.h"
#include "Util.h"
#include "lib\util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    void TestMonitorSetPermutations(const std::vector<std::pair<HMONITOR, RECT>>& monitorInfo)
    {
        auto monitorInfoPermutation = monitorInfo;

        do {
            auto monitorInfoCopy = monitorInfoPermutation;
            OrderMonitors(monitorInfoCopy);
            CustomAssert::AreEqual(monitorInfo, monitorInfoCopy);
        } while (std::next_permutation(monitorInfoPermutation.begin(), monitorInfoPermutation.end(), [](auto x, auto y) { return x.first < y.first; }));
    }

    void TestMonitorSetPermutationsOffsets(const std::vector<std::pair<HMONITOR, RECT>>& monitorInfo)
    {
        for (int offsetX = -3000; offsetX <= 3000; offsetX += 1000)
        {
            for (int offsetY = -3000; offsetY <= 3000; offsetY += 1000)
            {
                auto monitorInfoCopy = monitorInfo;
                for (auto& [monitor, rect] : monitorInfoCopy)
                {
                    rect.left += offsetX;
                    rect.right += offsetX;
                    rect.top += offsetY;
                    rect.bottom += offsetY;
                }
                TestMonitorSetPermutations(monitorInfoCopy);
            }
        }
    }

    TEST_CLASS(UtilUnitTests)
    {
        TEST_METHOD(TestParseDeviceId)
        {
            // We're interested in the unique part between the first and last #'s
            // Example input: \\?\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
            // Example output: DELA026#5&10a58c63&0&UID16777488
            PCWSTR input = L"\\\\?\\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
            wchar_t output[256]{};
            ParseDeviceId(input, output, ARRAYSIZE(output));
            Assert::AreEqual(0, wcscmp(output, L"DELA026#5&10a58c63&0&UID16777488"));
        }

        TEST_METHOD(TestParseInvalidDeviceId)
        {
            // We're interested in the unique part between the first and last #'s
            // Example input: \\?\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
            // Example output: DELA026#5&10a58c63&0&UID16777488
            PCWSTR input = L"AnInvalidDeviceId";
            wchar_t output[256]{};
            ParseDeviceId(input, output, ARRAYSIZE(output));
            Assert::AreEqual(0, wcscmp(output, L"FallbackDevice"));
        }

        TEST_METHOD(TestMonitorOrdering01)
        {
            // Three horizontally arranged monitors, bottom aligned, with increasing sizes
            std::vector<std::pair<HMONITOR, RECT>> monitorInfo = {
                {Mocks::Monitor(), RECT{.left = 0, .top = 200, .right = 1600, .bottom = 1100} },
                {Mocks::Monitor(), RECT{.left = 1600, .top = 100, .right = 3300, .bottom = 1100} },
                {Mocks::Monitor(), RECT{.left = 3300, .top = 0, .right = 5100, .bottom = 1100} },
            };

            TestMonitorSetPermutationsOffsets(monitorInfo);
        }

        TEST_METHOD(TestMonitorOrdering02)
        {
            // Three horizontally arranged monitors, bottom aligned, with equal sizes
            std::vector<std::pair<HMONITOR, RECT>> monitorInfo = {
                {Mocks::Monitor(), RECT{.left = 0, .top = 0, .right = 1600, .bottom = 900} },
                {Mocks::Monitor(), RECT{.left = 1600, .top = 0, .right = 3200, .bottom = 900} },
                {Mocks::Monitor(), RECT{.left = 3200, .top = 0, .right = 4800, .bottom = 900} },
            };

            TestMonitorSetPermutationsOffsets(monitorInfo);
        }

        TEST_METHOD(TestMonitorOrdering03)
        {
            // Three horizontally arranged monitors, bottom aligned, with decreasing sizes
            std::vector<std::pair<HMONITOR, RECT>> monitorInfo = {
                {Mocks::Monitor(), RECT{.left = 0, .top = 0, .right = 1800, .bottom = 1100} },
                {Mocks::Monitor(), RECT{.left = 1800, .top = 100, .right = 3500, .bottom = 1100} },
                {Mocks::Monitor(), RECT{.left = 3500, .top = 200, .right = 5100, .bottom = 1100} },
            };

            TestMonitorSetPermutationsOffsets(monitorInfo);
        }

        TEST_METHOD(TestMonitorOrdering04)
        {
            // Three horizontally arranged monitors, top aligned, with increasing sizes
            std::vector<std::pair<HMONITOR, RECT>> monitorInfo = {
                {Mocks::Monitor(), RECT{.left = 0, .top = 0, .right = 1600, .bottom = 900} },
                {Mocks::Monitor(), RECT{.left = 1600, .top = 0, .right = 3300, .bottom = 1000} },
                {Mocks::Monitor(), RECT{.left = 3300, .top = 0, .right = 5100, .bottom = 1100} },
            };

            TestMonitorSetPermutationsOffsets(monitorInfo);
        }

        TEST_METHOD(TestMonitorOrdering05)
        {
            // Three horizontally arranged monitors, top aligned, with equal sizes
            std::vector<std::pair<HMONITOR, RECT>> monitorInfo = {
                {Mocks::Monitor(), RECT{.left = 0, .top = 0, .right = 1600, .bottom = 900} },
                {Mocks::Monitor(), RECT{.left = 1600, .top = 0, .right = 3200, .bottom = 900} },
                {Mocks::Monitor(), RECT{.left = 3200, .top = 0, .right = 4800, .bottom = 900} },
            };

            TestMonitorSetPermutationsOffsets(monitorInfo);
        }

        TEST_METHOD(TestMonitorOrdering06)
        {
            // Three horizontally arranged monitors, top aligned, with decreasing sizes
            std::vector<std::pair<HMONITOR, RECT>> monitorInfo = {
                {Mocks::Monitor(), RECT{.left = 0, .top = 0, .right = 1800, .bottom = 1100} },
                {Mocks::Monitor(), RECT{.left = 1800, .top = 0, .right = 3500, .bottom = 1000} },
                {Mocks::Monitor(), RECT{.left = 3500, .top = 0, .right = 5100, .bottom = 900} },
            };

            TestMonitorSetPermutationsOffsets(monitorInfo);
        }

        TEST_METHOD(TestMonitorOrdering07)
        {
            // Three vertically arranged monitors, center aligned, with equal sizes, except the middle monitor is a bit wider
            std::vector<std::pair<HMONITOR, RECT>> monitorInfo = {
                {Mocks::Monitor(), RECT{.left = 100, .top = 0, .right = 1700, .bottom = 900} },
                {Mocks::Monitor(), RECT{.left = 0, .top = 900, .right = 1800, .bottom = 1800} },
                {Mocks::Monitor(), RECT{.left = 100, .top = 1800, .right = 1700, .bottom = 2700} },
            };

            TestMonitorSetPermutationsOffsets(monitorInfo);
        }

        TEST_METHOD(TestMonitorOrdering08)
        {
            // ------------------
            // |    ||    ||    |
            // |    ||    ||    |
            // ------------------
            // |       ||       |
            // |       ||       |
            // ------------------
            std::vector<std::pair<HMONITOR, RECT>> monitorInfo = {
                {Mocks::Monitor(), RECT{.left = 0, .top = 0, .right = 600, .bottom = 400} },
                {Mocks::Monitor(), RECT{.left = 600, .top = 0, .right = 1200, .bottom = 400} },
                {Mocks::Monitor(), RECT{.left = 1200, .top = 0, .right = 1800, .bottom = 400} },
                {Mocks::Monitor(), RECT{.left = 0, .top = 400, .right = 900, .bottom = 800} },
                {Mocks::Monitor(), RECT{.left = 900, .top = 400, .right = 1800, .bottom = 800} },
            };

            TestMonitorSetPermutationsOffsets(monitorInfo);
        }

        TEST_METHOD(TestMonitorOrdering09)
        {
            // Regular 3x3 grid
            std::vector<std::pair<HMONITOR, RECT>> monitorInfo = {
                {Mocks::Monitor(), RECT{.left = 0, .top = 0, .right = 400, .bottom = 300} },
                {Mocks::Monitor(), RECT{.left = 400, .top = 0, .right = 800, .bottom = 300} },
                {Mocks::Monitor(), RECT{.left = 800, .top = 0, .right = 1200, .bottom = 300} },
                {Mocks::Monitor(), RECT{.left = 0, .top = 300, .right = 400, .bottom = 600} },
                {Mocks::Monitor(), RECT{.left = 400, .top = 300, .right = 800, .bottom = 600} },
                {Mocks::Monitor(), RECT{.left = 800, .top = 300, .right = 1200, .bottom = 600} },
                {Mocks::Monitor(), RECT{.left = 0, .top = 600, .right = 400, .bottom = 900} },
                {Mocks::Monitor(), RECT{.left = 400, .top = 600, .right = 800, .bottom = 900} },
                {Mocks::Monitor(), RECT{.left = 800, .top = 600, .right = 1200, .bottom = 900} },
            };

            // Reduce running time by testing only rotations
            for (int i = 0; i < 9; i++)
            {
                auto monitorInfoCopy = monitorInfo;
                std::rotate(monitorInfoCopy.begin(), monitorInfoCopy.begin() + i, monitorInfoCopy.end());
                OrderMonitors(monitorInfoCopy);
                CustomAssert::AreEqual(monitorInfo, monitorInfoCopy);
            }
        }

        TEST_METHOD(TestMonitorOrdering10)
        {
            // ------------------
            // |       ||       |
            // |       ||       |
            // ------------------
            // |    ||    ||    |
            // |    ||    ||    |
            // ------------------
            std::vector<std::pair<HMONITOR, RECT>> monitorInfo = {
                {Mocks::Monitor(), RECT{.left = 0, .top = 0, .right = 900, .bottom = 400} },
                {Mocks::Monitor(), RECT{.left = 900, .top = 0, .right = 1800, .bottom = 400} },
                {Mocks::Monitor(), RECT{.left = 0, .top = 400, .right = 600, .bottom = 800} },
                {Mocks::Monitor(), RECT{.left = 600, .top = 400, .right = 1200, .bottom = 800} },
                {Mocks::Monitor(), RECT{.left = 1200, .top = 400, .right = 1800, .bottom = 800} },
            };

            TestMonitorSetPermutationsOffsets(monitorInfo);
        }

        TEST_METHOD(TestMonitorOrdering11)
        {
            // Random values, some monitors overlap, don't check order, just ensure it doesn't crash and it's the same every time
            std::vector<std::pair<HMONITOR, RECT>> monitorInfo = {
                {Mocks::Monitor(), RECT{.left = 410, .top = 630, .right = 988, .bottom = 631} },
                {Mocks::Monitor(), RECT{.left = 302, .top = 189, .right = 550, .bottom = 714} },
                {Mocks::Monitor(), RECT{.left = 158, .top = 115, .right = 657, .bottom = 499} },
                {Mocks::Monitor(), RECT{.left = 341, .top = 340, .right = 723, .bottom = 655} },
                {Mocks::Monitor(), RECT{.left = 433, .top = 393, .right = 846, .bottom = 544} },
            };

            auto monitorInfoPermutation = monitorInfo;
            auto firstTime = monitorInfo;
            OrderMonitors(firstTime);

            do {
                auto monitorInfoCopy = monitorInfoPermutation;
                OrderMonitors(monitorInfoCopy);
                CustomAssert::AreEqual(firstTime, monitorInfoCopy);
            } while (next_permutation(monitorInfoPermutation.begin(), monitorInfoPermutation.end(), [](auto x, auto y) { return x.first < y.first; }));
        }
    };
}

