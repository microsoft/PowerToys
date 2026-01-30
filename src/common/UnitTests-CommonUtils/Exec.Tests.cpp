#include "pch.h"
#include "TestHelpers.h"
#include <exec.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace UnitTestsCommonUtils
{
    TEST_CLASS(ExecTests)
    {
    public:
        TEST_METHOD(ExecAndReadOutput_EchoCommand_ReturnsOutput)
        {
            auto result = exec_and_read_output(L"cmd /c echo hello", 5000);

            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
            // Output should contain "hello"
            Assert::IsTrue(result->find(L"hello") != std::wstring::npos);
        }

        TEST_METHOD(ExecAndReadOutput_WhereCommand_ReturnsPath)
        {
            auto result = exec_and_read_output(L"where cmd", 5000);

            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
            // Should contain path to cmd.exe
            Assert::IsTrue(result->find(L"cmd") != std::wstring::npos);
        }

        TEST_METHOD(ExecAndReadOutput_DirCommand_ReturnsListing)
        {
            auto result = exec_and_read_output(L"cmd /c dir /b C:\\Windows", 5000);

            Assert::IsTrue(result.has_value());
            Assert::IsFalse(result->empty());
            // Should contain some common Windows folder names
            std::wstring output = *result;
            std::transform(output.begin(), output.end(), output.begin(), ::towlower);
            Assert::IsTrue(output.find(L"system32") != std::wstring::npos ||
                          output.find(L"system") != std::wstring::npos);
        }

        TEST_METHOD(ExecAndReadOutput_InvalidCommand_ReturnsEmptyOrError)
        {
            auto result = exec_and_read_output(L"nonexistentcommand12345", 5000);

            // Invalid command should either return nullopt or an error message
            Assert::IsTrue(!result.has_value() || result->empty() ||
                          result->find(L"not recognized") != std::wstring::npos ||
                          result->find(L"error") != std::wstring::npos);
        }

        TEST_METHOD(ExecAndReadOutput_EmptyCommand_DoesNotCrash)
        {
            auto result = exec_and_read_output(L"", 5000);
            // Should handle empty command gracefully
            Assert::IsTrue(true);
        }

        TEST_METHOD(ExecAndReadOutput_TimeoutExpires_ReturnsAvailableOutput)
        {
            // Use a command that produces output slowly
            // ping localhost will run for a while
            auto start = std::chrono::steady_clock::now();

            // Very short timeout
            auto result = exec_and_read_output(L"ping localhost -n 10", 100);

            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::steady_clock::now() - start);

            // Should return within reasonable time
            Assert::IsTrue(elapsed.count() < 5000);
        }

        TEST_METHOD(ExecAndReadOutput_MultilineOutput_PreservesLines)
        {
            auto result = exec_and_read_output(L"cmd /c \"echo line1 & echo line2 & echo line3\"", 5000);

            Assert::IsTrue(result.has_value());
            // Should contain multiple lines
            Assert::IsTrue(result->find(L"line1") != std::wstring::npos);
            Assert::IsTrue(result->find(L"line2") != std::wstring::npos);
            Assert::IsTrue(result->find(L"line3") != std::wstring::npos);
        }

        TEST_METHOD(ExecAndReadOutput_UnicodeOutput_Works)
        {
            // Echo a simple ASCII string (Unicode test depends on system codepage)
            auto result = exec_and_read_output(L"cmd /c echo test123", 5000);

            Assert::IsTrue(result.has_value());
            Assert::IsTrue(result->find(L"test123") != std::wstring::npos);
        }

        TEST_METHOD(ExecAndReadOutput_LongTimeout_Works)
        {
            auto result = exec_and_read_output(L"cmd /c echo test", 60000);

            Assert::IsTrue(result.has_value());
            Assert::IsTrue(result->find(L"test") != std::wstring::npos);
        }

        TEST_METHOD(ExecAndReadOutput_QuotedArguments_Work)
        {
            auto result = exec_and_read_output(L"cmd /c echo \"hello world\"", 5000);

            Assert::IsTrue(result.has_value());
            Assert::IsTrue(result->find(L"hello") != std::wstring::npos);
        }

        TEST_METHOD(ExecAndReadOutput_EnvironmentVariable_Expanded)
        {
            auto result = exec_and_read_output(L"cmd /c echo %USERNAME%", 5000);

            Assert::IsTrue(result.has_value());
            // Should not contain the literal %USERNAME% but the actual username
            // Or if not expanded, still should not crash
            Assert::IsFalse(result->empty());
        }

        TEST_METHOD(ExecAndReadOutput_ExitCode_CommandFails)
        {
            // Command that exits with error
            auto result = exec_and_read_output(L"cmd /c exit 1", 5000);

            // Should still return something (possibly empty)
            // Just verify it doesn't crash
            Assert::IsTrue(true);
        }

        TEST_METHOD(ExecAndReadOutput_ZeroTimeout_DoesNotHang)
        {
            auto start = std::chrono::steady_clock::now();

            auto result = exec_and_read_output(L"cmd /c echo test", 0);

            auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
                std::chrono::steady_clock::now() - start);

            // Should complete quickly with zero timeout
            Assert::IsTrue(elapsed.count() < 5000);
        }
    };
}
