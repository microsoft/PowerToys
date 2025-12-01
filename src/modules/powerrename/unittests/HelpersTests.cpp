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
            // Test that patterns without values show the pattern name with $ prefix
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";
            // ISO is not in the map

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$CAMERA_MAKE_$ISO", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_Canon_$ISO", result);
        }

        TEST_METHOD(EmptyPatternShowsPatternName)
        {
            // Test that patterns with empty value show the pattern name with $ prefix
            PowerRenameLib::MetadataPatternMap patterns;
            patterns[L"CAMERA_MAKE"] = L"Canon";
            patterns[L"ISO"] = L"";

            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetMetadataFileName(result, MAX_PATH, L"photo_$CAMERA_MAKE_$ISO", patterns);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"photo_Canon_$ISO", result);
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
            Assert::AreEqual(L"$price_Canon_$ISO 400_f/2.8_$LENS_$end", result);
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
            // Patterns should show with $ prefix since they're valid but have no values
            Assert::AreEqual(L"photo_$ISO_$CAMERA_MAKE", result);
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

    TEST_CLASS(GetDatedFileNameTests)
    {
    public:
        // Helper to get a fixed test time for consistent testing
        SYSTEMTIME GetTestTime()
        {
            SYSTEMTIME testTime = { 0 };
            testTime.wYear = 2024;
            testTime.wMonth = 3;      // March
            testTime.wDay = 15;       // 15th
            testTime.wHour = 14;      // 2 PM (24-hour format)
            testTime.wMinute = 30;
            testTime.wSecond = 45;
            testTime.wMilliseconds = 123;
            testTime.wDayOfWeek = 5;  // Friday (0=Sunday, 5=Friday)
            return testTime;
        }

        // Category 1: Tests for invalid patterns with extra characters (verify negative lookahead prevents wrong matching)

        TEST_METHOD(InvalidPattern_YYY_NotMatched)
        {
            // Test $YYY (3 Y's) is not a valid pattern and should remain unchanged
            // Negative lookahead in $YY(?!Y) prevents matching $YYY
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$YYY", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_$YYY", result);  // $YYY is invalid, should remain unchanged
        }

        TEST_METHOD(InvalidPattern_DDD_NotPartiallyMatched)
        {
            // Test that $DDD (short weekday) is not confused with $DD (2-digit day)
            // This verifies negative lookahead works correctly
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$DDD", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_Fri", result);  // Should be "Fri", not "15D"
        }

        TEST_METHOD(InvalidPattern_MMM_NotPartiallyMatched)
        {
            // Test that $MMM (short month name) is not confused with $MM (2-digit month)
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$MMM", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_Mar", result);  // Should be "Mar", not "03M"
        }

        TEST_METHOD(InvalidPattern_HHH_NotMatched)
        {
            // Test $HHH (3 H's) is not valid and negative lookahead prevents $HH from matching
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$HHH", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_$HHH", result);  // Should remain unchanged
        }

        TEST_METHOD(SeparatedPatterns_SingleY)
        {
            // Test multiple $Y with separators works correctly
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$Y-$Y-$Y", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_4-4-4", result);  // Each $Y outputs "4" (from 2024)
        }

        TEST_METHOD(SeparatedPatterns_SingleD)
        {
            // Test multiple $D with separators works correctly
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$D.$D.$D", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_15.15.15", result);  // Each $D outputs "15"
        }

        // Category 2: Tests for mixed length patterns (verify longer patterns don't get matched incorrectly)

        TEST_METHOD(MixedLengthYear_QuadFollowedBySingle)
        {
            // Test $YYYY$Y - should be 2024 + 4
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$YYYY$Y", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_20244", result);
        }

        TEST_METHOD(MixedLengthDay_TripleFollowedBySingle)
        {
            // Test $DDD$D - should be "Fri" + "15"
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$DDD$D", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_Fri15", result);
        }

        TEST_METHOD(MixedLengthDay_QuadFollowedByDouble)
        {
            // Test $DDDD$DD - should be "Friday" + "15"
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$DDDD$DD", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_Friday15", result);
        }

        TEST_METHOD(MixedLengthMonth_TripleFollowedBySingle)
        {
            // Test $MMM$M - should be "Mar" + "3"
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$MMM$M", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_Mar3", result);
        }

        // Category 3: Tests for boundary conditions (patterns at start, end, with special chars)

        TEST_METHOD(PatternAtStart)
        {
            // Test pattern at the very start of filename
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"$YYYY$M$D", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"2024315", result);
        }

        TEST_METHOD(PatternAtEnd)
        {
            // Test pattern at the very end of filename
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$Y", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_4", result);
        }

        TEST_METHOD(PatternWithSpecialChars)
        {
            // Test patterns surrounded by special characters
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file-$Y.$Y-$M", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file-4.4-3", result);
        }

        TEST_METHOD(EmptyFileName)
        {
            // Test with empty input string - should return E_INVALIDARG
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"", testTime);

            Assert::IsTrue(FAILED(hr));  // Empty string should fail
            Assert::AreEqual(E_INVALIDARG, hr);
        }

        // Category 4: Tests to explicitly verify negative lookahead is working

        TEST_METHOD(NegativeLookahead_YearNotMatchedInYYYY)
        {
            // Verify $Y doesn't match when part of $YYYY
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$YYYY", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_2024", result);  // Should be "2024", not "202Y"
        }

        TEST_METHOD(NegativeLookahead_MonthNotMatchedInMMM)
        {
            // Verify $M doesn't match when part of $MMM
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$MMM", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_Mar", result);  // Should be "Mar", not "3ar"
        }

        TEST_METHOD(NegativeLookahead_DayNotMatchedInDDDD)
        {
            // Verify $D doesn't match when part of $DDDD
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$DDDD", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_Friday", result);  // Should be "Friday", not "15riday"
        }

        TEST_METHOD(NegativeLookahead_HourNotMatchedInHH)
        {
            // Verify $H doesn't match when part of $HH
            // Note: $HH is 12-hour format, so 14:00 (2 PM) displays as "02"
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$HH", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_02", result);  // 14:00 in 12-hour format is "02 PM"
        }

        TEST_METHOD(NegativeLookahead_MillisecondNotMatchedInFFF)
        {
            // Verify $f doesn't match when part of $fff
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"file_$fff", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"file_123", result);  // Should be "123", not "1ff"
        }

        // Category 5: Complex mixed scenarios

        TEST_METHOD(ComplexMixedPattern_AllFormats)
        {
            // Test a complex realistic filename with mixed pattern lengths
            // Note: Using $hh for 24-hour format instead of $HH (which is 12-hour)
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"Photo_$YYYY-$MM-$DD_$hh-$mm-$ss_$fff", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"Photo_2024-03-15_14-30-45_123", result);
        }

        TEST_METHOD(ComplexMixedPattern_WithSeparators)
        {
            // Test multiple patterns of different lengths with separators
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"$YYYY_$Y-$Y_$MM_$M", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"2024_4-4_03_3", result);
        }

        TEST_METHOD(ComplexMixedPattern_DayFormats)
        {
            // Test all day format variations in one string
            SYSTEMTIME testTime = GetTestTime();
            wchar_t result[MAX_PATH] = { 0 };
            HRESULT hr = GetDatedFileName(result, MAX_PATH, L"$D-$DD-$DDD-$DDDD", testTime);

            Assert::IsTrue(SUCCEEDED(hr));
            Assert::AreEqual(L"15-15-Fri-Friday", result);
        }
    };
}
