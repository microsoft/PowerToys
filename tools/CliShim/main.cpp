// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <wil/resource.h>
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

    // Shim failures use codes outside the range the target CLIs use for themselves (see
    // doc/devdocs/cli-conventions.md: 0 success, 1 general error, 2 invalid arguments) so that a
    // caller can tell "the shim could not run the CLI" apart from "the CLI ran and failed".
    constexpr int ExitTargetNotFound = 9010;
    constexpr int ExitLaunchFailed = 9011;

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

    // The shim is the only handle a caller holds on the CLI, so killing the shim - taskkill without
    // /T, Process.Kill() without entireProcessTree, a script's own timeout, stopping the debugger -
    // must not leave the CLI running: PowerToys.FileLocksmith.CLI --wait polls until interrupted and
    // prints nothing while it does. KILL_ON_JOB_CLOSE takes the CLI down with the shim, because the
    // kernel closes the job handle however the shim dies.
    //
    // SILENT_BREAKAWAY_OK keeps the CLI's own children out of the job: PowerToys.FancyZones.CLI
    // open-settings starts a long-lived PowerToys.exe and returns, and without this flag that window
    // would be killed the moment the shim exits. Plain BREAKAWAY_OK cannot substitute - it requires
    // the creator to pass CREATE_BREAKAWAY_FROM_JOB, which Process.Start cannot express. This is the
    // one deliberate difference from src\runner\quick_access_host.cpp.
    wil::unique_handle CreateShimJob()
    {
        wil::unique_handle job{ CreateJobObjectW(nullptr, nullptr) };
        if (!job)
        {
            return {};
        }

        JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits{};
        limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK;
        if (!SetInformationJobObject(job.get(), JobObjectExtendedLimitInformation, &limits, sizeof(limits)))
        {
            // A job without the kill limit buys nothing, so drop it rather than assign to it.
            return {};
        }

        return job;
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
        return ExitTargetNotFound;
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
    wil::unique_process_information processInfo;

    // Best effort, and silent on failure: an unprotected CLI beats a CLI that will not start, and
    // this process's stderr belongs to the CLI's caller.
    const wil::unique_handle shimJob = CreateShimJob();

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

    if (shimJob)
    {
        AssignProcessToJobObject(shimJob.get(), processInfo.hProcess);
    }

    WaitForSingleObject(processInfo.hProcess, INFINITE);

    DWORD exitCode = static_cast<DWORD>(ExitLaunchFailed);
    GetExitCodeProcess(processInfo.hProcess, &exitCode);

    return static_cast<int>(exitCode);
}
