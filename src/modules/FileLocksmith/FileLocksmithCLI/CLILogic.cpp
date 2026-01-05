#include "pch.h"
#include "CLILogic.h"
#include <common/utils/json.h>
#include <iostream>
#include <sstream>
#include <chrono>
#include "resource.h"
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>

std::wstring FormatString(IStringProvider& strings, UINT id, ...)
{
    std::wstring format = strings.GetString(id);
    if (format.empty()) return L"";

    va_list args;
    va_start(args, id);
    
    LPWSTR buffer = nullptr;
    FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_STRING,
                   format.c_str(),
                   0,
                   0,
                   reinterpret_cast<LPWSTR>(&buffer),
                   0,
                   &args);
    va_end(args);

    if (buffer)
    {
        std::wstring result(buffer);
        LocalFree(buffer);
        return result;
    }
    return L"";
}

std::wstring get_usage(IStringProvider& strings)
{
    return strings.GetString(IDS_USAGE);
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
    return root.Stringify().c_str();
}

std::wstring get_text(const std::vector<ProcessResult>& results, IStringProvider& strings)
{
    std::wstringstream ss;
    if (results.empty())
    {
        ss << strings.GetString(IDS_NO_PROCESSES);
        return ss.str();
    }

    ss << strings.GetString(IDS_HEADER);
    for (const auto& result : results)
    {
        ss << result.pid << L"\t" 
           << result.user << L"\t" 
           << result.name << std::endl;
    }
    return ss.str();
}

std::wstring kill_processes(const std::vector<ProcessResult>& results, IProcessTerminator& terminator, IStringProvider& strings)
{
    std::wstringstream ss;
    for (const auto& result : results)
    {
        if (terminator.terminate(result.pid))
        {
            ss << FormatString(strings, IDS_TERMINATED, result.pid, result.name.c_str());
        }
        else
        {
            ss << FormatString(strings, IDS_FAILED_TERMINATE, result.pid, result.name.c_str());
        }
    }
    return ss.str();
}

CommandResult run_command(int argc, wchar_t* argv[], IProcessFinder& finder, IProcessTerminator& terminator, IStringProvider& strings)
{
    Logger::info("Parsing arguments");
    if (argc < 2)
    {
        Logger::warn("No arguments provided");
        return { 1, get_usage(strings) };
    }

    bool json_output = false;
    bool kill = false;
    bool wait = false;
    int timeout_ms = -1;
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
        else if (arg == L"--timeout")
        {
            if (i + 1 < argc)
            {
                try
                {
                    timeout_ms = std::stoi(argv[++i]);
                }
                catch (...)
                {
                    Logger::error("Invalid timeout value");
                    return { 1, strings.GetString(IDS_ERROR_INVALID_TIMEOUT) };
                }
            }
            else
            {
                Logger::error("Timeout argument missing");
                return { 1, strings.GetString(IDS_ERROR_TIMEOUT_ARG) };
            }
        }
        else if (arg == L"--help")
        {
            return { 0, get_usage(strings) };
        }
        else
        {
            paths.push_back(arg);
        }
    }

    if (paths.empty())
    {
        Logger::error("No paths specified");
        return { 1, strings.GetString(IDS_ERROR_NO_PATHS) };
    }

    Logger::info("Processing {} paths", paths.size());

    if (wait)
    {
        std::wstringstream ss;
        if (json_output)
        {
             Logger::warn("Wait is incompatible with JSON output");
             ss << strings.GetString(IDS_WARN_JSON_WAIT);
             json_output = false;
        }
        
        ss << strings.GetString(IDS_WAITING);
        auto start_time = std::chrono::steady_clock::now();
        while (true)
        {
            auto results = finder.find(paths);
            if (results.empty())
            {
                Logger::info("Files unlocked");
                ss << strings.GetString(IDS_UNLOCKED);
                break;
            }

            if (timeout_ms >= 0)
            {
                auto current_time = std::chrono::steady_clock::now();
                auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(current_time - start_time).count();
                if (elapsed > timeout_ms)
                {
                    Logger::warn("Timeout waiting for files to be unlocked");
                    ss << strings.GetString(IDS_TIMEOUT);
                    return { 1, ss.str() };
                }
            }

            Sleep(200);
        }
        return { 0, ss.str() };
    }

    auto results = finder.find(paths);
    Logger::info("Found {} processes locking the files", results.size());
    std::wstringstream output_ss;

    if (kill)
    {
        Logger::info("Killing processes");
        output_ss << kill_processes(results, terminator, strings);
        // Re-check after killing
        results = finder.find(paths);
        Logger::info("Remaining processes: {}", results.size());
    }

    if (json_output)
    {
        output_ss << get_json(results) << std::endl;
    }
    else
    {
        output_ss << get_text(results, strings);
    }

    return { 0, output_ss.str() };
}
