#include "pch.h"
#include "CLILogic.h"
#include "FileLocksmithLib/FileLocksmith.h"
#include <iostream>
#include "resource.h"
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>

struct RealProcessFinder : IProcessFinder
{
    std::vector<ProcessResult> find(const std::vector<std::wstring>& paths) override
    {
        return find_processes_recursive(paths);
    }
};

struct RealProcessTerminator : IProcessTerminator
{
    bool terminate(DWORD pid) override
    {
        HANDLE hProcess = OpenProcess(PROCESS_TERMINATE, FALSE, pid);
        if (hProcess)
        {
            bool result = TerminateProcess(hProcess, 0);
            CloseHandle(hProcess);
            return result;
        }
        return false;
    }
};

struct RealStringProvider : IStringProvider
{
    std::wstring GetString(UINT id) override
    {
        wchar_t buffer[4096];
        int len = LoadStringW(GetModuleHandle(NULL), id, buffer, ARRAYSIZE(buffer));
        if (len > 0)
        {
            return std::wstring(buffer, len);
        }
        return L"";
    }
};

#ifndef UNIT_TEST
int wmain(int argc, wchar_t* argv[])
{
    winrt::init_apartment();
    LoggerHelpers::init_logger(L"FileLocksmith", L"", LogSettings::fileLocksmithLoggerName);
    Logger::info("FileLocksmithCLI started");

    RealProcessFinder finder;
    RealProcessTerminator terminator;
    RealStringProvider strings;

    auto result = run_command(argc, argv, finder, terminator, strings);

    if (result.exit_code != 0)
    {
        Logger::error("Command failed with exit code {}", result.exit_code);
    }
    else
    {
        Logger::info("Command succeeded");
    }

    std::wcout << result.output;
    return result.exit_code;
}
#endif
