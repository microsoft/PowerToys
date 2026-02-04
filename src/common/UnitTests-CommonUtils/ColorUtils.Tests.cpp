#include "pch.h"
#include "TestHelpers.h"
#include <color.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(ColorUtilsTests)
    {
    public:
        // checkValidRGB tests
        TEST_METHOD(CheckValidRGB_ValidBlack_ReturnsTrue)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"#000000", &r, &g, &b);
            Assert::IsTrue(result);
            Assert::AreEqual(static_cast<uint8_t>(0), r);
            Assert::AreEqual(static_cast<uint8_t>(0), g);
            Assert::AreEqual(static_cast<uint8_t>(0), b);
        }

        TEST_METHOD(CheckValidRGB_ValidWhite_ReturnsTrue)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"#FFFFFF", &r, &g, &b);
            Assert::IsTrue(result);
            Assert::AreEqual(static_cast<uint8_t>(255), r);
            Assert::AreEqual(static_cast<uint8_t>(255), g);
            Assert::AreEqual(static_cast<uint8_t>(255), b);
        }

        TEST_METHOD(CheckValidRGB_ValidRed_ReturnsTrue)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"#FF0000", &r, &g, &b);
            Assert::IsTrue(result);
            Assert::AreEqual(static_cast<uint8_t>(255), r);
            Assert::AreEqual(static_cast<uint8_t>(0), g);
            Assert::AreEqual(static_cast<uint8_t>(0), b);
        }

        TEST_METHOD(CheckValidRGB_ValidGreen_ReturnsTrue)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"#00FF00", &r, &g, &b);
            Assert::IsTrue(result);
            Assert::AreEqual(static_cast<uint8_t>(0), r);
            Assert::AreEqual(static_cast<uint8_t>(255), g);
            Assert::AreEqual(static_cast<uint8_t>(0), b);
        }

        TEST_METHOD(CheckValidRGB_ValidBlue_ReturnsTrue)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"#0000FF", &r, &g, &b);
            Assert::IsTrue(result);
            Assert::AreEqual(static_cast<uint8_t>(0), r);
            Assert::AreEqual(static_cast<uint8_t>(0), g);
            Assert::AreEqual(static_cast<uint8_t>(255), b);
        }

        TEST_METHOD(CheckValidRGB_ValidMixed_ReturnsTrue)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"#AB12CD", &r, &g, &b);
            Assert::IsTrue(result);
            Assert::AreEqual(static_cast<uint8_t>(0xAB), r);
            Assert::AreEqual(static_cast<uint8_t>(0x12), g);
            Assert::AreEqual(static_cast<uint8_t>(0xCD), b);
        }

        TEST_METHOD(CheckValidRGB_MissingHash_ReturnsFalse)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"FFFFFF", &r, &g, &b);
            Assert::IsFalse(result);
        }

        TEST_METHOD(CheckValidRGB_TooShort_ReturnsFalse)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"#FFF", &r, &g, &b);
            Assert::IsFalse(result);
        }

        TEST_METHOD(CheckValidRGB_TooLong_ReturnsFalse)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"#FFFFFFFF", &r, &g, &b);
            Assert::IsFalse(result);
        }

        TEST_METHOD(CheckValidRGB_InvalidChars_ReturnsFalse)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"#GGHHII", &r, &g, &b);
            Assert::IsFalse(result);
        }

        TEST_METHOD(CheckValidRGB_LowercaseInvalid_ReturnsFalse)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"#ffffff", &r, &g, &b);
            Assert::IsFalse(result);
        }

        TEST_METHOD(CheckValidRGB_EmptyString_ReturnsFalse)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"", &r, &g, &b);
            Assert::IsFalse(result);
        }

        TEST_METHOD(CheckValidRGB_OnlyHash_ReturnsFalse)
        {
            uint8_t r, g, b;
            bool result = checkValidRGB(L"#", &r, &g, &b);
            Assert::IsFalse(result);
        }

        // checkValidARGB tests
        TEST_METHOD(CheckValidARGB_ValidBlackOpaque_ReturnsTrue)
        {
            uint8_t a, r, g, b;
            bool result = checkValidARGB(L"#FF000000", &a, &r, &g, &b);
            Assert::IsTrue(result);
            Assert::AreEqual(static_cast<uint8_t>(255), a);
            Assert::AreEqual(static_cast<uint8_t>(0), r);
            Assert::AreEqual(static_cast<uint8_t>(0), g);
            Assert::AreEqual(static_cast<uint8_t>(0), b);
        }

        TEST_METHOD(CheckValidARGB_ValidWhiteOpaque_ReturnsTrue)
        {
            uint8_t a, r, g, b;
            bool result = checkValidARGB(L"#FFFFFFFF", &a, &r, &g, &b);
            Assert::IsTrue(result);
            Assert::AreEqual(static_cast<uint8_t>(255), a);
            Assert::AreEqual(static_cast<uint8_t>(255), r);
            Assert::AreEqual(static_cast<uint8_t>(255), g);
            Assert::AreEqual(static_cast<uint8_t>(255), b);
        }

        TEST_METHOD(CheckValidARGB_ValidTransparent_ReturnsTrue)
        {
            uint8_t a, r, g, b;
            bool result = checkValidARGB(L"#00FFFFFF", &a, &r, &g, &b);
            Assert::IsTrue(result);
            Assert::AreEqual(static_cast<uint8_t>(0), a);
            Assert::AreEqual(static_cast<uint8_t>(255), r);
            Assert::AreEqual(static_cast<uint8_t>(255), g);
            Assert::AreEqual(static_cast<uint8_t>(255), b);
        }

        TEST_METHOD(CheckValidARGB_ValidSemiTransparent_ReturnsTrue)
        {
            uint8_t a, r, g, b;
            bool result = checkValidARGB(L"#80FF0000", &a, &r, &g, &b);
            Assert::IsTrue(result);
            Assert::AreEqual(static_cast<uint8_t>(0x80), a);
            Assert::AreEqual(static_cast<uint8_t>(255), r);
            Assert::AreEqual(static_cast<uint8_t>(0), g);
            Assert::AreEqual(static_cast<uint8_t>(0), b);
        }

        TEST_METHOD(CheckValidARGB_ValidMixed_ReturnsTrue)
        {
            uint8_t a, r, g, b;
            bool result = checkValidARGB(L"#12345678", &a, &r, &g, &b);
            Assert::IsTrue(result);
            Assert::AreEqual(static_cast<uint8_t>(0x12), a);
            Assert::AreEqual(static_cast<uint8_t>(0x34), r);
            Assert::AreEqual(static_cast<uint8_t>(0x56), g);
            Assert::AreEqual(static_cast<uint8_t>(0x78), b);
        }

        TEST_METHOD(CheckValidARGB_MissingHash_ReturnsFalse)
        {
            uint8_t a, r, g, b;
            bool result = checkValidARGB(L"FFFFFFFF", &a, &r, &g, &b);
            Assert::IsFalse(result);
        }

        TEST_METHOD(CheckValidARGB_TooShort_ReturnsFalse)
        {
            uint8_t a, r, g, b;
            bool result = checkValidARGB(L"#FFFFFF", &a, &r, &g, &b);
            Assert::IsFalse(result);
        }

        TEST_METHOD(CheckValidARGB_TooLong_ReturnsFalse)
        {
            uint8_t a, r, g, b;
            bool result = checkValidARGB(L"#FFFFFFFFFF", &a, &r, &g, &b);
            Assert::IsFalse(result);
        }

        TEST_METHOD(CheckValidARGB_InvalidChars_ReturnsFalse)
        {
            uint8_t a, r, g, b;
            bool result = checkValidARGB(L"#GGHHIIJJ", &a, &r, &g, &b);
            Assert::IsFalse(result);
        }

        TEST_METHOD(CheckValidARGB_LowercaseInvalid_ReturnsFalse)
        {
            uint8_t a, r, g, b;
            bool result = checkValidARGB(L"#ffffffff", &a, &r, &g, &b);
            Assert::IsFalse(result);
        }

        TEST_METHOD(CheckValidARGB_EmptyString_ReturnsFalse)
        {
            uint8_t a, r, g, b;
            bool result = checkValidARGB(L"", &a, &r, &g, &b);
            Assert::IsFalse(result);
        }
    };
}
