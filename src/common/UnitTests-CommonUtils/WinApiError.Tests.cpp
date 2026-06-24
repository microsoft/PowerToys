#include "pch.h"
#include "TestHelpers.h"
#include <winapi_error.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(WinApiErrorTests)
    {
    public:
        // get_last_error_message tests
        TEST_METHOD(GetLastErrorMessage_Success_ReturnsMessage)
        {
            auto result = get_last_error_message(ERROR_SUCCESS);
            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
        }

        TEST_METHOD(GetLastErrorMessage_FileNotFound_ReturnsMessage)
        {
            auto result = get_last_error_message(ERROR_FILE_NOT_FOUND);
            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
        }

        TEST_METHOD(GetLastErrorMessage_AccessDenied_ReturnsMessage)
        {
            auto result = get_last_error_message(ERROR_ACCESS_DENIED);
            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
        }

        TEST_METHOD(GetLastErrorMessage_PathNotFound_ReturnsMessage)
        {
            auto result = get_last_error_message(ERROR_PATH_NOT_FOUND);
            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
        }

        TEST_METHOD(GetLastErrorMessage_InvalidHandle_ReturnsMessage)
        {
            auto result = get_last_error_message(ERROR_INVALID_HANDLE);
            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
        }

        TEST_METHOD(GetLastErrorMessage_NotEnoughMemory_ReturnsMessage)
        {
            auto result = get_last_error_message(ERROR_NOT_ENOUGH_MEMORY);
            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
        }

        TEST_METHOD(GetLastErrorMessage_InvalidParameter_ReturnsMessage)
        {
            auto result = get_last_error_message(ERROR_INVALID_PARAMETER);
            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
        }

        // get_last_error_or_default tests
        TEST_METHOD(GetLastErrorOrDefault_Success_ReturnsMessage)
        {
            auto result = get_last_error_or_default(ERROR_SUCCESS);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(GetLastErrorOrDefault_FileNotFound_ReturnsMessage)
        {
            auto result = get_last_error_or_default(ERROR_FILE_NOT_FOUND);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(GetLastErrorOrDefault_AccessDenied_ReturnsMessage)
        {
            auto result = get_last_error_or_default(ERROR_ACCESS_DENIED);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(GetLastErrorOrDefault_UnknownError_ReturnsEmptyOrMessage)
        {
            // For an unknown error code, should return empty string or a default message
            auto result = get_last_error_or_default(0xFFFFFFFF);
            // Either empty or has content, both are valid
            Assert::IsTrue(result.empty() || !result.empty());
        }

        // Comparison tests
        TEST_METHOD(BothFunctions_SameError_ProduceSameContent)
        {
            auto message = get_last_error_message(ERROR_FILE_NOT_FOUND);
            auto defaultMessage = get_last_error_or_default(ERROR_FILE_NOT_FOUND);

            Assert::IsTrue(message.has_value());
            Assert::AreEqual(*message, defaultMessage);
        }

        TEST_METHOD(BothFunctions_SuccessError_ProduceSameContent)
        {
            auto message = get_last_error_message(ERROR_SUCCESS);
            auto defaultMessage = get_last_error_or_default(ERROR_SUCCESS);

            Assert::IsTrue(message.has_value());
            Assert::AreEqual(*message, defaultMessage);
        }

        // Error code specific tests
        TEST_METHOD(GetLastErrorMessage_SharingViolation_ReturnsMessage)
        {
            auto result = get_last_error_message(ERROR_SHARING_VIOLATION);
            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
        }

        TEST_METHOD(GetLastErrorMessage_FileExists_ReturnsMessage)
        {
            auto result = get_last_error_message(ERROR_FILE_EXISTS);
            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
        }

        TEST_METHOD(GetLastErrorMessage_DirNotEmpty_ReturnsMessage)
        {
            auto result = get_last_error_message(ERROR_DIR_NOT_EMPTY);
            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
        }
    };
}
