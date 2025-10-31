#include "pch.h"
#include "cli_server.h"

#include "command_registry.h"

#include <common/logger/logger.h>
#include <common/utils/json.h>

#include <atomic>
#include <thread>
#include <string>
#include <vector>

namespace
{
    constexpr wchar_t PIPE_NAME[] = LR"(\\.\pipe\PowerToys.Runner.CLI)";
    constexpr DWORD PIPE_BUFFER_SIZE = 64 * 1024;

    std::once_flag startFlag;
    std::atomic_bool running = false;

    std::wstring utf8_to_wstring(const std::string& input)
    {
        if (input.empty())
        {
            return {};
        }

        int wideSize = MultiByteToWideChar(CP_UTF8, 0, input.data(), static_cast<int>(input.size()), nullptr, 0);
        if (wideSize <= 0)
        {
            return {};
        }

        std::wstring result(static_cast<size_t>(wideSize), L'\0');
        MultiByteToWideChar(CP_UTF8, 0, input.data(), static_cast<int>(input.size()), result.data(), wideSize);
        return result;
    }

    std::string wstring_to_utf8(const std::wstring& input)
    {
        if (input.empty())
        {
            return {};
        }

        int narrowSize = WideCharToMultiByte(CP_UTF8, 0, input.data(), static_cast<int>(input.size()), nullptr, 0, nullptr, nullptr);
        if (narrowSize <= 0)
        {
            return {};
        }

        std::string result(static_cast<size_t>(narrowSize), '\0');
        WideCharToMultiByte(CP_UTF8, 0, input.data(), static_cast<int>(input.size()), result.data(), narrowSize, nullptr, nullptr);
        return result;
    }

    bool read_message(HANDLE pipe, std::string& out)
    {
        char buffer[4096];
        DWORD bytesRead = 0;
        bool continueReading = true;

        while (continueReading)
        {
            BOOL success = ReadFile(pipe, buffer, sizeof(buffer), &bytesRead, nullptr);
            if (!success)
            {
                DWORD error = GetLastError();
                if (error == ERROR_MORE_DATA)
                {
                    out.append(buffer, buffer + bytesRead);
                    continue;
                }

                if (error != ERROR_BROKEN_PIPE && error != ERROR_PIPE_NOT_CONNECTED)
                {
                    Logger::warn(L"CLI pipe read failed with error {}", error);
                }
                return false;
            }

            if (bytesRead > 0)
            {
                out.append(buffer, buffer + bytesRead);
            }
            continueReading = false;
        }

        return true;
    }

    json::JsonArray parameters_to_json(const std::vector<pt::cli::CommandParameter>& parameters)
    {
        json::JsonArray array;
        for (const auto& parameter : parameters)
        {
            json::JsonObject node;
            node.SetNamedValue(L"name", json::value(parameter.name));
            node.SetNamedValue(L"required", json::value(parameter.required));
            node.SetNamedValue(L"description", json::value(parameter.description));
            array.Append(json::value(std::move(node)));
        }
        return array;
    }

    pt::cli::CommandResult handle_system_command(const std::wstring& action, const json::JsonObject& args)
    {
        if (action == L"list-modules")
        {
            auto snapshot = CommandRegistry::instance().snapshot();
            json::JsonArray modules;
            for (auto& moduleInfo : snapshot)
            {
                json::JsonObject moduleJson;
                moduleJson.SetNamedValue(L"module", json::value(moduleInfo.moduleKey));

                json::JsonArray commands;
                for (const auto& descriptor : moduleInfo.commands)
                {
                    json::JsonObject cmdJson;
                    cmdJson.SetNamedValue(L"action", json::value(descriptor.action));
                    cmdJson.SetNamedValue(L"description", json::value(descriptor.description));
                    cmdJson.SetNamedValue(L"requiresElevation", json::value(descriptor.requiresElevation));
                    cmdJson.SetNamedValue(L"longRunning", json::value(descriptor.longRunning));
                    cmdJson.SetNamedValue(L"parameters", json::value(parameters_to_json(descriptor.parameters)));
                    commands.Append(json::value(std::move(cmdJson)));
                }

                moduleJson.SetNamedValue(L"commands", json::value(std::move(commands)));
                modules.Append(json::value(std::move(moduleJson)));
            }

            json::JsonObject payload;
            payload.SetNamedValue(L"modules", json::value(std::move(modules)));
            return pt::cli::CommandResult::Success(std::move(payload));
        }

        if (action == L"list-commands")
        {
            if (!args.HasKey(L"module"))
            {
                return pt::cli::CommandResult::Error(L"E_ARGS_INVALID", L"'module' argument is required.");
            }

            auto moduleName = std::wstring(args.GetNamedString(L"module").c_str());
            auto reflection = CommandRegistry::instance().snapshot(moduleName);
            if (!reflection.has_value())
            {
                return pt::cli::CommandResult::Error(L"E_MODULE_NOT_FOUND", L"Module not registered.");
            }

            json::JsonArray commands;
            for (const auto& descriptor : reflection->commands)
            {
                json::JsonObject cmdJson;
                cmdJson.SetNamedValue(L"action", json::value(descriptor.action));
                cmdJson.SetNamedValue(L"description", json::value(descriptor.description));
                cmdJson.SetNamedValue(L"requiresElevation", json::value(descriptor.requiresElevation));
                cmdJson.SetNamedValue(L"longRunning", json::value(descriptor.longRunning));
                cmdJson.SetNamedValue(L"parameters", json::value(parameters_to_json(descriptor.parameters)));
                commands.Append(json::value(std::move(cmdJson)));
            }

            json::JsonObject payload;
            payload.SetNamedValue(L"module", json::value(reflection->moduleKey));
            payload.SetNamedValue(L"commands", json::value(std::move(commands)));
            return pt::cli::CommandResult::Success(std::move(payload));
        }

        if (action == L"ping")
        {
            json::JsonObject payload;
            payload.SetNamedValue(L"status", json::value(L"ok"));
            return pt::cli::CommandResult::Success(std::move(payload));
        }

        return pt::cli::CommandResult::Error(L"E_COMMAND_NOT_FOUND", L"Unsupported system command.");
    }

    pt::cli::CommandResult dispatch_command(const std::wstring& module, const std::wstring& action, const json::JsonObject& args)
    {
        if (module == L"$system")
        {
            return handle_system_command(action, args);
        }

        pt::cli::CommandInvocation invocation{ action, args };
        return CommandRegistry::instance().execute(module, invocation);
    }

    json::JsonObject build_error_payload(const std::wstring& code, const std::wstring& message)
    {
        json::JsonObject error;
        error.SetNamedValue(L"code", json::value(code));
        error.SetNamedValue(L"message", json::value(message));
        return error;
    }

    void write_response(HANDLE pipe, const json::JsonObject& response)
    {
        auto serialized = response.Stringify();
        auto utf8 = wstring_to_utf8(serialized.c_str());
        DWORD bytesWritten = 0;
        WriteFile(pipe, utf8.data(), static_cast<DWORD>(utf8.size()), &bytesWritten, nullptr);
        FlushFileBuffers(pipe);
    }

    void handle_session(HANDLE pipe)
    {
        std::string rawRequest;
        if (!read_message(pipe, rawRequest))
        {
            return;
        }

        json::JsonObject response;
        response.SetNamedValue(L"v", json::value(1));

        try
        {
            auto requestText = utf8_to_wstring(rawRequest);
            if (requestText.empty())
            {
                response.SetNamedValue(L"status", json::value(L"error"));
                response.SetNamedValue(L"error", json::value(build_error_payload(L"E_INVALID", L"Empty request.")));
                write_response(pipe, response);
                return;
            }

            auto jsonValue = winrt::Windows::Data::Json::JsonValue::Parse(requestText);
            auto root = jsonValue.GetObjectW();

            auto correlationId = root.GetNamedString(L"correlationId", L"");
            response.SetNamedValue(L"correlationId", json::value(correlationId));

            if (!root.HasKey(L"command"))
            {
                response.SetNamedValue(L"status", json::value(L"error"));
                response.SetNamedValue(L"error", json::value(build_error_payload(L"E_INVALID", L"Missing command payload.")));
                write_response(pipe, response);
                return;
            }

            auto command = root.GetNamedObject(L"command");
            const std::wstring module = std::wstring(command.GetNamedString(L"module", L"").c_str());
            const std::wstring action = std::wstring(command.GetNamedString(L"action", L"").c_str());

            json::JsonObject args = json::JsonObject();
            if (command.HasKey(L"args"))
            {
                args = command.GetNamedObject(L"args");
            }

            if (module.empty() || action.empty())
            {
                response.SetNamedValue(L"status", json::value(L"error"));
                response.SetNamedValue(L"error", json::value(build_error_payload(L"E_ARGS_INVALID", L"'module' and 'action' must be provided.")));
                write_response(pipe, response);
                return;
            }

            auto result = dispatch_command(module, action, args);
            if (result.ok)
            {
                response.SetNamedValue(L"status", json::value(L"ok"));
                response.SetNamedValue(L"result", json::value(result.data));
            }
            else
            {
                response.SetNamedValue(L"status", json::value(L"error"));
                auto errorCode = result.errorCode.empty() ? L"E_INTERNAL" : result.errorCode;
                auto errorMsg = result.errorMessage.empty() ? L"Command failed." : result.errorMessage;
                response.SetNamedValue(L"error", json::value(build_error_payload(errorCode, errorMsg)));
            }
        }
        catch (const winrt::hresult_error&)
        {
            response.SetNamedValue(L"status", json::value(L"error"));
            response.SetNamedValue(L"error", json::value(build_error_payload(L"E_INVALID_JSON", L"Request payload was not valid JSON.")));
        }
        catch (const std::exception& ex)
        {
            Logger::error(L"CLI request processing threw: {}", winrt::to_hstring(ex.what()));
            response.SetNamedValue(L"status", json::value(L"error"));
            response.SetNamedValue(L"error", json::value(build_error_payload(L"E_INTERNAL", L"Internal processing failure.")));
        }
        catch (...)
        {
            Logger::error(L"CLI request processing failed with unknown exception.");
            response.SetNamedValue(L"status", json::value(L"error"));
            response.SetNamedValue(L"error", json::value(build_error_payload(L"E_INTERNAL", L"Unknown processing failure.")));
        }

        write_response(pipe, response);
    }

    void server_loop()
    {
        while (running.load())
        {
            HANDLE pipe = CreateNamedPipeW(
                PIPE_NAME,
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                PIPE_UNLIMITED_INSTANCES,
                PIPE_BUFFER_SIZE,
                PIPE_BUFFER_SIZE,
                0,
                nullptr);

            if (pipe == INVALID_HANDLE_VALUE)
            {
                DWORD error = GetLastError();
                Logger::error(L"Failed to create CLI named pipe (error {}).", error);
                Sleep(1000);
                continue;
            }

            BOOL connected = ConnectNamedPipe(pipe, nullptr)
                ? TRUE
                : (GetLastError() == ERROR_PIPE_CONNECTED);

            if (connected)
            {
                handle_session(pipe);
            }

            FlushFileBuffers(pipe);
            DisconnectNamedPipe(pipe);
            CloseHandle(pipe);
        }
    }
}

void start_cli_server()
{
    std::call_once(startFlag, [] {
        running = true;
        std::thread(server_loop).detach();
    });
}
