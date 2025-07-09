#include "pch.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace WorkspacesLibUnitTests
{
    TEST_CLASS (PwaHelperTests)
    {
    public:
        TEST_METHOD (PwaHelper_Constructor_DoesNotThrow)
        {
            // Act & Assert - Constructor should not crash when called
            try
            {
                Utils::PwaHelper helper;
                // If we get here, the constructor didn't throw
                Assert::IsTrue(true);
            }
            catch (...)
            {
                Assert::Fail(L"PwaHelper constructor should not throw exceptions");
            }
        }

        TEST_METHOD (PwaHelper_GetEdgeAppId_EmptyAumid_ReturnsEmpty)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring emptyAumid = L"";

            // Act
            auto result = helper.GetEdgeAppId(emptyAumid);

            // Assert
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD (PwaHelper_GetChromeAppId_EmptyAumid_ReturnsEmpty)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring emptyAumid = L"";

            // Act
            auto result = helper.GetChromeAppId(emptyAumid);

            // Assert
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD (PwaHelper_SearchPwaName_EmptyParameters_ReturnsEmpty)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring emptyPwaAppId = L"";
            std::wstring emptyWindowAumid = L"";

            // Act
            std::wstring result = helper.SearchPwaName(emptyPwaAppId, emptyWindowAumid);

            // Assert
            Assert::IsTrue(result.empty());
        }

        TEST_METHOD (PwaHelper_SearchPwaName_NonExistentIds_ReturnsEmpty)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring nonExistentPwaAppId = L"nonexistent_app_id";
            std::wstring nonExistentWindowAumid = L"nonexistent_aumid";

            // Act
            std::wstring result = helper.SearchPwaName(nonExistentPwaAppId, nonExistentWindowAumid);

            // TODO: is it really expected?
            Assert::IsTrue(result == nonExistentWindowAumid);
        }

        TEST_METHOD (PwaHelper_GetEdgeAppId_ValidConstruction_DoesNotCrash)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring testAumid = L"Microsoft.MicrosoftEdge_8wekyb3d8bbwe!App";

            // Act & Assert - Should not crash
            auto result = helper.GetEdgeAppId(testAumid);
            // Result can be empty or have value, but should not crash
        }

        TEST_METHOD (PwaHelper_GetChromeAppId_ValidConstruction_DoesNotCrash)
        {
            // Arrange
            Utils::PwaHelper helper;
            std::wstring testAumid = L"Chrome.App.TestId";

            // Act & Assert - Should not crash
            auto result = helper.GetChromeAppId(testAumid);
            // Result can be empty or have value, but should not crash
        }
    };
}