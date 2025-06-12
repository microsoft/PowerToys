#include "pch.h"
#include <StringUtils.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS(StringUtilsTests)
    {
    public:
        TEST_METHOD(CaseInsensitiveEquals_SameStrings_ReturnsTrue)
        {
            // Arrange
            std::wstring str1 = L"test";
            std::wstring str2 = L"test";

            // Act
            bool result = StringUtils::CaseInsensitiveEquals(str1, str2);

            // Assert
            Assert::IsTrue(result);
        }

        TEST_METHOD(CaseInsensitiveEquals_DifferentCase_ReturnsTrue)
        {
            // Arrange
            std::wstring str1 = L"Test";
            std::wstring str2 = L"TEST";

            // Act
            bool result = StringUtils::CaseInsensitiveEquals(str1, str2);

            // Assert
            Assert::IsTrue(result);
        }

        TEST_METHOD(CaseInsensitiveEquals_MixedCase_ReturnsTrue)
        {
            // Arrange
            std::wstring str1 = L"TeSt StRiNg";
            std::wstring str2 = L"test STRING";

            // Act
            bool result = StringUtils::CaseInsensitiveEquals(str1, str2);

            // Assert
            Assert::IsTrue(result);
        }

        TEST_METHOD(CaseInsensitiveEquals_DifferentStrings_ReturnsFalse)
        {
            // Arrange
            std::wstring str1 = L"test";
            std::wstring str2 = L"different";

            // Act
            bool result = StringUtils::CaseInsensitiveEquals(str1, str2);

            // Assert
            Assert::IsFalse(result);
        }

        TEST_METHOD(CaseInsensitiveEquals_DifferentLengths_ReturnsFalse)
        {
            // Arrange
            std::wstring str1 = L"test";
            std::wstring str2 = L"testing";

            // Act
            bool result = StringUtils::CaseInsensitiveEquals(str1, str2);

            // Assert
            Assert::IsFalse(result);
        }

        TEST_METHOD(CaseInsensitiveEquals_EmptyStrings_ReturnsTrue)
        {
            // Arrange
            std::wstring str1 = L"";
            std::wstring str2 = L"";

            // Act
            bool result = StringUtils::CaseInsensitiveEquals(str1, str2);

            // Assert
            Assert::IsTrue(result);
        }

        TEST_METHOD(CaseInsensitiveEquals_OneEmpty_ReturnsFalse)
        {
            // Arrange
            std::wstring str1 = L"test";
            std::wstring str2 = L"";

            // Act
            bool result = StringUtils::CaseInsensitiveEquals(str1, str2);

            // Assert
            Assert::IsFalse(result);
        }

        TEST_METHOD(CaseInsensitiveEquals_SpecialCharacters_ReturnsTrue)
        {
            // Arrange
            std::wstring str1 = L"Test-123_Special!";
            std::wstring str2 = L"test-123_special!";

            // Act
            bool result = StringUtils::CaseInsensitiveEquals(str1, str2);

            // Assert
            Assert::IsTrue(result);
        }
    };
}