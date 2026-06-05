#include "pch.h"

#include <AlwaysOnTop\ColorUtils.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace AlwaysOnTopUnitTests
{
    TEST_CLASS(ColorUtilsTests)
    {
    public:
        TEST_METHOD(HexToRGBParsesValidColorAndUsesFallbackForInvalidValue)
        {
            const COLORREF parsedColor = AlwaysOnTopColorUtils::HexToRGB(L"#1A2B3C");
            Assert::AreEqual(static_cast<DWORD>(RGB(0x1A, 0x2B, 0x3C)), static_cast<DWORD>(parsedColor));

            const COLORREF fallbackColor = RGB(1, 2, 3);
            const COLORREF invalidColor = AlwaysOnTopColorUtils::HexToRGB(L"not-a-color", fallbackColor);
            Assert::AreEqual(static_cast<DWORD>(fallbackColor), static_cast<DWORD>(invalidColor));
        }
    };
}
