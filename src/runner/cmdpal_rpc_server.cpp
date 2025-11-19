#include "pch.h"
#include "cmdpal_rpc_server.h"

#include "powertoy_module.h"
#include <common/logger/logger.h>

using namespace winrt::Windows::Data::Json;

namespace
{
    constexpr wchar_t PIPE_NAME[] = LR"(\\.\pipe\PowerToys.CmdPal.Rpc)";

    std::string ToUtf8(const winrt::hstring& value)
    {
        return winrt::to_string(value);
    }

    winrt::hstring ToHString(const std::string& value)
    {
        return winrt::to_hstring(value);
    }
}

CmdPalRpcServer::CmdPalRpcServer() = default;

CmdPalRpcServer::~CmdPalRpcServer()
{
    Stop();
}

void CmdPalRpcServer::Start()
{
    if (m_running.exchange(true))
    {
        return;
    }

    m_worker = std::thread([this]() { Run(); });
}

void CmdPalRpcServer::Stop()
{
    if (!m_running.exchange(false))
    {
        return;
    }

    // Trigger the server loop to exit by briefly connecting to the pipe.
    for (int attempt = 0; attempt < 5; ++attempt)
    {
        auto pipe = CreateFileW(PIPE_NAME, GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, 0, nullptr);
        if (pipe != INVALID_HANDLE_VALUE)
        {
            CloseHandle(pipe);
            break;
        }

        auto error = GetLastError();
        if (error == ERROR_FILE_NOT_FOUND)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        }
        else if (error == ERROR_PIPE_BUSY)
        {
            WaitNamedPipeW(PIPE_NAME, 200);
        }
        else
        {
            break;
        }
    }

    if (m_worker.joinable())
    {
        m_worker.join();
    }
}

void CmdPalRpcServer::Run()
{
    while (m_running.load())
    {
        HANDLE pipe = CreateNamedPipeW(PIPE_NAME,
                                       PIPE_ACCESS_DUPLEX,
                                       PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
                                       PIPE_UNLIMITED_INSTANCES,
                                       64 * 1024,
                                       64 * 1024,
                                       0,
                                       nullptr);

        if (pipe == INVALID_HANDLE_VALUE)
        {
            Logger::error(L"CmdPalRpcServer: failed to create pipe. Error: {}", GetLastError());
            std::this_thread::sleep_for(std::chrono::milliseconds(500));
            continue;
        }

        BOOL connected = ConnectNamedPipe(pipe, nullptr) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
        if (!connected)
        {
            CloseHandle(pipe);
            continue;
        }

        HandleClient(pipe);
    }
}

void CmdPalRpcServer::HandleClient(HANDLE pipe)
{
    auto closePipe = wil::scope_exit([pipe]() {
        FlushFileBuffers(pipe);
        DisconnectNamedPipe(pipe);
        CloseHandle(pipe);
    });

    while (m_running.load())
    {
        uint32_t length = 0;
        DWORD bytesRead = 0;
        if (!ReadFile(pipe, &length, sizeof(length), &bytesRead, nullptr) || bytesRead == 0)
        {
            break;
        }

        std::string payload;
        payload.resize(length);
        DWORD totalRead = 0;
        while (totalRead < length)
        {
            DWORD chunkRead = 0;
            if (!ReadFile(pipe, payload.data() + totalRead, length - totalRead, &chunkRead, nullptr) || chunkRead == 0)
            {
                return;
            }
            totalRead += chunkRead;
        }

        auto response = ProcessMessage(payload);
        uint32_t responseLength = static_cast<uint32_t>(response.size());
        DWORD bytesWritten = 0;
        if (!WriteFile(pipe, &responseLength, sizeof(responseLength), &bytesWritten, nullptr))
        {
            break;
        }

        if (responseLength > 0)
        {
            DWORD totalWritten = 0;
            while (totalWritten < responseLength)
            {
                DWORD chunkWritten = 0;
                if (!WriteFile(pipe, response.data() + totalWritten, responseLength - totalWritten, &chunkWritten, nullptr))
                {
                    return;
                }
                totalWritten += chunkWritten;
            }
        }
    }
}

std::string CmdPalRpcServer::ProcessMessage(const std::string& message)
{
    try
    {
        auto request = JsonObject::Parse(ToHString(message));
        std::wstring id;
        if (auto idValue = request.TryLookup(L"id"))
        {
            if (idValue.ValueType() == JsonValueType::String)
            {
                id = idValue.GetString().c_str();
            }
        }

        if (!request.HasKey(L"module") || !request.HasKey(L"method"))
        {
            return BuildErrorResponse(id, L"Bad.Request", L"Missing module or method");
        }

        auto moduleName = request.Lookup(L"module").GetString();
        auto methodName = request.Lookup(L"method").GetString();

        if (moduleName == L"core" && methodName == L"listModules")
        {
            return ListModulesResponse(id);
        }

        return ProcessModuleRequest(request, id);
    }
    catch (...)
    {
        return BuildErrorResponse(L"", L"Bad.Request", L"Malformed JSON");
    }
}

std::string CmdPalRpcServer::ProcessModuleRequest(const JsonObject& request, const std::wstring& id)
{
    std::wstring moduleKey = request.Lookup(L"module").GetString().c_str();
    auto methodName = request.Lookup(L"method").GetString();

    auto& loadedModules = modules();
    auto moduleIt = loadedModules.find(moduleKey);
    if (moduleIt == loadedModules.end())
    {
        return BuildErrorResponse(id, L"Module.NotFound", L"Requested module is not available");
    }

    std::wstring params = L"{}";
    if (auto paramsValue = request.TryLookup(L"params"))
    {
        params = paramsValue.Stringify().c_str();
    }

    auto start = std::chrono::steady_clock::now();
    std::wstring moduleResponse;
    try
    {
        moduleResponse = moduleIt->second->invoke(methodName.c_str(), params.c_str());
    }
    catch (...)
    {
        return BuildErrorResponse(id, L"Module.Failure", L"Module threw an exception");
    }
    auto elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now() - start).count();

    JsonObject response;
    if (!id.empty())
    {
        response.SetNamedValue(L"id", JsonValue::CreateStringValue(id));
    }
    response.SetNamedValue(L"elapsedMs", JsonValue::CreateNumberValue(static_cast<double>(elapsed)));

    try
    {
        auto moduleJsonValue = JsonValue::Parse(winrt::hstring(moduleResponse.c_str()));
        bool ok = true;
        if (moduleJsonValue.ValueType() == JsonValueType::Object)
        {
            auto moduleObject = moduleJsonValue.GetObject();
            if (moduleObject.HasKey(L"ok"))
            {
                ok = moduleObject.GetNamedBoolean(L"ok");
            }
        }

        response.SetNamedValue(L"ok", JsonValue::CreateBooleanValue(ok));
        if (ok)
        {
            response.SetNamedValue(L"result", moduleJsonValue);
        }
        else if (moduleJsonValue.ValueType() == JsonValueType::Object)
        {
            auto moduleObject = moduleJsonValue.GetObject();
            if (moduleObject.HasKey(L"error"))
            {
                response.SetNamedValue(L"error", moduleObject.Lookup(L"error"));
            }
            else
            {
                response.SetNamedValue(L"error", moduleJsonValue);
            }
        }
        else
        {
            response.SetNamedValue(L"error", moduleJsonValue);
        }

        return ToUtf8(response.Stringify());
    }
    catch (...)
    {
        return BuildErrorResponse(id, L"Module.Failure", L"Module returned invalid JSON");
    }
}

std::string CmdPalRpcServer::ListModulesResponse(const std::wstring& id)
{
    JsonObject response;
    if (!id.empty())
    {
        response.SetNamedValue(L"id", JsonValue::CreateStringValue(id));
    }
    response.SetNamedValue(L"ok", JsonValue::CreateBooleanValue(true));

    JsonArray modulesArray;
    for (auto& entry : modules())
    {
        try
        {
            auto describeJson = JsonValue::Parse(winrt::hstring(entry.second->describe()));
            if (describeJson.ValueType() == JsonValueType::Object)
            {
                modulesArray.Append(describeJson.GetObject());
            }
        }
        catch (...)
        {
            JsonObject fallback;
            fallback.SetNamedValue(L"name", JsonValue::CreateStringValue(entry.first));
            modulesArray.Append(fallback);
        }
    }

    JsonObject payload;
    payload.SetNamedValue(L"modules", modulesArray);
    response.SetNamedValue(L"result", payload);

    return ToUtf8(response.Stringify());
}

std::string CmdPalRpcServer::BuildErrorResponse(const std::wstring& id, const std::wstring_view code, const std::wstring_view message)
{
    JsonObject response;
    if (!id.empty())
    {
        response.SetNamedValue(L"id", JsonValue::CreateStringValue(id));
    }
    response.SetNamedValue(L"ok", JsonValue::CreateBooleanValue(false));
    JsonObject error;
    error.SetNamedValue(L"code", JsonValue::CreateStringValue(winrt::hstring(code.data(), static_cast<uint32_t>(code.size()))));
    error.SetNamedValue(L"message", JsonValue::CreateStringValue(winrt::hstring(message.data(), static_cast<uint32_t>(message.size()))));
    response.SetNamedValue(L"error", error);
    return ToUtf8(response.Stringify());
}
