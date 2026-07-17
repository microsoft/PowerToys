// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <wil/stl.h>
#include <wil/win32_helpers.h>

#include <cstdio>
#include <filesystem>
#include <string>

#include "CommandLine.h"

namespace
{
    // Match cmd.exe's "command not found" exit code for unmapped shim names.
    constexpr int ExitCommandNotMapped = 9009;

    // Distinguish mapped commands whose targets cannot be launched.
    constexpr int ExitLaunchFailed = 1;

    struct ShimTarget
    {
        const wchar_t* name;
        const wchar_t* target;
    };

    // Generated from CliShimManifest.props.
    constexpr ShimTarget ShimTargets[] = {
#include "CliShimTargets.g.inc"
    };

    // The child receives Ctrl+C/Break; keep the shim alive to return its exit code.
    BOOL WINAPI ConsoleCtrlHandler(DWORD /*controlType*/)
    {
        return TRUE;
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

    std::wstring modulePath;
    if (FAILED(wil::GetModuleFileNameW(nullptr, modulePath)))
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

    // Forward the raw tail so the caller's argument quoting remains unchanged.
    const std::wstring forwardedArguments = CommandLine::StripArgumentZero(GetCommandLineW());

    // lpApplicationName selects the target; argv[0] in the command line is cosmetic.
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
