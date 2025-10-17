#include "pch.h"
#include "Helpers.h"
#include "MetadataPatternExtractor.h"
#include "MetadataTypes.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace HelpersTests
{
    TEST_CLASS(GetMetadataFileNameTests)
    {
    public:
        TEST_METHOD(BasicPatternReplacement)
        {
            // Test basic pattern replacement with available metadata
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";
            patterns[L"ISO"] = L"ISO 400";
            patterns[L"DATE_TAKEN_YYYY"] = L"2024";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$CAMERA_MAKE_$ISO", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_Canon_ISO 400", result);
        }

        TEST_METHOD(PatternWithoutValueShowsPatternName)
        {
            // Test that patterns without values show the pattern name itself
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";
            // ISO is not in the map

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$CAMERA_MAKE_$ISO", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_Canon_ISO", result);
        }

        TEST_METHOD(UnsupportedPatternShowsPatternName)
        {
            // Test that patterns with "unsupported" value show the pattern name
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";
            patterns[L"ISO"] = L"unsupported";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$CAMERA_MAKE_$ISO", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_Canon_ISO", result);
        }

        TEST_METHOD(EmptyPatternShowsPatternName)
        {
            // Test that patterns with empty value show the pattern name
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";
            patterns[L"ISO"] = L"";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$CAMERA_MAKE_$ISO", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_Canon_ISO", result);
        }

        TEST_METHOD(EscapedDollarSigns)
        {
            // Test that $$ is converted to single $
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"ISO"] = L"ISO 400";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$$_$ISO", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_$_ISO 400", result);
        }

        TEST_METHOD(MultipleEscapedDollarSigns)
        {
            // Test that $$$$ is converted to $$
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"ISO"] = L"ISO 400";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$$$$price", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_$$price", result);
        }

        TEST_METHOD(OddDollarSignsWithPattern)
        {
            // Test that $$$ becomes $ followed by pattern
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"ISO"] = L"ISO 400";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$$$ISO", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_$ISO 400", result);
        }

        TEST_METHOD(LongestPatternMatchPriority)
        {
            // Test that longer patterns are matched first (DATE_TAKEN_YYYY vs DATE_TAKEN_YY)
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"DATE_TAKEN_YYYY"] = L"2024";
            patterns[L"DATE_TAKEN_YY"] = L"24";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$DATE_TAKEN_YYYY", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_2024", result);
        }

        TEST_METHOD(MultiplePatterns)
        {
            // Test multiple patterns in one string
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";
            patterns[L"CAMERA_MODEL"] = L"EOS R5";
            patterns[L"ISO"] = L"ISO 800";
            patterns[L"DATE_TAKEN_YYYY"] = L"2024";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, 
                L"$DATE_TAKEN_YYYY-$CAMERA_MAKE-$CAMERA_MODEL-$ISO", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"2024-Canon-EOS R5-ISO 800", result);
        }

        TEST_METHOD(UnrecognizedPatternIgnored)
        {
            // Test that unrecognized patterns are not replaced
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$CAMERA_MAKE_$INVALID_PATTERN", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_Canon_$INVALID_PATTERN", result);
        }

        TEST_METHOD(NoPatterns)
        {
            // Test string with no patterns
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_name_without_patterns", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_name_without_patterns", result);
        }

        TEST_METHOD(EmptyInput)
        {
            // Test with empty input string
            PowerRenameLib::MetadataPatternMap patterns;

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"", patterns);

            Assert::IsTrue(FAILED(hr));
        }

        TEST_METHOD(NullInput)
        {
            // Test with null input
            PowerRenameLib::MetadataPatternMap patterns;

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, nullptr, patterns);

            Assert::IsTrue(FAILED(hr));
        }

        TEST_METHOD(DollarAtEnd)
        {
            // Test dollar sign at the end of string
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"ISO"] = L"ISO 400";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$ISO$", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_ISO 400$", result);
        }

        TEST_METHOD(ThreeDollarsAtEnd)
        {
            // Test three dollar signs at the end
            PowerRenameLib::MetadataPatternMap patterns;

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo$$$", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo$$$", result);
        }

        TEST_METHOD(ComplexMixedScenario)
        {
            // Test complex scenario with mixed patterns, escapes, and regular text
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";
            patterns[L"ISO"] = L"ISO 400";
            patterns[L"APERTURE"] = L"f/2.8";
            patterns[L"LENS"] = L""; // Empty value

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, 
                L"$$price_$CAMERA_MAKE_$$$ISO_$APERTURE_$LENS_$$end", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"$price_Canon_$ISO 400_f/2.8_LENS_$end", result);
        }

        TEST_METHOD(AllEXIFPatterns)
        {
            // Test with various EXIF patterns
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"WIDTH"] = L"4000";
            patterns[L"HEIGHT"] = L"3000";
            patterns[L"FOCAL"] = L"50mm";
            patterns[L"SHUTTER"] = L"1/100s";
            patterns[L"FLASH"] = L"Flash Off";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, 
                L"photo_$WIDTH x $HEIGHT_$FOCAL_$SHUTTER_$FLASH", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_4000 x 3000_50mm_1/100s_Flash Off", result);
        }

        TEST_METHOD(AllXMPPatterns)
        {
            // Test with various XMP patterns
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"TITLE"] = L"Sunset";
            patterns[L"CREATOR"] = L"John Doe";
            patterns[L"DESCRIPTION"] = L"Beautiful sunset";
            patterns[L"CREATE_DATE_YYYY"] = L"2024";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, 
                L"$CREATE_DATE_YYYY-$TITLE-by-$CREATOR", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"2024-Sunset-by-John Doe", result);
        }

        TEST_METHOD(DateComponentPatterns)
        {
            // Test date component patterns
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"DATE_TAKEN_YYYY"] = L"2024";
            patterns[L"DATE_TAKEN_MM"] = L"03";
            patterns[L"DATE_TAKEN_DD"] = L"15";
            patterns[L"DATE_TAKEN_HH"] = L"14";
            patterns[L"DATE_TAKEN_mm"] = L"30";
            patterns[L"DATE_TAKEN_SS"] = L"45";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, 
                L"photo_$DATE_TAKEN_YYYY-$DATE_TAKEN_MM-$DATE_TAKEN_DD_$DATE_TAKEN_HH-$DATE_TAKEN_mm-$DATE_TAKEN_SS", 
                patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_2024-03-15_14-30-45", result);
        }

        TEST_METHOD(SpecialCharactersInValues)
        {
            // Test that special characters in metadata values are preserved
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"TITLE"] = L"Photo (with) [brackets] & symbols!";
            patterns[L"DESCRIPTION"] = L"Test: value; with, punctuation.";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, 
                L"$TITLE - $DESCRIPTION", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"Photo (with) [brackets] & symbols! - Test: value; with, punctuation.", result);
        }

        TEST_METHOD(ConsecutivePatternsWithoutSeparator)
        {
            // Test consecutive patterns without separator
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";
            patterns[L"CAMERA_MODEL"] = L"R5";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"$CAMERA_MAKE$CAMERA_MODEL", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"CanonR5", result);
        }

        TEST_METHOD(PatternAtStart)
        {
            // Test pattern at the beginning of string
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"$CAMERA_MAKE_photo", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"Canon_photo", result);
        }

        TEST_METHOD(PatternAtEnd)
        {
            // Test pattern at the end of string
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$CAMERA_MAKE", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_Canon", result);
        }

        TEST_METHOD(OnlyPattern)
        {
            // Test string with only a pattern
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"$CAMERA_MAKE", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"Canon", result);
        }
    };

    TEST_CLASS(PatternMatchingTests)
    {
    public:
        TEST_METHOD(VerifyLongestPatternMatching)
        {
            // This test verifies the greedy matching behavior
            // When we have overlapping pattern names, the longest should be matched first
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"DATE_TAKEN_Y"] = L"4";
            patterns[L"DATE_TAKEN_YY"] = L"24";
            patterns[L"DATE_TAKEN_YYYY"] = L"2024";

            wchar_t result[MAX_PATH] = { 0 };
            
            // Should match YYYY (longest)
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"$DATE_TAKEN_YYYY", patterns);
            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"2024", result);

            // Should match YY (available pattern)
            hr = GetMetadataFileName(result, MAX_PATH, L"$DATE_TAKEN_YY", patterns);
            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"24", result);
        }

        TEST_METHOD(PartialPatternNames)
        {
            // Test that partial pattern names don't match longer patterns
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MODEL"] = L"EOS R5";

            wchar_t result[MAX_PATH] = { 0 };
            // CAMERA is not a valid pattern, should not match
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"$CAMERA_MODEL", patterns);
            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"EOS R5", result);
        }

        TEST_METHOD(CaseSensitivePatterns)
        {
            // Test that pattern names are case-sensitive
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";

            wchar_t result[MAX_PATH] = { 0 };
            // lowercase should not match
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"$camera_make", patterns);
            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"$camera_make", result); // Not replaced
        }

        TEST_METHOD(EmptyPatternMap)
        {
            // Test with empty pattern map
            PowerRenameLib::MetadataPatternMap patterns; // Empty

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$ISO_$CAMERA_MAKE", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            // Patterns should show as pattern names since they're valid but have no values
            Assert::AreEqual(L"photo_ISO_CAMERA_MAKE", result);
        }
    };

    TEST_CLASS(EdgeCaseTests)
    {
    public:
        TEST_METHOD(VeryLongString)
        {
            // Test with a very long input string
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";

            std::wstring longInput = L"prefix_";
            for (int i = 0; i < 100; i++)
            {
                longInput += L"$CAMERA_MAKE_";
            }

            wchar_t result[4096] = { 0 };
            HRESULT hr = GetMetadataFileName(result, 4096, longInput.c_str(), patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            // Verify it starts correctly
            Assert::IsTrue(wcsstr(result, L"prefix_Canon_") == result);
        }

        TEST_METHOD(ManyConsecutiveDollars)
        {
            // Test with many consecutive dollar signs
            PowerRenameLib::MetadataPatternMap patterns;

            wchar_t result[MAX_PATH] = { 0 };
            // 8 dollars should become 4 dollars
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo$$$$$$$$name", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo$$$$name", result);
        }

        TEST_METHOD(OnlyDollars)
        {
            // Test string with only dollar signs
            PowerRenameLib::MetadataPatternMap patterns;

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"$$$$", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"$$", result);
        }

        TEST_METHOD(UnicodeCharacters)
        {
            // Test with unicode characters in pattern values
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"TITLE"] = L"照片_фото_φωτογραφία";
            patterns[L"CREATOR"] = L"张三_Иван_Γιάννης";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"$TITLE-$CREATOR", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"照片_фото_φωτογραφία-张三_Иван_Γιάννης", result);
        }

        TEST_METHOD(SingleDollar)
        {
            // Test with single dollar not followed by pattern
            PowerRenameLib::MetadataPatternMap patterns;

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"price$100", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"price$100", result);
        }

        TEST_METHOD(DollarFollowedByNumber)
        {
            // Test dollar followed by numbers (not a pattern)
            PowerRenameLib::MetadataPatternMap patterns;

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"cost_$123.45", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"cost_$123.45", result);
        }
    };
}
