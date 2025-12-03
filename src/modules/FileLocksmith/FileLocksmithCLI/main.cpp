#include "pch.h"
#include "FileLocksmithLib/FileLocksmith.h"
#include <common/utils/json.h>
#include <iostream>

void print_usage()
{
    std::wcout << L"Usage: FileLocksmithCLI.exe [options] <path1> [path2] ...\n"
               << L"Options:\n"
               << L"  --kill      Kill processes locking the files\n"
               << L"  --json      Output results in JSON format\n"
               << L"  --wait      Wait for files to be unlocked\n"
               << L"  --help      Show this help message\n";
}

void print_json(const std::vector<ProcessResult>& results)
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
    std::wcout << root.Stringify().c_str() << std::endl;
}

void print_text(const std::vector<ProcessResult>& results)
{
    if (results.empty())
    {
        std::wcout << L"No processes found locking the file(s)." << std::endl;
        return;
    }

    std::wcout << L"PID\tUser\tProcess" << std::endl;
    for (const auto& result : results)
    {
        std::wcout << result.pid << L"\t" 
                   << result.user << L"\t" 
                   << result.name << std::endl;
    }
}

void kill_processes(const std::vector<ProcessResult>& results)
{
    for (const auto& result : results)
    {
        HANDLE hProcess = OpenProcess(PROCESS_TERMINATE, FALSE, result.pid);
        if (hProcess)
        {
            if (TerminateProcess(hProcess, 0))
            {
                std::wcout << L"Terminated process " << result.pid << L" (" << result.name << L")" << std::endl;
            }
            else
            {
                std::wcerr << L"Failed to terminate process " << result.pid << L" (" << result.name << L")" << std::endl;
            }
            CloseHandle(hProcess);
        }
        else
        {
            std::wcerr << L"Failed to open process " << result.pid << L" (" << result.name << L")" << std::endl;
        }
    }
}

int wmain(int argc, wchar_t* argv[])
{
    winrt::init_apartment();

    if (argc < 2)
    {
        print_usage();
        return 1;
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
            print_usage();
            return 0;
        }
        else
        {
            paths.push_back(arg);
        }
    }

    if (paths.empty())
    {
        std::wcerr << L"Error: No paths specified." << std::endl;
        return 1;
    }

    if (wait)
    {
        if (json_output)
        {
             std::wcerr << L"Warning: --wait is incompatible with --json. Ignoring --json." << std::endl;
             json_output = false;
        }
        
        std::wcout << L"Waiting for files to be unlocked..." << std::endl;
        while (true)
        {
            auto results = find_processes_recursive(paths);
            if (results.empty())
            {
                std::wcout << L"Files unlocked." << std::endl;
                break;
            }
            Sleep(1000);
        }
        return 0;
    }

    auto results = find_processes_recursive(paths);

    if (kill)
    {
        kill_processes(results);
        // Re-check after killing
        results = find_processes_recursive(paths);
    }

    if (json_output)
    {
        print_json(results);
    }
    else
    {
        print_text(results);
    }

    return 0;
}
