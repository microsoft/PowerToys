#include "pch.h"
#include "MetadataFormatHelper.h"
#include <cmath>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;
using namespace PowerRenameLib;

namespace MetadataFormatHelperTests
{
    TEST_CLASS(FormatApertureTests)
    {
    public:
        TEST_METHOD(FormatAperture_ValidValue)
        {
            // Test formatting a typical aperture value
            std::wstring result = MetadataFormatHelper::FormatAperture(2.8);
            Assert::AreEqual(L"f/2.8", result.c_str());
        }

        TEST_METHOD(FormatAperture_SmallValue)
        {
            // Test small aperture (large f-number)
            std::wstring result = MetadataFormatHelper::FormatAperture(1.4);
            Assert::AreEqual(L"f/1.4", result.c_str());
        }

        TEST_METHOD(FormatAperture_LargeValue)
        {
            // Test large aperture (small f-number)
            std::wstring result = MetadataFormatHelper::FormatAperture(22.0);
            Assert::AreEqual(L"f/22.0", result.c_str());
        }

        TEST_METHOD(FormatAperture_RoundedValue)
        {
            // Test rounding to one decimal place
            std::wstring result = MetadataFormatHelper::FormatAperture(5.66666);
            Assert::AreEqual(L"f/5.7", result.c_str());
        }

        TEST_METHOD(FormatAperture_Zero)
        {
            // Test zero value
            std::wstring result = MetadataFormatHelper::FormatAperture(0.0);
            Assert::AreEqual(L"f/0.0", result.c_str());
        }
    };

    TEST_CLASS(FormatShutterSpeedTests)
    {
    public:
        TEST_METHOD(FormatShutterSpeed_FastSpeed)
        {
            // Test fast shutter speed (fraction of second)
            std::wstring result = MetadataFormatHelper::FormatShutterSpeed(0.002);
            Assert::AreEqual(L"1/500s", result.c_str());
        }

        TEST_METHOD(FormatShutterSpeed_VeryFastSpeed)
        {
            // Test very fast shutter speed
            std::wstring result = MetadataFormatHelper::FormatShutterSpeed(0.0001);
            Assert::AreEqual(L"1/10000s", result.c_str());
        }

        TEST_METHOD(FormatShutterSpeed_SlowSpeed)
        {
            // Test slow shutter speed (more than 1 second)
            std::wstring result = MetadataFormatHelper::FormatShutterSpeed(2.5);
            Assert::AreEqual(L"2.5s", result.c_str());
        }

        TEST_METHOD(FormatShutterSpeed_OneSecond)
        {
            // Test exactly 1 second
            std::wstring result = MetadataFormatHelper::FormatShutterSpeed(1.0);
            Assert::AreEqual(L"1.0s", result.c_str());
        }

        TEST_METHOD(FormatShutterSpeed_VerySlowSpeed)
        {
            // Test very slow shutter speed (< 1 second but close)
            std::wstring result = MetadataFormatHelper::FormatShutterSpeed(0.5);
            Assert::AreEqual(L"1/2s", result.c_str());
        }

        TEST_METHOD(FormatShutterSpeed_Zero)
        {
            // Test zero value
            std::wstring result = MetadataFormatHelper::FormatShutterSpeed(0.0);
            Assert::AreEqual(L"0", result.c_str());
        }

        TEST_METHOD(FormatShutterSpeed_Negative)
        {
            // Test negative value (invalid but should handle gracefully)
            std::wstring result = MetadataFormatHelper::FormatShutterSpeed(-1.0);
            Assert::AreEqual(L"0", result.c_str());
        }
    };

    TEST_CLASS(FormatISOTests)
    {
    public:
        TEST_METHOD(FormatISO_TypicalValue)
        {
            // Test typical ISO value
            std::wstring result = MetadataFormatHelper::FormatISO(400);
            Assert::AreEqual(L"ISO 400", result.c_str());
        }

        TEST_METHOD(FormatISO_LowValue)
        {
            // Test low ISO value
            std::wstring result = MetadataFormatHelper::FormatISO(100);
            Assert::AreEqual(L"ISO 100", result.c_str());
        }

        TEST_METHOD(FormatISO_HighValue)
        {
            // Test high ISO value
            std::wstring result = MetadataFormatHelper::FormatISO(12800);
            Assert::AreEqual(L"ISO 12800", result.c_str());
        }

        TEST_METHOD(FormatISO_Zero)
        {
            // Test zero value
            std::wstring result = MetadataFormatHelper::FormatISO(0);
            Assert::AreEqual(L"ISO", result.c_str());
        }

        TEST_METHOD(FormatISO_Negative)
        {
            // Test negative value (invalid but should handle gracefully)
            std::wstring result = MetadataFormatHelper::FormatISO(-100);
            Assert::AreEqual(L"ISO", result.c_str());
        }
    };

    TEST_CLASS(FormatFlashTests)
    {
    public:
        TEST_METHOD(FormatFlash_Off)
        {
            // Test flash off (bit 0 = 0)
            std::wstring result = MetadataFormatHelper::FormatFlash(0x0);
            Assert::AreEqual(L"Flash Off", result.c_str());
        }

        TEST_METHOD(FormatFlash_On)
        {
            // Test flash on (bit 0 = 1)
            std::wstring result = MetadataFormatHelper::FormatFlash(0x1);
            Assert::AreEqual(L"Flash On", result.c_str());
        }

        TEST_METHOD(FormatFlash_OnWithAdditionalFlags)
        {
            // Test flash on with additional flags
            std::wstring result = MetadataFormatHelper::FormatFlash(0x5); // 0b0101 = fired, return detected
            Assert::AreEqual(L"Flash On", result.c_str());
        }

        TEST_METHOD(FormatFlash_OffWithAdditionalFlags)
        {
            // Test flash off with additional flags
            std::wstring result = MetadataFormatHelper::FormatFlash(0x10); // Bit 0 is 0
            Assert::AreEqual(L"Flash Off", result.c_str());
        }
    };

    TEST_CLASS(FormatCoordinateTests)
    {
    public:
        TEST_METHOD(FormatCoordinate_NorthLatitude)
        {
            // Test north latitude
            std::wstring result = MetadataFormatHelper::FormatCoordinate(40.7128, true);
            Assert::AreEqual(L"40°42.77'N", result.c_str());
        }

        TEST_METHOD(FormatCoordinate_SouthLatitude)
        {
            // Test south latitude
            std::wstring result = MetadataFormatHelper::FormatCoordinate(-33.8688, true);
            Assert::AreEqual(L"33°52.13'S", result.c_str());
        }

        TEST_METHOD(FormatCoordinate_EastLongitude)
        {
            // Test east longitude
            std::wstring result = MetadataFormatHelper::FormatCoordinate(151.2093, false);
            Assert::AreEqual(L"151°12.56'E", result.c_str());
        }

        TEST_METHOD(FormatCoordinate_WestLongitude)
        {
            // Test west longitude
            std::wstring result = MetadataFormatHelper::FormatCoordinate(-74.0060, false);
            Assert::AreEqual(L"74°0.36'W", result.c_str());
        }

        TEST_METHOD(FormatCoordinate_ZeroLatitude)
        {
            // Test equator (0 degrees latitude)
            std::wstring result = MetadataFormatHelper::FormatCoordinate(0.0, true);
            Assert::AreEqual(L"0°0.00'N", result.c_str());
        }

        TEST_METHOD(FormatCoordinate_ZeroLongitude)
        {
            // Test prime meridian (0 degrees longitude)
            std::wstring result = MetadataFormatHelper::FormatCoordinate(0.0, false);
            Assert::AreEqual(L"0°0.00'E", result.c_str());
        }
    };

    TEST_CLASS(FormatSystemTimeTests)
    {
    public:
        TEST_METHOD(FormatSystemTime_ValidDateTime)
        {
            // Test formatting a valid date and time
            SYSTEMTIME st = { 0 };
            st.wYear = 2024;
            st.wMonth = 3;
            st.wDay = 15;
            st.wHour = 14;
            st.wMinute = 30;
            st.wSecond = 45;

            std::wstring result = MetadataFormatHelper::FormatSystemTime(st);
            Assert::AreEqual(L"2024-03-15 14:30:45", result.c_str());
        }

        TEST_METHOD(FormatSystemTime_Midnight)
        {
            // Test midnight time
            SYSTEMTIME st = { 0 };
            st.wYear = 2024;
            st.wMonth = 1;
            st.wDay = 1;
            st.wHour = 0;
            st.wMinute = 0;
            st.wSecond = 0;

            std::wstring result = MetadataFormatHelper::FormatSystemTime(st);
            Assert::AreEqual(L"2024-01-01 00:00:00", result.c_str());
        }

        TEST_METHOD(FormatSystemTime_EndOfDay)
        {
            // Test end of day time
            SYSTEMTIME st = { 0 };
            st.wYear = 2024;
            st.wMonth = 12;
            st.wDay = 31;
            st.wHour = 23;
            st.wMinute = 59;
            st.wSecond = 59;

            std::wstring result = MetadataFormatHelper::FormatSystemTime(st);
            Assert::AreEqual(L"2024-12-31 23:59:59", result.c_str());
        }
    };

    TEST_CLASS(ParseSingleRationalTests)
    {
    public:
        TEST_METHOD(ParseSingleRational_ValidValue)
        {
            // Test parsing a valid rational: 5/2 = 2.5
            uint8_t bytes[] = { 5, 0, 0, 0, 2, 0, 0, 0 };
            double result = MetadataFormatHelper::ParseSingleRational(bytes, 0);
            Assert::AreEqual(2.5, result, 0.001);
        }

        TEST_METHOD(ParseSingleRational_IntegerResult)
        {
            // Test parsing rational that results in integer: 10/5 = 2.0
            uint8_t bytes[] = { 10, 0, 0, 0, 5, 0, 0, 0 };
            double result = MetadataFormatHelper::ParseSingleRational(bytes, 0);
            Assert::AreEqual(2.0, result, 0.001);
        }

        TEST_METHOD(ParseSingleRational_LargeNumerator)
        {
            // Test parsing with large numerator: 1000/100 = 10.0
            uint8_t bytes[] = { 0xE8, 0x03, 0, 0, 100, 0, 0, 0 }; // 1000 in little-endian
            double result = MetadataFormatHelper::ParseSingleRational(bytes, 0);
            Assert::AreEqual(10.0, result, 0.001);
        }

        TEST_METHOD(ParseSingleRational_ZeroDenominator)
        {
            // Test parsing with zero denominator (should return 0.0)
            uint8_t bytes[] = { 5, 0, 0, 0, 0, 0, 0, 0 };
            double result = MetadataFormatHelper::ParseSingleRational(bytes, 0);
            Assert::AreEqual(0.0, result, 0.001);
        }

        TEST_METHOD(ParseSingleRational_ZeroNumerator)
        {
            // Test parsing with zero numerator: 0/5 = 0.0
            uint8_t bytes[] = { 0, 0, 0, 0, 5, 0, 0, 0 };
            double result = MetadataFormatHelper::ParseSingleRational(bytes, 0);
            Assert::AreEqual(0.0, result, 0.001);
        }

        TEST_METHOD(ParseSingleRational_WithOffset)
        {
            // Test parsing with offset
            uint8_t bytes[] = { 0xFF, 0xFF, 0xFF, 0xFF, 10, 0, 0, 0, 5, 0, 0, 0 }; // Offset = 4
            double result = MetadataFormatHelper::ParseSingleRational(bytes, 4);
            Assert::AreEqual(2.0, result, 0.001);
        }

        TEST_METHOD(ParseSingleRational_NullPointer)
        {
            // Test with null pointer (should return 0.0)
            double result = MetadataFormatHelper::ParseSingleRational(nullptr, 0);
            Assert::AreEqual(0.0, result, 0.001);
        }
    };

    TEST_CLASS(ParseSingleSRationalTests)
    {
    public:
        TEST_METHOD(ParseSingleSRational_PositiveValue)
        {
            // Test parsing positive signed rational: 5/2 = 2.5
            uint8_t bytes[] = { 5, 0, 0, 0, 2, 0, 0, 0 };
            double result = MetadataFormatHelper::ParseSingleSRational(bytes, 0);
            Assert::AreEqual(2.5, result, 0.001);
        }

        TEST_METHOD(ParseSingleSRational_NegativeNumerator)
        {
            // Test parsing negative numerator: -5/2 = -2.5
            uint8_t bytes[] = { 0xFB, 0xFF, 0xFF, 0xFF, 2, 0, 0, 0 }; // -5 in two's complement
            double result = MetadataFormatHelper::ParseSingleSRational(bytes, 0);
            Assert::AreEqual(-2.5, result, 0.001);
        }

        TEST_METHOD(ParseSingleSRational_NegativeDenominator)
        {
            // Test parsing negative denominator: 5/-2 = -2.5
            uint8_t bytes[] = { 5, 0, 0, 0, 0xFE, 0xFF, 0xFF, 0xFF }; // -2 in two's complement
            double result = MetadataFormatHelper::ParseSingleSRational(bytes, 0);
            Assert::AreEqual(-2.5, result, 0.001);
        }

        TEST_METHOD(ParseSingleSRational_BothNegative)
        {
            // Test parsing both negative: -5/-2 = 2.5
            uint8_t bytes[] = { 0xFB, 0xFF, 0xFF, 0xFF, 0xFE, 0xFF, 0xFF, 0xFF };
            double result = MetadataFormatHelper::ParseSingleSRational(bytes, 0);
            Assert::AreEqual(2.5, result, 0.001);
        }

        TEST_METHOD(ParseSingleSRational_ExposureBias)
        {
            // Test typical exposure bias value: -1/3 ≈ -0.333
            uint8_t bytes[] = { 0xFF, 0xFF, 0xFF, 0xFF, 3, 0, 0, 0 }; // -1/3
            double result = MetadataFormatHelper::ParseSingleSRational(bytes, 0);
            Assert::AreEqual(-0.333, result, 0.001);
        }

        TEST_METHOD(ParseSingleSRational_ZeroDenominator)
        {
            // Test with zero denominator (should return 0.0)
            uint8_t bytes[] = { 5, 0, 0, 0, 0, 0, 0, 0 };
            double result = MetadataFormatHelper::ParseSingleSRational(bytes, 0);
            Assert::AreEqual(0.0, result, 0.001);
        }

        TEST_METHOD(ParseSingleSRational_NullPointer)
        {
            // Test with null pointer (should return 0.0)
            double result = MetadataFormatHelper::ParseSingleSRational(nullptr, 0);
            Assert::AreEqual(0.0, result, 0.001);
        }
    };

    TEST_CLASS(SanitizeForFileNameTests)
    {
    public:
        TEST_METHOD(SanitizeForFileName_ValidString)
        {
            // Test string without illegal characters
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"Canon EOS 5D");
            Assert::AreEqual(L"Canon EOS 5D", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_WithColon)
        {
            // Test string with colon (illegal character)
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"Photo:001");
            Assert::AreEqual(L"Photo_001", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_WithSlashes)
        {
            // Test string with forward and backward slashes
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"Photos/2024\\January");
            Assert::AreEqual(L"Photos_2024_January", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_WithMultipleIllegalChars)
        {
            // Test string with multiple illegal characters
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"<Test>:File|Name*?.txt");
            Assert::AreEqual(L"_Test__File_Name__.txt", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_WithQuotes)
        {
            // Test string with quotes
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"Photo \"Best Shot\"");
            Assert::AreEqual(L"Photo _Best Shot_", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_WithTrailingDot)
        {
            // Test string with trailing dot (should be removed)
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"filename.");
            Assert::AreEqual(L"filename", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_WithTrailingSpace)
        {
            // Test string with trailing space (should be removed)
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"filename ");
            Assert::AreEqual(L"filename", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_WithMultipleTrailingDotsAndSpaces)
        {
            // Test string with multiple trailing dots and spaces
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"filename. . ");
            Assert::AreEqual(L"filename", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_WithControlCharacters)
        {
            // Test string with control characters
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"File\x01Name\x1F");
            Assert::AreEqual(L"File_Name_", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_EmptyString)
        {
            // Test empty string
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"");
            Assert::AreEqual(L"", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_OnlyIllegalCharacters)
        {
            // Test string with only illegal characters
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"<>:\"/\\|?*");
            Assert::AreEqual(L"_________", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_OnlyTrailingCharacters)
        {
            // Test string with only dots and spaces (should return empty)
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L". . ");
            Assert::AreEqual(L"", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_UnicodeCharacters)
        {
            // Test string with valid Unicode characters
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"照片_2024年");
            Assert::AreEqual(L"照片_2024年", result.c_str());
        }

        TEST_METHOD(SanitizeForFileName_MixedContent)
        {
            // Test realistic metadata string with multiple issues
            std::wstring result = MetadataFormatHelper::SanitizeForFileName(L"Copyright © 2024: John/Jane Doe. ");
            Assert::AreEqual(L"Copyright © 2024_ John_Jane Doe", result.c_str());
        }
    };
}
