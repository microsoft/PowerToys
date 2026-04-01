#pragma once
#include <vector>
#include <string>
#include "FileLocksmithLib/FileLocksmith.h"
#include <Windows.h>

struct CommandResult
{
    int exit_code;
    std::wstring output;
};

struct IProcessFinder
{
    virtual std::vector<ProcessResult> find(const std::vector<std::wstring>& paths) = 0;
    virtual ~IProcessFinder() = default;
};

struct IProcessTerminator
{
    virtual bool terminate(DWORD pid) = 0;
    virtual ~IProcessTerminator() = default;
};

struct IStringProvider
{
    virtual std::wstring GetString(UINT id) = 0;
    virtual ~IStringProvider() = default;
};

CommandResult run_command(int argc, wchar_t* argv[], IProcessFinder& finder, IProcessTerminator& terminator, IStringProvider& strings);
