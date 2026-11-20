// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning(push)
#pragma warning(disable : 26466)
#include "CppUnitTest.h"
#pragma warning(pop)

#include "../../MousePointerCrosshairs/CursorVisibility.h"
#include <array>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace AutoHideCursorUnitTests
{
    TEST_CLASS (CursorVisibilityTests)
    {
    public:
        TEST_METHOD (HiddenCursorFlagDoesNotRequireAnImage)
        {
            const CURSORINFO info{ sizeof(CURSORINFO) };
            Assert::IsFalse(mouse_pointer_crosshairs::IsCursorVisible(info).value());
        }

        TEST_METHOD (TransparentReplacementIsHiddenDespiteShowingFlag)
        {
            AssertMonochromeVisibility(0xFF, 0x00, false);
        }

        TEST_METHOD (OpaqueMonochromeCursorIsVisible)
        {
            AssertMonochromeVisibility(0x00, 0x00, true);
        }

        TEST_METHOD (InvertingMonochromeCursorIsVisible)
        {
            AssertMonochromeVisibility(0xFF, 0xFF, true);
        }

        TEST_METHOD (OneOpaquePixelAtTheEndOfTheMaskIsVisible)
        {
            AssertMonochromeVisibility(0xFF, 0x00, true, true);
        }

        TEST_METHOD (InvalidCursorImageIsNotTreatedAsHidden)
        {
            CURSORINFO info{ sizeof(CURSORINFO) };
            info.flags = CURSOR_SHOWING;
            Assert::IsFalse(mouse_pointer_crosshairs::IsCursorVisible(info).has_value());
        }

        TEST_METHOD (ColorCursorIsVisible)
        {
            std::array<DWORD, 256> pixels;
            pixels.fill(0xFFFF0000);
            const auto color = CreateBitmap(16, 16, 1, 32, pixels.data());
            const auto mask = CreateBitmap(16, 16, 1, 1, nullptr);
            Assert::IsNotNull(color);
            Assert::IsNotNull(mask);
            ICONINFO iconInfo{};
            iconInfo.hbmMask = mask;
            iconInfo.hbmColor = color;
            const auto cursor = CreateIconIndirect(&iconInfo);
            DeleteObject(color);
            DeleteObject(mask);
            Assert::IsNotNull(cursor);
            CURSORINFO info{ sizeof(CURSORINFO) };
            info.flags = CURSOR_SHOWING;
            info.hCursor = cursor;
            const auto visible = mouse_pointer_crosshairs::IsCursorVisible(info);
            DestroyCursor(cursor);
            Assert::IsTrue(visible.value());
        }

    private:
        static void AssertMonochromeVisibility(BYTE andValue, BYTE xorValue, bool expected, bool opaqueLastPixel = false)
        {
            std::array<BYTE, 32> andMask;
            std::array<BYTE, 32> xorMask;
            andMask.fill(andValue);
            xorMask.fill(xorValue);
            if (opaqueLastPixel)
            {
                andMask.back() &= 0xFE;
            }
            const auto cursor = CreateCursor(nullptr, 0, 0, 16, 16, andMask.data(), xorMask.data());
            Assert::IsNotNull(cursor);
            CURSORINFO info{ sizeof(CURSORINFO) };
            info.flags = CURSOR_SHOWING;
            info.hCursor = cursor;
            const auto visible = mouse_pointer_crosshairs::IsCursorVisible(info);
            DestroyCursor(cursor);
            Assert::IsTrue(visible.has_value());
            Assert::AreEqual(expected, *visible);
        }
    };
}
