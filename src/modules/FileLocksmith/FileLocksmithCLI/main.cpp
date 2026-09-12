#include "pch.h"
#include "CLILogic.h"
#include "FileLocksmithLib/FileLocksmith.h"
#include "FileLocksmithLib/Trace.h"
#include <common/utils/json.h>
#include <chrono>
#include <iostream>
#include <iterator>
#include <optional>
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

namespace
{
    constexpr std::wstring_view WorkerArgument = L"--worker-json";

    std::optional<std::vector<std::wstring>> read_worker_paths()
    {
        const std::string input{
            std::istreambuf_iterator<char>{ std::cin },
            std::istreambuf_iterator<char>{}
        };

        json::JsonObject request;
        if (!json::JsonObject::TryParse(winrt::to_hstring(input), request) || !request.HasKey(L"paths"))
        {
            return std::nullopt;
        }

        try
        {
            std::vector<std::wstring> paths;
            const auto json_paths = request.GetNamedArray(L"paths");
            paths.reserve(json_paths.Size());

            for (const auto& path : json_paths)
            {
                if (path.ValueType() != json::JsonValueType::String)
                {
                    return std::nullopt;
                }

                paths.emplace_back(path.GetString());
            }

            if (paths.empty())
            {
                return std::nullopt;
            }

            return paths;
        }
        catch (const winrt::hresult_error&)
        {
            return std::nullopt;
        }
    }
}

#ifndef UNIT_TEST
int wmain(int argc, wchar_t* argv[])
{
    winrt::init_apartment();
    Trace::RegisterProvider();
    LoggerHelpers::init_logger(L"FileLocksmithCLI", L"", LogSettings::fileLocksmithLoggerName);
    Logger::info("FileLocksmithCLI started");

    RealProcessFinder finder;
    RealProcessTerminator terminator;
    RealStringProvider strings;

    if (argc == 2 && argv[1] == WorkerArgument)
    {
        const auto paths = read_worker_paths();
        if (!paths)
        {
            Logger::error("Worker input was malformed");
            Trace::CLICommand(L"worker-query", false);
            Trace::UnregisterProvider();
            return 2;
        }

        Logger::info("Worker query started with {} paths", paths->size());
        const auto started = std::chrono::steady_clock::now();
        const auto result = run_worker_query(*paths, finder);
        const auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now() - started);
        Logger::info("Worker query completed in {} ms with exit code {}", duration.count(), result.exit_code);

        std::cout << winrt::to_string(result.output);
        Trace::CLICommand(result.command_name.c_str(), result.exit_code == 0);
        Trace::UnregisterProvider();
        return result.exit_code;
    }

    auto result = run_command(argc, argv, finder, terminator, strings);

    if (result.exit_code != 0)
    {
        Logger::error("Command failed with exit code {}", result.exit_code);
    }
    else
    {
        Logger::info("Command succeeded");
    }

    Trace::CLICommand(result.command_name.c_str(), result.exit_code == 0);

    std::wcout << result.output;
    Trace::UnregisterProvider();
    return result.exit_code;
}
#endif
