#include "pch.h"
#include "FileLocksmithLib/FileLocksmith.h"
#include <common/utils/json.h>
#include <iostream>
#include <sstream>

struct CommandResult
{
    int exit_code;
    std::wstring output;
};

std::wstring get_usage()
{
    std::wstringstream ss;
    ss << L"Usage: FileLocksmithCLI.exe [options] <path1> [path2] ...\n"
       << L"Options:\n"
       << L"  --kill      Kill processes locking the files\n"
       << L"  --json      Output results in JSON format\n"
       << L"  --wait      Wait for files to be unlocked\n"
       << L"  --help      Show this help message\n";
    return ss.str();
}

std::wstring get_json(const std::vector<ProcessResult>& results)
{
    json::JsonObject root;
    json::JsonArray processes;

    for (const auto& result : results)
    {
        json::JsonObject process;
        process.SetNamedValue(L"pid", json::JsonValue::CreateNumberValue(result.pid));
        process.SetNamedValue(L"name", json::JsonValue::CreateStringValue(result.name));
        process.SetNamedValue(L"user", json::JsonValue::CreateStringValue(result.user));
        
        json::JsonArray files;
        for (const auto& file : result.files)
        {
            files.Append(json::JsonValue::CreateStringValue(file));
        }
        process.SetNamedValue(L"files", files);
        
        processes.Append(process);
    }

    root.SetNamedValue(L"processes", processes);
    return root.Stringify();
}

std::wstring get_text(const std::vector<ProcessResult>& results)
{
    std::wstringstream ss;
    if (results.empty())
    {
        ss << L"No processes found locking the file(s)." << std::endl;
        return ss.str();
    }

    ss << L"PID\tUser\tProcess" << std::endl;
    for (const auto& result : results)
    {
        ss << result.pid << L"\t" 
           << result.user << L"\t" 
           << result.name << std::endl;
    }
    return ss.str();
}

std::wstring kill_processes(const std::vector<ProcessResult>& results)
{
    std::wstringstream ss;
    for (const auto& result : results)
    {
        HANDLE hProcess = OpenProcess(PROCESS_TERMINATE, FALSE, result.pid);
        if (hProcess)
        {
            if (TerminateProcess(hProcess, 0))
            {
                ss << L"Terminated process " << result.pid << L" (" << result.name << L")" << std::endl;
            }
            else
            {
                ss << L"Failed to terminate process " << result.pid << L" (" << result.name << L")" << std::endl;
            }
            CloseHandle(hProcess);
        }
        else
        {
            ss << L"Failed to open process " << result.pid << L" (" << result.name << L")" << std::endl;
        }
    }
    return ss.str();
}

CommandResult run_command(int argc, wchar_t* argv[])
{
    if (argc < 2)
    {
        return { 1, get_usage() };
    }

    bool json_output = false;
    bool kill = false;
    bool wait = false;
    std::vector<std::wstring> paths;

    for (int i = 1; i < argc; ++i)
    {
        std::wstring arg = argv[i];
        if (arg == L"--json")
        {
            json_output = true;
        }
        else if (arg == L"--kill")
        {
            kill = true;
        }
        else if (arg == L"--wait")
        {
            wait = true;
        }
        else if (arg == L"--help")
        {
            return { 0, get_usage() };
        }
        else
        {
            paths.push_back(arg);
        }
    }

    if (paths.empty())
    {
        return { 1, L"Error: No paths specified.\n" };
    }

    if (wait)
    {
        std::wstringstream ss;
        if (json_output)
        {
             ss << L"Warning: --wait is incompatible with --json. Ignoring --json." << std::endl;
             json_output = false;
        }
        
        ss << L"Waiting for files to be unlocked..." << std::endl;
        while (true)
        {
            auto results = find_processes_recursive(paths);
            if (results.empty())
            {
                ss << L"Files unlocked." << std::endl;
                break;
            }
            Sleep(1000);
        }
        return { 0, ss.str() };
    }

    auto results = find_processes_recursive(paths);
    std::wstringstream output_ss;

    if (kill)
    {
        output_ss << kill_processes(results);
        // Re-check after killing
        results = find_processes_recursive(paths);
    }

    if (json_output)
    {
        output_ss << get_json(results) << std::endl;
    }
    else
    {
        output_ss << get_text(results);
    }

    return { 0, output_ss.str() };
}

int wmain(int argc, wchar_t* argv[])
{
    winrt::init_apartment();
    auto result = run_command(argc, argv);
    std::wcout << result.output;
    return result.exit_code;
}
