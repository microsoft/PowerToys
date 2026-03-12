#include "pch.h"
#include "action_pipe_server.h"

#include "action_registry.h"

#include <common/interop/shared_constants.h>
#include <common/logger/logger.h>

#include <vector>

namespace
{
    constexpr DWORD MESSAGE_LENGTH_BYTES = sizeof(std::uint32_t);
    constexpr DWORD BUFFER_BYTES = 64 * 1024;

    std::wstring Utf8ToUtf16(const std::string& value)
    {
        if (value.empty())
        {
            return {};
        }

        const int size = MultiByteToWideChar(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), nullptr, 0);
        if (size <= 0)
        {
            return {};
        }

        std::wstring result(static_cast<size_t>(size), L'\0');
        MultiByteToWideChar(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), result.data(), size);
        return result;
    }

    std::string Utf16ToUtf8(const std::wstring& value)
    {
        if (value.empty())
        {
            return {};
        }

        const int size = WideCharToMultiByte(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), nullptr, 0, nullptr, nullptr);
        if (size <= 0)
        {
            return {};
        }

        std::string result(static_cast<size_t>(size), '\0');
        WideCharToMultiByte(CP_UTF8, 0, value.data(), static_cast<int>(value.size()), result.data(), size, nullptr, nullptr);
        return result;
    }

    bool ReadExact(HANDLE handle, void* buffer, DWORD bytes_to_read)
    {
        auto* target = static_cast<std::byte*>(buffer);
        DWORD total_read = 0;

        while (total_read < bytes_to_read)
        {
            DWORD bytes_read = 0;
            if (!ReadFile(handle, target + total_read, bytes_to_read - total_read, &bytes_read, nullptr))
            {
                return false;
            }

            if (bytes_read == 0)
            {
                return false;
            }

            total_read += bytes_read;
        }

        return true;
    }

    bool WriteExact(HANDLE handle, const void* buffer, DWORD bytes_to_write)
    {
        const auto* source = static_cast<const std::byte*>(buffer);
        DWORD total_written = 0;

        while (total_written < bytes_to_write)
        {
            DWORD bytes_written = 0;
            if (!WriteFile(handle, source + total_written, bytes_to_write - total_written, &bytes_written, nullptr))
            {
                return false;
            }

            total_written += bytes_written;
        }

        return true;
    }

    json::JsonObject ErrorResponse(const std::wstring& error_code, const std::wstring& message)
    {
        json::JsonObject response;
        response.SetNamedValue(L"success", json::JsonValue::CreateBooleanValue(false));
        response.SetNamedValue(L"error_code", json::value(error_code));
        response.SetNamedValue(L"message", json::value(message));
        return response;
    }

    json::JsonObject HandleRequestPayload(const std::string& payload)
    {
        json::JsonObject request;
        if (!json::JsonObject::TryParse(Utf8ToUtf16(payload), request))
        {
            return ErrorResponse(L"invalid_request", L"The action pipe request payload was not valid JSON.");
        }

        const std::wstring request_type = request.GetNamedString(L"type", L"").c_str();
        if (request_type == L"list_actions")
        {
            json::JsonObject response;
            response.SetNamedValue(L"success", json::JsonValue::CreateBooleanValue(true));
            response.SetNamedValue(L"actions", PowerToysActionRegistry::Instance().ListActions());
            return response;
        }

        if (request_type == L"invoke_action")
        {
            const std::wstring action_id = request.GetNamedString(L"action_id", L"").c_str();
            const std::wstring arguments = request.GetNamedString(L"arguments", L"{}").c_str();
            return PowerToysActionRegistry::Instance().InvokeAction(action_id, arguments);
        }

        return ErrorResponse(L"unsupported_request", L"The action pipe request type is not supported.");
    }

    void WakeActionPipeServer()
    {
        HANDLE client = CreateFileW(
            CommonSharedConstants::POWERTOYS_ACTIONS_PIPE,
            GENERIC_READ | GENERIC_WRITE,
            0,
            nullptr,
            OPEN_EXISTING,
            0,
            nullptr);

        if (client != INVALID_HANDLE_VALUE)
        {
            CloseHandle(client);
        }
    }
}

RunnerActionPipeServer::~RunnerActionPipeServer()
{
    Stop();
}

void RunnerActionPipeServer::Start()
{
    if (_server_thread.joinable())
    {
        return;
    }

    _stop_requested = false;
    _server_thread = std::thread(&RunnerActionPipeServer::Run, this);
}

void RunnerActionPipeServer::Stop()
{
    _stop_requested = true;
    WakeActionPipeServer();

    if (_server_thread.joinable())
    {
        _server_thread.join();
    }
}

void RunnerActionPipeServer::Run()
{
    while (!_stop_requested)
    {
        HANDLE pipe = CreateNamedPipeW(
            CommonSharedConstants::POWERTOYS_ACTIONS_PIPE,
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
            PIPE_UNLIMITED_INSTANCES,
            BUFFER_BYTES,
            BUFFER_BYTES,
            0,
            nullptr);

        if (pipe == INVALID_HANDLE_VALUE)
        {
            Logger::error(L"RunnerActionPipeServer: failed to create named pipe. error={}", GetLastError());
            return;
        }

        const BOOL connected = ConnectNamedPipe(pipe, nullptr) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
        if (!connected)
        {
            Logger::warn(L"RunnerActionPipeServer: client failed to connect. error={}", GetLastError());
            CloseHandle(pipe);
            continue;
        }

        std::uint32_t request_length = 0;
        if (ReadExact(pipe, &request_length, MESSAGE_LENGTH_BYTES))
        {
            std::vector<char> request_payload(request_length);
            const bool payload_ok = request_length == 0 || ReadExact(pipe, request_payload.data(), request_length);

            json::JsonObject response = payload_ok
                ? HandleRequestPayload(std::string(request_payload.begin(), request_payload.end()))
                : ErrorResponse(L"read_failed", L"Failed to read the action pipe request payload.");

            const auto response_utf8 = Utf16ToUtf8(response.Stringify().c_str());
            const auto response_length = static_cast<std::uint32_t>(response_utf8.size());
            WriteExact(pipe, &response_length, MESSAGE_LENGTH_BYTES);
            if (response_length > 0)
            {
                WriteExact(pipe, response_utf8.data(), response_length);
            }
        }

        FlushFileBuffers(pipe);
        DisconnectNamedPipe(pipe);
        CloseHandle(pipe);
    }
}
