// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>
#include <string_view>
#include <system_error>

#include <CppUnitTest.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    constexpr DWORD ExitCommandNotMapped = 9009;
    constexpr DWORD ExitTargetNotFound = 9010;
    constexpr DWORD ForwardedExitCode = 37;

    // Only used to unblock the test run if the shim ever hangs; the wait result is asserted.
    constexpr DWORD TimeoutTerminationExitCode = 0xFFFFFFFF;

    struct ShimMapping
    {
        const wchar_t* command;
        const wchar_t* relativeTarget;
    };

    // Generated from CliShimManifest.props, the same table the shim itself is built from, so a
    // newly mapped command is covered here automatically instead of by hand.
    constexpr ShimMapping ExpectedMappings[] = {
#include "CliShimTargets.g.inc"
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

    class UniqueHandle
    {
    public:
        UniqueHandle() = default;

        explicit UniqueHandle(HANDLE value) noexcept :
            handle{ value }
        {
        }

        UniqueHandle(const UniqueHandle&) = delete;
        UniqueHandle& operator=(const UniqueHandle&) = delete;

        ~UniqueHandle()
        {
            Reset();
        }

        void Reset() noexcept
        {
            if (handle != nullptr && handle != INVALID_HANDLE_VALUE)
            {
                CloseHandle(handle);
            }

            handle = nullptr;
        }

        HANDLE Get() const noexcept
        {
            return handle;
        }

    private:
        HANDLE handle = nullptr;
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

    std::filesystem::path GetShimUnderTest()
    {
        return GetTestBinaryDirectory() / L"PowerToys.CliShim.exe";
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

    // Runs the executable and returns its exit code. When standardOutput is set the child is
    // started with inherited handles so the shim's own redirection behaviour is exercised.
    DWORD RunProcess(
        const std::filesystem::path& executable,
        const std::wstring& arguments,
        HANDLE standardInput,
        HANDLE standardOutput)
    {
        std::wstring commandLine = L"\"" + executable.wstring() + L"\"";
        if (!arguments.empty())
        {
            commandLine.push_back(L' ');
            commandLine.append(arguments);
        }

        const bool redirect = standardOutput != nullptr;

        STARTUPINFOW startupInfo{};
        startupInfo.cb = sizeof(startupInfo);
        if (redirect)
        {
            startupInfo.dwFlags = STARTF_USESTDHANDLES;
            startupInfo.hStdInput = standardInput;
            startupInfo.hStdOutput = standardOutput;
            startupInfo.hStdError = standardOutput;
        }

        PROCESS_INFORMATION processInfo{};

        if (!CreateProcessW(
                executable.c_str(),
                commandLine.data(),
                nullptr,
                nullptr,
                redirect ? TRUE : FALSE,
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
            TerminateProcess(processInfo.hProcess, TimeoutTerminationExitCode);
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

    DWORD RunAndGetExitCode(const std::filesystem::path& executable, const std::wstring& arguments = {})
    {
        return RunProcess(executable, arguments, nullptr, nullptr);
    }

    std::string RunAndCaptureStandardOutput(
        const std::filesystem::path& executable,
        const std::wstring& arguments,
        const std::filesystem::path& capturePath,
        DWORD& exitCode)
    {
        SECURITY_ATTRIBUTES attributes{};
        attributes.nLength = sizeof(attributes);
        attributes.bInheritHandle = TRUE;

        UniqueHandle output{ CreateFileW(
            capturePath.c_str(),
            GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            &attributes,
            CREATE_ALWAYS,
            FILE_ATTRIBUTE_NORMAL,
            nullptr) };
        Assert::IsTrue(output.Get() != INVALID_HANDLE_VALUE, L"Could not create the output capture file.");

        UniqueHandle input{ CreateFileW(
            L"NUL",
            GENERIC_READ,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            &attributes,
            OPEN_EXISTING,
            0,
            nullptr) };
        Assert::IsTrue(input.Get() != INVALID_HANDLE_VALUE, L"Could not open NUL as the shim's standard input.");

        exitCode = RunProcess(executable, arguments, input.Get(), output.Get());
        output.Reset();

        std::ifstream stream{ capturePath, std::ios::binary };
        Assert::IsTrue(stream.is_open(), L"Could not read the captured output.");

        std::string captured{ std::istreambuf_iterator<char>{ stream }, std::istreambuf_iterator<char>{} };
        while (!captured.empty() && (captured.back() == '\r' || captured.back() == '\n'))
        {
            captured.pop_back();
        }

        return captured;
    }

    // The forwarded payload is deliberately ASCII-only so it round-trips through cmd.exe's ECHO.
    std::string NarrowAscii(std::wstring_view text)
    {
        std::string narrow;
        narrow.reserve(text.size());

        for (const wchar_t character : text)
        {
            narrow.push_back(static_cast<char>(character));
        }

        return narrow;
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
            const std::filesystem::path targetSource = GetSystemCommandInterpreter();

            for (const ShimMapping& mapping : ExpectedMappings)
            {
                const std::filesystem::path shimPath = binDirectory / (std::wstring{ mapping.command } + L".exe");
                const std::filesystem::path targetPath = (binDirectory / mapping.relativeTarget).lexically_normal();

                CopyExecutable(GetShimUnderTest(), shimPath);
                CopyExecutable(targetSource, targetPath);

                const DWORD exitCode = RunAndGetExitCode(shimPath, L"/d /c exit 37");
                const std::wstring message = L"Command failed: " + std::wstring{ mapping.command };
                Assert::AreEqual(ForwardedExitCode, exitCode, message.c_str());
            }
        }

        // cmd.exe's ECHO writes the remainder of its command line out verbatim, so this pins down
        // that the caller's quoting and spacing survive the hop through the shim unchanged.
        TEST_METHOD(ArgumentTailIsForwardedVerbatim)
        {
            constexpr const wchar_t* argumentTail = LR"(--path "C:\a b\c.png" --size 100 -q)";

            TemporaryDirectory installation;
            const std::filesystem::path shimPath = installation.GetPath() / L"bin" / L"PowerToys.FancyZones.CLI.exe";

            CopyExecutable(GetShimUnderTest(), shimPath);
            CopyExecutable(GetSystemCommandInterpreter(), installation.GetPath() / L"FancyZonesCLI.exe");

            DWORD exitCode = MAXDWORD;
            const std::string captured = RunAndCaptureStandardOutput(
                shimPath,
                std::wstring{ L"/d /c echo " } + argumentTail,
                installation.GetPath() / L"captured-output.txt",
                exitCode);

            Assert::AreEqual(static_cast<DWORD>(0), exitCode, L"The forwarded command did not succeed.");
            Assert::AreEqual(NarrowAscii(argumentTail), captured, L"The argument tail was not forwarded verbatim.");
        }

        TEST_METHOD(UnknownCommandReturnsCommandNotMapped)
        {
            TemporaryDirectory installation;
            const std::filesystem::path shimPath = installation.GetPath() / L"bin" / L"unknown.exe";

            CopyExecutable(GetShimUnderTest(), shimPath);

            Assert::AreEqual(ExitCommandNotMapped, RunAndGetExitCode(shimPath));
        }

        TEST_METHOD(LegacyCommandsReturnCommandNotMapped)
        {
            TemporaryDirectory installation;
            const std::filesystem::path binDirectory = installation.GetPath() / L"bin";

            for (const wchar_t* command : RejectedLegacyCommands)
            {
                const std::filesystem::path shimPath = binDirectory / (std::wstring{ command } + L".exe");
                CopyExecutable(GetShimUnderTest(), shimPath);

                const std::wstring message = L"Legacy command was unexpectedly mapped: " + std::wstring{ command };
                Assert::AreEqual(ExitCommandNotMapped, RunAndGetExitCode(shimPath), message.c_str());
            }
        }

        // A missing target must not be reported with an exit code the target CLI itself could
        // return, so callers can tell the two apart.
        TEST_METHOD(MissingTargetReturnsTargetNotFound)
        {
            TemporaryDirectory installation;
            const std::filesystem::path shimPath = installation.GetPath() / L"bin" / L"PowerToys.FancyZones.CLI.exe";

            CopyExecutable(GetShimUnderTest(), shimPath);

            Assert::AreEqual(ExitTargetNotFound, RunAndGetExitCode(shimPath));
        }
    };
}
