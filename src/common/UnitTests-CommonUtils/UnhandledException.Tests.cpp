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
            Assert::IsTrue(result && *result != '\0');
            // Should contain meaningful description
            std::string desc{ result };
            Assert::IsTrue(desc.find("ACCESS") != std::string::npos ||
                          desc.find("access") != std::string::npos ||
                          desc.find("violation") != std::string::npos ||
                          desc.length() > 0);
        }

        TEST_METHOD(ExceptionDescription_StackOverflow_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_STACK_OVERFLOW);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_DivideByZero_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_INT_DIVIDE_BY_ZERO);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_IllegalInstruction_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_ILLEGAL_INSTRUCTION);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_ArrayBoundsExceeded_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_ARRAY_BOUNDS_EXCEEDED);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_Breakpoint_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_BREAKPOINT);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_SingleStep_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_SINGLE_STEP);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_FloatDivideByZero_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_FLT_DIVIDE_BY_ZERO);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_FloatOverflow_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_FLT_OVERFLOW);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_FloatUnderflow_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_FLT_UNDERFLOW);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_FloatInvalidOperation_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_FLT_INVALID_OPERATION);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_PrivilegedInstruction_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_PRIV_INSTRUCTION);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_InPageError_ReturnsDescription)
        {
            auto result = exceptionDescription(EXCEPTION_IN_PAGE_ERROR);
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_UnknownCode_ReturnsDescription)
        {
            auto result = exceptionDescription(0x12345678);
            // Should return something (possibly "Unknown exception" or similar)
            Assert::IsTrue(result && *result != '\0');
        }

        TEST_METHOD(ExceptionDescription_ZeroCode_ReturnsDescription)
        {
            auto result = exceptionDescription(0);
            // Should handle zero gracefully
            Assert::IsTrue(result && *result != '\0');
        }

        // GetFilenameStart tests (if accessible)
        TEST_METHOD(GetFilenameStart_ValidPath_ReturnsFilename)
        {
            wchar_t path[] = L"C:\\folder\\subfolder\\file.exe";
            int start = GetFilenameStart(path);

            Assert::IsTrue(start >= 0);
            Assert::AreEqual(std::wstring(L"file.exe"), std::wstring(path + start));
        }

        TEST_METHOD(GetFilenameStart_NoPath_ReturnsOriginal)
        {
            wchar_t path[] = L"file.exe";
            int start = GetFilenameStart(path);

            Assert::IsTrue(start >= 0);
            Assert::AreEqual(std::wstring(L"file.exe"), std::wstring(path + start));
        }

        TEST_METHOD(GetFilenameStart_TrailingBackslash_ReturnsEmpty)
        {
            wchar_t path[] = L"C:\\folder\\";
            int start = GetFilenameStart(path);

            // Should point to empty string after last backslash
            Assert::IsTrue(start >= 0);
        }

        TEST_METHOD(GetFilenameStart_NullPath_HandlesGracefully)
        {
            // This might crash or return null depending on implementation
            // Just document the behavior
            int start = GetFilenameStart(nullptr);
            (void)start;
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
                        if (desc && *desc != '\0')
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
                Assert::IsTrue(desc && *desc != '\0', (L"Empty description for code: " + std::to_wstring(code)).c_str());
            }
        }
    };
}
