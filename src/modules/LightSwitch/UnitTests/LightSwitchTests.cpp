// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"
#include "CppUnitTest.h"

#include "../LightSwitchService/LightSwitchUtils.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace LightSwitchUnitTests
{
    TEST_CLASS(ShouldBeLightTests)
    {
    public:
        TEST_METHOD(NormalRange_BeforeLightTime_ReturnsDark)
        {
            Assert::IsFalse(ShouldBeLight(360, 480, 1200));
        }
    };
}
