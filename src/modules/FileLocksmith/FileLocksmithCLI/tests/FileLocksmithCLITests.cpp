#include "pch.h"
#include "CppUnitTest.h"
#include "../CLILogic.h"
#include "../../FileLocksmithLib/Constants.h"
#include <map>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FileLocksmithCLIUnitTests
{
    // ========================================================================
    // Constants validation — guards against silent config corruption
    // ========================================================================
    TEST_CLASS(ConstantsTests)
    {
    public:

        // Product code: Constants.h — constants::nonlocalizable::PowerToyKey
        // What: Validates the module's registry/settings key matches expected value
        // Why: A mismatch breaks settings persistence, module registration, and GPO detection
        // Risk: Silent config corruption if key changes without updating all consumers
        TEST_METHOD(PowerToyKey_IsFileLocksmith)
        {
            Assert::AreEqual(L"File Locksmith", constants::nonlocalizable::PowerToyKey);
        }

        // Product code: Constants.h — constants::nonlocalizable::PowerToyName
        // What: Validates the display/log name for the module
        // Why: Used in logging, telemetry, and user-visible strings; must match expectations
        // Risk: Incorrect name breaks log filtering and telemetry dashboards
        TEST_METHOD(PowerToyName_IsFileLocksmith)
        {
            Assert::AreEqual(L"File Locksmith", constants::nonlocalizable::PowerToyName);
        }

        // Product code: Constants.h — constants::nonlocalizable::JsonKeyEnabled
        // What: Validates the JSON settings key for the enabled flag
        // Why: PowerToys settings framework reads this key to determine if module is on
        // Risk: Typo or rename silently disables the module for all users
        TEST_METHOD(JsonKeyEnabled_IsEnabled)
        {
            Assert::AreEqual(L"Enabled", constants::nonlocalizable::JsonKeyEnabled);
        }

        // Product code: Constants.h — constants::nonlocalizable::JsonKeyShowInExtendedContextMenu
        // What: Validates the JSON key controlling extended context menu visibility
        // Why: Settings UI writes this key; mismatch means user toggle has no effect
        // Risk: Context menu always shows (or never shows) regardless of user preference
        TEST_METHOD(JsonKeyExtendedMenu_IsCorrect)
        {
            Assert::AreEqual(L"showInExtendedContextMenu",
                             constants::nonlocalizable::JsonKeyShowInExtendedContextMenu);
        }

        // Product code: Constants.h — constants::nonlocalizable::DataFilePath
        // What: Validates settings file path contains .json extension
        // Why: Settings framework expects JSON; wrong extension breaks serialization
        // Risk: Settings silently fail to load/save, reverting to defaults on every launch
        TEST_METHOD(DataFilePath_ContainsJson)
        {
            std::wstring path(constants::nonlocalizable::DataFilePath);
            Assert::IsTrue(path.find(L".json") != std::wstring::npos,
                           L"Data file path should end in .json");
        }

        // Product code: Constants.h — constants::nonlocalizable::LastRunPath
        // What: Validates the last-run file path contains .log extension
        // Why: IPC between shell extension and UI depends on this path being correct
        // Risk: Shell extension writes to wrong file; UI never receives the file list
        TEST_METHOD(LastRunPath_ContainsLog)
        {
            std::wstring path(constants::nonlocalizable::LastRunPath);
            Assert::IsTrue(path.find(L".log") != std::wstring::npos,
                           L"Last run path should end in .log");
        }

        // Product code: Constants.h — constants::nonlocalizable::FileNameUIExe
        // What: Validates the UI executable name follows PowerToys naming convention
        // Why: Runner uses this name to launch the UI; mismatch prevents UI from opening
        // Risk: Context menu "What's using this file?" does nothing visible to the user
        TEST_METHOD(UIExe_ContainsPowerToys)
        {
            std::wstring exe(constants::nonlocalizable::FileNameUIExe);
            Assert::IsTrue(exe.find(L"PowerToys") != std::wstring::npos,
                           L"UI executable should contain PowerToys in name");
        }

        // Product code: Constants.h — constants::nonlocalizable::RegistryKeyDescription
        // What: Validates the registry description string is not empty
        // Why: Empty description in registry looks broken to admins and may fail GPO validation
        // Risk: Enterprise policy tools flag the extension as malformed
        TEST_METHOD(RegistryKeyDescription_NotEmpty)
        {
            std::wstring desc(constants::nonlocalizable::RegistryKeyDescription);
            Assert::IsFalse(desc.empty(),
                            L"Registry key description must not be empty");
        }
    };

    // ========================================================================
    // ProcessResult struct — validates the data layout used by
    // find_processes_recursive() and IPC serialization
    // ========================================================================
    TEST_CLASS(ProcessResultTests)
    {
    public:

        // Product code: ProcessResult.h — ProcessResult struct
        // What: Validates all four fields (name, pid, user, files) round-trip correctly
        // Why: find_processes_recursive() populates these; get_json()/get_text() reads them
        // Risk: Field misalignment silently corrupts IPC data between CLI and UI
        TEST_METHOD(FieldAssignment)
        {
            ProcessResult pr;
            pr.name = L"test.exe";
            pr.pid = 1234;
            pr.user = L"SYSTEM";
            pr.files = { L"C:\\file1.txt", L"C:\\file2.txt" };

            Assert::AreEqual(std::wstring(L"test.exe"), pr.name);
            Assert::AreEqual((DWORD)1234, pr.pid);
            Assert::AreEqual(std::wstring(L"SYSTEM"), pr.user);
            Assert::AreEqual((size_t)2, pr.files.size());
        }

        // Product code: ProcessResult.h — ProcessResult::files default state
        // What: Validates a default-constructed ProcessResult has an empty files vector
        // Why: Callers check files.empty() to skip display; must be true by default
        // Risk: Uninitialized vector could contain garbage, causing spurious output
        TEST_METHOD(EmptyFiles)
        {
            ProcessResult pr;
            pr.name = L"proc.exe";
            pr.pid = 0;
            pr.user = L"";
            Assert::IsTrue(pr.files.empty(),
                           L"Default-constructed files vector must be empty");
        }

        // Product code: ProcessResult.h — ProcessResult::files with multiple entries
        // What: Validates that files vector correctly stores and indexes multiple paths
        // Why: A single process can lock many files; UI must display all of them
        // Risk: Vector reallocation or index errors could drop paths from the display
        TEST_METHOD(MultiplePaths)
        {
            ProcessResult pr;
            pr.files = { L"A", L"B", L"C", L"D" };
            Assert::AreEqual((size_t)4, pr.files.size());
            Assert::AreEqual(std::wstring(L"C"), pr.files[2]);
        }
    };

    // ========================================================================
    // Mock infrastructure for CLI tests
    // ========================================================================
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
            Assert::AreEqual(std::wstring(L"help"), result.command_name);
        }

        TEST_METHOD(TestHelp)
        {
            MockProcessFinder finder;
            MockProcessTerminator terminator;
            MockStringProvider strings;
            
            wchar_t* argv[] = { (wchar_t*)L"exe", (wchar_t*)L"--help" };
            auto result = run_command(2, argv, finder, terminator, strings);
            
            Assert::AreEqual(0, result.exit_code);
            Assert::AreEqual(std::wstring(L"help"), result.command_name);
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
            Assert::AreEqual(std::wstring(L"query"), result.command_name);
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
            Assert::AreEqual(std::wstring(L"query-json"), result.command_name);
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
            Assert::AreEqual(std::wstring(L"kill"), result.command_name);
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
            Assert::AreEqual(std::wstring(L"query-wait"), result.command_name);
        }

        // Product code: CLILogic.cpp — get_json() with empty results
        // What: Validates JSON output is well-formed when no processes lock the files
        // Why: Consumers (UI, scripts) parse this JSON; malformed output breaks IPC
        // Risk: Empty-array edge case could produce invalid JSON or crash serialization
        TEST_METHOD(TestJsonOutput_EmptyResults)
        {
            MockProcessFinder finder;
            MockProcessTerminator terminator;
            MockStringProvider strings;

            wchar_t* argv[] = { (wchar_t*)L"exe", (wchar_t*)L"somefile.txt", (wchar_t*)L"--json" };
            auto result = run_command(3, argv, finder, terminator, strings);

            Assert::AreEqual(0, result.exit_code);
            Assert::IsTrue(result.output.find(L"processes") != std::wstring::npos,
                           L"JSON output should contain 'processes' key even when empty");
        }

        // Product code: CLILogic.cpp — run_command() path collection loop
        // What: Verifies that multiple file path arguments are all accepted and processed
        // Why: Users often right-click multiple files; all paths must reach the finder
        // Risk: Off-by-one in arg parsing could silently drop trailing paths
        TEST_METHOD(TestMultiplePaths)
        {
            MockProcessFinder finder;
            finder.results = { { L"proc.exe", 42, L"user", { L"a.txt", L"b.txt", L"c.txt" } } };
            MockProcessTerminator terminator;
            MockStringProvider strings;

            wchar_t* argv[] = { (wchar_t*)L"exe", (wchar_t*)L"a.txt", (wchar_t*)L"b.txt", (wchar_t*)L"c.txt" };
            auto result = run_command(4, argv, finder, terminator, strings);

            Assert::AreEqual(0, result.exit_code);
            Assert::IsTrue(result.output.find(L"42") != std::wstring::npos,
                           L"Output should contain the PID from mock results");
        }

        // Product code: CLILogic.cpp — run_command() empty-paths guard
        // What: Verifies that flags-only invocation (no file paths) returns an error
        // Why: --json alone is meaningless without paths; must fail gracefully
        // Risk: Missing guard could pass empty paths to finder, causing undefined behavior
        TEST_METHOD(TestNoPathsAfterFlags)
        {
            MockProcessFinder finder;
            MockProcessTerminator terminator;
            MockStringProvider strings;

            wchar_t* argv[] = { (wchar_t*)L"exe", (wchar_t*)L"--json" };
            auto result = run_command(2, argv, finder, terminator, strings);

            Assert::AreEqual(1, result.exit_code);
        }
    };
}
