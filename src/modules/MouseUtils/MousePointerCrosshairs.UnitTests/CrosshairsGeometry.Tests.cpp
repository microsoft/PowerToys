#include "pch.h"

#include <CrosshairsGeometry.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace MousePointerCrosshairsUnitTests
{
    TEST_CLASS(CrosshairsGeometryTests)
    {
    private:
        static void AssertBounds(const CrosshairsVisualBounds& actual, double offsetX, double offsetY, double width, double height)
        {
            Assert::AreEqual(offsetX, static_cast<double>(actual.offsetX), 0.001, L"offsetX");
            Assert::AreEqual(offsetY, static_cast<double>(actual.offsetY), 0.001, L"offsetY");
            Assert::AreEqual(width, static_cast<double>(actual.width), 0.001, L"width");
            Assert::AreEqual(height, static_cast<double>(actual.height), 0.001, L"height");
        }

    public:
        TEST_METHOD(CalculateCrosshairsLayout_HorizontalOnly_HidesVerticalCrosshairsAndCentersHorizontalLines)
        {
            CrosshairsLayoutInput input{};
            input.cursorPosition = { 100, 75 };
            input.monitorUpperLeft = { 0, 0 };
            input.monitorBottomRight = { 200, 150 };
            input.crosshairsRadius = 10;
            input.crosshairsThickness = 5;
            input.crosshairsBorderSize = 2;
            input.crosshairsOrientation = CrosshairsOrientation::HorizontalOnly;

            const auto layout = CalculateCrosshairsLayout(input);

            AssertBounds(layout.left.line, 91.0, 75.5, 91.0, 5.0);
            AssertBounds(layout.left.border, 93.0, 75.5, 93.0, 9.0);
            AssertBounds(layout.right.line, 110.0, 75.5, 90.0, 5.0);
            AssertBounds(layout.right.border, 108.0, 75.5, 92.0, 9.0);
            AssertBounds(layout.top.line, 0.0, 0.0, 0.0, 0.0);
            AssertBounds(layout.top.border, 0.0, 0.0, 0.0, 0.0);
            AssertBounds(layout.bottom.line, 0.0, 0.0, 0.0, 0.0);
            AssertBounds(layout.bottom.border, 0.0, 0.0, 0.0, 0.0);
        }
    };
}
