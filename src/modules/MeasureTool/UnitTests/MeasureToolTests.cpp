// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "pch.h"

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "BGRATextureView.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace MeasureToolUnitTests
{
    static BGRATextureView MakeTexture(const uint32_t* data, size_t width, size_t height, size_t pitch = 0)
    {
        BGRATextureView view;
        view.pixels = data;
        view.width = width;
        view.height = height;
        view.pitch = pitch ? pitch : width;
        return view;
    }

    TEST_CLASS(BGRATextureViewTests)
    {
    public:
        TEST_METHOD(GetPixel_BasicAccess)
        {
            const uint32_t data[] = {
                0x01, 0x02, 0x03,
                0x04, 0x05, 0x06,
                0x07, 0x08, 0x09,
            };
            const auto view = MakeTexture(data, 3, 3);

            Assert::AreEqual(0x01u, view.GetPixel(0, 0));
            Assert::AreEqual(0x05u, view.GetPixel(1, 1));
            Assert::AreEqual(0x09u, view.GetPixel(2, 2));
            Assert::AreEqual(0x06u, view.GetPixel(2, 1));
        }
    };
}
