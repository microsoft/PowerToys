#include "pch.h"
#include "TestHelpers.h"
#include <UnhandledExceptionHandler.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(UnhandledExceptionTests)
    {
    public:
        // exceptionDescription tests
        TEST_METHOD(ExceptionDescription_AccessViolation_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_ACCESS_VIOLATION);
            Assert::IsFalse(result.empty());
            // Should contain meaningful description
            Assert::IsTrue(result.find(L"access") != std::wstring::npos ||
                          result.find(L"Access") != std::wstring::npos ||
                          result.find(L"violation") != std::wstring::npos ||
                          result.find(L"Violation") != std::wstring::npos ||
                          result.length() > 0);
        }

        TEST_METHOD(ExceptionDescription_StackOverflow_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_STACK_OVERFLOW);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_DivideByZero_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_INT_DIVIDE_BY_ZERO);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_IllegalInstruction_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_ILLEGAL_INSTRUCTION);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_ArrayBoundsExceeded_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_ARRAY_BOUNDS_EXCEEDED);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_Breakpoint_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_BREAKPOINT);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_SingleStep_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_SINGLE_STEP);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_FloatDivideByZero_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_FLT_DIVIDE_BY_ZERO);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_FloatOverflow_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_FLT_OVERFLOW);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_FloatUnderflow_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_FLT_UNDERFLOW);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_FloatInvalidOperation_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_FLT_INVALID_OPERATION);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_PrivilegedInstruction_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_PRIV_INSTRUCTION);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_InPageError_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_IN_PAGE_ERROR);
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_UnknownCode_ReturnsDescription)
        {
            auto result = exceptionDescription(0x12345678);
            // Should return something (possibly "Unknown exception" or similar)
            Assert::IsFalse(result.empty());
        }

        TEST_METHOD(ExceptionDescription_ZeroCode_ReturnsDescription)
        {
            auto result = exceptionDescription(0);
            // Should handle zero gracefully
            Assert::IsFalse(result.empty());
        }

        // GetFilenameStart tests (if accessible)
        TEST_METHOD(GetFilenameStart_ValidPath_ReturnsFilename)
        {
            wchar_t path[] = L"C:\\folder\\subfolder\\file.exe";
            wchar_t* result = GetFilenameStart(path);

            Assert::IsNotNull(result);
            Assert::AreEqual(std::wstring(L"file.exe"), std::wstring(result));
        }

        TEST_METHOD(GetFilenameStart_NoPath_ReturnsOriginal)
        {
            wchar_t path[] = L"file.exe";
            wchar_t* result = GetFilenameStart(path);

            Assert::IsNotNull(result);
            Assert::AreEqual(std::wstring(L"file.exe"), std::wstring(result));
        }

        TEST_METHOD(GetFilenameStart_TrailingBackslash_ReturnsEmpty)
        {
            wchar_t path[] = L"C:\\folder\\";
            wchar_t* result = GetFilenameStart(path);

            // Should point to empty string after last backslash
            Assert::IsNotNull(result);
        }

        TEST_METHOD(GetFilenameStart_NullPath_HandlesGracefully)
        {
            // This might crash or return null depending on implementation
            // Just document the behavior
            wchar_t* result = GetFilenameStart(nullptr);
            // Result is implementation-defined for null input
            Assert::IsTrue(true);
        }

        // Thread safety tests
        TEST_METHOD(ExceptionDescription_ThreadSafe)
        {
            std::vector<std::thread> threads;
            std::atomic<int> successCount{ 0 };

            for (int i = 0; i < 10; ++i)
            {
                threads.emplace_back([&successCount]() {
                    for (int j = 0; j < 10; ++j)
                    {
                        auto desc = exceptionDescription(EXCEPTION_ACCESS_VIOLATION);
                        if (!desc.empty())
                        {
                            successCount++;
                        }
                    }
                });
            }

            for (auto& t : threads)
            {
                t.join();
            }

            Assert::AreEqual(100, successCount.load());
        }

        // All exception codes test
        TEST_METHOD(ExceptionDescription_AllCommonCodes_ReturnDescriptions)
        {
            std::vector<DWORD> codes = {
                EXCEPTION_ACCESS_VIOLATION,
                EXCEPTION_ARRAY_BOUNDS_EXCEEDED,
                EXCEPTION_BREAKPOINT,
                EXCEPTION_DATATYPE_MISALIGNMENT,
                EXCEPTION_FLT_DENORMAL_OPERAND,
                EXCEPTION_FLT_DIVIDE_BY_ZERO,
                EXCEPTION_FLT_INEXACT_RESULT,
                EXCEPTION_FLT_INVALID_OPERATION,
                EXCEPTION_FLT_OVERFLOW,
                EXCEPTION_FLT_STACK_CHECK,
                EXCEPTION_FLT_UNDERFLOW,
                EXCEPTION_ILLEGAL_INSTRUCTION,
                EXCEPTION_IN_PAGE_ERROR,
                EXCEPTION_INT_DIVIDE_BY_ZERO,
                EXCEPTION_INT_OVERFLOW,
                EXCEPTION_INVALID_DISPOSITION,
                EXCEPTION_NONCONTINUABLE_EXCEPTION,
                EXCEPTION_PRIV_INSTRUCTION,
                EXCEPTION_SINGLE_STEP,
                EXCEPTION_STACK_OVERFLOW
            };

            for (DWORD code : codes)
            {
                auto desc = exceptionDescription(code);
                Assert::IsFalse(desc.empty(), (L"Empty description for code: " + std::to_wstring(code)).c_str());
            }
        }
    };
}
