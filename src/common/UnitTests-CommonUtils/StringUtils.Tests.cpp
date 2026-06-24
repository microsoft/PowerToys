#include "pch.h"
#include "TestHelpers.h"
#include <string_utils.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(StringUtilsTests)
    {
    public:
        // left_trim tests
        TEST_METHOD(LeftTrim_EmptyString_ReturnsEmpty)
        {
            std::string_view input = "";
            auto result = left_trim(input);
            Assert::AreEqual(std::string_view(""), result);
        }

        TEST_METHOD(LeftTrim_NoWhitespace_ReturnsOriginal)
        {
            std::string_view input = "hello";
            auto result = left_trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(LeftTrim_LeadingSpaces_TrimsSpaces)
        {
            std::string_view input = "   hello";
            auto result = left_trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(LeftTrim_LeadingTabs_TrimsTabs)
        {
            std::string_view input = "\t\thello";
            auto result = left_trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(LeftTrim_LeadingNewlines_TrimsNewlines)
        {
            std::string_view input = "\r\n\nhello";
            auto result = left_trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(LeftTrim_MixedWhitespace_TrimsAll)
        {
            std::string_view input = " \t\r\nhello";
            auto result = left_trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(LeftTrim_TrailingWhitespace_PreservesTrailing)
        {
            std::string_view input = "   hello   ";
            auto result = left_trim(input);
            Assert::AreEqual(std::string_view("hello   "), result);
        }

        TEST_METHOD(LeftTrim_OnlyWhitespace_ReturnsEmpty)
        {
            std::string_view input = "   \t\r\n";
            auto result = left_trim(input);
            Assert::AreEqual(std::string_view(""), result);
        }

        TEST_METHOD(LeftTrim_CustomChars_TrimsSpecified)
        {
            std::string_view input = "xxxhello";
            auto result = left_trim(input, std::string_view("x"));
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(LeftTrim_WideString_Works)
        {
            std::wstring_view input = L"   hello";
            auto result = left_trim(input);
            Assert::AreEqual(std::wstring_view(L"hello"), result);
        }

        // right_trim tests
        TEST_METHOD(RightTrim_EmptyString_ReturnsEmpty)
        {
            std::string_view input = "";
            auto result = right_trim(input);
            Assert::AreEqual(std::string_view(""), result);
        }

        TEST_METHOD(RightTrim_NoWhitespace_ReturnsOriginal)
        {
            std::string_view input = "hello";
            auto result = right_trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(RightTrim_TrailingSpaces_TrimsSpaces)
        {
            std::string_view input = "hello   ";
            auto result = right_trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(RightTrim_TrailingTabs_TrimsTabs)
        {
            std::string_view input = "hello\t\t";
            auto result = right_trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(RightTrim_TrailingNewlines_TrimsNewlines)
        {
            std::string_view input = "hello\r\n\n";
            auto result = right_trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(RightTrim_LeadingWhitespace_PreservesLeading)
        {
            std::string_view input = "   hello   ";
            auto result = right_trim(input);
            Assert::AreEqual(std::string_view("   hello"), result);
        }

        TEST_METHOD(RightTrim_OnlyWhitespace_ReturnsEmpty)
        {
            std::string_view input = "   \t\r\n";
            auto result = right_trim(input);
            Assert::AreEqual(std::string_view(""), result);
        }

        TEST_METHOD(RightTrim_CustomChars_TrimsSpecified)
        {
            std::string_view input = "helloxxx";
            auto result = right_trim(input, std::string_view("x"));
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(RightTrim_WideString_Works)
        {
            std::wstring_view input = L"hello   ";
            auto result = right_trim(input);
            Assert::AreEqual(std::wstring_view(L"hello"), result);
        }

        // trim tests
        TEST_METHOD(Trim_EmptyString_ReturnsEmpty)
        {
            std::string_view input = "";
            auto result = trim(input);
            Assert::AreEqual(std::string_view(""), result);
        }

        TEST_METHOD(Trim_NoWhitespace_ReturnsOriginal)
        {
            std::string_view input = "hello";
            auto result = trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(Trim_BothSides_TrimsBoth)
        {
            std::string_view input = "   hello   ";
            auto result = trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(Trim_MixedWhitespace_TrimsAll)
        {
            std::string_view input = " \t\r\nhello \t\r\n";
            auto result = trim(input);
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(Trim_InternalWhitespace_Preserved)
        {
            std::string_view input = "   hello world   ";
            auto result = trim(input);
            Assert::AreEqual(std::string_view("hello world"), result);
        }

        TEST_METHOD(Trim_OnlyWhitespace_ReturnsEmpty)
        {
            std::string_view input = "   \t\r\n   ";
            auto result = trim(input);
            Assert::AreEqual(std::string_view(""), result);
        }

        TEST_METHOD(Trim_CustomChars_TrimsSpecified)
        {
            std::string_view input = "xxxhelloxxx";
            auto result = trim(input, std::string_view("x"));
            Assert::AreEqual(std::string_view("hello"), result);
        }

        TEST_METHOD(Trim_WideString_Works)
        {
            std::wstring_view input = L"   hello   ";
            auto result = trim(input);
            Assert::AreEqual(std::wstring_view(L"hello"), result);
        }

        // replace_chars tests
        TEST_METHOD(ReplaceChars_EmptyString_NoChange)
        {
            std::string s = "";
            replace_chars(s, std::string_view("abc"), 'x');
            Assert::AreEqual(std::string(""), s);
        }

        TEST_METHOD(ReplaceChars_NoMatchingChars_NoChange)
        {
            std::string s = "hello";
            replace_chars(s, std::string_view("xyz"), '_');
            Assert::AreEqual(std::string("hello"), s);
        }

        TEST_METHOD(ReplaceChars_SingleChar_Replaces)
        {
            std::string s = "hello";
            replace_chars(s, std::string_view("l"), '_');
            Assert::AreEqual(std::string("he__o"), s);
        }

        TEST_METHOD(ReplaceChars_MultipleChars_ReplacesAll)
        {
            std::string s = "hello world";
            replace_chars(s, std::string_view("lo"), '_');
            Assert::AreEqual(std::string("he___ w_r_d"), s);
        }

        TEST_METHOD(ReplaceChars_WideString_Works)
        {
            std::wstring s = L"hello";
            replace_chars(s, std::wstring_view(L"l"), L'_');
            Assert::AreEqual(std::wstring(L"he__o"), s);
        }

        // unwide tests
        TEST_METHOD(Unwide_EmptyString_ReturnsEmpty)
        {
            std::wstring input = L"";
            auto result = unwide(input);
            Assert::AreEqual(std::string(""), result);
        }

        TEST_METHOD(Unwide_AsciiString_Converts)
        {
            std::wstring input = L"hello";
            auto result = unwide(input);
            Assert::AreEqual(std::string("hello"), result);
        }

        TEST_METHOD(Unwide_WithNumbers_Converts)
        {
            std::wstring input = L"test123";
            auto result = unwide(input);
            Assert::AreEqual(std::string("test123"), result);
        }

        TEST_METHOD(Unwide_WithSpecialChars_Converts)
        {
            std::wstring input = L"test!@#$%";
            auto result = unwide(input);
            Assert::AreEqual(std::string("test!@#$%"), result);
        }

        TEST_METHOD(Unwide_MixedCase_PreservesCase)
        {
            std::wstring input = L"HeLLo WoRLd";
            auto result = unwide(input);
            Assert::AreEqual(std::string("HeLLo WoRLd"), result);
        }

        TEST_METHOD(Unwide_LongString_Works)
        {
            std::wstring input = L"This is a longer string with multiple words and punctuation!";
            auto result = unwide(input);
            Assert::AreEqual(std::string("This is a longer string with multiple words and punctuation!"), result);
        }
    };
}
