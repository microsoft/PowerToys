// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/Layout.h>
#include <FancyZonesLib/util.h>

#include "Util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace FancyZonesDataTypes;
using namespace FancyZonesUtils;

namespace FancyZonesUnitTests
{
    TEST_CLASS(LayoutSeedTests)
    {
    public:
        TEST_METHOD(LayoutColumns2Zones)
        {
            LayoutData data{
                .uuid = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000002}").value(),
                .type = ZoneSetLayoutType::Columns,
                .showSpacing = false,
                .spacing = 0,
                .zoneCount = 2,
                .sensitivityRadius = 20,
            };

            auto layout = std::make_unique<Layout>(data);
            Assert::IsTrue(layout->Init(RECT{ 0, 0, 1920, 1080 }, Mocks::Monitor()));
            Assert::AreEqual(static_cast<size_t>(2), layout->Zones().size());

            for (const auto& [id, zone] : layout->Zones())
            {
                UNREFERENCED_PARAMETER(id);

                const auto& zoneRect = zone.GetZoneRect();
                Assert::IsTrue(zoneRect.left >= 0);
                Assert::IsTrue(zoneRect.top >= 0);
                Assert::IsTrue(zoneRect.left < zoneRect.right);
                Assert::IsTrue(zoneRect.top < zoneRect.bottom);
                Assert::IsTrue(zoneRect.right <= 1920);
                Assert::IsTrue(zoneRect.bottom <= 1080);
            }
        }
    };
}
