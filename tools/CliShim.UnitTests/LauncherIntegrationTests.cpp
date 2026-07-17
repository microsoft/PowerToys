// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <filesystem>
#include <string>
#include <system_error>

#include <CppUnitTest.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    constexpr DWORD ExitCommandNotMapped = 9009;
    constexpr DWORD ExitLaunchFailed = 1;
    constexpr DWORD ForwardedExitCode = 37;

    struct ShimMapping
    {
        const wchar_t* command;
        const wchar_t* relativeTarget;
    };

    // Keep this list in sync with CliShimManifest.props.
    constexpr ShimMapping ExpectedMappings[] = {
        { L"PowerToys.FancyZones.CLI", L"..\\FancyZonesCLI.exe" },
        { L"PowerToys.ImageResizer.CLI", L"..\\WinUI3Apps\\PowerToys.ImageResizerCLI.exe" },
        { L"PowerToys.FileLocksmith.CLI", L"..\\FileLocksmithCLI.exe" },
        { L"PowerToys.PowerDisplay.CLI", L"..\\WinUI3Apps\\PowerToys.PowerDisplay.Cli.exe" },
    };

    constexpr const wchar_t* RejectedLegacyCommands[] = {
        L"fancyzones",
        L"imageresizer",
        L"filelocksmith",
        L"powerdisplay",
        L"fancyzonescli",
        L"imageresizercli",
        L"filelocksmithcli",
        L"powerdisplaycli",
    };

    std::filesystem::path GetTestBinaryDirectory()
    {
        wchar_t modulePath[MAX_PATH]{};
        const DWORD length = GetModuleFileNameW(
            reinterpret_cast<HMODULE>(&__ImageBase),
            modulePath,
            ARRAYSIZE(modulePath));

        Assert::IsTrue(length > 0 && length < ARRAYSIZE(modulePath), L"Could not locate the test module.");
        return std::filesystem::path{ modulePath }.parent_path();
    }

    std::filesystem::path GetSystemCommandInterpreter()
    {
        wchar_t systemDirectory[MAX_PATH]{};
        const UINT length = GetSystemDirectoryW(systemDirectory, ARRAYSIZE(systemDirectory));

        Assert::IsTrue(length > 0 && length < ARRAYSIZE(systemDirectory), L"Could not locate the system directory.");
        return std::filesystem::path{ systemDirectory } / L"cmd.exe";
    }

    std::filesystem::path CreateTemporaryDirectory()
    {
        wchar_t temporaryRoot[MAX_PATH]{};
        const DWORD rootLength = GetTempPathW(ARRAYSIZE(temporaryRoot), temporaryRoot);
        Assert::IsTrue(rootLength > 0 && rootLength < ARRAYSIZE(temporaryRoot), L"Could not locate the temporary directory.");

        wchar_t temporaryFile[MAX_PATH]{};
        Assert::IsTrue(
            GetTempFileNameW(temporaryRoot, L"PTS", 0, temporaryFile) != 0,
            L"Could not reserve a temporary path.");
        Assert::IsTrue(DeleteFileW(temporaryFile), L"Could not remove the temporary placeholder file.");
        Assert::IsTrue(CreateDirectoryW(temporaryFile, nullptr), L"Could not create the temporary directory.");

        return temporaryFile;
    }

    class TemporaryDirectory
    {
    public:
        TemporaryDirectory() :
            path{ CreateTemporaryDirectory() }
        {
        }

        ~TemporaryDirectory()
        {
            std::error_code error;
            std::filesystem::remove_all(path, error);
        }

        const std::filesystem::path& GetPath() const noexcept
        {
            return path;
        }

    private:
        std::filesystem::path path;
    };

    void CopyExecutable(const std::filesystem::path& source, const std::filesystem::path& destination)
    {
        std::error_code error;
        std::filesystem::create_directories(destination.parent_path(), error);
        Assert::AreEqual(0, error.value(), L"Could not create the destination directory.");

        std::filesystem::copy_file(
            source,
            destination,
            std::filesystem::copy_options::overwrite_existing,
            error);
        Assert::AreEqual(0, error.value(), L"Could not copy the executable.");
    }

    DWORD RunAndGetExitCode(const std::filesystem::path& executable, const std::wstring& arguments = {})
    {
        std::wstring commandLine = L"\"" + executable.wstring() + L"\"";
        if (!arguments.empty())
        {
            commandLine.push_back(L' ');
            commandLine.append(arguments);
        }

        STARTUPINFOW startupInfo{};
        startupInfo.cb = sizeof(startupInfo);
        PROCESS_INFORMATION processInfo{};

        if (!CreateProcessW(
                executable.c_str(),
                commandLine.data(),
                nullptr,
                nullptr,
                FALSE,
                CREATE_NO_WINDOW,
                nullptr,
                nullptr,
                &startupInfo,
                &processInfo))
        {
            const std::wstring message = L"CreateProcessW failed with error " + std::to_wstring(GetLastError()) + L".";
            Assert::Fail(message.c_str());
        }

        const DWORD waitResult = WaitForSingleObject(processInfo.hProcess, 30'000);
        if (waitResult != WAIT_OBJECT_0)
        {
            TerminateProcess(processInfo.hProcess, ExitLaunchFailed);
            WaitForSingleObject(processInfo.hProcess, 5'000);
        }

        DWORD exitCode = 0;
        const BOOL gotExitCode = GetExitCodeProcess(processInfo.hProcess, &exitCode);

        CloseHandle(processInfo.hProcess);
        CloseHandle(processInfo.hThread);

        Assert::AreEqual(static_cast<DWORD>(WAIT_OBJECT_0), waitResult, L"The shim process did not exit.");
        Assert::IsTrue(gotExitCode, L"Could not read the shim process exit code.");
        return exitCode;
    }
}

namespace CliShimUnitTests
{
    TEST_CLASS(LauncherIntegrationTests)
    {
    public:
        TEST_METHOD(AllMappedCommandsLaunchExpectedRelativeTargets)
        {
            TemporaryDirectory installation;
            const std::filesystem::path binDirectory = installation.GetPath() / L"bin";
            const std::filesystem::path shimSource = GetTestBinaryDirectory() / L"PowerToys.CliShim.exe";
            const std::filesystem::path targetSource = GetSystemCommandInterpreter();

            for (const ShimMapping& mapping : ExpectedMappings)
            {
                const std::filesystem::path shimPath = binDirectory / (std::wstring{ mapping.command } + L".exe");
                const std::filesystem::path targetPath = (binDirectory / mapping.relativeTarget).lexically_normal();

                CopyExecutable(shimSource, shimPath);
                CopyExecutable(targetSource, targetPath);

                const DWORD exitCode = RunAndGetExitCode(shimPath, L"/d /c exit 37");
                const std::wstring message = L"Command failed: " + std::wstring{ mapping.command };
                Assert::AreEqual(ForwardedExitCode, exitCode, message.c_str());
            }
        }

        TEST_METHOD(UnknownCommandReturnsCommandNotMapped)
        {
            TemporaryDirectory installation;
            const std::filesystem::path shimPath = installation.GetPath() / L"bin" / L"unknown.exe";

            CopyExecutable(GetTestBinaryDirectory() / L"PowerToys.CliShim.exe", shimPath);

            Assert::AreEqual(ExitCommandNotMapped, RunAndGetExitCode(shimPath));
        }

        TEST_METHOD(LegacyCommandsReturnCommandNotMapped)
        {
            TemporaryDirectory installation;
            const std::filesystem::path binDirectory = installation.GetPath() / L"bin";
            const std::filesystem::path shimSource = GetTestBinaryDirectory() / L"PowerToys.CliShim.exe";

            for (const wchar_t* command : RejectedLegacyCommands)
            {
                const std::filesystem::path shimPath = binDirectory / (std::wstring{ command } + L".exe");
                CopyExecutable(shimSource, shimPath);

                const std::wstring message = L"Legacy command was unexpectedly mapped: " + std::wstring{ command };
                Assert::AreEqual(ExitCommandNotMapped, RunAndGetExitCode(shimPath), message.c_str());
            }
        }

        TEST_METHOD(MissingTargetReturnsLaunchFailed)
        {
            TemporaryDirectory installation;
            const std::filesystem::path shimPath = installation.GetPath() / L"bin" / L"PowerToys.FancyZones.CLI.exe";

            CopyExecutable(GetTestBinaryDirectory() / L"PowerToys.CliShim.exe", shimPath);

            Assert::AreEqual(ExitLaunchFailed, RunAndGetExitCode(shimPath));
        }
    };
}
