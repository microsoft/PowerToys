// Tests for string truncation helper
// Part of issue #45363 test

#include "pch.h"
#include "CppUnitTest.h"
#include <string>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    // Simple truncation helper for testing
    inline std::wstring TruncateString(const std::wstring& input, size_t maxLength)
    {
        if (input.length() <= maxLength)
        {
            return input;
        }
        if (maxLength <= 3)
        {
            return input.substr(0, maxLength);
        }
        return input.substr(0, maxLength - 3) + L"...";
    }

    TEST_CLASS(StringTruncationTests)
    {
    public:
        TEST_METHOD(TruncateString_WhenShorterThanMax_ReturnsOriginal)
        {
            std::wstring input = L"Hello";
            std::wstring result = TruncateString(input, 10);
            Assert::AreEqual(L"Hello", result.c_str());
        }

        TEST_METHOD(TruncateString_WhenExactlyMax_ReturnsOriginal)
        {
            std::wstring input = L"Hello";
            std::wstring result = TruncateString(input, 5);
            Assert::AreEqual(L"Hello", result.c_str());
        }

        TEST_METHOD(TruncateString_WhenLongerThanMax_TruncatesWithEllipsis)
        {
            std::wstring input = L"Hello World";
            std::wstring result = TruncateString(input, 8);
            Assert::AreEqual(L"Hello...", result.c_str());
        }

        TEST_METHOD(TruncateString_WhenVeryShortMaxLength_HandlesGracefully)
        {
            std::wstring input = L"Hello World";
            std::wstring result = TruncateString(input, 2);
            Assert::AreEqual(L"He", result.c_str());
        }

        TEST_METHOD(TruncateString_WhenEmptyString_ReturnsEmpty)
        {
            std::wstring input = L"";
            std::wstring result = TruncateString(input, 10);
            Assert::AreEqual(L"", result.c_str());
        }
    };
}
