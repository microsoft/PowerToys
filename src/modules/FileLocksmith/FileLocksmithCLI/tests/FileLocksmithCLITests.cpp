#include "pch.h"
#include "CppUnitTest.h"
#include "../CLILogic.h"
#include <map>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FileLocksmithCLIUnitTests
{
    struct MockProcessFinder : IProcessFinder
    {
        std::vector<ProcessResult> results;
        std::vector<ProcessResult> find(const std::vector<std::wstring>& paths) override
        {
            (void)paths;
            return results;
        }
    };

    struct MockProcessTerminator : IProcessTerminator
    {
        bool shouldSucceed = true;
        std::vector<DWORD> terminatedPids;
        bool terminate(DWORD pid) override
        {
            terminatedPids.push_back(pid);
            return shouldSucceed;
        }
    };

    struct MockStringProvider : IStringProvider
    {
        std::map<UINT, std::wstring> strings;
        std::wstring GetString(UINT id) override
        {
            if (strings.count(id)) return strings[id];
            return L"String_" + std::to_wstring(id);
        }
    };

    TEST_CLASS(CLITests)
    {
    public:
        
        TEST_METHOD(TestNoArgs)
        {
            MockProcessFinder finder;
            MockProcessTerminator terminator;
            MockStringProvider strings;
            
            wchar_t* argv[] = { (wchar_t*)L"exe" };
            auto result = run_command(1, argv, finder, terminator, strings);
            
            Assert::AreEqual(1, result.exit_code);
        }

        TEST_METHOD(TestHelp)
        {
            MockProcessFinder finder;
            MockProcessTerminator terminator;
            MockStringProvider strings;
            
            wchar_t* argv[] = { (wchar_t*)L"exe", (wchar_t*)L"--help" };
            auto result = run_command(2, argv, finder, terminator, strings);
            
            Assert::AreEqual(0, result.exit_code);
        }

        TEST_METHOD(TestFindProcesses)
        {
            MockProcessFinder finder;
            finder.results = { { L"process", 123, L"user", { L"file1" } } };
            MockProcessTerminator terminator;
            MockStringProvider strings;
            
            wchar_t* argv[] = { (wchar_t*)L"exe", (wchar_t*)L"file1" };
            auto result = run_command(2, argv, finder, terminator, strings);
            
            Assert::AreEqual(0, result.exit_code);
            Assert::IsTrue(result.output.find(L"123") != std::wstring::npos);
            Assert::IsTrue(result.output.find(L"process") != std::wstring::npos);
        }

        TEST_METHOD(TestJsonOutput)
        {
            MockProcessFinder finder;
            finder.results = { { L"process", 123, L"user", { L"file1" } } };
            MockProcessTerminator terminator;
            MockStringProvider strings;
            
            wchar_t* argv[] = { (wchar_t*)L"exe", (wchar_t*)L"file1", (wchar_t*)L"--json" };
            auto result = run_command(3, argv, finder, terminator, strings);
            
            Microsoft::VisualStudio::CppUnitTestFramework::Logger::WriteMessage(result.output.c_str());

            Assert::AreEqual(0, result.exit_code);
            Assert::IsTrue(result.output.find(L"\"pid\"") != std::wstring::npos);
            Assert::IsTrue(result.output.find(L"123") != std::wstring::npos);
        }

        TEST_METHOD(TestKill)
        {
            MockProcessFinder finder;
            finder.results = { { L"process", 123, L"user", { L"file1" } } };
            MockProcessTerminator terminator;
            MockStringProvider strings;
            
            wchar_t* argv[] = { (wchar_t*)L"exe", (wchar_t*)L"file1", (wchar_t*)L"--kill" };
            auto result = run_command(3, argv, finder, terminator, strings);
            
            Assert::AreEqual(0, result.exit_code);
            Assert::AreEqual((size_t)1, terminator.terminatedPids.size());
            Assert::AreEqual((DWORD)123, terminator.terminatedPids[0]);
        }

        TEST_METHOD(TestTimeout)
        {
            MockProcessFinder finder;
            // Always return results so it waits
            finder.results = { { L"process", 123, L"user", { L"file1" } } };
            MockProcessTerminator terminator;
            MockStringProvider strings;
            
            wchar_t* argv[] = { (wchar_t*)L"exe", (wchar_t*)L"file1", (wchar_t*)L"--wait", (wchar_t*)L"--timeout", (wchar_t*)L"100" };
            auto result = run_command(5, argv, finder, terminator, strings);
            
            Assert::AreEqual(1, result.exit_code);
        }
    };
}
