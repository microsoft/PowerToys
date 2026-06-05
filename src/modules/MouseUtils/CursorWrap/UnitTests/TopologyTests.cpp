// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "../CursorWrapCore.h"
#include "../MonitorTopology.h"

#include <cstdint>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CursorWrapUnitTests
{
    static MonitorInfo MakeMonitor(int index, LONG left, LONG top, LONG right, LONG bottom, bool primary = false)
    {
        MonitorInfo monitorInfo{};
        monitorInfo.hMonitor = reinterpret_cast<HMONITOR>(static_cast<uintptr_t>(index) + 1u);
        monitorInfo.rect = { left, top, right, bottom };
        monitorInfo.isPrimary = primary;
        monitorInfo.monitorId = index;
        return monitorInfo;
    }

    TEST_CLASS(TopologyTests)
    {
    public:
        TEST_METHOD(SingleMonitor_AllEdgesAreOuter)
        {
            MonitorTopology topology;
            const std::vector<MonitorInfo> monitors = {
                MakeMonitor(0, 0, 0, 1920, 1080, true),
            };

            topology.Initialize(monitors);

            Assert::AreEqual(4, static_cast<int>(topology.GetOuterEdges().size()));
        }
    };
}
