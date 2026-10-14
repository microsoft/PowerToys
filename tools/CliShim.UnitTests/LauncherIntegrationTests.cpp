// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <TlHelp32.h>

#include <filesystem>
#include <fstream>
#include <iterator>
#include <optional>
#include <string>
#include <string_view>
#include <system_error>
#include <utility>

#include <CppUnitTest.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    constexpr DWORD ExitCommandNotMapped = 9009;
    constexpr DWORD ExitTargetNotFound = 9010;
    constexpr DWORD ForwardedExitCode = 37;

    // How often the process-tree helpers re-scan while waiting for a process to appear.
    constexpr DWORD ProcessPollIntervalMilliseconds = 50;

    // How many times a temporary directory removal is retried before it is given up on.
    constexpr int CleanupAttempts = 20;

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

        UniqueHandle(UniqueHandle&& other) noexcept :
            handle{ std::exchange(other.handle, nullptr) }
        {
        }

        UniqueHandle& operator=(UniqueHandle&& other) noexcept
        {
            if (this != &other)
            {
                Reset();
                handle = std::exchange(other.handle, nullptr);
            }

            return *this;
        }

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

    std::filesystem::path GetSystemDirectoryPath()
    {
        wchar_t systemDirectory[MAX_PATH]{};
        const UINT length = GetSystemDirectoryW(systemDirectory, ARRAYSIZE(systemDirectory));

        Assert::IsTrue(length > 0 && length < ARRAYSIZE(systemDirectory), L"Could not locate the system directory.");
        return systemDirectory;
    }

    std::filesystem::path GetSystemCommandInterpreter()
    {
        return GetSystemDirectoryPath() / L"cmd.exe";
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
            // A process that has just been terminated can still have its image mapped for a moment,
            // which keeps the copied executable open, so give the removal a few attempts.
            for (int attempt = 0; attempt < CleanupAttempts; ++attempt)
            {
                std::error_code error;
                std::filesystem::remove_all(path, error);
                if (!error)
                {
                    return;
                }

                Sleep(ProcessPollIntervalMilliseconds);
            }
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

    UniqueHandle OpenNul(const DWORD access)
    {
        SECURITY_ATTRIBUTES attributes{};
        attributes.nLength = sizeof(attributes);
        attributes.bInheritHandle = TRUE;

        UniqueHandle handle{ CreateFileW(
            L"NUL",
            access,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            &attributes,
            OPEN_EXISTING,
            0,
            nullptr) };
        Assert::IsTrue(handle.Get() != INVALID_HANDLE_VALUE, L"Could not open NUL.");

        return handle;
    }

    // Toolhelp is the whole story for observing the shim's process tree. A child has to be
    // selected by image name rather than by "the first child", because the shim is also given a
    // console host child; and a process that outlived its parent has to be found by image name
    // alone, because by then its parent process id refers to a process that no longer exists.
    UniqueHandle OpenProcessByImageName(
        const std::wstring& imageName,
        const std::optional<DWORD> parentProcessId,
        const DWORD access)
    {
        UniqueHandle snapshot{ CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) };
        if (snapshot.Get() == INVALID_HANDLE_VALUE)
        {
            return {};
        }

        PROCESSENTRY32W entry{};
        entry.dwSize = sizeof(entry);

        for (BOOL found = Process32FirstW(snapshot.Get(), &entry); found; found = Process32NextW(snapshot.Get(), &entry))
        {
            if (parentProcessId.has_value() && entry.th32ParentProcessID != *parentProcessId)
            {
                continue;
            }

            if (CompareStringOrdinal(entry.szExeFile, -1, imageName.c_str(), -1, TRUE) != CSTR_EQUAL)
            {
                continue;
            }

            UniqueHandle process{ OpenProcess(access, FALSE, entry.th32ProcessID) };
            if (process.Get() != nullptr)
            {
                return process;
            }
        }

        return {};
    }

    UniqueHandle WaitForProcessByImageName(
        const std::wstring& imageName,
        const std::optional<DWORD> parentProcessId,
        const DWORD access,
        const DWORD timeoutMilliseconds)
    {
        for (DWORD elapsed = 0;; elapsed += ProcessPollIntervalMilliseconds)
        {
            UniqueHandle process = OpenProcessByImageName(imageName, parentProcessId, access);
            if (process.Get() != nullptr || elapsed >= timeoutMilliseconds)
            {
                return process;
            }

            Sleep(ProcessPollIntervalMilliseconds);
        }
    }

    struct LaunchedProcess
    {
        UniqueHandle process;
        DWORD processId = 0;
    };

    // CppUnitTest assertions throw, so anything the job-object tests start has to be torn down by
    // a destructor rather than by a line at the end of the test: both tests deliberately start a
    // process that never exits on its own, and a failing assertion would otherwise leave it
    // running - and its executable locked - for as long as the agent lives.
    class ProcessKiller
    {
    public:
        explicit ProcessKiller(UniqueHandle process) noexcept :
            handle{ std::move(process) }
        {
        }

        ProcessKiller(const ProcessKiller&) = delete;
        ProcessKiller& operator=(const ProcessKiller&) = delete;

        ~ProcessKiller()
        {
            if (handle.Get() != nullptr)
            {
                TerminateProcess(handle.Get(), 0);
                WaitForSingleObject(handle.Get(), 5'000);
            }
        }

        HANDLE Get() const noexcept
        {
            return handle.Get();
        }

    private:
        UniqueHandle handle;
    };

    // Starts the executable without waiting for it. When either standard handle is supplied the
    // child is started with inherited handles so the shim's own redirection behaviour is
    // exercised; the handles left null keep whatever the test host is using.
    LaunchedProcess StartProcess(
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

        const bool redirect = standardInput != nullptr || standardOutput != nullptr;

        STARTUPINFOW startupInfo{};
        startupInfo.cb = sizeof(startupInfo);
        if (redirect)
        {
            startupInfo.dwFlags = STARTF_USESTDHANDLES;
            startupInfo.hStdInput = standardInput != nullptr ? standardInput : GetStdHandle(STD_INPUT_HANDLE);
            startupInfo.hStdOutput = standardOutput != nullptr ? standardOutput : GetStdHandle(STD_OUTPUT_HANDLE);
            startupInfo.hStdError = standardOutput != nullptr ? standardOutput : GetStdHandle(STD_ERROR_HANDLE);
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

        CloseHandle(processInfo.hThread);
        return { UniqueHandle{ processInfo.hProcess }, processInfo.dwProcessId };
    }

    // Runs the executable to completion and returns its exit code.
    DWORD RunProcess(
        const std::filesystem::path& executable,
        const std::wstring& arguments,
        HANDLE standardInput,
        HANDLE standardOutput)
    {
        const LaunchedProcess launched = StartProcess(executable, arguments, standardInput, standardOutput);

        const DWORD waitResult = WaitForSingleObject(launched.process.Get(), 30'000);
        if (waitResult != WAIT_OBJECT_0)
        {
            TerminateProcess(launched.process.Get(), TimeoutTerminationExitCode);
            WaitForSingleObject(launched.process.Get(), 5'000);
        }

        DWORD exitCode = 0;
        const BOOL gotExitCode = GetExitCodeProcess(launched.process.Get(), &exitCode);

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

        const UniqueHandle input = OpenNul(GENERIC_READ);

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

        // The shim is the only handle a caller holds on the CLI, so a single-process kill of the
        // shim must not leave the CLI running - PowerToys.FileLocksmith.CLI --wait polls forever
        // and prints nothing, which would make the orphan invisible and permanent.
        TEST_METHOD(TerminatingTheShimTerminatesTheTargetCli)
        {
            TemporaryDirectory installation;
            const std::filesystem::path shimPath = installation.GetPath() / L"bin" / L"PowerToys.FancyZones.CLI.exe";

            CopyExecutable(GetShimUnderTest(), shimPath);
            CopyExecutable(GetSystemCommandInterpreter(), installation.GetPath() / L"FancyZonesCLI.exe");

            // PAUSE blocks inside the target itself, on a pipe that is never written, so the
            // target needs no console and starts no worker that could outlive the test.
            SECURITY_ATTRIBUTES attributes{};
            attributes.nLength = sizeof(attributes);
            attributes.bInheritHandle = TRUE;

            HANDLE readEnd = nullptr;
            HANDLE writeEnd = nullptr;
            Assert::IsTrue(CreatePipe(&readEnd, &writeEnd, &attributes, 0) != FALSE, L"Could not create the standard input pipe.");

            const UniqueHandle standardInput{ readEnd };
            const UniqueHandle keepPipeOpen{ writeEnd };
            const UniqueHandle standardOutput = OpenNul(GENERIC_WRITE);

            LaunchedProcess launchedShim = StartProcess(shimPath, L"/d /c pause", standardInput.Get(), standardOutput.Get());
            const DWORD shimProcessId = launchedShim.processId;
            const ProcessKiller shim{ std::move(launchedShim.process) };

            const ProcessKiller target{ WaitForProcessByImageName(
                L"FancyZonesCLI.exe",
                shimProcessId,
                SYNCHRONIZE | PROCESS_TERMINATE,
                10'000) };
            Assert::IsTrue(target.Get() != nullptr, L"The shim did not start the target CLI.");
            Assert::AreEqual(
                static_cast<DWORD>(WAIT_TIMEOUT),
                WaitForSingleObject(target.Get(), 200),
                L"The target CLI exited before the shim was terminated.");

            // taskkill without /T, Process.Kill() without entireProcessTree, a script's own
            // timeout, stopping the debugger: all of these reach the shim and nothing else.
            Assert::IsTrue(TerminateProcess(shim.Get(), 1) != FALSE, L"Could not terminate the shim.");

            Assert::AreEqual(
                static_cast<DWORD>(WAIT_OBJECT_0),
                WaitForSingleObject(target.Get(), 10'000),
                L"The target CLI outlived the shim.");

            WaitForSingleObject(shim.Get(), 5'000);
        }

        // The mirror of the test above: JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK keeps the CLI's own
        // children out of the job, so PowerToys.FancyZones.CLI open-settings can start a
        // long-lived PowerToys.exe and return without that window dying with the shim.
        TEST_METHOD(ProcessesStartedByTheTargetCliSurviveTheShim)
        {
            TemporaryDirectory installation;
            const std::filesystem::path shimPath = installation.GetPath() / L"bin" / L"PowerToys.FancyZones.CLI.exe";

            // Once the CLI that started it has exited, the survivor's parent process id refers to
            // a process that no longer exists, so it can only be found by image name. Deriving
            // that name from the temporary directory keeps concurrent test runs from colliding.
            const std::wstring survivorName = installation.GetPath().stem().wstring() + L"-survivor.exe";
            const std::filesystem::path survivorPath = installation.GetPath() / survivorName;

            CopyExecutable(GetShimUnderTest(), shimPath);
            CopyExecutable(GetSystemCommandInterpreter(), installation.GetPath() / L"FancyZonesCLI.exe");
            CopyExecutable(GetSystemDirectoryPath() / L"PING.EXE", survivorPath);

            // START returns as soon as the survivor is running, so the CLI - and with it the shim -
            // exits while the survivor is still alive.
            const std::wstring arguments = LR"(/d /c start "" /b ")" + survivorPath.wstring() + LR"(" -n 30 127.0.0.1 > nul)";
            Assert::AreEqual(static_cast<DWORD>(0), RunAndGetExitCode(shimPath, arguments), L"The target CLI did not succeed.");

            const ProcessKiller survivor{ WaitForProcessByImageName(
                survivorName,
                std::nullopt,
                SYNCHRONIZE | PROCESS_TERMINATE,
                10'000) };
            Assert::IsTrue(survivor.Get() != nullptr, L"The process started by the target CLI was killed with the shim.");

            Assert::AreEqual(
                static_cast<DWORD>(WAIT_TIMEOUT),
                WaitForSingleObject(survivor.Get(), 1'000),
                L"The process started by the target CLI did not outlive the shim.");
        }
    };
}
