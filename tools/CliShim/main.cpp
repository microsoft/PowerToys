// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <cstdio>
#include <filesystem>
#include <string>

#include "CommandLine.h"

namespace
{
    // The exit code cmd.exe returns for "command not found". Used only when the shim is
    // invoked under a name that is not in the table below (for example a renamed copy).
    constexpr int ExitCommandNotMapped = 9009;

    // A distinct code for "the command is known, but its target could not be launched"
    // (missing target, CreateProcess failure). Keeping this separate from 9009 lets a
    // calling script tell a mistyped or unmapped command from a broken install.
    constexpr int ExitLaunchFailed = 1;

    struct ShimTarget
    {
        const wchar_t* name;
        const wchar_t* target;
    };

    // Generated from CliShimManifest.props. Paths are relative to the shim directory; the
    // shims live in "<install>\cli\", so targets are one level up or under WinUI3Apps.
    constexpr ShimTarget ShimTargets[] = {
#include "CliShimTargets.g.inc"
    };

    // Stay alive on Ctrl+C / Ctrl+Break so we can still capture the child's exit code; the
    // child shares the console group and receives the signal directly. Returning TRUE tells
    // the system we handled it, so this process is not terminated.
    BOOL WINAPI ConsoleCtrlHandler(DWORD /*controlType*/)
    {
        return TRUE;
    }

    // Returns the full path of this executable, growing the buffer to accommodate long
    // paths. Returns an empty string on failure.
    std::wstring GetOwnModulePath()
    {
        std::wstring buffer(MAX_PATH, L'\0');
        for (;;)
        {
            const DWORD copied = GetModuleFileNameW(nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));
            if (copied == 0)
            {
                return {};
            }

            if (copied < buffer.size())
            {
                buffer.resize(copied);
                return buffer;
            }

            buffer.resize(buffer.size() * 2); // ERROR_INSUFFICIENT_BUFFER: grow and retry.
        }
    }

    const wchar_t* ResolveTarget(const std::wstring& commandName)
    {
        for (const ShimTarget& entry : ShimTargets)
        {
            if (CompareStringOrdinal(commandName.c_str(), -1, entry.name, -1, TRUE) == CSTR_EQUAL)
            {
                return entry.target;
            }
        }

        return nullptr;
    }
}

int wmain()
{
    SetConsoleCtrlHandler(ConsoleCtrlHandler, TRUE);

    const std::wstring modulePath = GetOwnModulePath();
    if (modulePath.empty())
    {
        std::fwprintf(stderr, L"cli-shim: could not determine the shim's own path.\n");
        return ExitLaunchFailed;
    }

    const std::filesystem::path selfPath{ modulePath };
    const std::wstring commandName = selfPath.stem().wstring();

    const wchar_t* relativeTarget = ResolveTarget(commandName);
    if (relativeTarget == nullptr)
    {
        std::fwprintf(stderr, L"cli-shim: no PowerToys CLI is mapped to the command '%s'.\n", commandName.c_str());
        return ExitCommandNotMapped;
    }

    const std::filesystem::path targetPath = (selfPath.parent_path() / relativeTarget).lexically_normal();

    std::error_code existsError;
    if (!std::filesystem::exists(targetPath, existsError))
    {
        std::fwprintf(stderr, L"cli-shim: target not found: \"%s\".\n", targetPath.c_str());
        return ExitLaunchFailed;
    }

    // Forward the user's arguments byte-for-byte. GetCommandLineW is the raw command line
    // (the Win32 equivalent of the managed Environment.CommandLine); stripping argv[0]
    // preserves the user's exact quoting, which re-quoting parsed args would corrupt.
    const std::wstring forwardedArguments = CommandLine::StripArgumentZero(GetCommandLineW());

    // Rebuild a command line whose argv[0] is the resolved (quoted) target followed by the
    // forwarded tail. lpApplicationName below still points at the target, so this argv[0]
    // is purely cosmetic for the child.
    std::wstring commandLine = L'"' + targetPath.wstring() + L'"';
    if (!forwardedArguments.empty())
    {
        commandLine.push_back(L' ');
        commandLine.append(forwardedArguments);
    }

    STARTUPINFOW startupInfo{};
    startupInfo.cb = sizeof(startupInfo);
    PROCESS_INFORMATION processInfo{};

    if (!CreateProcessW(
            targetPath.c_str(),
            commandLine.data(), // Requires a mutable buffer; CreateProcessW may write to it.
            nullptr,
            nullptr,
            TRUE, // Inherit handles: share stdin/stdout/stderr and stay in this console.
            0,
            nullptr,
            nullptr,
            &startupInfo,
            &processInfo))
    {
        std::fwprintf(stderr, L"cli-shim: failed to launch \"%s\" (error %lu).\n", targetPath.c_str(), GetLastError());
        return ExitLaunchFailed;
    }

    WaitForSingleObject(processInfo.hProcess, INFINITE);

    DWORD exitCode = static_cast<DWORD>(ExitLaunchFailed);
    GetExitCodeProcess(processInfo.hProcess, &exitCode);

    CloseHandle(processInfo.hProcess);
    CloseHandle(processInfo.hThread);

    return static_cast<int>(exitCode);
}
